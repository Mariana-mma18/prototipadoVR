using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Parte visual de la indicación de la olla con las imágenes de diseño (VENTANAS EMERGENTES):
// "Pon en la olla…" mientras se echan ingredientes, "¡Están todos! Agarra el cucharón…" al mezclar y
// "El inchicapi está listo…" al final. Va dentro del objeto Indicacion, así que OllaSopa sigue
// decidiendo cuándo se ve y hacia dónde mira. Solo lee la etapa; no cambia nada de la olla.
// El texto de OllaSopa queda solo para lo que la imagen no dice: el contador de ingredientes
// ("2 de 4") y el porcentaje de mezcla, escrito sobre la parte baja del cartel.
public class IndicacionOllaDiseno : MonoBehaviour
{
    public OllaSopa olla;
    public Image imagen;

    public Sprite echarIngredientes;
    public Sprite mezclar;
    public Sprite sopaLista;

    void LateUpdate()
    {
        if (!olla || !imagen) return;
        TMP_Text texto = olla.indicacion;

        Sprite sprite;
        bool mostrarTexto;
        switch (olla.EtapaActual)
        {
            case OllaSopa.Etapa.EcharIngredientes:
                sprite = echarIngredientes;
                mostrarTexto = olla.lista && olla.lista.EnOlla > 0;
                break;
            case OllaSopa.Etapa.Mezclar:
                sprite = mezclar;
                mostrarTexto = olla.ProgresoMezcla > 0f;
                break;
            default:
                sprite = sopaLista;
                mostrarTexto = false;
                break;
        }

        if (imagen.sprite != sprite) imagen.sprite = sprite;
        if (texto && texto.enabled != mostrarTexto) texto.enabled = mostrarTexto;
    }
}
