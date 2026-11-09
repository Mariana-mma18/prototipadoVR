using UnityEngine;
using UnityEngine.Events;

// Reproduce una línea de voz de Yari, solo el tramo del clip donde está la voz.
// Los audios de diseño traen la voz con relleno y ambiente antes y después; 'inicio' y 'fin' recortan ese tramo.
// Mientras habla, baja los ambientes (AmbienteEnBucle) para que la voz se entienda.
public class DialogoYari : MonoBehaviour
{
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volumen = 1f;

    [Tooltip("Sube la voz por encima del máximo de Unity (2 = el doble, unos +6 dB). Tiene límite suave para que no distorsione.")]
    [Range(1f, 4f)]
    public float ganancia = 2f;

    [Tooltip("Segundo del clip donde empieza la voz")]
    public float inicio = 0f;

    [Tooltip("Segundo del clip donde termina la voz. 0 = hasta el final del clip.")]
    public float fin = 0f;

    [Tooltip("Segundos de fundido al empezar y al terminar el tramo")]
    public float fundido = 0.5f;

    [Tooltip("Si está activo, solo suena la primera vez que se pide (por ejemplo, la primera vez que sube al bote)")]
    public bool soloUnaVez = true;

    [Tooltip("Volumen al que bajan los sonidos de fondo mientras habla (1 = no bajan)")]
    [Range(0f, 1f)]
    public float bajarAmbienteA = 0.5f;

    [Tooltip("Se llama cuando termina de hablar")]
    public UnityEvent alTerminar;

    public bool Hablando { get; private set; }

    AudioSource fuente;
    GananciaAudio amplificador;
    bool yaSono;
    float posicion;     // segundo del clip, con reloj propio (el clip va en streaming)

    void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.playOnAwake = false;
        fuente.loop = false;
        fuente.spatialBlend = 0f;   // 2D: la voz del guía se escucha igual mire donde mire
        fuente.volume = 0f;

        // Se añade después de la AudioSource para que el filtro actúe sobre su sonido
        amplificador = gameObject.AddComponent<GananciaAudio>();
        amplificador.ganancia = ganancia;
    }

    [ContextMenu("Probar: reproducir")]
    public void Reproducir()
    {
        if (clip == null || Hablando || (soloUnaVez && yaSono)) return;
        yaSono = true;
        Hablando = true;

        posicion = Mathf.Clamp(inicio, 0f, clip.length - 0.1f);
        fuente.Stop();
        fuente.time = posicion;
        fuente.volume = 0f;
        fuente.Play();
        AmbienteEnBucle.atenuacionVoz = bajarAmbienteA;
    }

    public void Detener()
    {
        if (!Hablando) return;
        Hablando = false;
        fuente.Stop();
        fuente.volume = 0f;
        AmbienteEnBucle.atenuacionVoz = 1f;
        alTerminar.Invoke();
    }

    void Update()
    {
        if (!Hablando) return;

        amplificador.ganancia = ganancia;   // permite ajustarla en el inspector durante Play
        posicion += Time.unscaledDeltaTime;
        float final = fin > 0f ? Mathf.Min(fin, clip.length) : clip.length;

        if (posicion >= final || (!fuente.isPlaying && posicion > inicio + 1f))
        {
            Detener();
            return;
        }

        // Fundido al entrar y al salir del tramo para que no se corte en seco
        float entrada = fundido > 0f ? Mathf.Clamp01((posicion - inicio) / fundido) : 1f;
        float salida = fundido > 0f ? Mathf.Clamp01((final - posicion) / fundido) : 1f;
        fuente.volume = volumen * Mathf.Min(entrada, salida);
    }

    // Si el objeto se apaga o se cambia de escena a mitad de la frase, devuelvo el volumen de los ambientes
    void OnDisable()
    {
        if (!Hablando) return;
        Hablando = false;
        if (fuente) fuente.Stop();
        AmbienteEnBucle.atenuacionVoz = 1f;
    }
}
