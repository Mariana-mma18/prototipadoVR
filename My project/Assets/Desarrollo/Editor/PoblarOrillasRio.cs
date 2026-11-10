using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Decora las orillas del río de TERRENO FINAL con assets de diseño:
//   - palmeras: copias de "chambira (1)" que ya está en la escena (FBX _Main/Scenes/Terreno/MODELOS/chambira (1).fbx),
//   - arbolitos y pasto: las imágenes _Main/Scenes/Terreno/Pasto/arbolito.png y Pastito.png en un plano
//     que gira hacia el jugador (VegetacionMirarCamara), como el pasto que diseño pinta en el terreno.
// Todo queda bajo la raíz "VEGETACIÓN RÍO". Volver a ejecutarlo borra esa raíz y la genera igual
// (semilla fija), así que se pueden cambiar los números de abajo y repetir.
// No toca archivos de diseño: los materiales se crean en Assets/Desarrollo/Materiales.
public static class PoblarOrillasRio
{
    const string NombreRaiz = "VEGETACIÓN RÍO";
    const string PalmeraModelo = "chambira (1)";
    const string TexArbolito = "Assets/_Main/Scenes/Terreno/Pasto/arbolito.png";
    const string TexPastito = "Assets/_Main/Scenes/Terreno/Pasto/Pastito.png";
    const string MatArbolito = "Assets/Desarrollo/Materiales/Arbolito (orilla).mat";
    const string MatPastito = "Assets/Desarrollo/Materiales/Pastito (orilla).mat";
    const int Semilla = 2026;

    // Nivel del agua (WaterBlock_50m) y altura a la que empieza la parte alta de la orilla.
    const float NivelAgua = 6f;
    const float AlturaBorde = 9.5f;

    // Cada cuántos metros del río se intenta poner algo y con qué probabilidad (por lado).
    const float Paso = 3f;
    const float ProbPasto = 0.8f;
    const float ProbArbolito = 0.55f;
    const float ProbPalmera = 0.22f;

    // Tamaño de la imagen del arbolito en metros (el árbol ocupa ~88 % de la altura).
    // Salen más medianos que gigantes: cuanto mayor CurvaTamano, más raros los grandes.
    const float TamArbolitoMin = 6f;
    const float TamArbolitoMax = 22f;
    const float CurvaTamano = 1.6f;

    // Bosque tierra adentro: árboles sueltos y en manchas desde la orilla hasta DistanciaBosque del río.
    const float DistanciaBosqueMin = 14f;  // desde el centro del río
    const float DistanciaBosque = 75f;
    const float CeldaBosque = 10f;         // un intento por celda de este tamaño (m)
    const float TamanoManchasBosque = 60f; // manchas de ruido: zonas más tupidas y claros
    const float ProbPalmeraBosque = 0.15f; // parte de los árboles del bosque son palmeras

    // Arbustos provisionales (grupos de Pastito grande y oscuro) hasta que diseño haga un arbusto.
    const string MatArbusto = "Assets/Desarrollo/Materiales/Arbusto provisional (orilla).mat";
    static readonly Color ColorArbusto = new Color(0.5f, 0.66f, 0.48f);
    const float ProbArbusto = 0.5f;
    const float SeparacionArbustos = 5f;
    const float ArbustoAltoMin = 3.5f, ArbustoAltoMax = 5f;   // alto de cada mata (m)
    const float ArbustoAnchoMin = 3f, ArbustoAnchoMax = 5f; // separación entre la primera y la última mata (m)

    // Distancia mínima entre elementos del mismo tipo (y entre árboles de distinto tipo).
    const float SeparacionPalmeras = 9f;
    const float SeparacionArbolitos = 6f;
    const float SeparacionArbolPalmera = 4f;
    const float SeparacionPasto = 1.3f;

    // Centro aproximado del cauce: un poco antes del muelle 1, la ruta del bote y un tramo después del muelle 4.
    static readonly Vector2[] Antes = { new Vector2(404f, 25f), new Vector2(396f, 60f) };
    static readonly Vector2[] Despues = { new Vector2(486f, 406f), new Vector2(508f, 421f), new Vector2(526f, 436f) };

    // Zona aplanada frente a la Maloca (ver AplanarZonaMaloca): se deja despejada.
    static readonly Vector2 CentroMaloca = new Vector2(455.6f, 415.2f);
    const float RadioMaloca = 24f;

    struct Zona { public Vector2 centro; public float radio; }

    [MenuItem("Desarrollo/Escenario/Poblar orillas del río")]
    public static string Poblar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        var terreno = Terrain.activeTerrain;
        var modelo = GameObject.Find(PalmeraModelo);
        if (terreno == null || modelo == null)
            return Error("Abre TERRENO FINAL: falta el Terrain o \"" + PalmeraModelo + "\".");

        var anterior = GameObject.Find(NombreRaiz);
        if (anterior != null) Undo.DestroyObjectImmediate(anterior);

        var matArbolito = CrearMaterial(MatArbolito, TexArbolito);
        var matPastito = CrearMaterial(MatPastito, TexPastito);
        var matArbusto = CrearMaterial(MatArbusto, TexPastito, ColorArbusto);
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        var raiz = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(raiz, "Poblar orillas del río");
        var palmeras = new GameObject("Palmeras").transform; palmeras.SetParent(raiz.transform);
        var arbolitos = new GameObject("Arbolitos").transform; arbolitos.SetParent(raiz.transform);
        var pasto = new GameObject("Pasto").transform; pasto.SetParent(raiz.transform);
        var arbustos = new GameObject("Arbustos (provisional)").transform; arbustos.SetParent(raiz.transform);

        // Que no cuente para el NavMesh del teleport si se vuelve a bakear.
        var mod = raiz.AddComponent<NavMeshModifier>();
        mod.ignoreFromBuild = true;
        var mirar = raiz.AddComponent<VegetacionMirarCamara>();
        mirar.grupos = new[] { arbolitos, pasto, arbustos };

        var zonas = ZonasDespejadas();
        var cauce = Cauce();
        var rnd = new System.Random(Semilla);

        var posPalmeras = new List<Vector3>();
        var posArbolitos = new List<Vector3>();
        var posPasto = new List<Vector3>();
        var posArbustos = new List<Vector3>();
        var rndArbustos = new System.Random(Semilla + 1);
        foreach (var g in escena.GetRootGameObjects())
            if (g.name.StartsWith("chambira")) posPalmeras.Add(g.transform.position);

        // Desfase entre la raíz de la palmera y la base del tronco, por unidad de escala.
        float baseModelo = BaseRelativa(modelo);

        for (int i = 0; i < cauce.Count - 1; i++)
        {
            Vector2 a = cauce[i], b = cauce[i + 1];
            float largo = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / largo;
            for (float s = 0f; s < largo; s += Paso)
            {
                Vector2 c = a + dir * s;
                if (terreno.SampleHeight(V3(c)) > NivelAgua - 1f) continue; // fuera del agua
                foreach (int lado in new[] { 1, -1 })
                {
                    Vector2 normal = new Vector2(dir.y, -dir.x) * lado;
                    if (!BuscarOrilla(terreno, c, normal, out float dAgua, out float dBorde)) continue;

                    // Pasto: desde el agua hasta un poco arriba del borde (dos intentos por paso).
                    for (int intento = 0; intento < 2; intento++)
                    {
                        if (rnd.NextDouble() >= ProbPasto) continue;
                        Vector2 p = c + normal * Rango(rnd, dAgua + 0.3f, dBorde + 3f) + dir * Rango(rnd, -1.5f, 1.5f);
                        if (Libre(p, zonas) && Lejos(p, posPasto, SeparacionPasto) && Pendiente(terreno, p) > 0.55f
                            && terreno.SampleHeight(V3(p)) > NivelAgua + 0.15f)
                        {
                            float tam = Rango(rnd, 1.2f, 2.1f);
                            var pos = Suelo(terreno, p);
                            posPasto.Add(pos);
                            CrearPlano("Pastito", pasto, pos, normal, quad, matPastito, tam * Rango(rnd, 1f, 1.35f), tam, 0.12f);
                        }
                    }

                    // Arbolito: arriba de la orilla.
                    if (rnd.NextDouble() < ProbArbolito)
                    {
                        Vector2 p = c + normal * (dBorde + Rango(rnd, 1.5f, 9f)) + dir * Rango(rnd, -1f, 1f);
                        if (Libre(p, zonas) && Lejos(p, posArbolitos, SeparacionArbolitos) && Lejos(p, posPalmeras, SeparacionArbolPalmera)
                            && Pendiente(terreno, p) > 0.8f && terreno.SampleHeight(V3(p)) > AlturaBorde - 0.5f)
                        {
                            var pos = Suelo(terreno, p);
                            posArbolitos.Add(pos);
                            CrearArbolito(arbolitos, pos, normal, quad, matArbolito, rnd);
                        }
                    }

                    // Palmera: un poco más atrás.
                    if (rnd.NextDouble() < ProbPalmera)
                    {
                        Vector2 p = c + normal * (dBorde + Rango(rnd, 4f, 16f)) + dir * Rango(rnd, -1.5f, 1.5f);
                        if (Libre(p, zonas) && Lejos(p, posPalmeras, SeparacionPalmeras) && Lejos(p, posArbolitos, SeparacionArbolPalmera)
                            && Pendiente(terreno, p) > 0.8f && terreno.SampleHeight(V3(p)) > AlturaBorde - 0.5f)
                        {
                            var pos = Suelo(terreno, p);
                            posPalmeras.Add(pos);
                            // Se inclina un poco hacia el río.
                            CrearPalmera(modelo, palmeras, pos, -V3(normal), baseModelo, posPalmeras.Count, rnd);
                        }
                    }

                    // Arbusto provisional: grupo de matas de pasto grandes y más oscuras, en la parte alta de la orilla.
                    // Usa su propio azar para no mover lo que ya estaba generado.
                    if (rndArbustos.NextDouble() < ProbArbusto)
                    {
                        Vector2 p = c + normal * (dBorde + Rango(rndArbustos, -1.5f, 4f)) + dir * Rango(rndArbustos, -1.5f, 1.5f);
                        if (Libre(p, zonas) && Lejos(p, posArbustos, SeparacionArbustos) && Lejos(p, posArbolitos, 2.5f)
                            && Lejos(p, posPalmeras, 2.5f) && Pendiente(terreno, p) > 0.7f && terreno.SampleHeight(V3(p)) > AlturaBorde - 1f)
                        {
                            var pos = Suelo(terreno, p);
                            posArbustos.Add(pos);
                            CrearArbusto(arbustos, pos, normal, quad, matArbusto, rndArbustos);
                        }
                    }
                }
            }
        }

        // Bosque tierra adentro: una celda cada CeldaBosque metros a lo largo del recorrido, con manchas de ruido
        // (zonas tupidas y claros). Azar propio para no mover lo de la orilla.
        var rndBosque = new System.Random(Semilla + 2);
        int enBosque = 0;
        var tp = terreno.transform.position; var ts = terreno.terrainData.size;
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var c in cauce) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.y); maxZ = Mathf.Max(maxZ, c.y); }
        minX = Mathf.Max(minX - DistanciaBosque, tp.x + 2f); maxX = Mathf.Min(maxX + DistanciaBosque, tp.x + ts.x - 2f);
        minZ = Mathf.Max(minZ - DistanciaBosque, tp.z + 2f); maxZ = Mathf.Min(maxZ + DistanciaBosque, tp.z + ts.z - 2f);
        for (float z = minZ; z < maxZ; z += CeldaBosque)
            for (float x = minX; x < maxX; x += CeldaBosque)
            {
                var p = new Vector2(x + Rango(rndBosque, 0f, CeldaBosque), z + Rango(rndBosque, 0f, CeldaBosque));
                float mancha = Mathf.PerlinNoise(p.x / TamanoManchasBosque + 31.7f, p.y / TamanoManchasBosque + 12.3f);
                double azar = rndBosque.NextDouble();
                bool esPalmera = rndBosque.NextDouble() < ProbPalmeraBosque;
                // Más probable cerca del río y dentro de las manchas.
                float dRio = DistanciaAlCauce(p, cauce);
                if (dRio < DistanciaBosqueMin || dRio > DistanciaBosque) continue;
                float prob = Mathf.Clamp01((mancha - 0.3f) * 1.6f) * Mathf.Lerp(1f, 0.55f, (dRio - DistanciaBosqueMin) / (DistanciaBosque - DistanciaBosqueMin));
                if (azar >= prob) continue;
                if (!Libre(p, zonas) || Pendiente(terreno, p) < 0.8f || terreno.SampleHeight(V3(p)) < AlturaBorde) continue;

                var pos = Suelo(terreno, p);
                Vector3 haciaRio = V3(CercanoEnCauce(p, cauce) - p).normalized;
                if (esPalmera)
                {
                    if (!Lejos(p, posPalmeras, SeparacionPalmeras) || !Lejos(p, posArbolitos, SeparacionArbolPalmera)) continue;
                    posPalmeras.Add(pos);
                    CrearPalmera(modelo, palmeras, pos, haciaRio, baseModelo, posPalmeras.Count, rndBosque);
                }
                else
                {
                    if (!Lejos(p, posArbolitos, SeparacionArbolitos) || !Lejos(p, posPalmeras, SeparacionArbolPalmera)) continue;
                    posArbolitos.Add(pos);
                    CrearArbolito(arbolitos, pos, new Vector2(-haciaRio.x, -haciaRio.z), quad, matArbolito, rndBosque);
                }
                enBosque++;
            }

        EditorSceneManager.MarkSceneDirty(escena);
        string resumen = $"Orillas del río: {palmeras.childCount} palmeras, {arbolitos.childCount} arbolitos, {pasto.childCount} matas de pasto, " +
                         $"{arbustos.childCount} arbustos ({enBosque} árboles tierra adentro).";
        Debug.Log(resumen);
        return resumen;
    }

    static string Error(string msg) { Debug.LogError(msg); return msg; }

    static List<Vector2> Cauce()
    {
        var puntos = new List<Vector2>(Antes);
        var trayectos = Object.FindObjectsByType<NavegarBote>(FindObjectsSortMode.None);
        System.Array.Sort(trayectos, (x, y) => string.CompareOrdinal(x.name, y.name));
        puntos.Add(new Vector2(391.5f, 85f)); // Parada Muelle 1
        foreach (var t in trayectos)
            if (t.ruta != null)
                foreach (Transform p in t.ruta) puntos.Add(new Vector2(p.position.x, p.position.z));
        puntos.AddRange(Despues);
        return puntos;
    }

    static List<Zona> ZonasDespejadas()
    {
        var zonas = new List<Zona> { new Zona { centro = CentroMaloca, radio = RadioMaloca } };
        // Muelles, bajadas, hotspots, botones y el punto de aparición.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name;
            if (n.StartsWith("Muelle ") || n.StartsWith("Bajada Muelle") || n.StartsWith("Hotspot Muelle")
                || n.StartsWith("Poke Subir") || n == "Punto de Aparicion")
                zonas.Add(new Zona { centro = new Vector2(t.position.x, t.position.z), radio = 12f });
        }
        // Las chagras: su caja (renderers y colliders) más un margen.
        foreach (var nombre in new[] { "Chagra 1", "Chagra 2", "Maloca", "Cocina Maloca" })
        {
            var g = GameObject.Find(nombre);
            if (g == null) continue;
            var b = new Bounds(g.transform.position, Vector3.zero);
            foreach (var r in g.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            foreach (var c in g.GetComponentsInChildren<Collider>()) b.Encapsulate(c.bounds);
            zonas.Add(new Zona { centro = new Vector2(b.center.x, b.center.z), radio = Mathf.Max(b.extents.x, b.extents.z) + 5f });
        }
        return zonas;
    }

    static bool BuscarOrilla(Terrain t, Vector2 c, Vector2 normal, out float dAgua, out float dBorde)
    {
        dAgua = dBorde = -1f;
        for (float d = 0f; d < 40f; d += 0.5f)
        {
            float h = t.SampleHeight(V3(c + normal * d));
            if (dAgua < 0f && h >= NivelAgua + 0.2f) dAgua = d;
            if (h >= AlturaBorde) { dBorde = d; break; }
        }
        return dAgua >= 0f && dBorde >= 0f;
    }

    static void CrearPlano(string nombre, Transform padre, Vector3 pos, Vector2 haciaAfuera, Mesh quad, Material mat,
                           float ancho, float alto, float hundir)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre);
        go.transform.position = pos;
        // Mirando desde el río (en juego gira hacia la cámara).
        go.transform.rotation = Quaternion.LookRotation(V3(haciaAfuera));

        var img = new GameObject("Imagen");
        img.transform.SetParent(go.transform, false);
        img.transform.localPosition = new Vector3(0f, alto * 0.5f - hundir, 0f);
        img.transform.localScale = new Vector3(ancho, alto, 1f);
        img.AddComponent<MeshFilter>().sharedMesh = quad;
        var mr = img.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    // Arbolito de tamaño variado: más medianos que gigantes, y algunos un poco más anchos o angostos.
    static void CrearArbolito(Transform padre, Vector3 pos, Vector2 haciaAfuera, Mesh quad, Material mat, System.Random rnd)
    {
        float tam = Mathf.Lerp(TamArbolitoMin, TamArbolitoMax, Mathf.Pow((float)rnd.NextDouble(), CurvaTamano));
        float ancho = tam * Rango(rnd, 0.9f, 1.15f);
        // La imagen deja ~5 % vacío abajo; se hunde un poco para que las raíces toquen el suelo.
        CrearPlano("Arbolito", padre, pos, haciaAfuera, quad, mat, ancho, tam, tam * 0.05f + 0.3f);
    }

    static void CrearPalmera(GameObject modelo, Transform padre, Vector3 pos, Vector3 inclinarHacia, float baseModelo, int numero, System.Random rnd)
    {
        float escala = Rango(rnd, 1.6f, 3f);
        var palmera = Object.Instantiate(modelo, padre);
        palmera.name = "Palmera " + numero;
        palmera.transform.localScale = Vector3.one * escala;
        var giro = Quaternion.Euler(0f, Rango(rnd, 0f, 360f), 0f);
        inclinarHacia.y = 0f;
        var eje = inclinarHacia.sqrMagnitude > 0.001f ? Vector3.Cross(Vector3.up, inclinarHacia.normalized) : Vector3.right;
        var inclinacion = Quaternion.AngleAxis(Rango(rnd, 0f, 6f), eje);
        palmera.transform.SetPositionAndRotation(pos + Vector3.up * (baseModelo * escala - 0.4f), inclinacion * giro);
        foreach (Transform hijo in palmera.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(hijo.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    static Vector2 CercanoEnCauce(Vector2 p, List<Vector2> cauce)
    {
        Vector2 mejor = cauce[0]; float dMejor = float.MaxValue;
        for (int i = 0; i < cauce.Count - 1; i++)
        {
            Vector2 a = cauce[i], ab = cauce[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            Vector2 q = a + ab * t;
            float d = (p - q).sqrMagnitude;
            if (d < dMejor) { dMejor = d; mejor = q; }
        }
        return mejor;
    }

    static float DistanciaAlCauce(Vector2 p, List<Vector2> cauce) => Vector2.Distance(p, CercanoEnCauce(p, cauce));

    // Grupo de 3 a 5 matas grandes en un mismo plano (todo el grupo gira hacia la cámara).
    static void CrearArbusto(Transform padre, Vector3 pos, Vector2 haciaAfuera, Mesh quad, Material mat, System.Random rnd)
    {
        var go = new GameObject("Arbusto");
        go.transform.SetParent(padre);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(V3(haciaAfuera));

        int matas = rnd.Next(3, 6);
        float ancho = Rango(rnd, ArbustoAnchoMin, ArbustoAnchoMax);
        for (int i = 0; i < matas; i++)
        {
            float alto = Rango(rnd, ArbustoAltoMin, ArbustoAltoMax);
            // Las del centro, más altas y adelante; las de los lados, más bajas y atrás.
            float x = matas == 1 ? 0f : Mathf.Lerp(-ancho * 0.5f, ancho * 0.5f, i / (matas - 1f)) + Rango(rnd, -0.3f, 0.3f);
            float lado = Mathf.Abs(x) / (ancho * 0.5f + 0.01f);
            alto *= Mathf.Lerp(1f, 0.7f, lado);
            float z = Mathf.Lerp(-0.1f, 0.35f, lado) + Rango(rnd, -0.1f, 0.1f);

            var img = new GameObject("Mata");
            img.transform.SetParent(go.transform, false);
            img.transform.localPosition = new Vector3(x, alto * 0.5f - 0.35f, z);
            img.transform.localScale = new Vector3(alto * Rango(rnd, 1.1f, 1.5f) * (rnd.NextDouble() < 0.5 ? -1f : 1f), alto, 1f);
            img.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = img.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    static Material CrearMaterial(string ruta, string rutaTextura) => CrearMaterial(ruta, rutaTextura, Color.white);

    static Material CrearMaterial(string ruta, string rutaTextura, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, ruta);
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTextura);
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);
        // Recorte por alfa (sin transparencia: no hay problemas de orden en VR) y visible por ambos lados.
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Cull", 0f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    static float BaseRelativa(GameObject modelo)
    {
        float minY = float.MaxValue;
        foreach (var r in modelo.GetComponentsInChildren<Renderer>()) minY = Mathf.Min(minY, r.bounds.min.y);
        return (modelo.transform.position.y - minY) / modelo.transform.lossyScale.y;
    }

    static bool Libre(Vector2 p, List<Zona> zonas)
    {
        foreach (var z in zonas) if (Vector2.Distance(p, z.centro) < z.radio) return false;
        return true;
    }

    static bool Lejos(Vector2 p, List<Vector3> otros, float minimo)
    {
        foreach (var o in otros) if (Vector2.Distance(p, new Vector2(o.x, o.z)) < minimo) return false;
        return true;
    }

    static float Pendiente(Terrain t, Vector2 p)
    {
        var d = t.terrainData; var tp = t.transform.position;
        return d.GetInterpolatedNormal((p.x - tp.x) / d.size.x, (p.y - tp.z) / d.size.z).y;
    }

    static Vector3 Suelo(Terrain t, Vector2 p) { var v = V3(p); v.y = t.SampleHeight(v) + t.transform.position.y; return v; }
    static Vector3 V3(Vector2 p) => new Vector3(p.x, 0f, p.y);
    static float Rango(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);
}
