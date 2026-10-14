using UnityEngine;

// Poke para bajarse del bote. Aparece frente al jugador cuando el bote llega al muelle
// y al presionarlo lo deja parado en el muelle con la locomoción encendida.
public class BajarDelBote : MonoBehaviour
{
    [Tooltip("Script del poke para subir; guarda el estado del jugador en el bote")]
    public SubirAlBote jugador;

    [Tooltip("Punto del muelle donde queda el jugador al bajarse. Su eje Z azul marca hacia dónde mira.")]
    public Transform destino;

    [Tooltip("Botón físico del poke (ButtonVisual); se usa para ubicarlo al alcance de la mano")]
    public Transform boton;

    [Tooltip("Distancia del botón frente a los ojos del jugador, en metros")]
    public float distancia = 0.45f;

    [Tooltip("Cuánto más abajo de los ojos aparece el botón, en metros")]
    public float bajoLosOjos = 0.35f;

    bool ocultar;

    [ContextMenu("Probar: mostrar botón")]
    public void Mostrar()
    {
        Transform ojos = jugador.camaraRig.centerEyeAnchor;
        Vector3 frente = Vector3.ProjectOnPlane(ojos.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.0001f) frente = jugador.camaraRig.transform.forward;
        frente.Normalize();

        // El canvas se lee mirando en la misma dirección que su eje Z
        transform.rotation = Quaternion.LookRotation(frente);
        Vector3 objetivo = ojos.position + frente * distancia + Vector3.down * bajoLosOjos;
        transform.position += objetivo - boton.position;

        gameObject.SetActive(true);
    }

    [ContextMenu("Probar: bajar del bote")]
    public void Bajar()
    {
        // El poke y el botón del canvas pueden llamar los dos a la vez
        if (!jugador.EnBote) return;

        jugador.Bajar(destino);
        ocultar = true;
    }

    void LateUpdate()
    {
        // Lo oculto después de que el poke termine de procesar la pulsación
        if (!ocultar) return;
        ocultar = false;
        gameObject.SetActive(false);
    }
}
