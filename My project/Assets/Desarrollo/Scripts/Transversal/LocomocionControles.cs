using Oculus.Interaction.Locomotion;
using UnityEngine;

// Cómo se mueve el jugador cuando usa controles. Las manos no cambian.
// Va en el Camera Rig de cada escena. Al iniciar fija los ajustes de locomoción del rig de Meta
// (Locomotor/LocomotionSettings: Moving y Turning), así que el cambio se hace solo aquí.
// - Teleport: en cualquiera de los dos controles, stick hacia adelante muestra el arco y al soltarlo se
//   teletransporta; stick a los lados gira; stick hacia atrás da un paso atrás. Usa los mismos destinos que
//   las manos (NavMesh y hotspots).
// - Joystick: el stick izquierdo camina (con viñeta para marear menos) y el derecho gira. Ojo: así el jugador
//   puede caminar fuera de los muelles y meterse al río; el teleport solo deja ir a donde hay NavMesh.
// Saltar y agacharse con los botones quedan apagados.
// Al subir al bote, SubirAlBote apaga la locomoción de controles igual que la de manos.
// El modo mixto (un control y una mano a la vez) se activa en el OVRManager del rig: funciona en Quest 3, 3S y Pro;
// en Quest 2 no está disponible y queda el cambio normal entre manos y controles.
[DefaultExecutionOrder(-100)]
public class LocomocionControles : MonoBehaviour
{
    public enum Modo { Teleport, Joystick }
    public enum Giro { Escalonado, Suave }

    [Tooltip("Teleport (igual que con las manos) o Joystick (caminar con el stick izquierdo)")]
    public Modo modo = Modo.Teleport;

    [Tooltip("Escalonado marea menos que el giro suave")]
    public Giro giro = Giro.Escalonado;

    [Tooltip("Grados de cada giro escalonado")]
    public float gradosGiro = 45f;

    void Awake()
    {
        Aplicar();
    }

    [ContextMenu("Aplicar ahora")]
    public void Aplicar()
    {
        MovingSetting movimiento = GetComponentInChildren<MovingSetting>(true);
        if (movimiento)
            movimiento.ControllerMovement.Value = modo == Modo.Teleport
                ? MovingSetting.MovementStyle.Teleport
                : MovingSetting.MovementStyle.Slide;
        else
            Debug.LogWarning($"[LocomocionControles] No encontré el ajuste Moving de la locomoción en {name}");

        TurningSetting giros = GetComponentInChildren<TurningSetting>(true);
        if (giros)
        {
            giros.ControllerTurn.Value = giro == Giro.Escalonado ? TurningSetting.RotationStyle.Snap : TurningSetting.RotationStyle.Smooth;
            giros.RotationSnapAngle.Value = gradosGiro;
        }

        // Saltar y agacharse no tienen sentido en el recorrido
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if ((t.name == "Jump" || t.name == "Crouch") && t.parent && t.parent.name == "ControllerLocomotionSlideActions")
                t.gameObject.SetActive(false);
    }
}
