using System.Collections.Generic;
using UnityEngine;

// Parte visual de la lista de ingredientes con la imagen de diseño (VENTANAS EMERGENTES/INGREDIENTES).
// La imagen ya trae los nombres; aquí solo se prende el chulo verde de cada ingrediente cuando está
// en la canasta o ya se echó en la olla. Lee el estado de ListaIngredientes, no lo cambia.
// Los chulos son objetos normales dentro del panel: se pueden mover a mano en la escena.
public class ListaIngredientesDiseno : MonoBehaviour
{
    [System.Serializable]
    public class Marca
    {
        [Tooltip("Igual al nombre del ingrediente en ListaIngredientes")]
        public string ingrediente;
        public GameObject chulo;
    }

    public ListaIngredientes lista;
    public List<Marca> marcas = new List<Marca>();

    void Reset() => lista = GetComponent<ListaIngredientes>();

    void LateUpdate()
    {
        if (!lista || !lista.Abierto) return;
        foreach (Marca marca in marcas)
        {
            if (!marca.chulo) continue;
            bool marcado = false;
            foreach (ListaIngredientes.Ingrediente ingrediente in lista.ingredientes)
                if (ingrediente.nombre == marca.ingrediente)
                {
                    marcado = ingrediente.enOlla || lista.EnCanasta(ingrediente);
                    break;
                }
            if (marca.chulo.activeSelf != marcado) marca.chulo.SetActive(marcado);
        }
    }
}
