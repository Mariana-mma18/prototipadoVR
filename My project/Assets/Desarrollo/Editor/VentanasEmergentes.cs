using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Cambia el aspecto de las ventanas emergentes por las imágenes de diseño de Assets/Desarrollo/VENTANAS EMERGENTES.
// Solo es visual: no toca el PokeInteractable, el InteractableUnityEventWrapper (lo que hace el botón), los
// Button ni sus onClick (BotonesConRayoControl usa los apagados para el rayo del control), ni los scripts.
//
// Las imágenes se usan desde VENTANAS EMERGENTES/Recortes: copias de FINALES sin el borde transparente
// (los originales miden 2363×2363 y el cartel ocupa ~30 %, memoria desperdiciada en Quest).
// "FONDO CARTEL VACIO" es un cartel de diseño con el texto borrado, para lo que diseño no mandó
// (el marco del mapa). Los textos que la imagen ya trae quedan apagados, no se borran.
//
// Cuando un panel cambia de tamaño también se escala la Surface del poke en la misma proporción, para que la
// zona que se puede tocar (con el dedo o con el rayo) siga siendo exactamente el panel que se ve.
// Se puede volver a ejecutar: cada paso deja el mismo resultado.
public static class VentanasEmergentes
{
    const string Carpeta = "Assets/Desarrollo/VENTANAS EMERGENTES/Recortes/";
    const string Prefab = "Assets/Desarrollo/Prefabs/MenuPalma.prefab";
    const string Muelles = "----- [ VR ] -----/Rio y Bote/Botones Poke (Subir - Bajar)/";

    // Café de los textos de diseño
    static readonly Color Cafe = new Color32(68, 38, 2, 255);

    // ---------- Menús ----------

    [MenuItem("Desarrollo/Interfaz/Aplicar diseño de ventanas (escena abierta)")]
    public static void AplicarEscenaAbierta()
    {
        int hechos = 0;

        var carteles = new (string poke, string cartel, string boton)[]
        {
            ("Poke Subir Muelle 1", "PRIMERA INTERACCIÓN - SELVA-06.png", "BOTÓN - SUBIR CANOA-12.png"),
            ("Poke Bajar Muelle 2", "LLEGADA CHAGRA 1-09.png", "BOTÓN BAJAR CANOA-08.png"),
            ("Poke Subir Muelle 2", "SUBIR CANOA - CHAGRA 2-10.png", "BOTÓN - SUBIR CANOA-12.png"),
            ("Poke Bajar Muelle 3", "LLEGAR - CHAGRA 2-13.png", "BOTÓN BAJAR CANOA-08.png"),
            ("Poke Subir Muelle 3", "SUBIR A LA CANOA-03.png", "BOTÓN - SUBIR CANOA-12.png"),
            ("Poke Bajar Muelle 4", "LLEGADA A LA MALOCA-02.png", "BOTÓN BAJAR CANOA-08.png"),
        };
        foreach (var c in carteles)
        {
            GameObject poke = GameObject.Find(Muelles + c.poke);
            if (!poke) continue;
            AplicarCartelConBoton(poke, Cargar(c.cartel), Cargar(c.boton));
            hechos++;
        }

        // Restaurante: el poke que cambia a la escena del río
        foreach (var wrapper in Object.FindObjectsByType<Oculus.Interaction.InteractableUnityEventWrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (wrapper.WhenSelect.GetPersistentEventCount() == 0) continue;
            if (wrapper.WhenSelect.GetPersistentMethodName(0) != "CargarEscena") continue;
            AplicarCartelConBoton(wrapper.transform.parent.gameObject, Cargar("INICIO_RESTAURANTE-01.png"), Cargar("BOTÓN - INICIAR RECORRIDO-04.png"));
            hechos++;
        }

        foreach (var mapa in Object.FindObjectsByType<MapaRecorrido>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { AplicarMapa(mapa); hechos++; }
        foreach (var lista in Object.FindObjectsByType<ListaIngredientes>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { AplicarIngredientes(lista); hechos++; }
        foreach (var olla in Object.FindObjectsByType<OllaSopa>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { AplicarOlla(olla); hechos++; }

        Debug.Log($"[VentanasEmergentes] {hechos} ventanas con diseño en la escena. El menú de la palma va aparte (prefab).");
    }

    [MenuItem("Desarrollo/Interfaz/Aplicar diseño al prefab MenuPalma")]
    public static void AplicarPrefabMenuPalma()
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            AplicarMenuPalma(raiz.GetComponent<MenuPalma>());
            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        Debug.Log("[VentanasEmergentes] MenuPalma.prefab con diseño");
    }

    // ---------- Carteles con botón (muelles y restaurante) ----------

    // Cartel de diseño arriba (con su texto) y el botón de diseño debajo.
    // 'poke' es la raíz del poke (o el building block de adentro).
    public static void AplicarCartelConBoton(GameObject poke, Sprite cartel, Sprite boton)
    {
        const float anchoPanel = 680f, anchoCartel = 600f, anchoBoton = 420f, margen = 5f, separacion = 10f;

        Canvas canvas = poke.GetComponentInChildren<Canvas>(true);
        RectTransform rtCanvas = (RectTransform)canvas.transform;
        RectTransform rtBoton = (RectTransform)rtCanvas.Find("Button");
        Transform textoOriginal = rtCanvas.Find("Text (TMP)");
        Transform botonVisual = canvas.transform.parent.Find("ButtonVisual");
        Registrar(poke);

        float altoCartel = anchoCartel * Proporcion(cartel);
        float altoBoton = anchoBoton * Proporcion(boton);
        float altoPanel = margen + altoCartel + separacion + altoBoton + margen;
        CambiarTamano(canvas, new Vector2(anchoPanel, altoPanel));

        Image imgCartel = BuscarOCrearImagen(rtCanvas, "Cartel diseño");
        imgCartel.transform.SetSiblingIndex(0);
        PonerImagen(imgCartel, cartel);
        imgCartel.rectTransform.sizeDelta = new Vector2(anchoCartel, altoCartel);
        imgCartel.rectTransform.anchoredPosition = new Vector2(0f, altoPanel / 2f - margen - altoCartel / 2f);
        if (textoOriginal) textoOriginal.gameObject.SetActive(false);

        // El Button (apagado a propósito) sigue igual; solo cambia su imagen. El texto ya viene en ella.
        PonerImagen(rtBoton.GetComponent<Image>(), boton);
        rtBoton.sizeDelta = new Vector2(anchoBoton, altoBoton);
        rtBoton.anchoredPosition = new Vector2(0f, -altoPanel / 2f + margen + altoBoton / 2f);
        foreach (Transform hijo in rtBoton) hijo.gameObject.SetActive(false);

        // La pieza 3D que se hunde al presionar (PokeInteractableVisual) va detrás del botón.
        // BajarDelBote usa esta pieza para dejar el botón al alcance de la mano.
        if (botonVisual)
        {
            Vector3 centroBoton = rtBoton.TransformPoint(rtBoton.rect.center);
            Vector3 local = botonVisual.parent.InverseTransformPoint(centroBoton);
            botonVisual.localPosition = new Vector3(botonVisual.localPosition.x, local.y, botonVisual.localPosition.z);
        }

        Terminar(poke, $"{poke.name}: '{cartel.name}' + '{boton.name}'");
    }

    // ---------- Menú de la palma (Mapa / Menú / X) ----------

    public static void AplicarMenuPalma(MenuPalma menu)
    {
        Canvas canvas = menu.panel.GetComponentInChildren<Canvas>(true);
        RectTransform rt = (RectTransform)canvas.transform;
        Registrar(menu.gameObject);

        // Base de diseño con dos huecos para los botones (medidos en la imagen de 1988×1175)
        Sprite fondo = Cargar("BASE - BOTÓN MENÚ-05.png");
        Vector2 tam = new Vector2(rt.sizeDelta.x, rt.sizeDelta.x * Proporcion(fondo));
        CambiarTamano(canvas, tam);
        PonerImagen(rt.Find("Fondo").GetComponent<Image>(), fondo, false);

        Vector2 imagen = fondo.rect.size;
        ColocarEnImagen((RectTransform)rt.Find("BotonMapa"), Cargar("BOTÓN - MAPA-16.png"), new Rect(142, 404, 732, 426), imagen, tam);
        ColocarEnImagen((RectTransform)rt.Find("BotonMenu"), Cargar("BOTÓN - MENÚ-17.png"), new Rect(1084, 404, 732, 426), imagen, tam);
        BotonX((RectTransform)rt.Find("BotonCerrar"), new Vector2(tam.x / 2f - 45f, tam.y / 2f - 45f), 70f);

        Terminar(menu.gameObject, "Menú de la palma");
    }

    // ---------- Mapa (diseño no mandó marco: se hace parecido con el cartel vacío) ----------

    public static void AplicarMapa(MapaRecorrido mapa)
    {
        Canvas canvas = mapa.GetComponentInChildren<Canvas>(true);
        RectTransform rt = (RectTransform)canvas.transform;
        Registrar(mapa.gameObject);

        Vector2 tam = new Vector2(620f, 500f);
        CambiarTamano(canvas, tam);

        Image fondo = rt.Find("Fondo").GetComponent<Image>();
        Sprite vacio = Cargar("FONDO CARTEL VACIO.png");
        PonerImagen(fondo, vacio, false);
        fondo.type = Image.Type.Sliced;            // bordes y huellas sin deformar
        fondo.pixelsPerUnitMultiplier = 2.5f;      // huellas al tamaño de los demás carteles

        var titulo = rt.Find("Titulo").GetComponent<TMP_Text>();
        titulo.color = Cafe;
        titulo.fontStyle = FontStyles.Bold;
        ((RectTransform)titulo.transform).anchoredPosition = new Vector2(0f, 212f);

        BotonX((RectTransform)rt.Find("BotonCerrar"), new Vector2(tam.x / 2f - 48f, tam.y / 2f - 38f), 52f);

        Terminar(mapa.gameObject, "Mapa");
    }

    // ---------- Lista de ingredientes (imagen de diseño + chulos) ----------

    public static void AplicarIngredientes(ListaIngredientes lista)
    {
        Canvas canvas = lista.panel.GetComponentInChildren<Canvas>(true);
        RectTransform rt = (RectTransform)canvas.transform;
        Registrar(lista.gameObject);

        Sprite imagen = Cargar("INGREDIENTES-07.png");
        Vector2 tam = new Vector2(rt.sizeDelta.x, rt.sizeDelta.x * Proporcion(imagen));
        CambiarTamano(canvas, tam);
        PonerImagen(rt.Find("Fondo").GetComponent<Image>(), imagen, false);

        // La imagen ya trae título y nombres: lo que dibuja ListaIngredientes queda oculto (sigue funcionando)
        if (lista.titulo) lista.titulo.gameObject.SetActive(false);
        lista.contenido.gameObject.SetActive(false);

        // El mensaje de progreso no tiene reemplazo: se queda, en café, en la parte baja vacía del cartel
        Vector2 px = imagen.rect.size;
        var progreso = (RectTransform)lista.textoProgreso.transform;
        progreso.anchoredPosition = EnCanvas(new Vector2(900f, 890f), px, tam);
        progreso.sizeDelta = new Vector2(tam.x * 0.78f, 34f);
        lista.textoProgreso.color = Cafe;
        lista.textoProgreso.alignment = TextAlignmentOptions.Center;

        // Chulo a la derecha de cada nombre (posiciones en la imagen de 1879×1014)
        var posiciones = new Dictionary<string, Vector2>
        {
            { "Maíz", new Vector2(400f, 575f) },
            { "Maní", new Vector2(400f, 740f) },
            { "Yuca", new Vector2(960f, 578f) },
            { "Sacha culantro", new Vector2(1265f, 745f) },
            // "Gallina Criolla" llega casi al borde derecho: el chulo va debajo del nombre
            { "Gallina criolla", new Vector2(1545f, 690f) },
        };
        Sprite chulo = Cargar("BOTÓN CHULO-11.png");
        RectTransform contenedor = BuscarOCrear(rt, "Chulos");
        contenedor.SetSiblingIndex(rt.Find("Fondo").GetSiblingIndex() + 1);
        contenedor.anchorMin = Vector2.zero; contenedor.anchorMax = Vector2.one;
        contenedor.sizeDelta = Vector2.zero; contenedor.anchoredPosition = Vector2.zero;

        var diseno = lista.GetComponent<ListaIngredientesDiseno>();
        if (!diseno) diseno = Undo.AddComponent<ListaIngredientesDiseno>(lista.gameObject);
        diseno.lista = lista;
        diseno.marcas.Clear();
        foreach (var ingrediente in lista.ingredientes)
        {
            if (!posiciones.TryGetValue(ingrediente.nombre, out Vector2 enImagen))
            {
                Debug.LogWarning($"[VentanasEmergentes] '{ingrediente.nombre}' no está en la imagen de diseño: no lleva chulo");
                continue;
            }
            Image img = BuscarOCrearImagen(contenedor, "Chulo " + ingrediente.nombre);
            PonerImagen(img, chulo);
            img.rectTransform.sizeDelta = new Vector2(30f, 30f);
            img.rectTransform.anchoredPosition = EnCanvas(enImagen, px, tam);
            img.gameObject.SetActive(false);
            diseno.marcas.Add(new ListaIngredientesDiseno.Marca { ingrediente = ingrediente.nombre, chulo = img.gameObject });
        }

        BotonX((RectTransform)rt.Find("BotonCerrar"), new Vector2(tam.x / 2f - 45f, tam.y / 2f - 45f), 70f);

        EditorUtility.SetDirty(diseno);
        Terminar(lista.gameObject, "Lista de ingredientes");
    }

    // ---------- Indicación de la olla (3 imágenes según la etapa) ----------

    public static void AplicarOlla(OllaSopa olla)
    {
        TMP_Text texto = olla.indicacion;
        if (!texto) return;
        Registrar(olla.gameObject);

        Sprite echar = Cargar("PONER EN OLLA-18.png");
        Sprite mezclar = Cargar("TOMAR CUCHARON-19.png");
        Sprite lista = Cargar("FIN-20.png");

        // El aviso gira hacia el jugador (OllaSopa); la olla está a ~1 m de la pared del fondo, así que se
        // corre un poco hacia el centro de la Maloca para que al girar no se meta en la pared.
        const float haciaElCentro = 0.6f, alturaSobreOlla = 0.85f;   // metros
        GameObject maloca = GameObject.Find("Maloca");
        Vector3 centro = olla.boca.position;
        if (maloca)
        {
            var renderers = maloca.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                centro = b.center;
            }
        }
        Vector3 direccion = Vector3.ProjectOnPlane(centro - olla.boca.position, Vector3.up);
        Vector3 desplazamiento = direccion.sqrMagnitude > 0.01f ? direccion.normalized * haciaElCentro : Vector3.zero;
        Undo.RecordObject(texto.transform, "Diseño ventana emergente");
        texto.transform.position = olla.boca.position + Vector3.up * alturaSobreOlla + desplazamiento;

        // Canvas propio dentro de Indicacion: OllaSopa sigue prendiéndolo y girándolo hacia el jugador
        const float ancho = 1.1f;                       // metros
        Vector2 px = echar.rect.size;
        float escala = ancho / px.x;
        float alto = ancho * Proporcion(echar);

        Transform t = texto.transform.Find("Imagen diseño");
        Canvas canvas;
        if (!t)
        {
            var go = new GameObject("Imagen diseño", typeof(RectTransform), typeof(Canvas));
            Undo.RegisterCreatedObjectUndo(go, "Diseño ventana emergente");
            go.layer = texto.gameObject.layer;
            go.transform.SetParent(texto.transform, false);
            t = go.transform;
        }
        canvas = t.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = -1;                       // el contador de OllaSopa se dibuja encima
        var rt = (RectTransform)t;
        rt.sizeDelta = px;
        rt.localScale = Vector3.one * escala;
        rt.localRotation = Quaternion.identity;
        // El texto de OllaSopa (contador) cae en la parte baja vacía del cartel
        rt.localPosition = new Vector3(0f, alto * 0.36f, 0.02f);

        Image img = BuscarOCrearImagen(rt, "Imagen");
        PonerImagen(img, echar, false);
        img.rectTransform.anchorMin = Vector2.zero; img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.sizeDelta = Vector2.zero; img.rectTransform.anchoredPosition = Vector2.zero;

        var diseno = t.GetComponent<IndicacionOllaDiseno>();
        if (!diseno) diseno = Undo.AddComponent<IndicacionOllaDiseno>(t.gameObject);
        diseno.olla = olla;
        diseno.imagen = img;
        diseno.echarIngredientes = echar;
        diseno.mezclar = mezclar;
        diseno.sopaLista = lista;

        // El contador queda en café y más pequeño, dentro del cartel
        texto.color = Cafe;
        texto.fontSize = 0.5f;
        texto.rectTransform.sizeDelta = new Vector2(ancho * 0.85f, 0.3f);

        EditorUtility.SetDirty(diseno);
        Terminar(olla.gameObject, "Indicación de la olla");
    }

    // ---------- Ayudas ----------

    public static Sprite Cargar(string archivo)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Carpeta + archivo);
        if (!sprite) throw new System.Exception("[VentanasEmergentes] No encontré el sprite " + Carpeta + archivo);
        return sprite;
    }

    static float Proporcion(Sprite s) => s.rect.height / s.rect.width;

    static void Registrar(GameObject go) => Undo.RegisterFullObjectHierarchyUndo(go, "Diseño ventana emergente");

    static void Terminar(GameObject go, string que)
    {
        EditorUtility.SetDirty(go);
        if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[VentanasEmergentes] " + que);
    }

    // Cambia el tamaño del canvas del poke y escala la Surface en la misma proporción (zona tocable = panel)
    static void CambiarTamano(Canvas canvas, Vector2 tamano)
    {
        var rt = (RectTransform)canvas.transform;
        Vector2 antes = rt.sizeDelta;
        rt.sizeDelta = tamano;

        Transform surface = canvas.transform.parent.parent.Find("Model/Surface");
        if (!surface) { Debug.LogWarning("[VentanasEmergentes] " + canvas.name + " no tiene Surface de poke"); return; }
        Vector3 s = surface.localScale;
        surface.localScale = new Vector3(s.x * tamano.x / antes.x, s.y * tamano.y / antes.y, s.z);
    }

    static void PonerImagen(Image img, Sprite sprite, bool mantenerProporcion = true)
    {
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = mantenerProporcion;
        img.color = Color.white;
    }

    // Píxel de la imagen de diseño (origen arriba-izquierda) → posición en un canvas que muestra la imagen completa
    static Vector2 EnCanvas(Vector2 pixel, Vector2 imagen, Vector2 canvas) =>
        new Vector2((pixel.x / imagen.x - 0.5f) * canvas.x, (0.5f - pixel.y / imagen.y) * canvas.y);

    static void ColocarEnImagen(RectTransform boton, Sprite sprite, Rect hueco, Vector2 imagen, Vector2 canvas)
    {
        PonerImagen(boton.GetComponent<Image>(), sprite);
        boton.anchoredPosition = EnCanvas(hueco.center, imagen, canvas);
        boton.sizeDelta = new Vector2(hueco.width / imagen.x * canvas.x, hueco.height / imagen.y * canvas.y);
        foreach (Transform hijo in boton) hijo.gameObject.SetActive(false);
    }

    static void BotonX(RectTransform boton, Vector2 posicion, float lado)
    {
        PonerImagen(boton.GetComponent<Image>(), Cargar("BOTÓN X-15.png"));
        boton.anchoredPosition = posicion;
        boton.sizeDelta = new Vector2(lado, lado);
        foreach (Transform hijo in boton) hijo.gameObject.SetActive(false);
    }

    static RectTransform BuscarOCrear(RectTransform padre, string nombre)
    {
        Transform t = padre.Find(nombre);
        if (!t)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Diseño ventana emergente");
            go.layer = padre.gameObject.layer;
            t = go.transform;
            t.SetParent(padre, false);
        }
        return (RectTransform)t;
    }

    static Image BuscarOCrearImagen(RectTransform padre, string nombre)
    {
        RectTransform rt = BuscarOCrear(padre, nombre);
        if (!rt.GetComponent<Image>()) Undo.AddComponent<Image>(rt.gameObject);
        Image img = rt.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }
}
