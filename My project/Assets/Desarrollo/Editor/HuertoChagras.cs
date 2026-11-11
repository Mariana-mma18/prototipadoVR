using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Huerto de las chagras de TERRENO FINAL con los cultivos que hizo diseño en su escena
// (_Main/Scenes/Terreno/TERRENO FINAL: HUERTA MAIZ, HUERTA MANI, YUCA), sin tocar sus archivos.
//
// Diseño arma cada mata con piezas sueltas a otra escala (la mata de maíz mide 8 m y cada mazorca
// 1,3 m; el maní 4 m; la yuca 4,4 m) y el maíz pesa ~170.000 triángulos por mazorca. Aquí:
//   1. Se copian sus composiciones (mata RAIZ_HUERTA + mazorcas maiz_FINAL, raíces yuca.obj) a
//      tamaño real y se juntan en una sola malla por mata, simplificada y con niveles de detalle
//      (Mesh LOD), en Assets/Desarrollo/Modelos/Huerto. Prefabs en Assets/Desarrollo/Prefabs/Huerto.
//   2. Se siembran en surcos dentro de las paredes "Límite" de cada chagra, dejando libre la llegada
//      del muelle, el canasto, las gallinas y los ingredientes, y sin meterse a la orilla del río.
//      Raíz "Huerto" dentro de "Chagra 1" (maíz y maní) y "Chagra 2" (yuca).
//   3. A los ingredientes que se recogen (cubos grises) se les pone el modelo de diseño: la mazorca
//      al Maíz y la raíz de yuca a la Yuca. El agarre y los encajes no cambian: el cubo se esconde y su
//      collider toma el tamaño del modelo. Maní y Sacha culantro siguen como cubos porque diseño los
//      entregó en .blend (Unity solo los abre con Blender instalado).
// Volver a ejecutar cualquiera de los pasos los rehace igual (semilla fija).
public static class HuertoChagras
{
    const string CarpetaMallas = "Assets/Desarrollo/Modelos/Huerto";
    const string CarpetaPrefabs = "Assets/Desarrollo/Prefabs/Huerto";

    const string FbxPlanta = "Assets/_Main/Scenes/Terreno/MODELOS/RAIZ/RAIZ_HUERTA.fbx";
    const string FbxMaiz = "Assets/_Main/Scenes/Terreno/MODELOS/maiz/maiz_FINAL.fbx";
    const string ObjYuca = "Assets/_Main/Scenes/Terreno/MODELOS/YUCA/yuca.obj";
    const string MatPlanta = "Assets/_Main/Scenes/Terreno/MODELOS/RAIZ/New Material.mat";
    const string MatHojasMaiz = "Assets/_Main/Scenes/Terreno/MODELOS/maiz/New Material 1.mat";
    const string MatGranosMaiz = "Assets/_Main/Scenes/Terreno/MODELOS/maiz/New Material 2.mat";
    const string MatYuca = "Assets/_Main/Scenes/Terreno/MODELOS/YUCA/YUCA.mat";

    // Tamaño real respecto a diseño: maíz 8 m -> 2,4 m (mazorcas de ~0,33 m entre 1 y 2,4 m de alto),
    // maní 4 m -> 1 m (más bajo se pierde entre el pasto), yuca 4,4 m -> 1,6 m (raíces de ~0,27 m).
    const float EscalaMaiz = 0.3f;
    const float EscalaMani = 0.25f;
    const float EscalaYuca = 0.36f;

    // Largo de los ingredientes que se agarran (los cubos medían 0,2 m y 0,29 m)
    const float LargoMazorcaIngrediente = 0.22f;
    const float LargoYucaIngrediente = 0.28f;

    // Triángulos máximos por pieza (se usa el primer nivel simplificado que quepa)
    const int TrisPlanta = 1500;        // mata del maíz y la yuca (original 5.828)
    const int TrisPlantaMani = 800;     // matas de maní, más pequeñas
    const int TrisGranosCampo = 1100;   // mazorcas de las matas (original 124.800)
    const int TrisHojasCampo = 900;     // hojas de las mazorcas (original 46.080)
    const int TrisGranosMano = 8000;    // mazorca que se agarra
    const int TrisHojasMano = 6500;

    const int Semilla = 1111;

    // ---------------------------------------------------------------------------------------------
    // Composición de diseño (medida en su escena): posición, giro y escala de cada pieza respecto a la
    // base de la mata (centro de la mata a la altura de la punta de sus raíces), en sus unidades.
    struct Pieza
    {
        public Vector3 p; public Quaternion r; public Vector3 s;
        public Pieza(Vector3 p, Quaternion r, Vector3 s) { this.p = p; this.r = r; this.s = s; }
        public Matrix4x4 Matriz => Matrix4x4.TRS(p, r, s);
    }

    static readonly Pieza MataMaiz = new Pieza(new Vector3(-0.56252f, 5.70944f, -0.72655f), new Quaternion(-0.66909f, 0.22874f, 0.22874f, 0.66909f), new Vector3(14.35467f, 14.55593f, 18.06361f));
    // Mazorcas de abajo hacia arriba: hojas (BézierCurve.001) y granos (Sphere.001)
    static readonly Pieza[] HojasMazorca =
    {
        new Pieza(new Vector3(-0.0009f, 3.75432f, -0.12769f), new Quaternion(0.56404f, 0.42645f, -0.56404f, -0.42645f), Vector3.one * 23.07739f),
        new Pieza(new Vector3(-0.38445f, 5.29465f, 0.30177f), new Quaternion(0.48412f, 0.84084f, -0.17293f, -0.16945f), Vector3.one * 23.07739f),
        new Pieza(new Vector3(-0.16481f, 6.68648f, 0.37622f), new Quaternion(-0.41262f, -0.65026f, 0.33969f, 0.53992f), Vector3.one * 23.07739f),
        new Pieza(new Vector3(0.13637f, 7.31426f, 0.05157f), new Quaternion(0.56621f, 0.49281f, -0.44293f, -0.49026f), Vector3.one * 23.07739f),
    };
    static readonly Pieza[] GranosMazorca =
    {
        new Pieza(new Vector3(-0.0009f, 3.86709f, -0.40517f), new Quaternion(-0.85472f, 0f, 0f, 0.51909f), new Vector3(23.07739f, 23.07739f, 53.13039f)),
        new Pieza(new Vector3(-0.2932f, 5.56673f, 0.21596f), new Quaternion(-0.53466f, -0.44998f, -0.26705f, 0.66358f), new Vector3(23.07739f, 23.07739f, 53.13039f)),
        new Pieza(new Vector3(-0.13878f, 6.95411f, 0.24428f), new Quaternion(-0.61457f, -0.07239f, -0.05922f, 0.7833f), new Vector3(23.07739f, 23.07739f, 53.13039f)),
        new Pieza(new Vector3(0.18803f, 7.48907f, -0.1861f), new Quaternion(-0.78038f, 0.00705f, -0.0869f, 0.61919f), new Vector3(23.07739f, 23.07739f, 53.13039f)),
    };
    static readonly Pieza MataMani = new Pieza(new Vector3(-0.28358f, 2.87834f, -0.36628f), new Quaternion(-0.66909f, 0.22874f, 0.22874f, 0.66909f), new Vector3(7.23671f, 7.33817f, 9.10652f));
    static readonly Pieza MataYuca = new Pieza(new Vector3(-0.50057f, 3.12229f, -0.04385f), new Quaternion(-0.52136f, 0.47769f, 0.47769f, 0.52136f), new Vector3(7.85007f, 7.96013f, 9.87835f));
    static readonly Pieza[] RaicesYuca =
    {
        new Pieza(new Vector3(0.48595f, -0.07384f, -0.52686f), new Quaternion(0.12045f, 0.38219f, 0.2754f, 0.87383f), Vector3.one * 0.1486f),
        new Pieza(new Vector3(0.52759f, -0.07383f, 0.28712f), new Quaternion(-0.07162f, -0.22724f, 0.29193f, 0.92629f), Vector3.one * 0.1486f),
    };

    // Matas de maní por grupo (un solo objeto por grupo para no multiplicar las llamadas de dibujo)
    static readonly (Vector3 pos, float giro, float escala)[] GrupoMani =
    {
        (new Vector3(-0.16f, 0f, -0.14f), 0f, 1f),
        (new Vector3(0.24f, 0f, -0.04f), 125f, 0.85f),
        (new Vector3(-0.06f, 0f, 0.26f), 245f, 0.95f),
    };

    // ---------------------------------------------------------------------------------------------
    // Trazado en las chagras (coordenadas del mundo; el terreno es el mismo de diseño)

    // Chagra 1 (muelle 2): el jugador llega mirando al oeste en (378, 195); el claro del centro
    // (canasto, gallinas, ingredientes) queda libre. Maíz al norte y maní al oeste, de frente al
    // llegar. El sur no se usa: diseño pintó pasto alto ahí y tapa el maní.
    static readonly Vector2 MaizOrigen = new Vector2(361.6f, 203.9f);
    const int MaizColumnas = 10, MaizFilas = 5;
    const float MaizPasoX = 1.5f, MaizPasoZ = 1.45f;

    static readonly Vector2 ManiOrigen = new Vector2(359.8f, 191.2f);
    const int ManiColumnas = 6, ManiFilas = 10;
    const float ManiPaso = 1.2f;

    const float AlturaMinimaChagra1 = 10.2f; // más abajo ya es la bajada al río

    // Chagra 2 (muelle 3): franja girada entre el río y la pared "Límite" del este. Ejes alineados
    // con esa pared: A hacia el norte (a lo largo de la pared) y B hacia el río.
    static readonly Vector2 Chagra2Pared = new Vector2(446.0f, 294.9f);
    static readonly Vector2 Chagra2EjeA = new Vector2(0.276f, 0.961f);
    static readonly Vector2 Chagra2EjeB = new Vector2(-0.961f, 0.276f);
    // Yuca en la mitad norte; la mitad sur queda libre para el sacha culantro.
    const float YucaA0 = 4.5f, YucaB0 = 2.2f, YucaPaso = 1.6f;
    const int YucaFilasA = 9, YucaFilasB = 9;
    const float AlturaMinimaChagra2 = 12f;  // aleja la yuca del filo del barranco

    static readonly Vector2 BajadaMuelle2 = new Vector2(378.1f, 195.4f);
    static readonly Vector2 BajadaMuelle3 = new Vector2(425.95f, 297.3f);

    // Dónde quedan los ingredientes que se recogen (en el suelo, al pie de su cultivo)
    static readonly Vector2 SitioMaiz = new Vector2(370.4f, 202.6f);
    static readonly Vector2 SitioMani = new Vector2(367.4f, 197.0f);
    static readonly Vector2 SitioYuca = Chagra2Pared + Chagra2EjeA * 3.0f + Chagra2EjeB * 12f;

    // ---------------------------------------------------------------------------------------------

    [MenuItem("Desarrollo/Escenario/Huerto de las chagras/Hacer todo (mallas, siembra e ingredientes)")]
    public static void HacerTodo()
    {
        GenerarMallasYPrefabs();
        SembrarHuerto();
        PonerModelosIngredientes();
    }

    [MenuItem("Desarrollo/Escenario/Huerto de las chagras/1. Generar mallas y prefabs")]
    public static void GenerarMallasYPrefabs()
    {
        CrearCarpeta(CarpetaMallas);
        CrearCarpeta(CarpetaPrefabs);

        Mesh planta = CargarMalla(FbxPlanta, "Plane.004");
        Mesh granos = CargarMalla(FbxMaiz, "Sphere.001");
        Mesh hojas = CargarMalla(FbxMaiz, "BézierCurve.001");
        Mesh raiz = CargarMalla(ObjYuca, "default");

        Mesh plantaCampo = Reducir(planta, TrisPlanta);
        Mesh plantaMani = Reducir(planta, TrisPlantaMani);
        Mesh granosCampo = Reducir(granos, TrisGranosCampo);
        Mesh hojasCampo = Reducir(hojas, TrisHojasCampo);
        Mesh granosMano = Reducir(granos, TrisGranosMano);
        Mesh hojasMano = Reducir(hojas, TrisHojasMano);

        Material mPlanta = Cargar<Material>(MatPlanta);
        Material mHojas = Cargar<Material>(MatHojasMaiz);
        Material mGranos = Cargar<Material>(MatGranosMaiz);
        Material mYuca = Cargar<Material>(MatYuca);

        // Maíz: submallas mata / hojas de mazorca / granos
        Matrix4x4 eMaiz = Matrix4x4.Scale(Vector3.one * EscalaMaiz);
        foreach (int mazorcas in new[] { 4, 2 })
        {
            var partes = new List<Parte> { new Parte(plantaCampo, eMaiz * MataMaiz.Matriz, 0) };
            for (int i = 0; i < mazorcas; i++)
            {
                partes.Add(new Parte(hojasCampo, eMaiz * HojasMazorca[i].Matriz, 1));
                partes.Add(new Parte(granosCampo, eMaiz * GranosMazorca[i].Matriz, 2));
            }
            string nombre = $"Maíz ({mazorcas} mazorcas)";
            Mesh malla = GuardarMalla(Combinar(nombre, partes, 3, true), nombre);
            GuardarPrefab(nombre, malla, new[] { mPlanta, mHojas, mGranos });
        }

        // Maní: grupo de 3 matas pequeñas
        {
            var partes = new List<Parte>();
            foreach (var m in GrupoMani)
                partes.Add(new Parte(plantaMani, Matrix4x4.TRS(m.pos, Quaternion.Euler(0f, m.giro, 0f), Vector3.one * m.escala) * Matrix4x4.Scale(Vector3.one * EscalaMani) * MataMani.Matriz, 0));
            const string nombre = "Maní (grupo de matas)";
            Mesh malla = GuardarMalla(Combinar(nombre, partes, 1, true), nombre);
            GuardarPrefab(nombre, malla, new[] { mPlanta });
        }

        // Yuca: mata y dos raíces asomadas
        {
            Matrix4x4 e = Matrix4x4.Scale(Vector3.one * EscalaYuca);
            var partes = new List<Parte> { new Parte(plantaCampo, e * MataYuca.Matriz, 0) };
            foreach (Pieza r in RaicesYuca) partes.Add(new Parte(raiz, e * r.Matriz, 1));
            const string nombre = "Yuca (mata)";
            Mesh malla = GuardarMalla(Combinar(nombre, partes, 2, true), nombre);
            GuardarPrefab(nombre, malla, new[] { mPlanta, mYuca });
        }

        // Ingredientes: centrados y con el lado largo en Z, como los cubos que reemplazan
        {
            // La mazorca de abajo, en el marco de sus granos (su eje largo es el Z local)
            Matrix4x4 marco = Matrix4x4.TRS(GranosMazorca[0].p, GranosMazorca[0].r, Vector3.one).inverse;
            var partes = new List<Parte>
            {
                new Parte(hojasMano, marco * HojasMazorca[0].Matriz, 0),
                new Parte(granosMano, marco * GranosMazorca[0].Matriz, 1),
            };
            GuardarMalla(Ajustar(Combinar("Mazorca (ingrediente)", partes, 2, false), LargoMazorcaIngrediente), "Mazorca (ingrediente)");
        }
        {
            // La raíz de yuca.obj es larga en Y: se acuesta sobre Z
            var partes = new List<Parte> { new Parte(raiz, Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)), 0) };
            GuardarMalla(Ajustar(Combinar("Yuca (ingrediente)", partes, 1, false), LargoYucaIngrediente), "Yuca (ingrediente)");
        }

        foreach (Mesh m in new[] { plantaCampo, plantaMani, granosCampo, hojasCampo, granosMano, hojasMano })
            if (m != planta && m != granos && m != hojas) Object.DestroyImmediate(m);

        AssetDatabase.SaveAssets();
        Debug.Log("Huerto: mallas y prefabs generados en " + CarpetaMallas + " y " + CarpetaPrefabs);
    }

    [MenuItem("Desarrollo/Escenario/Huerto de las chagras/2. Sembrar huerto en las chagras")]
    public static void SembrarHuerto()
    {
        Terrain terreno = Object.FindFirstObjectByType<Terrain>();
        Transform chagra1 = BuscarRaiz("Chagra 1");
        Transform chagra2 = BuscarRaiz("Chagra 2");
        if (!terreno || !chagra1 || !chagra2)
        {
            Debug.LogError("Huerto: abre TERRENO FINAL de Desarrollo (falta Terrain, Chagra 1 o Chagra 2).");
            return;
        }

        GameObject maiz4 = Cargar<GameObject>(CarpetaPrefabs + "/Maíz (4 mazorcas).prefab");
        GameObject maiz2 = Cargar<GameObject>(CarpetaPrefabs + "/Maíz (2 mazorcas).prefab");
        GameObject mani = Cargar<GameObject>(CarpetaPrefabs + "/Maní (grupo de matas).prefab");
        GameObject yuca = Cargar<GameObject>(CarpetaPrefabs + "/Yuca (mata).prefab");
        if (!maiz4 || !maiz2 || !mani || !yuca)
        {
            Debug.LogError("Huerto: faltan los prefabs; ejecuta antes '1. Generar mallas y prefabs'.");
            return;
        }

        var azar = new System.Random(Semilla);
        int total = 0;

        // ---- Chagra 1 ----
        Transform huerto1 = NuevaRaiz(chagra1, "Huerto");
        Transform campoMaiz = NuevoHijo(huerto1, "Maíz");
        for (int fila = 0; fila < MaizFilas; fila++)
        {
            for (int col = 0; col < MaizColumnas; col++)
            {
                Vector2 p = MaizOrigen + new Vector2(col * MaizPasoX, fila * MaizPasoZ) + Temblor(azar, 0.25f);
                // La fila de adelante (la que se ve desde el claro) con todas sus mazorcas
                GameObject prefab = fila == 0 || azar.NextDouble() < 0.45 ? maiz4 : maiz2;
                if (Sembrar(terreno, campoMaiz, prefab, p, azar, 0.88f, 1.1f, 0.35f, AlturaMinimaChagra1, BajadaMuelle2, 4f)) total++;
            }
        }
        Transform campoMani = NuevoHijo(huerto1, "Maní");
        for (int fila = 0; fila < ManiFilas; fila++)
        {
            for (int col = 0; col < ManiColumnas; col++)
            {
                Vector2 p = ManiOrigen + new Vector2(col, fila) * ManiPaso + Temblor(azar, 0.2f);
                if (Sembrar(terreno, campoMani, mani, p, azar, 0.85f, 1.15f, 0.3f, AlturaMinimaChagra1, BajadaMuelle2, 4f)) total++;
            }
        }

        // ---- Chagra 2 ----
        Transform huerto2 = NuevaRaiz(chagra2, "Huerto");
        Transform campoYuca = NuevoHijo(huerto2, "Yuca");
        for (int i = 0; i < YucaFilasA; i++)
        {
            for (int j = 0; j < YucaFilasB; j++)
            {
                Vector2 p = Chagra2Pared + Chagra2EjeA * (YucaA0 + i * YucaPaso) + Chagra2EjeB * (YucaB0 + j * YucaPaso) + Temblor(azar, 0.3f);
                if (Sembrar(terreno, campoYuca, yuca, p, azar, 0.85f, 1.15f, 0.4f, AlturaMinimaChagra2, BajadaMuelle3, 5f)) total++;
            }
        }

        EditorSceneManager.MarkSceneDirty(chagra1.gameObject.scene);
        Debug.Log($"Huerto: {total} matas sembradas ({campoMaiz.childCount} maíz, {campoMani.childCount} grupos de maní, {campoYuca.childCount} yuca).");
    }

    [MenuItem("Desarrollo/Escenario/Huerto de las chagras/3. Poner modelos a los ingredientes")]
    public static void PonerModelosIngredientes()
    {
        Terrain terreno = Object.FindFirstObjectByType<Terrain>();
        Mesh mazorca = Cargar<Mesh>(CarpetaMallas + "/Mazorca (ingrediente).asset");
        Mesh raiz = Cargar<Mesh>(CarpetaMallas + "/Yuca (ingrediente).asset");
        if (!terreno || !mazorca || !raiz)
        {
            Debug.LogError("Huerto: abre TERRENO FINAL y ejecuta antes '1. Generar mallas y prefabs'.");
            return;
        }

        Material mHojas = Cargar<Material>(MatHojasMaiz);
        Material mGranos = Cargar<Material>(MatGranosMaiz);
        Material mYuca = Cargar<Material>(MatYuca);

        Transform maiz = BuscarIngrediente("Chagra 1", "Maíz_Cubo");
        Transform mani = BuscarIngrediente("Chagra 1", "Maní_Cubo");
        Transform yuca = BuscarIngrediente("Chagra 2", "Yuca_Cubo");

        if (maiz) { PonerModelo(maiz, mazorca, new[] { mHojas, mGranos }); Ubicar(terreno, maiz, SitioMaiz, 75f); }
        if (yuca) { PonerModelo(yuca, raiz, new[] { mYuca }); Ubicar(terreno, yuca, SitioYuca, 30f); }
        // El maní sigue como cubo (modelo en .blend); solo se pone junto a su cultivo
        if (mani) Ubicar(terreno, mani, SitioMani, 20f);

        if (maiz) EditorSceneManager.MarkSceneDirty(maiz.gameObject.scene);
        Debug.Log("Huerto: ingredientes con modelo (maíz " + (maiz ? "sí" : "NO ENCONTRADO") + ", yuca " + (yuca ? "sí" : "NO ENCONTRADO") + ").");
    }

    // ---------------------------------------------------------------------------------------------
    // Siembra

    static Vector2 Temblor(System.Random azar, float radio) =>
        new Vector2((float)(azar.NextDouble() * 2 - 1) * radio, (float)(azar.NextDouble() * 2 - 1) * radio);

    static bool Sembrar(Terrain terreno, Transform padre, GameObject prefab, Vector2 p, System.Random azar,
        float escalaMin, float escalaMax, float radio, float alturaMinima, Vector2 bajada, float radioBajada)
    {
        // Los números al azar se sacan siempre, se siembre o no, para que el resto no cambie
        float giro = (float)azar.NextDouble() * 360f;
        float escala = Mathf.Lerp(escalaMin, escalaMax, (float)azar.NextDouble());

        if (Vector2.Distance(p, bajada) < radioBajada) return false;

        // Se apoya en el punto más bajo bajo la mata para que en pendiente no quede flotando
        float centro = Altura(terreno, p);
        float minimo = centro, maximo = centro;
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.PI * 0.5f;
            float h = Altura(terreno, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radio * escala);
            minimo = Mathf.Min(minimo, h);
            maximo = Mathf.Max(maximo, h);
        }
        if (minimo < alturaMinima || maximo - minimo > 0.9f) return false;

        var mata = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        mata.transform.SetPositionAndRotation(new Vector3(p.x, minimo - 0.03f, p.y), Quaternion.Euler(0f, giro, 0f));
        mata.transform.localScale = Vector3.one * escala;
        return true;
    }

    static float Altura(Terrain terreno, Vector2 p) =>
        terreno.SampleHeight(new Vector3(p.x, 0f, p.y)) + terreno.transform.position.y;

    static Transform BuscarRaiz(string nombre)
    {
        var escena = EditorSceneManager.GetActiveScene();
        foreach (GameObject g in escena.GetRootGameObjects())
            if (g.name == nombre) return g.transform;
        return null;
    }

    static Transform NuevaRaiz(Transform chagra, string nombre)
    {
        Transform vieja = chagra.Find(nombre);
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);

        var raiz = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(raiz, "Sembrar huerto");
        raiz.transform.SetParent(chagra, false);
        // Que el huerto no cambie el NavMesh del teleport si se vuelve a hornear
        var modificador = raiz.AddComponent<NavMeshModifier>();
        modificador.ignoreFromBuild = true;
        return raiz.transform;
    }

    static Transform NuevoHijo(Transform padre, string nombre)
    {
        var hijo = new GameObject(nombre);
        hijo.transform.SetParent(padre, false);
        // Las matas se colocan en coordenadas del mundo: el contenedor no debe girarlas
        hijo.transform.rotation = Quaternion.identity;
        hijo.transform.position = Vector3.zero;
        return hijo.transform;
    }

    // ---------------------------------------------------------------------------------------------
    // Ingredientes

    static Transform BuscarIngrediente(string chagra, string nombre)
    {
        Transform raiz = BuscarRaiz(chagra);
        if (!raiz) return null;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }

    // Cuerpo del ingrediente: el hijo con el Rigidbody y el Grabbable ("[BuildingBlock] Cube")
    static Transform Cuerpo(Transform ingrediente)
    {
        Rigidbody rb = ingrediente.GetComponentInChildren<Rigidbody>(true);
        return rb ? rb.transform : null;
    }

    static void PonerModelo(Transform ingrediente, Mesh malla, Material[] materiales)
    {
        Transform cuerpo = Cuerpo(ingrediente);
        Transform cubo = cuerpo ? cuerpo.Find("Cube") : null;
        if (!cubo)
        {
            Debug.LogError("Huerto: no encuentro el cubo de " + ingrediente.name);
            return;
        }

        Undo.RecordObject(cubo, "Modelo ingrediente");
        var rendCubo = cubo.GetComponent<MeshRenderer>();
        if (rendCubo)
        {
            Undo.RecordObject(rendCubo, "Modelo ingrediente");
            rendCubo.enabled = false;
        }

        Transform modelo = cuerpo.Find("Modelo");
        if (!modelo)
        {
            var go = new GameObject("Modelo");
            Undo.RegisterCreatedObjectUndo(go, "Modelo ingrediente");
            modelo = go.transform;
            modelo.SetParent(cuerpo, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
        }
        modelo.localPosition = Vector3.zero;
        modelo.localRotation = Quaternion.identity;
        modelo.localScale = Vector3.one;
        modelo.GetComponent<MeshFilter>().sharedMesh = malla;
        modelo.GetComponent<MeshRenderer>().sharedMaterials = materiales;

        // El collider sólido del cubo (con el que se apoya en el suelo) toma el tamaño del modelo,
        // y el trigger de agarre un poco más grande
        Bounds b = malla.bounds;
        cubo.localPosition = b.center;
        cubo.localRotation = Quaternion.identity;
        cubo.localScale = b.size;
        foreach (BoxCollider caja in cuerpo.GetComponents<BoxCollider>())
        {
            if (!caja.isTrigger) continue;
            Undo.RecordObject(caja, "Modelo ingrediente");
            caja.center = b.center;
            caja.size = b.size + Vector3.one * 0.03f;
        }
    }

    static void Ubicar(Terrain terreno, Transform ingrediente, Vector2 sitio, float giro)
    {
        Transform cuerpo = Cuerpo(ingrediente);
        if (!cuerpo) return;
        Undo.RecordObject(cuerpo, "Ubicar ingrediente");
        float alto = 0.06f;
        Transform cubo = cuerpo.Find("Cube");
        if (cubo) alto = cubo.localScale.y * cuerpo.lossyScale.y;
        cuerpo.SetPositionAndRotation(new Vector3(sitio.x, Altura(terreno, sitio) + alto * 0.5f + 0.01f, sitio.y), Quaternion.Euler(0f, giro, 0f));
    }

    // ---------------------------------------------------------------------------------------------
    // Mallas

    struct Parte
    {
        public Mesh malla; public Matrix4x4 matriz; public int submalla;
        public Parte(Mesh malla, Matrix4x4 matriz, int submalla) { this.malla = malla; this.matriz = matriz; this.submalla = submalla; }
    }

    // Junta las partes en una malla con una submalla por material; opcionalmente con niveles de detalle
    static Mesh Combinar(string nombre, List<Parte> partes, int submallas, bool conNiveles)
    {
        var grupos = new Mesh[submallas];
        int vertices = 0;
        for (int s = 0; s < submallas; s++)
        {
            var lista = new List<CombineInstance>();
            foreach (Parte p in partes)
                if (p.submalla == s) lista.Add(new CombineInstance { mesh = p.malla, transform = p.matriz });
            grupos[s] = new Mesh { indexFormat = IndexFormat.UInt32 };
            grupos[s].CombineMeshes(lista.ToArray(), true, true);
            vertices += grupos[s].vertexCount;
        }

        var final = new Mesh { name = nombre, indexFormat = vertices > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        var instancias = new CombineInstance[submallas];
        for (int s = 0; s < submallas; s++) instancias[s] = new CombineInstance { mesh = grupos[s], transform = Matrix4x4.identity };
        final.CombineMeshes(instancias, false, false);
        foreach (Mesh g in grupos) Object.DestroyImmediate(g);

        final.RecalculateBounds();
        if (conNiveles)
        {
            try { MeshLodUtility.GenerateMeshLods(final, 6); }
            catch (System.Exception e) { Debug.LogWarning("Huerto: sin niveles de detalle para " + nombre + ": " + e.Message); }
        }
        return final;
    }

    // Centra la malla en el origen y la escala para que su lado Z mida "largo"
    static Mesh Ajustar(Mesh malla, float largo)
    {
        Bounds b = malla.bounds;
        float k = largo / b.size.z;
        var v = new List<Vector3>();
        malla.GetVertices(v);
        for (int i = 0; i < v.Count; i++) v[i] = (v[i] - b.center) * k;
        malla.SetVertices(v);
        malla.RecalculateBounds();
        return malla;
    }

    // Copia de la malla con el primer nivel simplificado que no pase de maxTris triángulos
    static Mesh Reducir(Mesh fuente, int maxTris)
    {
        if (fuente.triangles.Length / 3 <= maxTris) return fuente;

        Mesh copia = Object.Instantiate(fuente);
        MeshLodUtility.GenerateMeshLods(copia, 12);
        int nivel = copia.lodCount - 1;
        for (int l = 0; l < copia.lodCount; l++)
            if (copia.GetLod(0, l).indexCount / 3 <= maxTris) { nivel = l; break; }

        MeshLodRange rango = copia.GetLod(0, nivel);
        SubMeshDescriptor sub = copia.GetSubMesh(0);
        var indices = new int[rango.indexCount];
        using (Mesh.MeshDataArray datos = Mesh.AcquireReadOnlyMeshData(copia))
        {
            Mesh.MeshData d = datos[0];
            int inicio = sub.indexStart + (int)rango.indexStart;
            if (d.indexFormat == IndexFormat.UInt16)
            {
                var ib = d.GetIndexData<ushort>();
                for (int i = 0; i < indices.Length; i++) indices[i] = ib[inicio + i] + sub.baseVertex;
            }
            else
            {
                var ib = d.GetIndexData<uint>();
                for (int i = 0; i < indices.Length; i++) indices[i] = (int)ib[inicio + i] + sub.baseVertex;
            }
        }
        Object.DestroyImmediate(copia);

        // Solo los vértices que usa ese nivel
        var mapa = new Dictionary<int, int>();
        var usados = new List<int>();
        for (int i = 0; i < indices.Length; i++)
        {
            if (!mapa.TryGetValue(indices[i], out int n)) { n = usados.Count; mapa[indices[i]] = n; usados.Add(indices[i]); }
            indices[i] = n;
        }

        var salida = new Mesh { name = fuente.name + " (" + indices.Length / 3 + " tris)" };
        if (usados.Count > 65000) salida.indexFormat = IndexFormat.UInt32;
        var v3 = new List<Vector3>();
        fuente.GetVertices(v3); salida.SetVertices(usados.ConvertAll(i => v3[i]));
        fuente.GetNormals(v3); if (v3.Count > 0) salida.SetNormals(usados.ConvertAll(i => v3[i]));
        var v4 = new List<Vector4>();
        fuente.GetTangents(v4); if (v4.Count > 0) salida.SetTangents(usados.ConvertAll(i => v4[i]));
        var v2 = new List<Vector2>();
        for (int canal = 0; canal < 4; canal++)
        {
            fuente.GetUVs(canal, v2);
            if (v2.Count > 0) salida.SetUVs(canal, usados.ConvertAll(i => v2[i]));
        }
        var colores = new List<Color>();
        fuente.GetColors(colores); if (colores.Count > 0) salida.SetColors(usados.ConvertAll(i => colores[i]));
        salida.SetTriangles(indices, 0);
        salida.RecalculateBounds();
        return salida;
    }

    static Mesh GuardarMalla(Mesh malla, string nombre)
    {
        string ruta = CarpetaMallas + "/" + nombre + ".asset";
        malla.name = nombre;
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente)
        {
            // Se conserva el GUID para no romper las referencias de la escena y los prefabs
            existente.Clear();
            EditorUtility.CopySerialized(malla, existente);
            Object.DestroyImmediate(malla);
            EditorUtility.SetDirty(existente);
            return existente;
        }
        AssetDatabase.CreateAsset(malla, ruta);
        return malla;
    }

    static void GuardarPrefab(string nombre, Mesh malla, Material[] materiales)
    {
        var go = new GameObject(nombre);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var rend = go.AddComponent<MeshRenderer>();
        rend.sharedMaterials = materiales;
        // Son muchas matas: sin sombras, como la vegetación del río
        rend.shadowCastingMode = ShadowCastingMode.Off;
        PrefabUtility.SaveAsPrefabAsset(go, CarpetaPrefabs + "/" + nombre + ".prefab");
        Object.DestroyImmediate(go);
    }

    static Mesh CargarMalla(string ruta, string nombre)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ruta))
            if (o is Mesh m && m.name == nombre) return m;
        throw new System.Exception("Huerto: no encuentro la malla " + nombre + " en " + ruta);
    }

    static T Cargar<T>(string ruta) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (!a) Debug.LogError("Huerto: no encuentro " + ruta);
        return a;
    }

    static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        int barra = ruta.LastIndexOf('/');
        CrearCarpeta(ruta.Substring(0, barra));
        AssetDatabase.CreateFolder(ruta.Substring(0, barra), ruta.Substring(barra + 1));
    }
}
