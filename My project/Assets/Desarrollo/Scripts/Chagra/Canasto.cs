using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

// Canasto donde se guardan los ingredientes recogidos hasta cocinar en la maloka.
// El agarre y los encajes son building blocks del ISDK: el jugador lo agarra con la mano y lo
// suelta junto a su cuerpo (anclajes de AnclajesCuerpo) para llevarlo mientras camina, y los
// ingredientes se encajan en la zona de adentro y viajan con él.
// Este script solo coordina lo que el ISDK no hace solo:
// - mientras el canasto está en la mano o en el cuerpo, sus colliders son trigger, para no frenar al jugador al caminar;
// - los ingredientes no chocan con el canasto, para poder quedar adentro;
// - al subir al bote, si el jugador no lo lleva en la mano, se lo pone a los pies para que viaje con él;
// - el canasto y los ingredientes encajados quedan alineados en el mismo frame aunque el jugador vaya en el bote.
// Va después de AnclajesCuerpo, que mueve los anclajes en LateUpdate.
[DefaultExecutionOrder(110)]
public class Canasto : MonoBehaviour
{
    [Tooltip("Grabbable del canasto (building block de agarre)")]
    public Grabbable agarrable;

    [Tooltip("Snap Interactor del canasto; lo encaja en los anclajes del cuerpo")]
    public SnapInteractor encaje;

    [Tooltip("Anclaje donde se pone el canasto al subir al bote si el jugador no lo tiene en la mano")]
    public SnapInteractable anclajeViaje;

    [Tooltip("Snap Interactable de adentro del canasto donde se encajan los ingredientes")]
    public SnapInteractable zonaIngredientes;

    [Tooltip("Segundos que tarda un encaje en llegar a su lugar; después de eso se alinea cada frame")]
    public float tiempoEncaje = 0.5f;

    [Tooltip("Indicadores de los anclajes del cuerpo; se muestran mientras el jugador tiene el canasto en la mano")]
    public GameObject[] indicadores;

    [Tooltip("Colliders sólidos del canasto (cuerpo y asa)")]
    public Collider[] colliders;

    [Tooltip("Raíces de los ingredientes que se pueden guardar en el canasto")]
    public Transform[] ingredientes;

    // Ya lo agarró el jugador al menos una vez; antes de eso no lo sigue en el bote
    public bool Recogido { get; private set; }

    public bool EnMano => manos.Count > 0;

    readonly HashSet<int> manos = new HashSet<int>();
    readonly Dictionary<SnapInteractor, float> encajadosDesde = new Dictionary<SnapInteractor, float>();
    SnapInteractor[] encajesIngredientes;
    bool comoTrigger;
    bool forzandoEncaje;

    void Start()
    {
        var encajes = new List<SnapInteractor>();
        foreach (Transform ingrediente in ingredientes)
        {
            if (!ingrediente) continue;
            foreach (Collider otro in ingrediente.GetComponentsInChildren<Collider>(true))
                foreach (Collider propio in colliders)
                    Physics.IgnoreCollision(propio, otro);

            SnapInteractor encajeIngrediente = ingrediente.GetComponentInChildren<SnapInteractor>(true);
            if (encajeIngrediente) encajes.Add(encajeIngrediente);
        }
        encajesIngredientes = encajes.ToArray();

        MostrarIndicadores(false);
    }

    void OnEnable()
    {
        agarrable.WhenPointerEventRaised += AlEventoDeAgarre;
    }

    void OnDisable()
    {
        agarrable.WhenPointerEventRaised -= AlEventoDeAgarre;
    }

    void AlEventoDeAgarre(PointerEvent evento)
    {
        // Los eventos del propio Snap Interactor no son de una mano
        if (evento.Identifier == encaje.Identifier) return;

        switch (evento.Type)
        {
            case PointerEventType.Select:
                manos.Add(evento.Identifier);
                Recogido = true;
                MostrarIndicadores(true);
                break;
            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                if (manos.Remove(evento.Identifier) && manos.Count == 0) MostrarIndicadores(false);
                break;
        }
    }

    void Update()
    {
        bool sostenido = agarrable.SelectingPointsCount > 0;
        if (sostenido != comoTrigger)
        {
            comoTrigger = sostenido;
            foreach (Collider propio in colliders)
                if (propio) propio.isTrigger = sostenido;
        }

        if (forzandoEncaje && encaje.HasSelectedInteractable) forzandoEncaje = false;
    }

    // El ISDK mueve lo encajado en Update, pero el rig (bote) y los anclajes se mueven en LateUpdate,
    // así que al navegar el canasto quedaría un frame atrás del jugador y los ingredientes otro más.
    // Cuando un encaje ya llegó a su lugar, lo alineo aquí en el mismo frame.
    void LateUpdate()
    {
        if (!EnMano && Llego(encaje) && encaje.SelectedInteractable.PoseForInteractor(encaje, out Pose lugarCanasto))
            Colocar(agarrable.transform, encaje.transform, lugarCanasto);

        foreach (SnapInteractor encajeIngrediente in encajesIngredientes)
        {
            if (!Llego(encajeIngrediente) || encajeIngrediente.SelectedInteractable != zonaIngredientes) continue;
            if (zonaIngredientes.PoseForInteractor(encajeIngrediente, out Pose ranura))
                Colocar(encajeIngrediente.Rigidbody.transform, encajeIngrediente.transform, ranura);
        }
    }

    bool Llego(SnapInteractor interactor)
    {
        if (!interactor.HasSelectedInteractable)
        {
            encajadosDesde.Remove(interactor);
            return false;
        }
        if (!encajadosDesde.TryGetValue(interactor, out float desde))
        {
            encajadosDesde[interactor] = Time.time;
            return false;
        }
        return Time.time - desde >= tiempoEncaje;
    }

    // Mueve la raíz de un objeto para que su punto de encaje quede justo en el destino
    static void Colocar(Transform raiz, Transform punto, Pose destino)
    {
        Quaternion giroPunto = Quaternion.Inverse(raiz.rotation) * punto.rotation;
        Vector3 desfasePunto = Quaternion.Inverse(raiz.rotation) * (punto.position - raiz.position);
        Quaternion rotacion = destino.rotation * Quaternion.Inverse(giroPunto);
        raiz.SetPositionAndRotation(destino.position - rotacion * desfasePunto, rotacion);
    }

    // Se llama al subir al bote (evento alSubir de SubirAlBote)
    [ContextMenu("Probar: ir con el jugador")]
    public void IrConJugador()
    {
        if (!Recogido || EnMano || encaje.HasSelectedInteractable || forzandoEncaje) return;

        // Lo pongo ya en el anclaje para que no vuele desde donde quedó
        Transform raiz = agarrable.transform;
        Colocar(raiz, encaje.transform, anclajeViaje.transform.GetPose());

        Rigidbody cuerpo = encaje.Rigidbody;
        cuerpo.position = raiz.position;
        cuerpo.rotation = raiz.rotation;
        if (!cuerpo.isKinematic)
        {
            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
        }

        // El ISDK limpia estas órdenes solo cuando el encaje se selecciona
        forzandoEncaje = true;
        encaje.SetComputeCandidateOverride(() => anclajeViaje);
        encaje.SetComputeShouldSelectOverride(() => true);
    }

    void MostrarIndicadores(bool mostrar)
    {
        foreach (GameObject indicador in indicadores)
            if (indicador) indicador.SetActive(mostrar);
    }
}
