using System.Collections.Generic;
using UnityEngine;

// Reproduce un sonido de fondo en bucle sin cortes: cuando el clip va a terminar, arranca otra copia
// y cruza los volúmenes. Sirve para audios que no se diseñaron para repetirse (empiezan o terminan con fundido).
public class AmbienteEnBucle : MonoBehaviour
{
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volumen = 1f;

    [Tooltip("Segundo del clip donde empieza cada repetición. La primera vez arranca desde 'inicioPrimeraVez'.")]
    public float inicioBucle = 0f;

    [Tooltip("Segundo del clip donde se corta para repetir. 0 = hasta el final del clip.")]
    public float finBucle = 0f;

    [Tooltip("Segundo del clip donde arranca la primera vez que suena")]
    public float inicioPrimeraVez = 0f;

    [Tooltip("Segundos que duran mezcladas la copia que termina y la que empieza")]
    public float cruce = 3f;

    [Tooltip("Segundos que tarda en subir el volumen al iniciar la escena")]
    public float fundidoEntrada = 3f;

    [Tooltip("Multiplicador extra del volumen que pueden mover otros scripts (por ejemplo SonidoBote)")]
    [Range(0f, 1f)]
    public float intensidad = 1f;

    static readonly List<AmbienteEnBucle> activos = new List<AmbienteEnBucle>();

    // Volumen al que bajan todos los ambientes mientras habla Yari (lo fija DialogoYari; 1 = sin bajar)
    public static float atenuacionVoz = 1f;
    const float velocidadAtenuacion = 1.5f;   // cuánto cambia por segundo al bajar o subir

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => atenuacionVoz = 1f;

    AudioSource[] fuentes;
    float voz = 1f;
    float[] pesos = new float[2];
    int actual;
    float posicion, posicionInicio;
    float general;         // fundido de entrada/salida de toda la escena
    float objetivoGeneral = 1f;
    float velocidadGeneral;

    void Awake()
    {
        fuentes = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            var f = gameObject.AddComponent<AudioSource>();
            f.clip = clip;
            f.playOnAwake = false;
            f.loop = false;
            f.spatialBlend = 0f;     // 2D: se escucha igual mire donde mire
            f.volume = 0f;
            fuentes[i] = f;
        }
    }

    void OnEnable() => activos.Add(this);
    void OnDisable() => activos.Remove(this);

    void Start()
    {
        if (clip == null) return;
        general = fundidoEntrada > 0f ? 0f : 1f;
        velocidadGeneral = fundidoEntrada > 0f ? 1f / fundidoEntrada : 0f;
        Reproducir(actual, inicioPrimeraVez);
        pesos[actual] = 1f;
    }

    void Update()
    {
        if (clip == null) return;

        general = Mathf.MoveTowards(general, objetivoGeneral, velocidadGeneral * Time.unscaledDeltaTime);
        voz = Mathf.MoveTowards(voz, atenuacionVoz, velocidadAtenuacion * Time.unscaledDeltaTime);

        // Posición en el clip llevada con reloj propio: AudioSource.time es poco fiable con clips en streaming
        posicion += Time.unscaledDeltaTime;
        float fin = FinBucle();
        bool seDetuvo = !fuentes[actual].isPlaying && posicion > posicionInicio + 1f;

        // Cuando falta 'cruce' para el final (o si la fuente se detuvo sola), arranca la siguiente copia
        if (seDetuvo || posicion >= fin - cruce)
        {
            int siguiente = 1 - actual;
            Reproducir(siguiente, inicioBucle);
            actual = siguiente;
        }

        float paso = cruce > 0f ? Time.unscaledDeltaTime / cruce : 1f;
        for (int i = 0; i < 2; i++)
        {
            pesos[i] = Mathf.MoveTowards(pesos[i], i == actual ? 1f : 0f, paso);
            if (i != actual && pesos[i] <= 0f && fuentes[i].isPlaying) fuentes[i].Stop();

            // Fundido de potencia constante para que el cruce no se sienta como un bajón
            float curva = Mathf.Sin(pesos[i] * Mathf.PI * 0.5f);
            fuentes[i].volume = volumen * intensidad * general * voz * curva;
        }
    }

    float FinBucle()
    {
        float largo = clip.length;
        float fin = finBucle > 0f ? Mathf.Min(finBucle, largo) : largo;
        return Mathf.Max(fin, inicioBucle + cruce + 0.5f);
    }

    void Reproducir(int indice, float desde)
    {
        var f = fuentes[indice];
        f.Stop();
        posicion = posicionInicio = Mathf.Clamp(desde, 0f, clip.length - 0.1f);
        f.time = posicion;
        f.Play();
    }

    // Baja el volumen hasta silencio en 'segundos' (para salir de la escena)
    public void Desvanecer(float segundos)
    {
        objetivoGeneral = 0f;
        velocidadGeneral = segundos > 0f ? 1f / segundos : 1000f;
    }

    // Desvanece todos los ambientes que estén sonando
    public static void DesvanecerTodos(float segundos)
    {
        foreach (var a in activos) a.Desvanecer(segundos);
    }
}
