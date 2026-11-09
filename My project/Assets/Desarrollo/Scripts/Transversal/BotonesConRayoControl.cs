using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

// Deja pulsar los botones poke de subir/bajar del bote apuntando con el rayo del control y apretando el gatillo.
// Va en el Camera Rig de cada escena.
//
// Cada botón poke tiene un canvas con su propio RayInteractable. En los paneles del menú, el mapa y los
// ingredientes el rayo ya funciona solo, porque sus botones de Unity UI están activos.
// En los de subir/bajar el Button del canvas está apagado a propósito (el poke ya llama a Subir/Bajar y no
// queremos que se pulse dos veces), aunque su onClick sí apunta a Subir/Bajar. Este script escucha el
// RayInteractable de esos canvas y, cuando lo selecciona el rayo de un CONTROL, llama a ese onClick.
// Las manos no cambian: siguen pulsando con el dedo (el rayo de la mano con pellizco no cuenta, salvo que
// se active tambienRayoDeManos).
public class BotonesConRayoControl : MonoBehaviour
{
    [Tooltip("Si se activa, el rayo de las manos (apuntar y pellizcar) también pulsa estos botones. Apagado = solo los controles.")]
    public bool tambienRayoDeManos = false;

    readonly List<Button> pendientes = new List<Button>();

    void Start()
    {
        int conectados = 0;
        foreach (RayInteractable rayo in FindObjectsByType<RayInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!rayo.GetComponent<PointableCanvas>()) continue;

            // Solo los canvas cuyo botón está apagado; los demás ya responden al rayo por Unity UI
            Button boton = null;
            foreach (Button b in rayo.GetComponentsInChildren<Button>(true))
                if (!b.enabled && b.onClick.GetPersistentEventCount() > 0) { boton = b; break; }
            if (!boton) continue;

            rayo.WhenSelectingInteractorViewAdded += vista =>
            {
                if (EsRayoValido(vista) && !pendientes.Contains(boton)) pendientes.Add(boton);
            };
            conectados++;
        }
        Debug.Log($"[BotonesConRayoControl] {conectados} botones se pueden pulsar con el rayo del control");
    }

    void Update()
    {
        // Lo llamo aquí y no dentro del evento del rayo, para no apagar la locomoción (Subir) mientras Meta
        // todavía está procesando la selección de ese mismo rayo
        if (pendientes.Count == 0) return;
        var botones = pendientes.ToArray();
        pendientes.Clear();
        foreach (Button boton in botones)
            if (boton) boton.onClick.Invoke();
    }

    bool EsRayoValido(IInteractorView vista)
    {
        if (tambienRayoDeManos) return true;
        // En el rig de Meta: ControllerRayInteractor (control) y HandRayInteractor (mano)
        return vista is Component c && c && c.name.StartsWith("Controller");
    }
}
