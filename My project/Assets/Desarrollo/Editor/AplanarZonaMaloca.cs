using UnityEditor;
using UnityEngine;

// Aplana el terreno frente al muelle 4 (llegada a la Maloca), sin dejarlo artificialmente plano.
// El terreno (Assets/New Terrain 11.asset) es de diseño. Si un merge de main choca en ese archivo:
//   1. quedarse con la versión de diseño (git checkout --theirs "My project/Assets/New Terrain 11.asset"),
//   2. abrir Unity y ejecutar Desarrollo > Terreno > Aplanar zona de la Maloca,
//   3. (opcional) Desarrollo > Terreno > Guardar copia en Desarrollo.
// Ejecutarlo una sola vez sobre el terreno de diseño; si se ejecuta dos veces, aplana un poco más.
public static class AplanarZonaMaloca
{
    const string RutaTerreno = "Assets/New Terrain 11.asset";
    const string RutaCopia = "Assets/Desarrollo/Terreno/New Terrain 11 (aplanado).asset";

    // Punto donde se baja el jugador en el muelle 4 ("Bajada Muelle 4") y hacia dónde mira (grados).
    static readonly Vector2 Bajada = new Vector2(465.67f, 399.10f);
    const float RumboBajada = 309.6f;

    // Centro de la zona, medido desde la bajada: metros hacia adelante y hacia la derecha.
    const float CentroAdelante = 18f;
    const float CentroDerecha = 6f;

    // Dentro de RadioPleno se aplana con toda la fuerza; hasta RadioBorde se mezcla suave con el terreno original.
    const float RadioPleno = 14f;
    const float RadioBorde = 28f;

    // Altura a la que tiende la zona, en metros (la orilla del muelle está entre 12,5 y 13).
    const float AlturaObjetivo = 14f;

    // Cuánto relieve original se conserva para que no quede plano: en lo que sobra (colina) y en lo que falta (huecos).
    const float ConservarSobrante = 0.25f;
    const float ConservarFaltante = 0.5f;

    // La orilla no se toca: hasta OrillaSinCambio metros delante de la bajada no cambia nada,
    // y hasta OrillaCompleta se va aplicando de a poco.
    const float OrillaSinCambio = 3f;
    const float OrillaCompleta = 8f;

    [MenuItem("Desarrollo/Terreno/Aplanar zona de la Maloca")]
    static void Aplanar()
    {
        var datos = AssetDatabase.LoadAssetAtPath<TerrainData>(RutaTerreno);
        if (!datos)
        {
            EditorUtility.DisplayDialog("Aplanar zona de la Maloca", "No encuentro " + RutaTerreno, "OK");
            return;
        }

        Aplicar(datos, preguntar: true);
    }

    [MenuItem("Desarrollo/Terreno/Guardar copia en Desarrollo")]
    static void GuardarCopia()
    {
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(RutaCopia) &&
            !EditorUtility.DisplayDialog("Guardar copia", "Ya existe " + RutaCopia + ". ¿Reemplazarla?", "Reemplazar", "Cancelar"))
            return;

        CopiarADesarrollo();
    }

    public static bool CopiarADesarrollo()
    {
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.IsValidFolder("Assets/Desarrollo/Terreno"))
            AssetDatabase.CreateFolder("Assets/Desarrollo", "Terreno");
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(RutaCopia))
            AssetDatabase.DeleteAsset(RutaCopia);
        bool ok = AssetDatabase.CopyAsset(RutaTerreno, RutaCopia);
        Debug.Log(ok ? "Copia del terreno guardada en " + RutaCopia : "No se pudo copiar el terreno a " + RutaCopia);
        return ok;
    }

    public static string Aplicar(TerrainData datos, bool preguntar)
    {
        float rumbo = RumboBajada * Mathf.Deg2Rad;
        var adelante = new Vector2(Mathf.Sin(rumbo), Mathf.Cos(rumbo));
        var derecha = new Vector2(adelante.y, -adelante.x);
        Vector2 centro = Bajada + adelante * CentroAdelante + derecha * CentroDerecha;

        // El terreno de la escena está en el origen; las posiciones del mundo coinciden con las del terreno
        Vector3 tam = datos.size;
        int res = datos.heightmapResolution;
        float paso = tam.x / (res - 1);

        int x0 = Mathf.Clamp(Mathf.FloorToInt((centro.x - RadioBorde) / paso), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((centro.x + RadioBorde) / paso), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((centro.y - RadioBorde) / paso), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((centro.y + RadioBorde) / paso), 0, res - 1);
        int ancho = x1 - x0 + 1, alto = z1 - z0 + 1;

        float[,] alturas = datos.GetHeights(x0, z0, ancho, alto);

        // Si la colina ya está baja, probablemente ya se aplanó
        float maximo = 0f;
        for (int j = 0; j < alto; j++)
            for (int i = 0; i < ancho; i++)
            {
                var p = new Vector2((x0 + i) * paso, (z0 + j) * paso);
                if (Vector2.Distance(p, centro) <= RadioPleno) maximo = Mathf.Max(maximo, alturas[j, i] * tam.y);
            }
        if (preguntar)
        {
            string aviso = maximo < AlturaObjetivo + 1.5f
                ? "Parece que esta zona ya está aplanada (altura máxima " + maximo.ToString("F1") + " m). Si sigues, se aplanará un poco más.\n\n"
                : "";
            if (!EditorUtility.DisplayDialog("Aplanar zona de la Maloca",
                    aviso + "Se modificará " + RutaTerreno + " (terreno de diseño) frente al muelle 4.\nSe puede deshacer con Ctrl+Z.",
                    "Aplanar", "Cancelar"))
                return "cancelado";
        }

        Undo.RegisterCompleteObjectUndo(datos, "Aplanar zona de la Maloca");

        float antesMin = float.MaxValue, antesMax = float.MinValue, despuesMin = float.MaxValue, despuesMax = float.MinValue;
        int cambiados = 0;
        for (int j = 0; j < alto; j++)
            for (int i = 0; i < ancho; i++)
            {
                var p = new Vector2((x0 + i) * paso, (z0 + j) * paso);
                float distancia = Vector2.Distance(p, centro);
                if (distancia >= RadioBorde) continue;

                float frente = Vector2.Dot(p - Bajada, adelante);
                float peso = (1f - Suave(RadioPleno, RadioBorde, distancia)) * Suave(OrillaSinCambio, OrillaCompleta, frente);
                if (peso <= 0f) continue;

                float h = alturas[j, i] * tam.y;
                float desvio = h - AlturaObjetivo;
                float destino = AlturaObjetivo + desvio * (desvio > 0f ? ConservarSobrante : ConservarFaltante);
                float nueva = Mathf.Lerp(h, destino, peso);

                if (distancia <= RadioPleno)
                {
                    antesMin = Mathf.Min(antesMin, h); antesMax = Mathf.Max(antesMax, h);
                    despuesMin = Mathf.Min(despuesMin, nueva); despuesMax = Mathf.Max(despuesMax, nueva);
                }
                alturas[j, i] = nueva / tam.y;
                cambiados++;
            }

        datos.SetHeights(x0, z0, alturas);
        EditorUtility.SetDirty(datos);
        AssetDatabase.SaveAssets();

        string resumen = "Zona de la Maloca aplanada: " + cambiados + " puntos. Zona central antes " + antesMin.ToString("F1") + "–" + antesMax.ToString("F1") +
                         " m, después " + despuesMin.ToString("F1") + "–" + despuesMax.ToString("F1") + " m.";
        Debug.Log(resumen);
        return resumen;
    }

    // 0 antes de desde, 1 después de hasta, curva suave en medio
    static float Suave(float desde, float hasta, float valor)
    {
        float t = Mathf.Clamp01((valor - desde) / (hasta - desde));
        return t * t * (3f - 2f * t);
    }
}
