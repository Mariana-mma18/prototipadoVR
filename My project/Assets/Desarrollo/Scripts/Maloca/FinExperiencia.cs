using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Cierra la experiencia cuando ya no queda nada por hacer:
// 1. Espera a que la sopa esté lista (OllaSopa) y a que Yari termine de hablar (ningún DialogoYari sonando).
// 2. Tras unos segundos para disfrutar el final, funde la vista y el sonido a negro.
// 3. En la oscuridad muestra un mensaje final (solo se ve ese texto, el mundo y las manos dejan de dibujarse).
// 4. Vuelve al inicio (escena 0, el restaurante) para el siguiente visitante, cierra la app o se queda en negro.
public class FinExperiencia : MonoBehaviour
{
    public enum Despues { VolverAlInicio, CerrarAplicacion, QuedarseEnNegro }

    [Tooltip("Si se deja vacío, busca la OllaSopa de la escena")]
    public OllaSopa olla;

    [Tooltip("Segundos de espera desde que la sopa está lista y Yari terminó de hablar hasta que empieza el fundido")]
    public float esperaAntes = 6f;

    [Tooltip("Segundos que tarda la vista (y el sonido) en irse a negro")]
    public float fundidoNegro = 3f;

    [Header("Mensaje final")]
    [TextArea(3, 8)]
    public string mensaje = "<size=140%><b>Selva que alimenta</b></size>\n\nGracias por acompañarnos\na preparar el inchicapi";

    [Tooltip("Vacío = la fuente por defecto de TextMeshPro")]
    public TMP_FontAsset fuente;

    public float tamanoTexto = 0.9f;

    [Tooltip("Metros entre los ojos y el mensaje")]
    public float distanciaMensaje = 2f;

    [Tooltip("Segundos que se ve el mensaje (sin contar sus fundidos)")]
    public float duracionMensaje = 10f;

    public float fundidoMensaje = 1.5f;

    [Header("Al final")]
    public Despues despues = Despues.VolverAlInicio;

    [Tooltip("Escena que se carga con 'Volver al inicio' (0 = RESTAURANTE en Build Settings)")]
    public int escenaInicio = 0;

    [Tooltip("Se llama al empezar el fundido a negro (por ejemplo, para una música final)")]
    public UnityEvent alEmpezarFin;

    // Capa sin nombre que no usa nada más: al final la cámara solo dibuja esta capa
    const int capaFin = 31;

    public bool Terminando { get; private set; }

    OVRCameraRig camaraRig;
    DialogoYari[] dialogos;
    float listoDesde = -1f;
    TextMeshPro texto;

    void Start()
    {
        if (!olla) olla = FindFirstObjectByType<OllaSopa>();
        dialogos = FindObjectsByType<DialogoYari>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    void Update()
    {
        if (Terminando) return;

        if (!olla || olla.EtapaActual != OllaSopa.Etapa.SopaLista || AlguienHablando())
        {
            listoDesde = -1f;
            return;
        }

        if (listoDesde < 0f) listoDesde = Time.time;
        if (Time.time - listoDesde >= esperaAntes) Terminar();
    }

    bool AlguienHablando()
    {
        foreach (DialogoYari d in dialogos)
            if (d && d.Hablando) return true;
        return false;
    }

    [ContextMenu("Probar: terminar la experiencia ahora")]
    public void Terminar()
    {
        if (Terminando || !Application.isPlaying) return;
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        if (!camaraRig || !camaraRig.centerEyeAnchor)
        {
            Debug.LogWarning("[FinExperiencia] No encuentro el OVRCameraRig, no puedo cerrar la experiencia");
            return;
        }
        Terminando = true;
        StartCoroutine(Secuencia());
    }

    IEnumerator Secuencia()
    {
        Debug.Log("[FinExperiencia] Fin de la experiencia: fundido a negro");
        alEmpezarFin.Invoke();

        Transform ojos = camaraRig.centerEyeAnchor;
        Camera camara = ojos.GetComponent<Camera>();

        // ---------- 1. Vista y sonido a negro ----------
        AmbienteEnBucle.DesvanecerTodos(fundidoNegro);
        float volumenInicial = AudioListener.volume;
        Image negro = CrearFundido(ojos, camara);
        if (camara) camara.cullingMask |= 1 << capaFin;

        for (float t = 0f; t < fundidoNegro; t += Time.unscaledDeltaTime)
        {
            float k = t / fundidoNegro;
            negro.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, k));
            AudioListener.volume = volumenInicial * (1f - k);   // también apaga diálogos, remos, etc.
            yield return null;
        }
        negro.color = Color.black;
        AudioListener.volume = 0f;

        // ---------- 2. Solo el mensaje en la oscuridad ----------
        // Las cámaras dejan de dibujar el mundo y las manos; fondo negro liso
        foreach (Camera c in Camera.allCameras)
        {
            c.cullingMask = 1 << capaFin;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
        }
        negro.canvas.gameObject.SetActive(false);

        texto = CrearMensaje();
        ColocarMensaje(ojos, true);

        yield return FundirMensaje(0f, 1f, ojos);
        for (float t = 0f; t < duracionMensaje; t += Time.unscaledDeltaTime)
        {
            ColocarMensaje(ojos, false);
            yield return null;
        }

        if (despues == Despues.QuedarseEnNegro) yield break;
        yield return FundirMensaje(1f, 0f, ojos);
        yield return new WaitForSecondsRealtime(0.5f);

        // ---------- 3. Volver al inicio o cerrar ----------
        if (despues == Despues.CerrarAplicacion)
        {
            Debug.Log("[FinExperiencia] Cerrando la aplicación");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            yield break;
        }

        // AudioListener.volume no se reinicia solo al cambiar de escena: se devuelve cuando la nueva ya cargó
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += RestaurarVolumen;
        UnityEngine.SceneManagement.SceneManager.LoadScene(escenaInicio);
    }

    static void RestaurarVolumen(UnityEngine.SceneManagement.Scene escena, UnityEngine.SceneManagement.LoadSceneMode modo)
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= RestaurarVolumen;
        AudioListener.volume = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarVolumen() => AudioListener.volume = 1f;

    // Panel negro pegado a los ojos, justo después del plano cercano de la cámara para que tape todo
    Image CrearFundido(Transform ojos, Camera camara)
    {
        float distancia = (camara ? camara.nearClipPlane : 0.1f) + 0.03f;

        var lienzo = new GameObject("Fin - fundido a negro", typeof(RectTransform)).AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.WorldSpace;
        lienzo.sortingOrder = short.MaxValue;   // se dibuja después de cualquier otro transparente
        lienzo.gameObject.layer = capaFin;

        var rt = (RectTransform)lienzo.transform;
        rt.SetParent(ojos, false);
        rt.localPosition = new Vector3(0f, 0f, distancia);
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(100f, 100f);
        rt.localScale = Vector3.one * (distancia * 12f / 100f);   // cubre unos 160° de campo de visión

        var imagen = new GameObject("Negro", typeof(RectTransform)).AddComponent<Image>();
        imagen.gameObject.layer = capaFin;
        imagen.transform.SetParent(rt, false);
        imagen.rectTransform.anchorMin = Vector2.zero;
        imagen.rectTransform.anchorMax = Vector2.one;
        imagen.rectTransform.offsetMin = imagen.rectTransform.offsetMax = Vector2.zero;
        imagen.raycastTarget = false;
        imagen.color = new Color(0f, 0f, 0f, 0f);
        return imagen;
    }

    TextMeshPro CrearMensaje()
    {
        var t = new GameObject("Fin - mensaje").AddComponent<TextMeshPro>();
        t.gameObject.layer = capaFin;
        if (fuente) t.font = fuente;
        t.text = mensaje;
        t.fontSize = tamanoTexto;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = new Vector2(1.6f, 1f);
        t.color = new Color(1f, 1f, 1f, 0f);
        return t;
    }

    // El mensaje queda fijo en el espacio frente al usuario; si gira mucho la cabeza, lo sigue con suavidad
    void ColocarMensaje(Transform ojos, bool deGolpe)
    {
        Vector3 frente = Vector3.ProjectOnPlane(ojos.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.001f) frente = Vector3.ProjectOnPlane(ojos.up, Vector3.up);
        frente.Normalize();

        Vector3 actual = Vector3.ProjectOnPlane(texto.transform.position - ojos.position, Vector3.up).normalized;
        Vector3 direccion = deGolpe || Vector3.Angle(actual, frente) > 30f
            ? Vector3.Slerp(deGolpe ? frente : actual, frente, deGolpe ? 1f : 2f * Time.unscaledDeltaTime)
            : actual;

        Vector3 posicion = ojos.position + direccion * distanciaMensaje;
        texto.transform.SetPositionAndRotation(posicion, Quaternion.LookRotation(direccion));
    }

    IEnumerator FundirMensaje(float desde, float hasta, Transform ojos)
    {
        for (float t = 0f; t < fundidoMensaje; t += Time.unscaledDeltaTime)
        {
            texto.alpha = Mathf.Lerp(desde, hasta, t / fundidoMensaje);
            ColocarMensaje(ojos, false);
            yield return null;
        }
        texto.alpha = hasta;
    }
}
