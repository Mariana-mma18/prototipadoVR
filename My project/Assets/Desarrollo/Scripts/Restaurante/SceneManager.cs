using UnityEngine;

public class SceneManager : MonoBehaviour
{
    public void CargarEscena(int indice)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(indice);
    }
}
