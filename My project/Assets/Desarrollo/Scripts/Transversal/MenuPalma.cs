using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Menú de la palma (elemento transversal, va en todas las escenas).
// Al abrir la mano con la palma volteada hacia la cara aparece un ícono sobre ella; si se mantiene, se abre el menú (Mapa / Menú).
// El menú se cierra con la X. También se abre y cierra con el gesto de menú de Meta (mirar la palma izquierda y pellizcar)
// o con el botón de menú del control izquierdo.
public class MenuPalma : MonoBehaviour
{
    [Header("Referencias (vacías = se buscan solas en el Camera Rig)")]
    public OVRCameraRig camaraRig;

    [Tooltip("Mano izquierda (ComprehensiveInteractorsLeft)")]
    public Hand manoIzquierda;

    [Tooltip("Mano derecha (ComprehensiveInteractorsRight)")]
    public Hand manoDerecha;

    [Tooltip("Permite abrir el menú también con la palma derecha")]
    public bool usarManoDerecha = true;

    [Header("Partes del menú")]
    [Tooltip("Ícono que flota sobre la palma")]
    public Transform icono;

    [Tooltip("Panel con los botones Mapa, Menú y X. Su eje Z debe apuntar hacia donde se lee.")]
    public Transform panel;

    [Header("Gesto")]
    [Tooltip("Qué tan de frente debe estar la palma hacia los ojos, en grados (0 = totalmente de frente)")]
    public float anguloPalma = 50f;

    [Tooltip("Qué tan lejos del centro de la vista puede estar la mano, en grados (las gafas ven unos 50° hacia cada lado)")]
    public float anguloVista = 50f;

    [Tooltip("Segundos con la palma volteada antes de abrir el menú")]
    public float tiempoParaAbrir = 0.3f;

    [Header("Ubicación")]
    [Tooltip("Separación del ícono desde la palma hacia la cara, en metros")]
    public float alturaIcono = 0.04f;

    [Tooltip("Distancia del panel frente a los ojos al abrirse, en metros. El panel queda fijo ahí para poder tocarlo.")]
    public float distanciaPanel = 0.45f;

    [Tooltip("Cuánto más abajo de los ojos aparece el panel, en metros")]
    public float bajoLosOjos = 0.2f;

    [Tooltip("Si el jugador se aleja más que esto del panel (por ejemplo al teletransportarse), el menú se cierra solo")]
    public float distanciaCierre = 1.5f;

    [Header("Botones")]
    public UnityEvent alPresionarMapa;
    public UnityEvent alPresionarMenu;

    [Header("Depuración")]
    [Tooltip("Muestra frente a los ojos los ángulos en vivo, para afinar el gesto con las gafas puestas")]
    public bool textoDepuracion;

    [Tooltip("Solo para mirar en Play: ángulos actuales de cada mano (palma / vista). 'sin tracking' si no se ve la mano.")]
    public string lecturaIzquierda;
    public string lecturaDerecha;

    public bool Abierto { get; private set; }

    struct Lectura
    {
        public bool detectada, mirando;
        public float anguloPalma, anguloVista;
        public Vector3 palma, haciaCara;
    }

    readonly TransformJointData datosMano = new TransformJointData();
    Hand manoActiva;
    float tiempoMirando;
    bool esperarQueBajeLaMano;
    bool cerrarPendiente;
    TextMeshPro texto;

    void Start()
    {
        icono.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!BuscarReferencias()) return;

        Lectura izquierda = Leer(manoIzquierda);
        Lectura derecha = usarManoDerecha ? Leer(manoDerecha) : default;
        lecturaIzquierda = Describir(izquierda);
        lecturaDerecha = usarManoDerecha ? Describir(derecha) : "desactivada";
        MostrarDepuracion();

        // Con el menú abierto solo cuenta la mano que lo abrió; si no, la primera que voltee la palma
        Lectura lectura = default;
        if (Abierto || tiempoMirando > 0f)
            lectura = manoActiva == manoDerecha ? derecha : izquierda;
        else if (izquierda.mirando) { manoActiva = manoIzquierda; lectura = izquierda; }
        else if (derecha.mirando) { manoActiva = manoDerecha; lectura = derecha; }

        bool mirando = lectura.mirando;
        tiempoMirando = mirando ? tiempoMirando + Time.deltaTime : 0f;

        // Después de cerrar, no se vuelve a abrir hasta que baje la mano
        if (!izquierda.mirando && !derecha.mirando) esperarQueBajeLaMano = false;

        bool mostrarIcono = mirando && !Abierto && !esperarQueBajeLaMano;
        if (icono.gameObject.activeSelf != mostrarIcono) icono.gameObject.SetActive(mostrarIcono);
        if (mostrarIcono)
        {
            icono.position = lectura.palma + lectura.haciaCara * alturaIcono;
            icono.rotation = MirandoAlJugador(icono.position);
        }

        if (!Abierto && !esperarQueBajeLaMano && tiempoMirando >= tiempoParaAbrir)
            AbrirFrenteAlJugador();

        // El panel queda fijo donde se abrió (si se moviera, el poke de los dedos no alcanza a registrar la pulsación).
        // Si el jugador se aleja, se cierra solo.
        if (Abierto && !cerrarPendiente
            && Vector3.Distance(panel.position, camaraRig.centerEyeAnchor.position) > distanciaCierre)
            Cerrar();

        // Gesto de menú de Meta (palma izquierda + pellizco) o botón de menú del control izquierdo
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
        Vector3 frente = Vector3.ProjectOnPlane(ojos.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.0001f) frente = camaraRig.transform.forward;
        Abrir(ojos.position + frente.normalized * distanciaPanel + Vector3.down * bajoLosOjos);
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

        // Vertical (solo gira hacia el jugador en horizontal), como un cartel frente a él
        Vector3 haciaPanel = Vector3.ProjectOnPlane(posicion - camaraRig.centerEyeAnchor.position, Vector3.up);
        panel.SetPositionAndRotation(posicion, Quaternion.LookRotation(haciaPanel));
        panel.gameObject.SetActive(true);
    }

    // El canvas se lee mirando en la misma dirección que su eje Z
    Quaternion MirandoAlJugador(Vector3 posicion)
    {
        Vector3 direccion = posicion - camaraRig.centerEyeAnchor.position;
        return direccion.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direccion) : Quaternion.identity;
    }

    Lectura Leer(Hand mano)
    {
        var lectura = new Lectura();
        if (!mano) return lectura;

        Pose muneca = default, centroPalma = default;
        lectura.detectada = mano.IsTrackedDataValid
            && mano.GetJointPose(HandJointId.HandWristRoot, out muneca)
            && mano.GetJointPose(HandJointId.HandPalm, out centroPalma);
        if (!lectura.detectada) return lectura;

        Transform ojos = camaraRig.centerEyeAnchor;
        datosMano.IsValid = true;
        datosMano.Handedness = mano.Handedness;
        datosMano.WristPose = muneca;
        datosMano.CenterEyePose = new Pose(ojos.position, ojos.rotation);
        datosMano.TrackingSystemUp = camaraRig.trackingSpace.up;
        datosMano.TrackingSystemForward = camaraRig.trackingSpace.forward;

        // Dirección del dorso de la mano según el SDK de Meta (la misma que usa su gesto "PalmTowardsFace")
        Vector3 dorso = TransformFeatureValueProvider.GetHandVectorForFeature(TransformFeature.PalmTowardsFace, datosMano).normalized;
        lectura.palma = centroPalma.position;
        lectura.haciaCara = -dorso;

        // Se mide contra la dirección palma → ojos, no contra hacia dónde apunta la cabeza:
        // así funciona aunque el jugador mire la mano bajando los ojos sin mover la cabeza
        Vector3 haciaOjos = ojos.position - lectura.palma;
        lectura.anguloPalma = Vector3.Angle(lectura.haciaCara, haciaOjos);
        lectura.anguloVista = Vector3.Angle(ojos.forward, -haciaOjos);

        // Margen extra para la mano que ya está abriendo el menú, para que no parpadee en el borde
        float margen = mano == manoActiva && (Abierto || tiempoMirando > 0f) ? 10f : 0f;
        lectura.mirando = lectura.anguloPalma < anguloPalma + margen && lectura.anguloVista < anguloVista + margen;
        return lectura;
    }

    static string Describir(Lectura lectura)
    {
        if (!lectura.detectada) return "sin tracking";
        return $"palma {lectura.anguloPalma:F0}° / vista {lectura.anguloVista:F0}°" + (lectura.mirando ? "  ✓" : "");
    }

    void MostrarDepuracion()
    {
        if (!textoDepuracion)
        {
            if (texto) texto.gameObject.SetActive(false);
            return;
        }

        if (!texto)
        {
            texto = new GameObject("Texto depuración palma").AddComponent<TextMeshPro>();
            texto.transform.SetParent(transform, false);
            texto.fontSize = 0.25f;
            texto.alignment = TextAlignmentOptions.Center;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.overflowMode = TextOverflowModes.Overflow;
            texto.rectTransform.sizeDelta = new Vector2(1f, 0.25f);
        }
        texto.gameObject.SetActive(true);

        Transform ojos = camaraRig.centerEyeAnchor;
        texto.transform.SetPositionAndRotation(ojos.position + ojos.forward * 0.6f - ojos.up * 0.18f, Quaternion.LookRotation(ojos.forward, ojos.up));
        texto.text = $"Izq: {lecturaIzquierda}\nDer: {lecturaDerecha}\nAbrir si palma < {anguloPalma:F0}° y vista < {anguloVista:F0}°";
    }

    bool BuscarReferencias()
    {
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        if (!camaraRig || !camaraRig.centerEyeAnchor) return false;

        if (!manoIzquierda || (usarManoDerecha && !manoDerecha))
        {
            foreach (var t in camaraRig.GetComponentsInChildren<Transform>(true))
            {
                if (!manoIzquierda && t.name == "ComprehensiveInteractorsLeft") manoIzquierda = t.GetComponent<Hand>();
                if (!manoDerecha && t.name == "ComprehensiveInteractorsRight") manoDerecha = t.GetComponent<Hand>();
            }
        }
        return manoIzquierda;
    }
}
