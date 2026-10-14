using UnityEngine;
using UnityEngine.Events;

// Lleva el bote por una ruta de puntos hasta el siguiente muelle, arrancando y frenando suave.
public class NavegarBote : MonoBehaviour
{
    [Tooltip("Objeto que se mueve (el bote). El asiento del jugador debe ser hijo de este objeto.")]
    public Transform bote;

    [Tooltip("Objeto cuyos hijos, en orden, son los puntos de la ruta. El último es donde el bote se detiene.")]
    public Transform ruta;

    [Tooltip("Velocidad de crucero en metros por segundo")]
    public float velocidad = 2f;

    [Tooltip("Qué tan rápido arranca y frena, en m/s²")]
    public float aceleracion = 0.4f;

    [Tooltip("Qué tan rápido gira el bote para seguir la ruta, en grados por segundo")]
    public float velocidadGiro = 20f;

    [Tooltip("Segundos de espera antes de zarpar, para que el jugador se ubique")]
    public float esperaAlZarpar = 1.5f;

    [Tooltip("Se llama cuando el bote se detiene en el último punto de la ruta")]
    public UnityEvent alLlegar;

    public bool Navegando { get; private set; }

    const int muestrasPorTramo = 12;

    Vector3[] camino;
    float[] distancias;
    float recorrido;
    float velocidadActual;
    float espera;

    public void Zarpar()
    {
        if (Navegando || ruta == null || ruta.childCount == 0) return;

        ConstruirCamino();
        recorrido = 0f;
        velocidadActual = 0f;
        espera = esperaAlZarpar;
        Navegando = true;
    }

    void Update()
    {
        if (!Navegando) return;

        if (espera > 0f)
        {
            espera -= Time.deltaTime;
            return;
        }

        float total = distancias[distancias.Length - 1];
        float restante = total - recorrido;

        // Acelera hasta la velocidad de crucero y frena a tiempo para quedar justo en el último punto
        float velocidadFreno = Mathf.Sqrt(2f * aceleracion * restante);
        velocidadActual = Mathf.Min(velocidad, velocidadActual + aceleracion * Time.deltaTime, velocidadFreno);
        velocidadActual = Mathf.Max(velocidadActual, 0.05f);
        recorrido = Mathf.Min(recorrido + velocidadActual * Time.deltaTime, total);

        bote.position = PuntoEn(recorrido, out Vector3 direccion);

        direccion.y = 0f;
        if (direccion.sqrMagnitude > 0.0001f)
            bote.rotation = Quaternion.RotateTowards(bote.rotation, Quaternion.LookRotation(direccion), velocidadGiro * Time.deltaTime);

        if (recorrido >= total)
        {
            Navegando = false;
            alLlegar.Invoke();
        }
    }

    // Suaviza la ruta (Catmull-Rom) desde donde está el bote, pasando por cada punto, a la altura actual del bote
    void ConstruirCamino()
    {
        var puntos = new Vector3[ruta.childCount + 1];
        puntos[0] = bote.position;
        for (int i = 0; i < ruta.childCount; i++)
        {
            puntos[i + 1] = ruta.GetChild(i).position;
            puntos[i + 1].y = bote.position.y;
        }

        int tramos = puntos.Length - 1;
        camino = new Vector3[tramos * muestrasPorTramo + 1];
        distancias = new float[camino.Length];

        for (int t = 0; t < tramos; t++)
        {
            Vector3 p0 = puntos[Mathf.Max(t - 1, 0)];
            Vector3 p1 = puntos[t];
            Vector3 p2 = puntos[t + 1];
            Vector3 p3 = puntos[Mathf.Min(t + 2, puntos.Length - 1)];
            for (int m = 0; m < muestrasPorTramo; m++)
                camino[t * muestrasPorTramo + m] = CatmullRom(p0, p1, p2, p3, m / (float)muestrasPorTramo);
        }
        camino[camino.Length - 1] = puntos[puntos.Length - 1];

        for (int i = 1; i < camino.Length; i++)
            distancias[i] = distancias[i - 1] + Vector3.Distance(camino[i - 1], camino[i]);
    }

    Vector3 PuntoEn(float distancia, out Vector3 direccion)
    {
        int i = 1;
        while (i < distancias.Length - 1 && distancias[i] < distancia) i++;

        direccion = camino[i] - camino[i - 1];
        float largo = distancias[i] - distancias[i - 1];
        float t = largo > 0f ? (distancia - distancias[i - 1]) / largo : 1f;
        return Vector3.Lerp(camino[i - 1], camino[i], t);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
    }

    void OnDrawGizmos()
    {
        if (ruta == null || ruta.childCount == 0) return;

        Gizmos.color = Color.cyan;
        Vector3 anterior = bote ? bote.position : ruta.GetChild(0).position;
        foreach (Transform punto in ruta)
        {
            Gizmos.DrawLine(anterior, punto.position);
            Gizmos.DrawSphere(punto.position, 0.4f);
            anterior = punto.position;
        }
    }
}
