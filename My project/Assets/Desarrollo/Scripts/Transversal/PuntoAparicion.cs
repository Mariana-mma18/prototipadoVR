using System.Collections;
using UnityEngine;

// Punto de aparición del jugador (uno por escena).
// En VR la cabeza aparece desplazada según dónde esté parado el jugador en su espacio de juego (guardian).
// Al cargar la escena, en cuanto el visor tiene tracking, este script mueve y gira el Camera Rig para que la
// CABEZA quede justo en este punto y mirando hacia su eje Z azul. Si el jugador recentra la vista
// (botón Meta mantenido), lo deja donde estaba en vez de moverlo.
public class PuntoAparicion : MonoBehaviour
{
    [Tooltip("Vacío = se busca solo")]
    public OVRCameraRig camaraRig;

    [Tooltip("Segundos máximos esperando el tracking del visor; después ubica al jugador igual")]
    public float esperaMaxima = 3f;

    [Tooltip("Al recentrar la vista, mantiene al jugador en el lugar y dirección donde estaba")]
    public bool mantenerAlRecentrar = true;

    public bool Ubicado { get; private set; }

    Vector3 ultimaCabeza;
    float ultimoGiro;
    bool recentrado;

    IEnumerator Start()
    {
        if (!camaraRig) camaraRig = FindFirstObjectByType<OVRCameraRig>();
        if (!camaraRig) yield break;

        // Espera a que el visor reporte la posición de la cabeza (sin tracking queda en 0,0,0)
        float limite = Time.realtimeSinceStartup + esperaMaxima;
        while (Time.realtimeSinceStartup < limite && camaraRig.centerEyeAnchor.localPosition.sqrMagnitude < 0.0001f)
            yield return null;
        yield return null;

        Ubicar(transform.position, transform.eulerAngles.y);
        Ubicado = true;

        if (mantenerAlRecentrar && OVRManager.display != null)
            OVRManager.display.RecenteredPose += AlRecentrar;
    }

    void OnDestroy()
    {
        if (OVRManager.display != null) OVRManager.display.RecenteredPose -= AlRecentrar;
    }

    void LateUpdate()
    {
        if (!Ubicado) return;

        if (recentrado)
        {
            // El sistema movió el origen del tracking: devuelvo la cabeza a donde estaba
            recentrado = false;
            Ubicar(new Vector3(ultimaCabeza.x, camaraRig.transform.position.y, ultimaCabeza.z), ultimoGiro);
        }

        Transform ojos = camaraRig.centerEyeAnchor;
        ultimaCabeza = ojos.position;
        ultimoGiro = ojos.eulerAngles.y;
    }

    void AlRecentrar() => recentrado = true;

    // Deja la cabeza sobre 'punto' (el rig a la altura del punto) mirando hacia 'giro'
    void Ubicar(Vector3 punto, float giro)
    {
        Transform rig = camaraRig.transform;
        Transform ojos = camaraRig.centerEyeAnchor;

        var personaje = camaraRig.GetComponentInChildren<Oculus.Interaction.Locomotion.CharacterController>(true);
        float alturaPersonaje = personaje ? personaje.Pose.position.y - rig.position.y : 0f;

        rig.Rotate(0f, Mathf.DeltaAngle(ojos.eulerAngles.y, giro), 0f, Space.World);
        Vector3 cabeza = Vector3.ProjectOnPlane(ojos.position - rig.position, Vector3.up);
        rig.position = new Vector3(punto.x, punto.y, punto.z) - cabeza;

        // La cápsula del locomotor de Meta va bajo la cabeza; si no, arrastra al jugador de vuelta
        if (personaje)
            personaje.SetPosition(new Vector3(punto.x, punto.y + alturaPersonaje, punto.z));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.75f, 1f);
        Vector3 p = transform.position;
        const int lados = 24;
        for (int i = 0; i < lados; i++)
        {
            float a = i * Mathf.PI * 2f / lados, b = (i + 1) * Mathf.PI * 2f / lados;
            Gizmos.DrawLine(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.35f, p + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * 0.35f);
        }
        Vector3 frente = transform.forward * 0.8f;
        Gizmos.DrawLine(p, p + frente);
        Gizmos.DrawLine(p + frente, p + frente * 0.75f + transform.right * 0.15f);
        Gizmos.DrawLine(p + frente, p + frente * 0.75f - transform.right * 0.15f);
        Gizmos.DrawLine(p, p + Vector3.up * 1.6f);
    }
}
