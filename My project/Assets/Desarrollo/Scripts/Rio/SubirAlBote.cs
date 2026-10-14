using UnityEngine;
using UnityEngine.Events;

// Sube al jugador al bote al presionar el poke del muelle.
// Apaga la locomoción y deja al jugador fijo en el asiento; solo puede mirar alrededor con la cabeza.
public class SubirAlBote : MonoBehaviour
{
    [Tooltip("Raíz del jugador ([BuildingBlock] Camera Rig)")]
    public OVRCameraRig camaraRig;

    [Tooltip("Punto del bote donde queda el jugador. Su eje Z azul marca hacia dónde mira el bote.")]
    public Transform asiento;

    [Tooltip("Objetos de locomoción que se apagan al subir (Locomotor y grupos de teleport/desplazamiento de manos y controles)")]
    public GameObject[] locomocion;

    [Tooltip("Se llama cuando el jugador ya quedó sentado en el bote (por ejemplo, para zarpar)")]
    public UnityEvent alSubir;

    public bool EnBote { get; private set; }

    float giroRelativo;
    Vector3 desfaseCabeza;

    // Cápsula del locomotor de Meta; hay que moverla junto con el jugador al bajarse
    Oculus.Interaction.Locomotion.CharacterController personaje;
    float alturaPersonaje;

    [ContextMenu("Probar: subir al bote")]
    public void Subir()
    {
        // El poke y el botón del canvas pueden llamar los dos a la vez
        if (EnBote) return;
        EnBote = true;

        Transform rig = camaraRig.transform;
        Transform ojos = camaraRig.centerEyeAnchor;

        personaje = camaraRig.GetComponentInChildren<Oculus.Interaction.Locomotion.CharacterController>(true);
        if (personaje) alturaPersonaje = personaje.Pose.position.y - rig.position.y;

        foreach (var obj in locomocion)
            if (obj) obj.SetActive(false);

        // Giro el rig para que el jugador quede mirando hacia la proa
        rig.Rotate(0f, Mathf.DeltaAngle(ojos.eulerAngles.y, asiento.eulerAngles.y), 0f, Space.World);
        giroRelativo = Mathf.DeltaAngle(asiento.eulerAngles.y, rig.eulerAngles.y);

        // Guardo dónde está la cabeza respecto al rig para centrarla sobre el asiento
        desfaseCabeza = Quaternion.Inverse(rig.rotation) * Vector3.ProjectOnPlane(ojos.position - rig.position, Vector3.up);

        SeguirAsiento();
        alSubir.Invoke();
    }

    // Deja al jugador parado en el destino, mirando hacia su eje Z, y le devuelve la locomoción
    public void Bajar(Transform destino)
    {
        if (!EnBote) return;
        EnBote = false;

        Transform rig = camaraRig.transform;
        Transform ojos = camaraRig.centerEyeAnchor;

        rig.Rotate(0f, Mathf.DeltaAngle(ojos.eulerAngles.y, destino.eulerAngles.y), 0f, Space.World);
        rig.position = destino.position - Vector3.ProjectOnPlane(ojos.position - rig.position, Vector3.up);

        // Llevo la cápsula bajo la cabeza antes de encender el locomotor; si no, arrastra al jugador al muelle anterior
        if (personaje)
            personaje.SetPosition(new Vector3(destino.position.x, destino.position.y + alturaPersonaje, destino.position.z));

        foreach (var obj in locomocion)
            if (obj) obj.SetActive(true);
    }

    void LateUpdate()
    {
        // Sigo al asiento cada frame para que el jugador viaje con el bote cuando navegue
        if (EnBote) SeguirAsiento();
    }

    void SeguirAsiento()
    {
        Transform rig = camaraRig.transform;
        rig.rotation = Quaternion.Euler(0f, asiento.eulerAngles.y + giroRelativo, 0f);
        rig.position = asiento.position - rig.rotation * desfaseCabeza;
    }
}
