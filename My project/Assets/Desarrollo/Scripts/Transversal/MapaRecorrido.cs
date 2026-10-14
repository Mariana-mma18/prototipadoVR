using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mapa del recorrido tipo constelación. Dibuja el camino del bote con la forma del río, las paradas,
// cuáles ya visitó el jugador y dónde está ahora. Se abre desde el botón MAPA del menú de la palma.
public class MapaRecorrido : MonoBehaviour
{
    [System.Serializable]
    public class Parada
    {
        public string nombre;

        [Tooltip("Punto de la parada sobre el camino del bote (la parada del trayecto, o el bote al inicio)")]
        public Transform punto;
    }

    [Header("Recorrido (objetos de la escena)")]
    [Tooltip("Paradas en orden")]
    public List<Parada> paradas = new List<Parada>();

    [Tooltip("Rutas de los trayectos en orden (los objetos 'Ruta' de Navegacion Rio). Dan la forma del río en el mapa.")]
    public List<Transform> rutas = new List<Transform>();

    [Tooltip("A esta distancia de una parada, en metros, se considera que el jugador está en ella")]
    public float radioParada = 15f;

    [Header("Panel")]
    public OVRCameraRig camaraRig;

    [Tooltip("Pivote del panel. Su eje Z apunta hacia donde se lee.")]
    public Transform panel;

    [Tooltip("Zona del canvas donde se dibuja el mapa")]
    public RectTransform contenido;

    public TMP_Text textoProgreso;
    public Sprite spriteCirculo;

    [Tooltip("Distancia del panel frente a los ojos al abrirse, en metros")]
    public float distanciaPanel = 0.55f;

    [Tooltip("Cuánto más abajo de los ojos aparece el panel, en metros")]
    public float bajoLosOjos = 0.2f;

    [Header("Estilo")]
    public Color colorRecorrido = Color.white;
    public Color colorPendiente = new Color(0.5f, 0.53f, 0.58f);
    public Color colorActual = new Color(0.2f, 0.75f, 1f);
    public float grosorLinea = 7f;
    public float tamanoNodo = 46f;

    public bool Abierto { get; private set; }

    readonly List<Vector3> camino = new List<Vector3>();
    readonly List<float> distancias = new List<float>();
    readonly List<(Image imagen, float distancia)> segmentos = new List<(Image, float)>();
    float[] distanciaParada;
    Image[] nodos;
    RectTransform marcador;
    float maximoRecorrido;
    bool dibujado;
    bool cerrarPendiente;

    // Proyección del mundo (XZ) al plano del mapa
    Vector2 origen;
    float angulo, escala;
    Vector2 centroMapa;

    void Start()
    {
        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!BuscarRig()) return;

        // El progreso se registra aunque el mapa esté cerrado
        if (!dibujado) Dibujar();
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
        if (!dibujado) Dibujar();

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
        foreach (var (imagen, distancia) in segmentos)
            imagen.color = distancia <= maximoRecorrido ? colorRecorrido : colorPendiente;

        int ultimaVisitada = 0;
        for (int i = 0; i < nodos.Length; i++)
        {
            bool visitada = maximoRecorrido >= distanciaParada[i] - radioParada;
            if (visitada) ultimaVisitada = i;
            nodos[i].color = visitada ? colorRecorrido : colorPendiente;
        }

        marcador.anchoredPosition = AlMapa(PuntoEnCamino(actual));

        // Texto de progreso: en una parada, o en camino a la siguiente
        int cercana = -1;
        for (int i = 0; i < paradas.Count; i++)
            if (Mathf.Abs(actual - distanciaParada[i]) <= radioParada) cercana = i;

        string donde = cercana >= 0
            ? "Estás en: " + paradas[cercana].nombre
            : "En camino a: " + paradas[Mathf.Min(ultimaVisitada + 1, paradas.Count - 1)].nombre;
        textoProgreso.text = $"{donde}   ·   Paradas visitadas: {ultimaVisitada + 1} de {paradas.Count}";
    }

    void Dibujar()
    {
        ConstruirCamino();
        CalcularProyeccion();

        // Camino suavizado con la forma del río
        var puntosMapa = new List<Vector2>();
        var distanciasMapa = new List<float>();
        for (int i = 0; i < camino.Count - 1; i++)
        {
            Vector3 p0 = camino[Mathf.Max(i - 1, 0)], p1 = camino[i], p2 = camino[i + 1], p3 = camino[Mathf.Min(i + 2, camino.Count - 1)];
            for (int m = 0; m < 8; m++)
            {
                float t = m / 8f;
                puntosMapa.Add(AlMapa(CatmullRom(p0, p1, p2, p3, t)));
                distanciasMapa.Add(Mathf.Lerp(distancias[i], distancias[i + 1], t));
            }
        }
        puntosMapa.Add(AlMapa(camino[camino.Count - 1]));
        distanciasMapa.Add(distancias[distancias.Count - 1]);

        for (int i = 0; i < puntosMapa.Count - 1; i++)
        {
            Vector2 a = puntosMapa[i], b = puntosMapa[i + 1];
            var linea = CrearImagen("Linea " + i, null);
            linea.rectTransform.sizeDelta = new Vector2(Vector2.Distance(a, b) + grosorLinea * 0.5f, grosorLinea);
            linea.rectTransform.anchoredPosition = (a + b) / 2f;
            linea.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            segmentos.Add((linea, (distanciasMapa[i] + distanciasMapa[i + 1]) / 2f));
        }

        // Paradas
        nodos = new Image[paradas.Count];
        distanciaParada = new float[paradas.Count];
        for (int i = 0; i < paradas.Count; i++)
        {
            distanciaParada[i] = DistanciaEnCamino(paradas[i].punto.position);
            var nodo = CrearImagen("Parada " + paradas[i].nombre, spriteCirculo);
            nodo.rectTransform.sizeDelta = Vector2.one * tamanoNodo;
            nodo.rectTransform.anchoredPosition = AlMapa(paradas[i].punto.position);
            nodos[i] = nodo;

            var etiqueta = new GameObject("Nombre", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            etiqueta.transform.SetParent(nodo.transform, false);
            etiqueta.text = paradas[i].nombre;
            etiqueta.fontSize = 22;
            etiqueta.fontStyle = FontStyles.Bold;
            etiqueta.alignment = TextAlignmentOptions.Center;
            etiqueta.textWrappingMode = TextWrappingModes.NoWrap;
            etiqueta.raycastTarget = false;
            // Nombres alternados arriba y abajo para que no choquen con el camino
            etiqueta.rectTransform.anchoredPosition = new Vector2(0, (i % 2 == 0 ? -1 : 1) * (tamanoNodo * 0.5f + 16f));
            etiqueta.rectTransform.sizeDelta = new Vector2(160, 30);
        }

        // Marcador "Estás aquí"
        var anillo = CrearImagen("Estás aquí", spriteCirculo);
        anillo.color = colorActual;
        anillo.rectTransform.sizeDelta = Vector2.one * (tamanoNodo * 0.6f);
        marcador = anillo.rectTransform;

        dibujado = true;
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

    // Gira el camino para que vaya de izquierda a derecha y lo escala para que quepa en el contenido
    void CalcularProyeccion()
    {
        Vector3 inicio = camino[0], fin = camino[camino.Count - 1];
        origen = new Vector2(inicio.x, inicio.z);
        angulo = Mathf.Atan2(fin.z - inicio.z, fin.x - inicio.x);

        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var p in camino)
        {
            Vector2 r = Rotar(new Vector2(p.x, p.z));
            min = Vector2.Min(min, r);
            max = Vector2.Max(max, r);
        }

        Vector2 tamano = contenido.rect.size - Vector2.one * (tamanoNodo + 40f);
        Vector2 rango = Vector2.Max(max - min, Vector2.one);
        escala = Mathf.Min(tamano.x / rango.x, tamano.y / rango.y);
        centroMapa = (min + max) / 2f;
    }

    Vector2 Rotar(Vector2 p)
    {
        Vector2 d = p - origen;
        float c = Mathf.Cos(-angulo), s = Mathf.Sin(-angulo);
        return new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
    }

    Vector2 AlMapa(Vector3 mundo) => (Rotar(new Vector2(mundo.x, mundo.z)) - centroMapa) * escala;

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

    Vector3 PuntoEnCamino(float distancia)
    {
        int i = 1;
        while (i < distancias.Count - 1 && distancias[i] < distancia) i++;
        float t = Mathf.InverseLerp(distancias[i - 1], distancias[i], distancia);
        return Vector3.Lerp(camino[i - 1], camino[i], t);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
    }

    Image CrearImagen(string nombre, Sprite sprite)
    {
        var imagen = new GameObject(nombre, typeof(RectTransform)).AddComponent<Image>();
        imagen.transform.SetParent(contenido, false);
        imagen.sprite = sprite;
        imagen.raycastTarget = false;
        return imagen;
    }

    bool BuscarRig()
    {
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        return camaraRig && camaraRig.centerEyeAnchor;
    }
}
