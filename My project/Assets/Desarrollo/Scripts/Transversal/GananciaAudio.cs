using UnityEngine;

// Sube el volumen de la AudioSource de este objeto por encima del máximo de Unity (volumen 1).
// Debe ir DESPUÉS de la AudioSource en el inspector: Unity aplica los filtros en ese orden.
// Tiene un límite suave: si la señal se acerca al máximo la redondea en vez de cortarla, así no suena rota.
public class GananciaAudio : MonoBehaviour
{
    [Tooltip("Multiplicador del volumen (2 = el doble, unos +6 dB)")]
    [Range(1f, 4f)]
    public float ganancia = 1f;

    const float umbral = 0.8f;   // a partir de aquí empieza a redondear los picos

    // Pico más alto que salió (0 a 1), para revisar si se está limitando mucho
    public float PicoMaximo { get; private set; }
    // Cuántas muestras pasaron por el límite suave
    public int MuestrasLimitadas { get; private set; }

    public void ReiniciarMedicion()
    {
        PicoMaximo = 0f;
        MuestrasLimitadas = 0;
    }

    void OnAudioFilterRead(float[] datos, int canales)
    {
        float g = ganancia;
        if (g <= 1f) return;

        float pico = PicoMaximo;
        int limitadas = 0;
        for (int i = 0; i < datos.Length; i++)
        {
            float x = datos[i] * g;
            float a = Mathf.Abs(x);
            if (a > umbral)
            {
                limitadas++;
                float resto = 1f - umbral;
                a = umbral + resto * Tanh((a - umbral) / resto);
                x = Mathf.Sign(x) * a;
            }
            if (a > pico) pico = a;
            datos[i] = x;
        }
        PicoMaximo = pico;
        MuestrasLimitadas += limitadas;
    }

    static float Tanh(float x)
    {
        float e = Mathf.Exp(2f * x);
        return (e - 1f) / (e + 1f);
    }
}
