using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mapa del recorrido con las ilustraciones de diseño: una imagen por parada, con la canoa en ese muelle.
// Muestra la parada donde está el jugador o, en camino, la última que visitó. Se abre desde el botón MAPA
// del menú de la palma.
public class MapaRecorrido : MonoBehaviour
{
    [System.Serializable]
    public class Parada
    {
        public string nombre;

        [Tooltip("Punto de la parada sobre el camino del bote (la parada del trayecto, o el bote al inicio)")]
        public Transform punto;

        [Tooltip("Ilustración del mapa con la canoa en esta parada")]
        public Sprite imagen;
    }

    [Header("Recorrido (objetos de la escena)")]
    [Tooltip("Paradas en orden")]
    public List<Parada> paradas = new List<Parada>();

    [Tooltip("Rutas de los trayectos en orden (los objetos 'Ruta' de Navegacion Rio). Sirven para saber cuánto ha avanzado el jugador.")]
    public List<Transform> rutas = new List<Transform>();

    [Tooltip("A esta distancia de una parada, en metros, se considera que el jugador está en ella")]
    public float radioParada = 15f;

    [Header("Panel")]
    public OVRCameraRig camaraRig;

    [Tooltip("Pivote del panel. Su eje Z apunta hacia donde se lee.")]
    public Transform panel;

    [Tooltip("Imagen del canvas donde se muestra la ilustración de la parada")]
    public Image imagenMapa;

    public TMP_Text textoProgreso;

    [Tooltip("Distancia del panel frente a los ojos al abrirse, en metros")]
    public float distanciaPanel = 0.55f;

    [Tooltip("Cuánto más abajo de los ojos aparece el panel, en metros")]
    public float bajoLosOjos = 0.2f;

    public bool Abierto { get; private set; }

    readonly List<Vector3> camino = new List<Vector3>();
    readonly List<float> distancias = new List<float>();
    float[] distanciaParada;
    float maximoRecorrido;
    bool cerrarPendiente;

    void Start()
    {
        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!BuscarRig()) return;

        // El progreso se registra aunque el mapa esté cerrado
        Preparar();
        float actual = DistanciaEnCamino(camaraRig.centerEyeAnchor.position);
        maximoRecorrido = Mathf.Max(maximoRecorrido, actual);

        if (Abierto) Actualizar(actual);
    }

    void LateUpdate()
    {
        // Se oculta después de que el poke termine de procesar la pulsación de la X
        if (!cerrarPendiente) return;
        cerrarPendiente = false;
        panel.gameObject.SetActive(false);
        panel.SetParent(transform, true);
    }

    [ContextMenu("Probar: abrir")]
    public void Abrir()
    {
        if (!BuscarRig()) return;
        Preparar();

        Transform ojos = camaraRig.centerEyeAnchor;
        Vector3 frente = Vector3.ProjectOnPlane(ojos.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.0001f) frente = camaraRig.transform.forward;
        Vector3 posicion = ojos.position + frente.normalized * distanciaPanel + Vector3.down * bajoLosOjos;

        // Hijo del rig: viaja con el jugador en el bote o al teletransportarse, y queda quieto para el poke
        panel.SetParent(camaraRig.transform, true);
        panel.SetPositionAndRotation(posicion, Quaternion.LookRotation(frente));
        panel.gameObject.SetActive(true);
        Abierto = true;
        cerrarPendiente = false;
        Actualizar(DistanciaEnCamino(ojos.position));
    }

    [ContextMenu("Probar: cerrar")]
    public void Cerrar()
    {
        if (!Abierto) return;
        Abierto = false;
        cerrarPendiente = true;
    }

    void Actualizar(float actual)
    {
        int ultimaVisitada = 0;
        for (int i = 0; i < paradas.Count; i++)
            if (maximoRecorrido >= distanciaParada[i] - radioParada) ultimaVisitada = i;

        int cercana = -1;
        for (int i = 0; i < paradas.Count; i++)
            if (Mathf.Abs(actual - distanciaParada[i]) <= radioParada) cercana = i;

        // La ilustración de la parada donde está, o de la última que visitó si va en camino
        Sprite imagen = paradas[cercana >= 0 ? cercana : ultimaVisitada].imagen;
        if (imagen) imagenMapa.sprite = imagen;

        string donde = cercana >= 0
            ? "Estás en: " + paradas[cercana].nombre
            : "En camino a: " + paradas[Mathf.Min(ultimaVisitada + 1, paradas.Count - 1)].nombre;
        textoProgreso.text = $"{donde}   ·   Paradas visitadas: {ultimaVisitada + 1} de {paradas.Count}";
    }

    void Preparar()
    {
        if (distanciaParada != null) return;
        ConstruirCamino();
        distanciaParada = new float[paradas.Count];
        for (int i = 0; i < paradas.Count; i++)
            distanciaParada[i] = DistanciaEnCamino(paradas[i].punto.position);
    }

    void ConstruirCamino()
    {
        camino.Clear();
        camino.Add(Plano(paradas[0].punto.position));
        foreach (var ruta in rutas)
            foreach (Transform punto in ruta)
                camino.Add(Plano(punto.position));

        distancias.Clear();
        distancias.Add(0f);
        for (int i = 1; i < camino.Count; i++)
            distancias.Add(distancias[i - 1] + Vector3.Distance(camino[i - 1], camino[i]));
    }

    static Vector3 Plano(Vector3 p) => new Vector3(p.x, 0f, p.z);

    // Distancia recorrida a lo largo del camino hasta el punto más cercano a 'posicion'
    float DistanciaEnCamino(Vector3 posicion)
    {
        if (camino.Count < 2) ConstruirCamino();
        Vector3 p = Plano(posicion);
        float mejor = float.MaxValue, resultado = 0f;
        for (int i = 0; i < camino.Count - 1; i++)
        {
            Vector3 a = camino[i], b = camino[i + 1];
            float largo = Vector3.Distance(a, b);
            float t = largo > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, (b - a) / largo) / largo) : 0f;
            float d = Vector3.Distance(p, Vector3.Lerp(a, b, t));
            if (d < mejor)
            {
                mejor = d;
                resultado = distancias[i] + largo * t;
            }
        }
        return resultado;
    }

    bool BuscarRig()
    {
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        return camaraRig && camaraRig.centerEyeAnchor;
    }
}
