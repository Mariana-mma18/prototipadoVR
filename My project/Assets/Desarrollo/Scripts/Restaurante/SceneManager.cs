using System.Collections;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    [Tooltip("Segundos que tarda en apagarse el sonido de fondo antes de cambiar de escena (0 = cambio inmediato)")]
    public float desvanecerSonido = 1f;

    bool cargando;

    public void CargarEscena(int indice)
    {
        if (cargando) return;
        cargando = true;
        StartCoroutine(Cargar(indice));
    }

    IEnumerator Cargar(int indice)
    {
        if (desvanecerSonido > 0f)
        {
            AmbienteEnBucle.DesvanecerTodos(desvanecerSonido);
            yield return new WaitForSecondsRealtime(desvanecerSonido);
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(indice);
    }
}
