using UnityEngine;

// Sube el sonido de remos y agua cuando el bote avanza y lo baja cuando se detiene.
// Va junto a un AmbienteEnBucle con el clip de la canoa y le mueve la 'intensidad' según la velocidad del bote.
[RequireComponent(typeof(AmbienteEnBucle))]
public class SonidoBote : MonoBehaviour
{
    [Tooltip("El objeto que se mueve (el mismo 'bote' de NavegarBote)")]
    public Transform bote;

    [Tooltip("Velocidad (m/s) a la que el sonido llega a su volumen completo")]
    public float velocidadVolumenCompleto = 1.5f;

    [Tooltip("Segundos que tarda el sonido en subir o bajar")]
    public float suavizado = 1.5f;

    AmbienteEnBucle ambiente;
    Vector3 posicionAnterior;

    void Awake()
    {
        ambiente = GetComponent<AmbienteEnBucle>();
        ambiente.intensidad = 0f;
    }

    void Start()
    {
        if (bote) posicionAnterior = bote.position;
    }

    void Update()
    {
        if (bote == null || Time.deltaTime <= 0f) return;

        float velocidad = (bote.position - posicionAnterior).magnitude / Time.deltaTime;
        posicionAnterior = bote.position;

        float objetivo = Mathf.Clamp01(velocidad / velocidadVolumenCompleto);
        ambiente.intensidad = Mathf.MoveTowards(ambiente.intensidad, objetivo, Time.deltaTime / Mathf.Max(0.01f, suavizado));
    }
}
