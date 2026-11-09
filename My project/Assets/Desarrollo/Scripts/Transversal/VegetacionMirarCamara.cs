using System.Collections.Generic;
using UnityEngine;

// Hace que la vegetación plana (imágenes de diseño como "arbolito" y "Pastito") gire solo en el eje Y
// para mirar siempre al jugador, igual que el pasto que diseño pinta en el terreno.
// Va en la raíz "VEGETACIÓN RÍO"; gira los hijos directos de cada grupo indicado.
public class VegetacionMirarCamara : MonoBehaviour
{
    [Tooltip("Grupos cuyos hijos directos giran hacia la cámara.")]
    public Transform[] grupos;

    readonly List<Transform> elementos = new List<Transform>();
    Transform camara;

    void Start()
    {
        elementos.Clear();
        foreach (var grupo in grupos)
        {
            if (grupo == null) continue;
            foreach (Transform hijo in grupo) elementos.Add(hijo);
        }
    }

    void LateUpdate()
    {
        if (camara == null)
        {
            var principal = Camera.main;
            if (principal == null) return;
            camara = principal.transform;
        }

        Vector3 ojo = camara.position;
        for (int i = 0; i < elementos.Count; i++)
        {
            Transform t = elementos[i];
            Vector3 dir = t.position - ojo;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(dir);
        }
    }
}
