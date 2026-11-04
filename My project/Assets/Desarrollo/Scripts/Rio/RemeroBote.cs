using UnityEngine;

// Anima al remero de la canoa solo mientras el bote avanza por algún trayecto.
public class RemeroBote : MonoBehaviour
{
    [Tooltip("Animator del remero")]
    public Animator animador;

    [Tooltip("Trayectos del río; el remero rema mientras alguno esté en marcha")]
    public NavegarBote[] trayectos;

    [Tooltip("Segundos que tarda en empezar a remar o en detenerse")]
    public float suavizado = 0.5f;

    void Start()
    {
        animador.speed = 0f;
    }

    void Update()
    {
        animador.speed = Mathf.MoveTowards(animador.speed, HayTrayectoEnMarcha() ? 1f : 0f, Time.deltaTime / suavizado);

        // El clip del modelo no viene marcado en loop y es de diseño, así que lo reinicio desde aquí
        AnimatorStateInfo estado = animador.GetCurrentAnimatorStateInfo(0);
        if (estado.normalizedTime >= 1f)
            animador.Play(estado.fullPathHash, 0, estado.normalizedTime % 1f);
    }

    bool HayTrayectoEnMarcha()
    {
        foreach (NavegarBote trayecto in trayectos)
            if (trayecto && trayecto.EnMovimiento) return true;
        return false;
    }
}
