using System.Collections.Generic;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lista de ingredientes del plato de la experiencia. Se abre desde el botón MENÚ del menú de la palma.
// Cada ingrediente se marca con un chulito mientras está guardado en la canasta (encajado en la zona de
// ingredientes del Canasto); si el jugador lo saca, se desmarca. Al echarlo en la olla de la maloca
// sigue marcado. Los ingredientes se agrupan en columnas según su chagra.
public class ListaIngredientes : MonoBehaviour
{
    [System.Serializable]
    public class Ingrediente
    {
        public string nombre;

        [Tooltip("Columna donde aparece (por ejemplo, la chagra donde se recoge)")]
        public string grupo;

        [Tooltip("Raíz del ingrediente en la escena (la misma que está en la lista del Canasto)")]
        public Transform objeto;

        // Ya lo echaron en la olla de la maloca (lo marca OllaSopa)
        [System.NonSerialized] public bool enOlla;

        [System.NonSerialized] public SnapInteractor encaje;
        [System.NonSerialized] public Image fondoCasilla, palitoCorto, palitoLargo;
        [System.NonSerialized] public TMP_Text texto;
    }

    [Header("Plato")]
    [Tooltip("Nombre del plato que se cocina en la Maloka (sale en el título de la lista)")]
    public string nombrePlato = "Inchicapi";

    [Tooltip("Mensaje de la lista cuando ya están todos los ingredientes en la canasta")]
    public string mensajeTodoRecogido = "¡Tienes todo! Lleva la canasta a la Maloka para cocinar el inchicapi.";

    public List<Ingrediente> ingredientes = new List<Ingrediente>();

    [Tooltip("Canasta donde se guardan los ingredientes")]
    public Canasto canasto;

    [Header("Panel")]
    public OVRCameraRig camaraRig;

    [Tooltip("Pivote del panel. Su eje Z apunta hacia donde se lee.")]
    public Transform panel;

    [Tooltip("Zona del canvas donde se dibuja la lista")]
    public RectTransform contenido;

    public TMP_Text titulo;
    public TMP_Text textoProgreso;

    [Tooltip("Distancia del panel frente a los ojos al abrirse, en metros")]
    public float distanciaPanel = 0.55f;

    [Tooltip("Cuánto más abajo de los ojos aparece el panel, en metros")]
    public float bajoLosOjos = 0.2f;

    [Header("Estilo")]
    public Color colorGuardado = new Color(0.35f, 0.85f, 0.4f);
    public Color colorPendiente = new Color(0.5f, 0.53f, 0.58f);
    public Color colorTextoGuardado = Color.white;
    public Color colorTextoPendiente = new Color(0.72f, 0.74f, 0.78f);
    public float tamanoTexto = 24f;
    public float altoFila = 31f;

    public bool Abierto { get; private set; }

    // Cuántos ingredientes hay ahora en la canasta y cuántos ya se echaron en la olla
    public int Guardados { get; private set; }
    public int EnOlla { get; private set; }
    public bool Completa => Guardados + EnOlla == ingredientes.Count && ingredientes.Count > 0;

    // Mensaje final que muestra la lista cuando el plato queda listo (lo pone OllaSopa)
    [System.NonSerialized] public string mensajeFinal;

    bool dibujado;
    bool cerrarPendiente;

    void Start()
    {
        panel.gameObject.SetActive(false);
        foreach (Ingrediente ingrediente in ingredientes)
            if (ingrediente.objeto) ingrediente.encaje = ingrediente.objeto.GetComponentInChildren<SnapInteractor>(true);
    }

    void Update()
    {
        // Se cuenta aunque el panel esté cerrado, para que otros scripts (la olla) puedan preguntar
        int guardados = 0, enOlla = 0;
        foreach (Ingrediente ingrediente in ingredientes)
        {
            if (ingrediente.enOlla) enOlla++;
            else if (EnCanasta(ingrediente)) guardados++;
        }
        Guardados = guardados;
        EnOlla = enOlla;

        if (Abierto) Actualizar();
    }

    void LateUpdate()
    {
        // Se oculta después de que el poke termine de procesar la pulsación de la X
        if (!cerrarPendiente) return;
        cerrarPendiente = false;
        panel.gameObject.SetActive(false);
        panel.SetParent(transform, true);
    }

    public bool EnCanasta(Ingrediente ingrediente)
    {
        SnapInteractor encaje = ingrediente.encaje;
        return encaje && canasto && encaje.HasSelectedInteractable
               && encaje.SelectedInteractable == canasto.zonaIngredientes;
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
        Actualizar();
    }

    [ContextMenu("Probar: cerrar")]
    public void Cerrar()
    {
        if (!Abierto) return;
        Abierto = false;
        cerrarPendiente = true;
    }

    void Actualizar()
    {
        int guardados = 0, enOlla = 0;
        foreach (Ingrediente ingrediente in ingredientes)
        {
            if (ingrediente.enOlla) enOlla++;
            else if (EnCanasta(ingrediente)) guardados++;
            bool marcado = ingrediente.enOlla || EnCanasta(ingrediente);
            if (!ingrediente.texto) continue;
            ingrediente.fondoCasilla.color = marcado ? colorGuardado : colorPendiente;
            ingrediente.palitoCorto.enabled = marcado;
            ingrediente.palitoLargo.enabled = marcado;
            ingrediente.texto.color = marcado ? colorTextoGuardado : colorTextoPendiente;
        }

        int total = ingredientes.Count;
        if (!string.IsNullOrEmpty(mensajeFinal))
            textoProgreso.text = mensajeFinal;
        else if (enOlla == total)
            textoProgreso.text = "Todo está en la olla: mezcla con la cuchara.";
        else if (enOlla > 0)
            textoProgreso.text = $"En la olla: {enOlla} de {total}   ·   En la canasta: {guardados}";
        else if (guardados == total)
            textoProgreso.text = mensajeTodoRecogido;
        else
            textoProgreso.text = $"En la canasta: {guardados} de {total}";
    }

    // Una columna por grupo, en el orden en que aparecen en la lista
    void Dibujar()
    {
        dibujado = true;
        if (titulo) titulo.text = $"Ingredientes · {nombrePlato}";

        // Los mensajes largos se achican para caber en una línea
        textoProgreso.fontSizeMax = textoProgreso.fontSize;
        textoProgreso.fontSizeMin = 14f;
        textoProgreso.enableAutoSizing = true;
        foreach (Transform hijo in contenido) Destroy(hijo.gameObject);

        var grupos = new List<string>();
        foreach (Ingrediente ingrediente in ingredientes)
            if (!grupos.Contains(ingrediente.grupo)) grupos.Add(ingrediente.grupo);
        if (grupos.Count == 0) return;

        Rect zona = contenido.rect;
        float anchoColumna = zona.width / grupos.Count;
        bool conEncabezado = grupos.Count > 1 || !string.IsNullOrEmpty(grupos[0]);

        for (int c = 0; c < grupos.Count; c++)
        {
            float x = zona.xMin + anchoColumna * c + 12f;
            float y = zona.yMax;

            if (conEncabezado)
            {
                TMP_Text encabezado = CrearTexto("Grupo " + grupos[c], grupos[c], tamanoTexto * 0.8f, new Vector2(x, y), anchoColumna - 24f);
                encabezado.fontStyle = FontStyles.Bold;
                encabezado.color = colorTextoGuardado;
                y -= altoFila;
            }

            foreach (Ingrediente ingrediente in ingredientes)
            {
                if (ingrediente.grupo != grupos[c]) continue;
                CrearFila(ingrediente, new Vector2(x, y), anchoColumna - 24f);
                y -= altoFila;
            }
        }
    }

    void CrearFila(Ingrediente ingrediente, Vector2 arribaIzquierda, float ancho)
    {
        float lado = altoFila * 0.7f;
        Vector2 centroCasilla = arribaIzquierda + new Vector2(lado / 2f, -altoFila / 2f);

        ingrediente.fondoCasilla = CrearImagen("Casilla " + ingrediente.nombre, contenido, centroCasilla, new Vector2(lado, lado), 0f);

        // Chulito: dos palitos blancos sobre la casilla
        RectTransform casilla = ingrediente.fondoCasilla.rectTransform;
        float grosor = lado * 0.16f;
        ingrediente.palitoCorto = CrearImagen("Chulito corto", casilla, new Vector2(-lado * 0.17f, -lado * 0.06f), new Vector2(lado * 0.36f, grosor), -45f);
        ingrediente.palitoLargo = CrearImagen("Chulito largo", casilla, new Vector2(lado * 0.1f, lado * 0.04f), new Vector2(lado * 0.62f, grosor), 50f);
        ingrediente.palitoCorto.color = ingrediente.palitoLargo.color = Color.white;

        ingrediente.texto = CrearTexto("Texto " + ingrediente.nombre, ingrediente.nombre, tamanoTexto,
            arribaIzquierda + new Vector2(lado + 12f, 0f), ancho - lado - 12f);
    }

    Image CrearImagen(string nombre, RectTransform padre, Vector2 posicion, Vector2 tamano, float giro)
    {
        var imagen = new GameObject(nombre, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransform rt = imagen.rectTransform;
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamano;
        rt.anchoredPosition = posicion;
        rt.localRotation = Quaternion.Euler(0f, 0f, giro);
        imagen.raycastTarget = false;
        return imagen;
    }

    TMP_Text CrearTexto(string nombre, string contenidoTexto, float tamano, Vector2 arribaIzquierda, float ancho)
    {
        var texto = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        RectTransform rt = texto.rectTransform;
        rt.SetParent(contenido, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(ancho, altoFila);
        rt.anchoredPosition = arribaIzquierda;
        if (textoProgreso) texto.font = textoProgreso.font;
        texto.fontSize = tamano;
        texto.alignment = TextAlignmentOptions.MidlineLeft;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.overflowMode = TextOverflowModes.Ellipsis;
        texto.raycastTarget = false;
        texto.text = contenidoTexto;
        return texto;
    }

    bool BuscarRig()
    {
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        return camaraRig && camaraRig.centerEyeAnchor;
    }
}
