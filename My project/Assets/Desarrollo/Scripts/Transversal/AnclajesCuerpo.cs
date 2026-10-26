using UnityEngine;

// Puntos alrededor del cuerpo del jugador donde se suelta el canasto para llevarlo consigo:
// frente a los pies y a cada lado. Cada punto es un Snap Interactable del ISDK; este script solo
// los mantiene pegados al jugador mientras camina, se teletransporta o viaja en el bote.
// Va después de SubirAlBote, que mueve el rig en LateUpdate.
[DefaultExecutionOrder(100)]
public class AnclajesCuerpo : MonoBehaviour
{
    [Tooltip("Raíz del jugador ([BuildingBlock] Camera Rig)")]
    public OVRCameraRig camaraRig;

    [Tooltip("Anclaje frente a los pies; el canasto queda apoyado en el suelo")]
    public Transform pies;

    [Tooltip("Anclaje al lado izquierdo del cuerpo")]
    public Transform ladoIzquierdo;

    [Tooltip("Anclaje al lado derecho del cuerpo")]
    public Transform ladoDerecho;

    [Tooltip("Distancia del canasto frente a los pies, en metros")]
    public float distanciaPies = 0.4f;

    [Tooltip("Separación de cada lado desde el centro del cuerpo, en metros")]
    public float separacionLados = 0.32f;

    [Tooltip("Altura de la base del canasto en los lados, como fracción de la altura de los ojos (0.28 ≈ canasto colgando de la mano)")]
    public float alturaLados = 0.28f;

    [Tooltip("Altura mínima de la base del canasto en los lados, en metros (en el editor sin gafas la cabeza queda en el suelo)")]
    public float alturaLadosMinima = 0.4f;

    [Tooltip("Qué tan rápido gira el cuerpo para seguir la mirada; más alto gira más rápido")]
    public float velocidadGiro = 3f;

    [Tooltip("Capas que cuentan como suelo para apoyar el canasto a los pies")]
    public LayerMask suelo = ~0;

    float giro;
    bool iniciado;
    readonly RaycastHit[] golpes = new RaycastHit[16];

    void LateUpdate()
    {
        Transform rig = camaraRig.transform;
        Transform ojos = camaraRig.centerEyeAnchor;

        // El cuerpo sigue la mirada con retraso, para que mirar alrededor no balancee el canasto
        Vector3 frente = Vector3.ProjectOnPlane(ojos.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.0001f) frente = Vector3.ProjectOnPlane(rig.forward, Vector3.up);
        float giroCabeza = Quaternion.LookRotation(frente).eulerAngles.y;
        giro = iniciado ? Mathf.LerpAngle(giro, giroCabeza, 1f - Mathf.Exp(-velocidadGiro * Time.deltaTime)) : giroCabeza;
        iniciado = true;

        // Con el tracking a nivel del suelo, el rig queda en el piso bajo el jugador
        float piso = rig.position.y;
        Quaternion rotacion = Quaternion.Euler(0f, giro, 0f);
        Vector3 centro = new Vector3(ojos.position.x, piso, ojos.position.z);
        transform.SetPositionAndRotation(centro, rotacion);

        float alturaLado = Mathf.Max((ojos.position.y - piso) * alturaLados, alturaLadosMinima);
        ladoIzquierdo.SetPositionAndRotation(centro + rotacion * new Vector3(-separacionLados, alturaLado, 0f), rotacion);
        ladoDerecho.SetPositionAndRotation(centro + rotacion * new Vector3(separacionLados, alturaLado, 0f), rotacion);

        Vector3 frentePies = centro + rotacion * new Vector3(0f, 0f, distanciaPies);
        pies.SetPositionAndRotation(new Vector3(frentePies.x, AlturaSuelo(frentePies, ojos.position.y, piso), frentePies.z), rotacion);
    }

    // Busca el suelo bajo el punto (terreno, muelle o bote). Ignora objetos con Rigidbody,
    // como el canasto o los ingredientes, para que el canasto no se monte sobre lo que lleva,
    // y lo que esté muy por encima del piso del jugador (paredes, ramas, mesas).
    float AlturaSuelo(Vector3 punto, float desde, float piso)
    {
        const float escalonMaximo = 0.5f;
        Vector3 origen = new Vector3(punto.x, Mathf.Max(desde, piso + escalonMaximo), punto.z);
        int total = Physics.RaycastNonAlloc(origen, Vector3.down, golpes, origen.y - piso + 1.5f, suelo, QueryTriggerInteraction.Ignore);

        float mejor = float.NegativeInfinity;
        for (int i = 0; i < total; i++)
        {
            if (golpes[i].collider.attachedRigidbody) continue;
            float altura = golpes[i].point.y;
            if (altura > piso + escalonMaximo) continue;
            if (altura > mejor) mejor = altura;
        }
        return float.IsNegativeInfinity(mejor) ? piso : mejor;
    }
}
