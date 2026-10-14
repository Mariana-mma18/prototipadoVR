using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;
using UnityEngine.Events;

// Menú de la palma (elemento transversal, va en todas las escenas).
// Al girar la mano y mirar la palma aparece un ícono sobre ella; si la sigue mirando se abre el menú (Mapa / Menú).
// El menú se cierra con la X. Con controles se abre y cierra con el botón de menú del control izquierdo.
public class MenuPalma : MonoBehaviour
{
    [Header("Referencias (vacías = se buscan solas en el Camera Rig)")]
    public OVRCameraRig camaraRig;

    [Tooltip("Mano que abre el menú. Por defecto la izquierda (ComprehensiveInteractorsLeft)")]
    public Hand mano;

    [Header("Partes del menú")]
    [Tooltip("Ícono que flota sobre la palma")]
    public Transform icono;

    [Tooltip("Panel con los botones Mapa, Menú y X. Su eje Z debe apuntar hacia donde se lee.")]
    public Transform panel;

    [Header("Gesto")]
    [Tooltip("Ángulo máximo entre la palma y la mirada para considerar que el jugador mira su palma")]
    public float anguloPalma = 50f;

    [Tooltip("Qué tan centrada debe estar la mano en la vista, en grados")]
    public float anguloVista = 35f;

    [Tooltip("Segundos mirando la palma antes de abrir el menú")]
    public float tiempoParaAbrir = 0.5f;

    [Header("Ubicación")]
    [Tooltip("Separación del ícono desde la palma hacia la cara, en metros")]
    public float alturaIcono = 0.04f;

    [Tooltip("Cuánto más arriba de la palma aparece el panel, en metros")]
    public float alturaPanel = 0.14f;

    [Tooltip("Qué tan rápido sigue el panel a la mano")]
    public float suavizado = 12f;

    [Header("Botones")]
    public UnityEvent alPresionarMapa;
    public UnityEvent alPresionarMenu;

    [Header("Depuración (se actualiza en Play, solo para mirar)")]
    [Tooltip("La mano tiene tracking válido")]
    public bool manoDetectada;

    [Tooltip("Ángulo actual entre la palma y la mirada; tiene que bajar de 'anguloPalma' para abrir")]
    public float anguloPalmaActual;

    [Tooltip("Qué tan lejos del centro de la vista está la mano; tiene que bajar de 'anguloVista' para abrir")]
    public float anguloVistaActual;

    public bool Abierto { get; private set; }

    readonly TransformJointData datosMano = new TransformJointData();
    float tiempoMirando;
    bool esperarQueBajeLaMano;
    bool cerrarPendiente;

    void Start()
    {
        icono.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!BuscarReferencias()) return;

        bool mirando = MirandoPalma(out Vector3 palma, out Vector3 haciaCara);
        tiempoMirando = mirando ? tiempoMirando + Time.deltaTime : 0f;

        // Después de cerrar, no se vuelve a abrir hasta que baje la mano
        if (!mirando) esperarQueBajeLaMano = false;

        bool mostrarIcono = mirando && !Abierto && !esperarQueBajeLaMano;
        if (icono.gameObject.activeSelf != mostrarIcono) icono.gameObject.SetActive(mostrarIcono);
        if (mostrarIcono)
        {
            icono.position = palma + haciaCara * alturaIcono;
            icono.rotation = MirandoAlJugador(icono.position);
        }

        if (!Abierto && !esperarQueBajeLaMano && tiempoMirando >= tiempoParaAbrir)
            Abrir(palma + Vector3.up * alturaPanel);

        // Mientras mira la palma el panel la sigue; si baja la mano, el panel se queda donde está para poder tocarlo
        if (Abierto && mirando)
            Ubicar(palma + Vector3.up * alturaPanel, Time.deltaTime * suavizado);

        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            if (Abierto) Cerrar();
            else AbrirFrenteAlJugador();
        }
    }

    void LateUpdate()
    {
        // Se oculta después de que el poke termine de procesar la pulsación de la X
        if (!cerrarPendiente) return;
        cerrarPendiente = false;
        panel.gameObject.SetActive(false);
    }

    [ContextMenu("Probar: abrir frente al jugador")]
    public void AbrirFrenteAlJugador()
    {
        if (!BuscarReferencias()) return;
        Transform ojos = camaraRig.centerEyeAnchor;
        Vector3 frente = Vector3.ProjectOnPlane(ojos.forward, Vector3.up).normalized;
        Abrir(ojos.position + frente * 0.45f + Vector3.down * 0.15f);
    }

    [ContextMenu("Probar: cerrar")]
    public void Cerrar()
    {
        if (!Abierto) return;
        Abierto = false;
        esperarQueBajeLaMano = true;
        cerrarPendiente = true;
    }

    public void PresionarMapa()
    {
        Debug.Log("[MenuPalma] Mapa");
        alPresionarMapa.Invoke();
    }

    public void PresionarMenu()
    {
        Debug.Log("[MenuPalma] Menú");
        alPresionarMenu.Invoke();
    }

    void Abrir(Vector3 posicion)
    {
        Abierto = true;
        cerrarPendiente = false;
        icono.gameObject.SetActive(false);
        Ubicar(posicion, 1f);
        panel.gameObject.SetActive(true);
    }

    void Ubicar(Vector3 posicion, float t)
    {
        panel.position = Vector3.Lerp(panel.position, posicion, t);
        panel.rotation = Quaternion.Slerp(panel.rotation, MirandoAlJugador(posicion), t);
    }

    // El canvas se lee mirando en la misma dirección que su eje Z
    Quaternion MirandoAlJugador(Vector3 posicion)
    {
        Vector3 direccion = posicion - camaraRig.centerEyeAnchor.position;
        return direccion.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direccion) : Quaternion.identity;
    }

    bool MirandoPalma(out Vector3 palma, out Vector3 haciaCara)
    {
        palma = haciaCara = Vector3.zero;
        Pose muneca = default, centroPalma = default;
        manoDetectada = mano.IsTrackedDataValid
            && mano.GetJointPose(HandJointId.HandWristRoot, out muneca)
            && mano.GetJointPose(HandJointId.HandPalm, out centroPalma);
        if (!manoDetectada) return false;

        Transform ojos = camaraRig.centerEyeAnchor;
        datosMano.IsValid = true;
        datosMano.Handedness = mano.Handedness;
        datosMano.WristPose = muneca;
        datosMano.CenterEyePose = new Pose(ojos.position, ojos.rotation);
        datosMano.TrackingSystemUp = camaraRig.trackingSpace.up;
        datosMano.TrackingSystemForward = camaraRig.trackingSpace.forward;

        // Mismo cálculo que el gesto "PalmTowardsFace" del SDK de Meta: el dorso de la mano apunta hacia donde mira el jugador
        Vector3 dorso = TransformFeatureValueProvider.GetHandVectorForFeature(TransformFeature.PalmTowardsFace, datosMano);
        palma = centroPalma.position;
        haciaCara = -dorso.normalized;

        // Margen extra una vez detectado, para que no parpadee en el borde
        float margen = tiempoMirando > 0f ? 10f : 0f;
        anguloPalmaActual = Vector3.Angle(dorso, ojos.forward);
        anguloVistaActual = Vector3.Angle(ojos.forward, palma - ojos.position);
        return anguloPalmaActual < anguloPalma + margen && anguloVistaActual < anguloVista + margen;
    }

    bool BuscarReferencias()
    {
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        if (!camaraRig || !camaraRig.centerEyeAnchor) return false;

        if (!mano)
        {
            foreach (var t in camaraRig.GetComponentsInChildren<Transform>(true))
                if (t.name == "ComprehensiveInteractorsLeft") mano = t.GetComponent<Hand>();
        }
        return mano;
    }
}
