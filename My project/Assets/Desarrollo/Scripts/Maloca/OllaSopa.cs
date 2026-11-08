using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Cocina de la maloca: final del recorrido.
// 1. El jugador saca los ingredientes de la canasta y los suelta sobre la olla; cada uno se hunde hasta el fondo
//    y ya no se puede volver a agarrar. La lista del menú de la palma los sigue mostrando marcados.
// 2. Con todos adentro, agarra la cuchara y mezcla: mientras la punta se mueve dentro de la olla, los ingredientes
//    giran y la sopa va subiendo.
// 3. Al completar la mezcla la sopa queda lista y se lanza alTerminar (fin de la experiencia).
// Los ingredientes se detectan por posición (no hacen falta colliders en la olla). Si la cuchara se suelta,
// vuelve sola a su lugar para que no se pierda.
public class OllaSopa : MonoBehaviour
{
    public enum Etapa { EcharIngredientes, Mezclar, SopaLista }

    [Header("Ingredientes")]
    [Tooltip("Lista de ingredientes del plato (la del menú de la palma)")]
    public ListaIngredientes lista;

    [Header("Olla")]
    [Tooltip("Centro de la boca de la olla, a la altura del borde")]
    public Transform boca;

    [Tooltip("Radio interior de la olla, en metros")]
    public float radio = 0.46f;

    [Tooltip("Del borde al fondo de adentro, en metros")]
    public float profundidad = 0.57f;

    [Tooltip("Hasta qué altura sobre el borde se recibe un ingrediente que se suelta, en metros")]
    public float margenArriba = 0.35f;

    [Tooltip("Segundos que tarda un ingrediente en hundirse hasta el fondo")]
    public float tiempoHundir = 0.6f;

    [Header("Sopa")]
    [Tooltip("La sopa (superficie dentro de la olla). Se oculta al empezar y sube mientras se mezcla.")]
    public Transform sopa;

    [Tooltip("Cuánto más abajo de su lugar aparece la sopa al empezar a mezclar, en metros")]
    public float subidaSopa = 0.4f;

    [Header("Cuchara")]
    public Grabbable cuchara;

    [Tooltip("Punto de la cuchara que tiene que moverse dentro de la olla (la pala)")]
    public Transform puntaCuchara;

    [Tooltip("Metros que tiene que recorrer la punta dentro de la olla para terminar (una vuelta amplia son unos 2 m)")]
    public float recorridoMezcla = 3f;

    [Tooltip("Cuánto giran los ingredientes al mezclar (1 = igual que la cuchara)")]
    public float arrastreIngredientes = 0.6f;

    [Tooltip("Segundos sin agarrar la cuchara antes de que vuelva sola a su lugar")]
    public float esperaVolver = 1.5f;

    [Header("Indicación sobre la olla")]
    public TMP_Text indicacion;

    [Tooltip("Más lejos que esto, en metros, la indicación no se muestra")]
    public float distanciaIndicacion = 12f;

    public OVRCameraRig camaraRig;

    [Header("Eventos (sonidos, final, etc.)")]
    public UnityEvent alEcharIngrediente;
    public UnityEvent alCompletarIngredientes;
    public UnityEvent alEmpezarMezcla;
    public UnityEvent alTerminar;

    public Etapa EtapaActual { get; private set; }
    public float ProgresoMezcla { get; private set; }

    class EnOlla
    {
        public Transform objeto;
        public Vector3 posicion;     // respecto al centro del fondo, sin girar
        public Quaternion giro;
        public bool hundido;
    }

    readonly Dictionary<ListaIngredientes.Ingrediente, Grabbable> agarres = new Dictionary<ListaIngredientes.Ingrediente, Grabbable>();
    readonly List<EnOlla> enOlla = new List<EnOlla>();
    float anguloMezcla;
    bool puntaAdentro;
    Vector3 puntaAnterior;
    Vector3 sopaFinal;
    Pose cucharaInicio;
    float sueltaDesde = -1f;

    Vector3 Fondo => boca.position + Vector3.down * profundidad;

    void Start()
    {
        foreach (ListaIngredientes.Ingrediente ingrediente in lista.ingredientes)
            if (ingrediente.objeto) agarres[ingrediente] = ingrediente.objeto.GetComponentInChildren<Grabbable>(true);

        if (sopa)
        {
            sopaFinal = sopa.position;
            sopa.gameObject.SetActive(false);
        }

        if (cuchara)
        {
            cucharaInicio = cuchara.transform.GetPose();
            Rigidbody cuerpo = cuchara.GetComponent<Rigidbody>();
            if (cuerpo)
            {
                cuerpo.isKinematic = true;
                cuerpo.useGravity = false;
            }
        }

        EtapaActual = Etapa.EcharIngredientes;
    }

    void Update()
    {
        if (EtapaActual == Etapa.EcharIngredientes) RecibirIngredientes();
        else if (EtapaActual == Etapa.Mezclar) Mezclar();

        AcomodarIngredientes();
        DevolverCuchara();
        ActualizarIndicacion();
    }

    // ---------- 1. Echar ingredientes ----------

    void RecibirIngredientes()
    {
        foreach (ListaIngredientes.Ingrediente ingrediente in lista.ingredientes)
        {
            if (ingrediente.enOlla || !ingrediente.objeto || !ingrediente.objeto.gameObject.activeInHierarchy) continue;
            if (!Suelto(ingrediente)) continue;
            if (Adentro(ingrediente.objeto.position, margenArriba)) Echar(ingrediente);
        }
    }

    // Ni en la mano ni encajado en la canasta: lo soltaron
    bool Suelto(ListaIngredientes.Ingrediente ingrediente)
    {
        if (agarres.TryGetValue(ingrediente, out Grabbable agarre) && agarre && agarre.SelectingPointsCount > 0) return false;
        return !(ingrediente.encaje && ingrediente.encaje.HasSelectedInteractable);
    }

    bool Adentro(Vector3 punto, float arriba)
    {
        Vector3 desdeBoca = punto - boca.position;
        if (desdeBoca.y > arriba || desdeBoca.y < -profundidad) return false;
        desdeBoca.y = 0f;
        return desdeBoca.magnitude <= radio;
    }

    public void Echar(ListaIngredientes.Ingrediente ingrediente)
    {
        if (ingrediente.enOlla) return;
        ingrediente.enOlla = true;
        Transform objeto = ingrediente.objeto;

        // Ya no se puede agarrar ni encajar, y no choca con nada
        foreach (MonoBehaviour componente in objeto.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string espacio = componente.GetType().Namespace;
            if (espacio != null && espacio.StartsWith("Oculus.Interaction")) componente.enabled = false;
        }
        foreach (Collider choque in objeto.GetComponentsInChildren<Collider>(true)) choque.enabled = false;
        Rigidbody cuerpo = objeto.GetComponent<Rigidbody>();
        if (cuerpo)
        {
            if (!cuerpo.isKinematic)
            {
                cuerpo.linearVelocity = Vector3.zero;
                cuerpo.angularVelocity = Vector3.zero;
            }
            cuerpo.isKinematic = true;
            cuerpo.useGravity = false;
        }

        // Lugar en el fondo: repartidos en espiral para que no queden todos encima
        int numero = enOlla.Count;
        float angulo = numero * 137.5f;
        float distancia = radio * 0.6f * Mathf.Sqrt((numero + 0.5f) / Mathf.Max(1, lista.ingredientes.Count));
        Vector3 lugar = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward * distancia + Vector3.up * 0.05f;
        var dentro = new EnOlla { objeto = objeto, posicion = lugar, giro = Quaternion.Euler(0f, angulo + 90f, 0f) };
        enOlla.Add(dentro);
        StartCoroutine(Hundir(dentro));

        alEcharIngrediente.Invoke();

        if (enOlla.Count >= lista.ingredientes.Count)
        {
            EtapaActual = Etapa.Mezclar;
            alCompletarIngredientes.Invoke();
        }
    }

    IEnumerator Hundir(EnOlla dentro)
    {
        Vector3 desde = dentro.objeto.position;
        Quaternion giroDesde = dentro.objeto.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, tiempoHundir))
        {
            float suave = t * t * (3f - 2f * t);
            Quaternion vuelta = Quaternion.Euler(0f, anguloMezcla, 0f);
            dentro.objeto.SetPositionAndRotation(
                Vector3.Lerp(desde, Fondo + vuelta * dentro.posicion, suave),
                Quaternion.Slerp(giroDesde, vuelta * dentro.giro, suave));
            yield return null;
        }
        dentro.hundido = true;
    }

    // Los ingredientes del fondo giran con la mezcla y viajan con la olla si alguien la mueve
    void AcomodarIngredientes()
    {
        Quaternion vuelta = Quaternion.Euler(0f, anguloMezcla, 0f);
        foreach (EnOlla dentro in enOlla)
            if (dentro.hundido && dentro.objeto.gameObject.activeSelf)
                dentro.objeto.SetPositionAndRotation(Fondo + vuelta * dentro.posicion, vuelta * dentro.giro);
    }

    // ---------- 2. Mezclar ----------

    void Mezclar()
    {
        if (!cuchara || !puntaCuchara) return;

        Vector3 punta = puntaCuchara.position;
        bool adentro = cuchara.SelectingPointsCount > 0 && Adentro(punta, 0.02f);
        if (adentro && puntaAdentro)
        {
            Vector3 antes = puntaAnterior - boca.position, ahora = punta - boca.position;
            antes.y = ahora.y = 0f;

            // Límite por frame para que un salto del tracking no complete la mezcla de golpe
            float avance = Mathf.Min((ahora - antes).magnitude, 0.15f);
            if (avance > 0.0005f)
            {
                if (ProgresoMezcla <= 0f)
                {
                    if (sopa) sopa.gameObject.SetActive(true);
                    alEmpezarMezcla.Invoke();
                }
                ProgresoMezcla = Mathf.Clamp01(ProgresoMezcla + avance / recorridoMezcla);
                if (antes.sqrMagnitude > 0.0004f && ahora.sqrMagnitude > 0.0004f)
                    anguloMezcla += Vector3.SignedAngle(antes, ahora, Vector3.up) * arrastreIngredientes;
            }
        }
        puntaAdentro = adentro;
        puntaAnterior = punta;

        if (sopa && sopa.gameObject.activeSelf)
            sopa.position = sopaFinal + Vector3.down * (subidaSopa * (1f - ProgresoMezcla));

        if (ProgresoMezcla >= 1f) Terminar();
    }

    // ---------- 3. Sopa lista ----------

    void Terminar()
    {
        EtapaActual = Etapa.SopaLista;
        ProgresoMezcla = 1f;
        if (sopa)
        {
            sopa.gameObject.SetActive(true);
            sopa.position = sopaFinal;
        }
        foreach (EnOlla dentro in enOlla) dentro.objeto.gameObject.SetActive(false);

        if (lista) lista.mensajeFinal = $"¡La {lista.nombrePlato.ToLower()} está lista!";
        Debug.Log("[OllaSopa] La sopa está lista: fin del recorrido");
        alTerminar.Invoke();
    }

    // ---------- Cuchara e indicación ----------

    void DevolverCuchara()
    {
        if (!cuchara) return;
        if (cuchara.SelectingPointsCount > 0)
        {
            sueltaDesde = -1f;
            return;
        }
        if (sueltaDesde < 0f) sueltaDesde = Time.time;
        if (Time.time - sueltaDesde < esperaVolver) return;

        Transform t = cuchara.transform;
        if ((t.position - cucharaInicio.position).sqrMagnitude < 0.000001f) return;
        t.SetPositionAndRotation(
            Vector3.MoveTowards(t.position, cucharaInicio.position, 1.5f * Time.deltaTime),
            Quaternion.RotateTowards(t.rotation, cucharaInicio.rotation, 180f * Time.deltaTime));
    }

    void ActualizarIndicacion()
    {
        if (!indicacion) return;
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        if (!camaraRig || !camaraRig.centerEyeAnchor) return;

        Vector3 ojos = camaraRig.centerEyeAnchor.position;
        bool cerca = Vector3.Distance(ojos, boca.position) <= distanciaIndicacion;
        if (indicacion.gameObject.activeSelf != cerca) indicacion.gameObject.SetActive(cerca);
        if (!cerca) return;

        Vector3 haciaTexto = Vector3.ProjectOnPlane(indicacion.transform.position - ojos, Vector3.up);
        if (haciaTexto.sqrMagnitude > 0.0001f) indicacion.transform.rotation = Quaternion.LookRotation(haciaTexto);

        int total = lista.ingredientes.Count;
        switch (EtapaActual)
        {
            case Etapa.EcharIngredientes:
                indicacion.text = enOlla.Count == 0
                    ? "Echa en la olla los ingredientes\nde la canasta"
                    : $"Ingredientes en la olla: {enOlla.Count} de {total}";
                break;
            case Etapa.Mezclar:
                indicacion.text = ProgresoMezcla <= 0f
                    ? "¡Están todos!\nAgarra la cuchara y mezcla"
                    : $"Mezclando... {Mathf.RoundToInt(ProgresoMezcla * 100f)} %";
                break;
            default:
                indicacion.text = $"¡La {lista.nombrePlato.ToLower()} está lista!";
                break;
        }
    }

    // ---------- Pruebas en el editor ----------

    [ContextMenu("Probar: echar todos los ingredientes")]
    void ProbarEcharTodos()
    {
        foreach (ListaIngredientes.Ingrediente ingrediente in lista.ingredientes)
            if (!ingrediente.enOlla && ingrediente.objeto) Echar(ingrediente);
    }

    [ContextMenu("Probar: terminar la mezcla")]
    void ProbarTerminar()
    {
        if (EtapaActual == Etapa.Mezclar) Terminar();
    }
}
