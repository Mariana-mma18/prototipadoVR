using System.Collections.Generic;
using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

// Arregla las manos que "se ven pero no hacen nada" con el modo mixto (un control y una mano a la vez).
// Va en el Camera Rig de cada escena.
//
// El problema: el rig de Meta (ComprehensiveInteractorsLeft/Right) solo enciende el poke, el agarre y el rayo
// de una mano si en ese lado NO hay control conectado. Con el modo mixto, un control soltado en la mesa o
// colgando de la correa sigue "conectado", así que la mano se dibuja pero sus interacciones quedan apagadas,
// y el rayo del control soltado le puede quitar el hover a la mano.
//
// La solución: al iniciar, en cada lado cambio la condición "hay control" del rig por "hay control Y está en
// la mano". Un control soltado deja de contar: se encienden las interacciones de la mano y se apagan las del
// control (rayo, agarre, teleport) hasta que lo vuelvan a coger.
// Si el visor no tiene modo mixto (Quest 2) todo queda igual que antes: Meta responde "en la mano" por defecto.
public class ControlesSoloEnMano : MonoBehaviour
{
    [Tooltip("Segundos que el control tiene que estar soltado antes de pasarle las interacciones a la mano (evita parpadeos al acomodarlo en la mano)")]
    public float esperaAlSoltar = 0.3f;

    [Tooltip("Solo para mirar en Play: estado de cada control")]
    public string estadoIzquierdo;
    public string estadoDerecho;

    // Para el texto de depuración del MenuPalma
    public static string Resumen { get; private set; } = "";

    readonly List<ControlEnMano> lados = new List<ControlEnMano>();

    void Start()
    {
        Resumen = "";
        lados.Clear();
        foreach (ControllerRef control in FindObjectsByType<ControllerRef>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // La raíz de ComprehensiveInteractorsLeft/Right tiene el ControllerRef y el HandRef juntos
            // (algunos interactores internos de Meta también; esos no tienen condiciones y se saltan)
            if (!control.GetComponent<HandRef>()) continue;

            ControlEnMano enMano = control.gameObject.AddComponent<ControlEnMano>();
            enMano.control = control;
            enMano.dueno = this;

            int cambios = Reemplazar(control.transform, control, enMano);
            if (cambios == 0)
            {
                Destroy(enMano);
                continue;
            }
            lados.Add(enMano);
            Debug.Log($"[ControlesSoloEnMano] {control.name}: {cambios} condiciones ahora piden el control en la mano");
        }

        if (lados.Count < 2)
            Debug.LogWarning($"[ControlesSoloEnMano] Esperaba los dos lados (ComprehensiveInteractorsLeft/Right) y encontré {lados.Count}; ¿cambió el building block de Meta?");
    }

    void Update()
    {
        estadoIzquierdo = estadoDerecho = "-";
        foreach (ControlEnMano lado in lados)
        {
            if (!lado) continue;
            if (lado.EsIzquierdo) estadoIzquierdo = lado.Estado;
            else estadoDerecho = lado.Estado;
        }
        Resumen = $"Control izq: {estadoIzquierdo}   der: {estadoDerecho}";
    }

    // Cambia el ControllerRef por la condición nueva en las ActiveStateGroup/Not/Tracker de ese lado
    static int Reemplazar(Transform raiz, ControllerRef viejo, ControlEnMano nuevo)
    {
        int cambios = 0;

        foreach (ActiveStateGroup grupo in raiz.GetComponentsInChildren<ActiveStateGroup>(true))
        {
            var lista = Campo<List<Object>>(grupo, "_activeStates");
            if (lista == null || !lista.Contains(viejo)) continue;
            var estados = new List<IActiveState>();
            foreach (Object o in lista) estados.Add(o == viejo ? nuevo : o as IActiveState);
            grupo.InjectActiveStates(estados);
            cambios++;
        }

        foreach (ActiveStateNot negacion in raiz.GetComponentsInChildren<ActiveStateNot>(true))
            if (Campo<Object>(negacion, "_activeState") == viejo)
            {
                negacion.InjectActiveState(nuevo);
                cambios++;
            }

        foreach (ActiveStateTracker seguidor in raiz.GetComponentsInChildren<ActiveStateTracker>(true))
            if (Campo<Object>(seguidor, "_activeState") == viejo)
            {
                seguidor.InjectActiveState(nuevo);
                cambios++;
            }

        return cambios;
    }

    static T Campo<T>(object objeto, string nombre) where T : class
    {
        FieldInfo campo = objeto.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
        return campo?.GetValue(objeto) as T;
    }
}

// "Hay control en este lado y lo tienen en la mano". Se agrega sola en runtime; no hace falta ponerla a mano.
public class ControlEnMano : MonoBehaviour, IActiveState
{
    [HideInInspector] public ControllerRef control;
    [HideInInspector] public ControlesSoloEnMano dueno;

    bool activo;
    float soltadoDesde = -1f;
    int ultimoFrame = -1;
    int modoMixto = -1; // -1 sin revisar, 0 no, 1 sí

    public bool EsIzquierdo => control && control.Handedness == Handedness.Left;
    public string Estado { get; private set; } = "-";

    public bool Active
    {
        get
        {
            // Lo leen varias condiciones en el mismo frame; calculo una sola vez
            if (ultimoFrame != Time.frameCount)
            {
                ultimoFrame = Time.frameCount;
                Calcular();
            }
            return activo;
        }
    }

    void Calcular()
    {
        if (!control || !control.IsConnected)
        {
            activo = false;
            soltadoDesde = -1f;
            Estado = "sin control";
            return;
        }

        if (modoMixto < 0)
            modoMixto = OVRManager.instance && OVRManager.instance.launchSimultaneousHandsControllersOnStartup
                        && OVRPlugin.IsMultimodalHandsControllersSupported() ? 1 : 0;

        if (modoMixto == 0)
        {
            activo = true;
            Estado = "conectado (sin modo mixto)";
            return;
        }

        OVRInput.Hand mano = EsIzquierdo ? OVRInput.Hand.HandLeft : OVRInput.Hand.HandRight;
        bool soltado = OVRInput.GetControllerIsInHandState(mano) == OVRInput.ControllerInHandState.ControllerNotInHand;

        if (!soltado)
        {
            soltadoDesde = -1f;
            activo = true;
            Estado = "en la mano";
            return;
        }

        if (soltadoDesde < 0f) soltadoDesde = Time.unscaledTime;
        float espera = dueno ? dueno.esperaAlSoltar : 0.3f;
        activo = Time.unscaledTime - soltadoDesde < espera;
        Estado = activo ? "soltándolo..." : "soltado (manda la mano)";
    }
}
