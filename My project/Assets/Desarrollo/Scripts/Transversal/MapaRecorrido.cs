using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mapa del recorrido con la ilustración de diseño. La canoa avanza por el río del dibujo según lo que el
// jugador ha recorrido, y detrás queda el rastro hasta el último muelle visitado. Se abre desde el botón
// MAPA del menú de la palma.
public class MapaRecorrido : MonoBehaviour
{
    [System.Serializable]
    public class Parada
    {
        public string nombre;

        [Tooltip("Punto de la parada sobre el camino del bote (la parada del trayecto, o el bote al inicio)")]
        public Transform punto;

        [Tooltip("Dónde va la canoa en la ilustración, en píxeles desde la esquina superior izquierda")]
        public Vector2 posicionEnMapa;

        [Tooltip("Giro de la canoa en este muelle, en grados (positivo = antihorario)")]
        public float giroEnMapa;

        [Tooltip("Rastro de la canoa hasta este muelle (del tamaño de la ilustración)")]
        public Sprite rastro;
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

    [Tooltip("Imagen con el fondo del mapa. La canoa y los rastros son hijos suyos.")]
    public Image imagenMapa;

    public Image rastro;

    [Tooltip("Rastro que se desvanece al llegar a un muelle nuevo")]
    public Image rastroAnterior;

    public RectTransform canoa;

    [Tooltip("Opcional: texto con la parada actual y las visitadas")]
    public TMP_Text textoProgreso;

    [Tooltip("Tamaño en píxeles de la ilustración original (para las posiciones de la canoa)")]
    public Vector2 tamanoIlustracion = new Vector2(3508, 2480);

    [Tooltip("Segundos del cambio de rastro al llegar a un muelle")]
    public float duracionCambioRastro = 0.6f;

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
    int rastroMostrado = -1;
    float cambioRastro = 1f;

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

        if (Abierto) Actualizar(actual, true);
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
        Actualizar(DistanciaEnCamino(ojos.position), false);
    }

    [ContextMenu("Probar: cerrar")]
    public void Cerrar()
    {
        if (!Abierto) return;
        Abierto = false;
        cerrarPendiente = true;
    }

    void Actualizar(float actual, bool animar)
    {
        int ultimaVisitada = 0;
        for (int i = 0; i < paradas.Count; i++)
            if (maximoRecorrido >= distanciaParada[i] - radioParada) ultimaVisitada = i;

        int cercana = -1;
        for (int i = 0; i < paradas.Count; i++)
            if (Mathf.Abs(actual - distanciaParada[i]) <= radioParada) cercana = i;

        MoverCanoa(actual);
        MostrarRastro(ultimaVisitada, animar);

        if (!textoProgreso) return;
        string donde = cercana >= 0
            ? "Estás en: " + paradas[cercana].nombre
            : "En camino a: " + paradas[Mathf.Min(ultimaVisitada + 1, paradas.Count - 1)].nombre;
        textoProgreso.text = $"{donde}   ·   Paradas visitadas: {ultimaVisitada + 1} de {paradas.Count}";
    }

    // La canoa queda en el muelle mientras el jugador está en la parada y avanza por el tramo al remar
    void MoverCanoa(float actual)
    {
        int n = paradas.Count;
        int tramo = n - 2;
        for (int i = 0; i < n - 1; i++)
            if (actual < distanciaParada[i + 1]) { tramo = i; break; }

        float desde = distanciaParada[tramo] + radioParada, hasta = distanciaParada[tramo + 1] - radioParada;
        float t = hasta > desde
            ? Mathf.Clamp01((actual - desde) / (hasta - desde))
            : (actual >= (desde + hasta) / 2f ? 1f : 0f);

        Vector2 p = CatmullRom(PosicionMuelle(tramo - 1), PosicionMuelle(tramo), PosicionMuelle(tramo + 1), PosicionMuelle(tramo + 2), t);
        float giro = Mathf.LerpAngle(paradas[tramo].giroEnMapa, paradas[tramo + 1].giroEnMapa, t);

        canoa.anchoredPosition = AlMapa(p);
        canoa.localRotation = Quaternion.Euler(0f, 0f, giro);
    }

    Vector2 PosicionMuelle(int i) => paradas[Mathf.Clamp(i, 0, paradas.Count - 1)].posicionEnMapa;

    void MostrarRastro(int indice, bool animar)
    {
        if (indice != rastroMostrado)
        {
            rastroAnterior.sprite = rastro.sprite;
            rastroAnterior.enabled = animar && rastro.sprite;
            rastro.sprite = paradas[indice].rastro;
            rastro.enabled = rastro.sprite;
            rastroMostrado = indice;
            cambioRastro = animar ? 0f : 1f;
        }

        if (cambioRastro < 1f)
            cambioRastro = Mathf.Min(1f, cambioRastro + Time.deltaTime / Mathf.Max(duracionCambioRastro, 0.01f));

        rastro.color = new Color(1f, 1f, 1f, cambioRastro);
        rastroAnterior.color = new Color(1f, 1f, 1f, 1f - cambioRastro);
        if (cambioRastro >= 1f) rastroAnterior.enabled = false;
    }

    // Píxeles de la ilustración (desde arriba a la izquierda) → posición dentro de la imagen del mapa
    Vector2 AlMapa(Vector2 pixel)
    {
        Rect r = imagenMapa.rectTransform.rect;
        return new Vector2((pixel.x / tamanoIlustracion.x - 0.5f) * r.width, (0.5f - pixel.y / tamanoIlustracion.y) * r.height);
    }

    void Preparar()
    {
        if (distanciaParada != null) return;
        ConstruirCamino();
        distanciaParada = new float[paradas.Count];
        for (int i = 0; i < paradas.Count; i++)
            distanciaParada[i] = DistanciaEnCamino(paradas[i].punto.position);

        // La canoa a escala de la ilustración (su sprite no debe reducirse al importarlo: tamaño máximo ≥ 1024)
        Image imagenCanoa = canoa.GetComponent<Image>();
        if (imagenCanoa && imagenCanoa.sprite)
        {
            Sprite s = imagenCanoa.sprite;
            canoa.sizeDelta = s.rect.size * (imagenMapa.rectTransform.rect.width / tamanoIlustracion.x);
            canoa.pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
        }
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

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
    }

    bool BuscarRig()
    {
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        return camaraRig && camaraRig.centerEyeAnchor;
    }
}
