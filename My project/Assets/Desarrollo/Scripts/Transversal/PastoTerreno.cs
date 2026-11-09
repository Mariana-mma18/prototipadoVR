using UnityEngine;

// Extiende el "Pastito" que diseño pintó en el terreno (solo cerca del muelle 1) a casi todo el mapa.
// No toca el archivo de diseño (New Terrain 11.asset): al iniciar trabaja sobre una COPIA del TerrainData
// y pinta ahí el pasto. Por eso el pasto nuevo solo se ve en Play y en las gafas, no en la vista de escena.
// El TerrainCollider sigue usando el terreno original (las alturas son las mismas).
// Va en el objeto "Terrain" de TERRENO FINAL.
[RequireComponent(typeof(Terrain))]
public class PastoTerreno : MonoBehaviour
{
    [Tooltip("Nombre de la textura del pasto entre los detalles del terreno.")]
    public string texturaPasto = "Pastito";

    [Tooltip("Densidad general del pasto nuevo (0 a 1).")]
    [Range(0f, 1f)] public float densidad = 1f;

    [Tooltip("Por debajo de esta altura no hay pasto (nivel del agua del río + un margen).")]
    public float alturaMinima = 6.4f;

    [Tooltip("Pendiente (grados) desde la que el pasto empieza a ralear, y desde la que ya no hay.")]
    public float pendienteRalea = 28f;
    public float pendienteMaxima = 42f;

    [Tooltip("Tamaño (m) de las manchas: más grande = parches más amplios.")]
    public float tamanoManchas = 45f;

    [Tooltip("Lugares sin pasto: se usa la caja (renderers y colliders) de cada objeto más el margen. " +
             "Chagras (para no tapar ingredientes) y la Maloca (para que no atraviese el piso).")]
    public GameObject[] zonasSinPasto;
    public float margenZonas = 1.5f;

    TerrainData original;

    void Awake()
    {
        Aplicar();
    }

    void OnDestroy()
    {
        Restaurar();
    }

    // Copia el TerrainData y pinta el pasto en la copia. Público para poder probarlo desde el editor.
    public void Aplicar()
    {
        var terreno = GetComponent<Terrain>();
        if (original != null || terreno.terrainData == null) return;

        original = terreno.terrainData;
        int capa = BuscarCapa(original);
        if (capa < 0)
        {
            Debug.LogWarning("PastoTerreno: el terreno no tiene un detalle con la textura \"" + texturaPasto + "\".");
            original = null;
            return;
        }

        var copia = Instantiate(original);
        copia.name = original.name + " (pasto en ejecución)";
        copia.hideFlags = HideFlags.DontSave;
        Pintar(copia, capa, terreno.transform.position);
        terreno.terrainData = copia;
    }

    // Vuelve a dejar el terreno original (lo usa OnDestroy y las pruebas en el editor).
    public void Restaurar()
    {
        if (original == null) return;
        var terreno = GetComponent<Terrain>();
        var copia = terreno.terrainData;
        terreno.terrainData = original;
        original = null;
        if (copia != null && copia != terreno.terrainData)
        {
            if (Application.isPlaying) Destroy(copia); else DestroyImmediate(copia);
        }
    }

    int BuscarCapa(TerrainData datos)
    {
        var protos = datos.detailPrototypes;
        for (int i = 0; i < protos.Length; i++)
            if (protos[i].prototypeTexture != null && protos[i].prototypeTexture.name == texturaPasto) return i;
        return -1;
    }

    void Pintar(TerrainData datos, int capa, Vector3 origen)
    {
        Vector3 tam = datos.size;
        int res = datos.detailResolution;
        int hRes = datos.heightmapResolution;
        float[,] alturas = datos.GetHeights(0, 0, hRes, hRes);

        // Pendiente (en grados) de cada punto del heightmap.
        float paso = tam.x / (hRes - 1);
        var pendiente = new float[hRes, hRes];
        for (int y = 0; y < hRes; y++)
            for (int x = 0; x < hRes; x++)
            {
                int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, hRes - 1);
                int y0 = Mathf.Max(y - 1, 0), y1 = Mathf.Min(y + 1, hRes - 1);
                float dx = (alturas[y, x1] - alturas[y, x0]) * tam.y / ((x1 - x0) * paso);
                float dz = (alturas[y1, x] - alturas[y0, x]) * tam.y / ((y1 - y0) * paso);
                pendiente[y, x] = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
            }

        // Zonas sin pasto (en coordenadas de mundo XZ).
        int nZonas = zonasSinPasto != null ? zonasSinPasto.Length : 0;
        var zonas = new Rect[nZonas];
        for (int i = 0; i < nZonas; i++)
        {
            var g = zonasSinPasto[i];
            if (g == null) { zonas[i] = Rect.zero; continue; }
            var b = new Bounds(g.transform.position, Vector3.zero);
            foreach (var r in g.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            foreach (var c in g.GetComponentsInChildren<Collider>()) b.Encapsulate(c.bounds);
            zonas[i] = Rect.MinMaxRect(b.min.x - margenZonas, b.min.z - margenZonas, b.max.x + margenZonas, b.max.z + margenZonas);
        }

        int[,] mapa = datos.GetDetailLayer(0, 0, res, res, capa);
        float escala = 1f / Mathf.Max(tamanoManchas, 1f);
        for (int z = 0; z < res; z++)
        {
            float wz = origen.z + (z + 0.5f) / res * tam.z;
            float hz = (z + 0.5f) / res * (hRes - 1);
            int hz0 = Mathf.Min((int)hz, hRes - 2); float tz = hz - hz0;
            for (int x = 0; x < res; x++)
            {
                float wx = origen.x + (x + 0.5f) / res * tam.x;
                float hx = (x + 0.5f) / res * (hRes - 1);
                int hx0 = Mathf.Min((int)hx, hRes - 2); float tx = hx - hx0;

                float altura = origen.y + tam.y * Mathf.Lerp(
                    Mathf.Lerp(alturas[hz0, hx0], alturas[hz0, hx0 + 1], tx),
                    Mathf.Lerp(alturas[hz0 + 1, hx0], alturas[hz0 + 1, hx0 + 1], tx), tz);
                if (altura < alturaMinima) continue;

                float inclinacion = Mathf.Lerp(
                    Mathf.Lerp(pendiente[hz0, hx0], pendiente[hz0, hx0 + 1], tx),
                    Mathf.Lerp(pendiente[hz0 + 1, hx0], pendiente[hz0 + 1, hx0 + 1], tx), tz);
                if (inclinacion >= pendienteMaxima) continue;

                bool dentro = false;
                for (int i = 0; i < nZonas; i++)
                    if (zonas[i].width > 0f && zonas[i].Contains(new Vector2(wx, wz))) { dentro = true; break; }
                if (dentro) continue;

                // Manchas grandes + variación fina, para que no quede como alfombra pareja.
                float grande = Mathf.PerlinNoise(wx * escala + 13.1f, wz * escala + 7.7f);
                float fina = Mathf.PerlinNoise(wx * escala * 6f + 101.3f, wz * escala * 6f + 55.9f);
                float cobertura = Mathf.Clamp01((grande * 0.7f + fina * 0.3f - 0.1f) * 2f);
                cobertura *= 1f - Mathf.InverseLerp(pendienteRalea, pendienteMaxima, inclinacion);

                int valor = Mathf.RoundToInt(cobertura * densidad * 255f);
                if (valor > mapa[z, x]) mapa[z, x] = valor; // lo que pintó diseño se respeta
            }
        }
        datos.SetDetailLayer(0, 0, capa, mapa);
    }
}
