using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Pone en la Maloka de TERRENO FINAL dos modelos de diseño que estaban en .blend (necesitan Blender
// instalado para generarse; las mallas resultantes quedan en Assets/Desarrollo/Modelos/Maloca):
//   - Pollo muerto: sobre una tabla junto a la olla, como en la escena de diseño (MALOCA-ELEMENTOS).
//     Es un objeto que se agarra igual que los ingredientes (copia de la estructura de Maní_Cubo:
//     Grabbable + HandGrab del ISDK + Rigidbody). Es la "Gallina criolla" de la receta (columna Maloca
//     de la imagen de ingredientes de diseño): está en ListaIngredientes con enLaMaloka, así que se echa
//     directo en la olla sin pasar por la canasta. Su encaje a la canasta queda apagado: el pollo es más
//     grande que la canasta.
//   - Cucharón: reemplaza solo la parte visible de "Cuchara (provisional)". El agarre, los colliders,
//     el Rigidbody y el punto "Punta" (el que OllaSopa usa para medir la mezcla) no cambian: el cazo
//     del cucharón queda centrado en "Punta" y el mango sobre el collider del mango.
// Volver a ejecutarlo actualiza lo mismo, sin duplicar.
public static class ElementosMaloca
{
    const string CarpetaMallas = "Assets/Desarrollo/Modelos/Maloca";

    const string BlendPollo = "Assets/_Main/Scenes/Terreno/MODELOS/pollo/pollo muerto.blend";
    const string MatPollo = "Assets/_Main/Scenes/Terreno/MODELOS/pollo/pollo.mat";
    const string MatTabla = "Assets/_Main/Scenes/Terreno/MODELOS/pollo/New Material.mat";
    const string BlendCucharon = "Assets/_Main/Scenes/Terreno/MODELOS/CUCHARON/CUCHARON_SOPA.blend";
    const string MatCucharon = "Assets/_Main/Scenes/Terreno/MODELOS/CUCHARON/New Material.mat";

    const string NombrePollo = "Pollo muerto";
    const string NombreTabla = "Tabla del pollo";

    // Dónde va la tabla con el pollo: dentro de la Maloka, al lado de la olla (en diseño queda a 1,7 m
    // de la olla; nuestra Maloka es más pequeña). Giro del pollo sobre la tabla.
    static readonly Vector2 SitioTabla = new Vector2(454.95f, 406.25f);
    const float GiroPollo = 160f;
    static readonly Vector3 TamanoTabla = new Vector3(0.68f, 0.05f, 0.68f); // como base_pollo_muerto de diseño

    const int TrisCucharon = 3000; // original 19.072

    // Piezas del pollo de diseño respecto a su base (centro abajo), en metros (el pollo mide ~0,46 m)
    static readonly (string malla, Vector3 p, Quaternion r, Vector3 s)[] PiezasPollo =
    {
        ("alita 1", new Vector3(-0.06668f, 0.14031f, -0.15959f), new Quaternion(0.51619f, -0.51165f, -0.48901f, -0.48231f), new Vector3(0.28764f, 0.28764f, 0.3383f)),
        ("alita 2", new Vector3(-0.0661f, 0.15847f, 0.16119f), new Quaternion(0.51619f, -0.51165f, -0.48901f, -0.48231f), new Vector3(0.28764f, 0.28764f, 0.3383f)),
        ("cuerpo", new Vector3(-0.01852f, 0.21454f, -0.00243f), new Quaternion(0.53476f, -0.49203f, -0.50875f, -0.46165f), new Vector3(0.28764f, 0.28798f, 0.33801f)),
        ("muslo 1", new Vector3(0.12317f, 0.19796f, 0.155f), new Quaternion(0.51619f, -0.51165f, -0.48901f, -0.48231f), new Vector3(0.07182f, 0.07182f, 0.08447f)),
        ("muslo 2", new Vector3(0.12259f, 0.18021f, -0.1586f), new Quaternion(0.51619f, -0.51165f, -0.48901f, -0.48231f), new Vector3(0.07182f, 0.07182f, 0.08447f)),
    };

    // Cucharón de diseño (malla "Sphere"): escala con la que diseño lo tiene junto a la olla, centro del
    // cazo y punta del mango en coordenadas de la malla. El cazo se abre hacia +X de la malla.
    static readonly Vector3 EscalaCucharon = new Vector3(0.1782f, 0.1909f, 0.1248f);
    static readonly Vector3 CentroCazo = new Vector3(0f, 0f, 0.96f);
    static readonly Vector3 PuntaMango = new Vector3(1.75f, 0f, 7.64f);

    [MenuItem("Desarrollo/Escenario/Maloka: pollo y cucharón de diseño")]
    public static void Aplicar()
    {
        HuertoChagras.CrearCarpeta(CarpetaMallas);
        Mesh pollo = GenerarPollo();
        Mesh cucharon = GenerarCucharon();
        AssetDatabase.SaveAssets();

        PonerPollo(pollo);
        PonerCucharon(cucharon);
        AnadirALaReceta();
    }

    const string NombreIngrediente = "Gallina criolla"; // igual a la clave de VentanasEmergentes (chulo)
    const string GrupoIngrediente = "Maloka";

    // Pone el pollo como quinto ingrediente: en la lista (y su chulo en la imagen de diseño) y en el
    // Canasto (solo para que no choquen). OllaSopa cuenta los ingredientes de la lista, así que pide los 5.
    [MenuItem("Desarrollo/Escenario/Maloka: gallina en la receta")]
    public static void AnadirALaReceta()
    {
        Transform cocina = HuertoChagras.BuscarRaiz("Cocina Maloca");
        Transform pollo = cocina ? cocina.Find(NombrePollo) : null;
        Transform cuerpo = pollo ? HuertoChagras.Cuerpo(pollo) : null;
        var lista = Object.FindFirstObjectByType<ListaIngredientes>(FindObjectsInactive.Include);
        var canasto = Object.FindFirstObjectByType<Canasto>(FindObjectsInactive.Include);
        if (!cuerpo || !lista || !canasto)
        {
            Debug.LogError("Maloka: falta el pollo (ejecuta antes 'Maloka: pollo y cucharón'), la ListaIngredientes o el Canasto.");
            return;
        }

        Undo.RecordObject(lista, "Gallina en la receta");
        var ingrediente = lista.ingredientes.Find(i => i.nombre == NombreIngrediente);
        if (ingrediente == null)
        {
            ingrediente = new ListaIngredientes.Ingrediente();
            lista.ingredientes.Add(ingrediente);
        }
        ingrediente.nombre = NombreIngrediente;
        ingrediente.grupo = GrupoIngrediente;
        ingrediente.objeto = cuerpo;
        ingrediente.enLaMaloka = true;
        EditorUtility.SetDirty(lista);

        if (System.Array.IndexOf(canasto.ingredientes, cuerpo) < 0)
        {
            Undo.RecordObject(canasto, "Gallina en la receta");
            ArrayUtility.Add(ref canasto.ingredientes, cuerpo);
            EditorUtility.SetDirty(canasto);
        }

        // Vuelve a armar la lista con la imagen de diseño para que la gallina tenga su chulo
        VentanasEmergentes.AplicarIngredientes(lista);

        EditorSceneManager.MarkSceneDirty(lista.gameObject.scene);
        Debug.Log($"Maloka: '{NombreIngrediente}' en la receta ({lista.ingredientes.Count} ingredientes).");
    }

    static Mesh GenerarPollo()
    {
        var partes = new List<HuertoChagras.Parte>();
        foreach (var p in PiezasPollo)
            partes.Add(new HuertoChagras.Parte(HuertoChagras.CargarMalla(BlendPollo, p.malla), Matrix4x4.TRS(p.p, p.r, p.s), 0));
        Mesh malla = HuertoChagras.Combinar(NombrePollo, partes, 1, false);

        // Centrado en el origen (como los cubos de los ingredientes), sin cambiar su tamaño
        Bounds b = malla.bounds;
        var v = new List<Vector3>();
        malla.GetVertices(v);
        for (int i = 0; i < v.Count; i++) v[i] -= b.center;
        malla.SetVertices(v);
        malla.RecalculateBounds();
        return HuertoChagras.GuardarMalla(malla, NombrePollo, CarpetaMallas);
    }

    static Mesh GenerarCucharon()
    {
        Mesh fuente = HuertoChagras.CargarMalla(BlendCucharon, "Sphere");
        Mesh reducida = HuertoChagras.Reducir(fuente, TrisCucharon);

        // El cazo queda en el origen (se pone en "Punta"), el mango hacia +Z y la boca del cazo hacia +Y,
        // igual que la pala y el mango de la cuchara provisional
        Vector3 mango = Vector3.Scale(EscalaCucharon, PuntaMango - CentroCazo);
        Vector3 arriba = Vector3.Scale(EscalaCucharon, Vector3.right);
        Quaternion alinear = Quaternion.Inverse(Quaternion.LookRotation(mango, arriba));
        Matrix4x4 m = Matrix4x4.Rotate(alinear) * Matrix4x4.Scale(EscalaCucharon) * Matrix4x4.Translate(-CentroCazo);

        var partes = new List<HuertoChagras.Parte> { new HuertoChagras.Parte(reducida, m, 0) };
        Mesh malla = HuertoChagras.Combinar("Cucharón", partes, 1, false);
        if (reducida != fuente) Object.DestroyImmediate(reducida);
        return HuertoChagras.GuardarMalla(malla, "Cucharón", CarpetaMallas);
    }

    static void PonerPollo(Mesh malla)
    {
        Transform cocina = HuertoChagras.BuscarRaiz("Cocina Maloca");
        Terrain terreno = Object.FindFirstObjectByType<Terrain>();
        if (!cocina || !terreno)
        {
            Debug.LogError("Maloka: abre TERRENO FINAL de Desarrollo (falta Cocina Maloca o Terrain).");
            return;
        }
        float suelo = HuertoChagras.Altura(terreno, SitioTabla);

        // Tabla (decorativa, con collider para que el pollo se apoye)
        Transform tabla = cocina.Find(NombreTabla);
        if (!tabla)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = NombreTabla;
            Undo.RegisterCreatedObjectUndo(go, "Pollo Maloka");
            tabla = go.transform;
            tabla.SetParent(cocina, true);
        }
        Undo.RecordObject(tabla, "Pollo Maloka");
        tabla.SetPositionAndRotation(new Vector3(SitioTabla.x, suelo + TamanoTabla.y * 0.5f, SitioTabla.y), Quaternion.Euler(0f, 70f, 0f));
        tabla.localScale = TamanoTabla;
        tabla.GetComponent<MeshRenderer>().sharedMaterial = HuertoChagras.Cargar<Material>(MatTabla);

        // Pollo: misma estructura de agarre que los ingredientes
        Transform pollo = cocina.Find(NombrePollo);
        if (!pollo)
        {
            Transform molde = BuscarEnEscena("Maní_Cubo");
            if (!molde)
            {
                Debug.LogError("Maloka: no encuentro Maní_Cubo para copiar su agarre.");
                return;
            }
            GameObject copia = Object.Instantiate(molde.gameObject, cocina);
            copia.name = NombrePollo;
            Undo.RegisterCreatedObjectUndo(copia, "Pollo Maloka");
            pollo = copia.transform;
        }

        HuertoChagras.PonerModelo(pollo, malla, new[] { HuertoChagras.Cargar<Material>(MatPollo) });

        // Mientras no esté en la receta, que no se encaje en la canasta
        foreach (var encaje in pollo.GetComponentsInChildren<Oculus.Interaction.SnapInteractor>(true))
        {
            Undo.RecordObject(encaje.gameObject, "Pollo Maloka");
            encaje.gameObject.SetActive(false);
        }

        Transform cuerpo = HuertoChagras.Cuerpo(pollo);
        Undo.RecordObject(cuerpo, "Pollo Maloka");
        float alto = malla.bounds.size.y;
        cuerpo.SetPositionAndRotation(new Vector3(SitioTabla.x, suelo + TamanoTabla.y + alto * 0.5f + 0.01f, SitioTabla.y), Quaternion.Euler(0f, GiroPollo, 0f));

        EditorSceneManager.MarkSceneDirty(cocina.gameObject.scene);
        Debug.Log("Maloka: pollo muerto puesto sobre su tabla junto a la olla.");
    }

    static void PonerCucharon(Mesh malla)
    {
        OllaSopa olla = Object.FindFirstObjectByType<OllaSopa>();
        if (!olla || !olla.cuchara || !olla.puntaCuchara)
        {
            Debug.LogError("Maloka: no encuentro la OllaSopa con su cuchara y su punta.");
            return;
        }
        Transform cuchara = olla.cuchara.transform;

        // Se esconden las piezas provisionales (sus colliders siguen igual)
        foreach (string nombre in new[] { "Mango", "Pala" })
        {
            Transform pieza = cuchara.Find(nombre);
            var rend = pieza ? pieza.GetComponent<MeshRenderer>() : null;
            if (!rend) continue;
            Undo.RecordObject(rend, "Cucharón");
            rend.enabled = false;
        }

        Transform modelo = cuchara.Find("Modelo");
        if (!modelo)
        {
            var go = new GameObject("Modelo");
            Undo.RegisterCreatedObjectUndo(go, "Cucharón");
            modelo = go.transform;
            modelo.SetParent(cuchara, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
        }
        Undo.RecordObject(modelo, "Cucharón");
        modelo.localPosition = cuchara.InverseTransformPoint(olla.puntaCuchara.position);
        modelo.localRotation = Quaternion.identity;
        modelo.localScale = Vector3.one;
        modelo.GetComponent<MeshFilter>().sharedMesh = malla;
        modelo.GetComponent<MeshRenderer>().sharedMaterial = HuertoChagras.Cargar<Material>(MatCucharon);

        EditorSceneManager.MarkSceneDirty(cuchara.gameObject.scene);
        Debug.Log("Maloka: cucharón de diseño en la cuchara (agarre y mezcla sin cambios).");
    }

    static Transform BuscarEnEscena(string nombre)
    {
        var escena = EditorSceneManager.GetActiveScene();
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
        return null;
    }
}
