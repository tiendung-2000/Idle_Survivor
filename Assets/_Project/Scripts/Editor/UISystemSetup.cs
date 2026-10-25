using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Công cụ dựng hệ thống UI trong Editor. Chạy lại nhiều lần vẫn an toàn, không tạo trùng.
///   Tools > UI System > 1. Setup Scene               Lớp Canvas + UIManager + registry
///   Tools > UI System > 2. Create Sample UI          Prefab MainMenuUI / InventoryUI + nút MENU trên HUD
///   Tools > UI System > Register Selected Prefab(s)  Đăng ký prefab đang chọn vào registry (lớp Popup)
/// </summary>
public static class UISystemSetup
{
    public const string RegistryPath = "Assets/_Project/Settings/UIPrefabRegistry.asset";
    public const string PrefabFolder = "Assets/_Project/Prefabs/UI";
    private static readonly string[] LayerNames = { "BackgroundLayer", "ContentLayer", "PopupLayer", "OverlayLayer" };

    // Cùng phong cách với HUD hiện có
    private static readonly Color Ink = new Color(0.13f, 0.12f, 0.18f);
    private static readonly Color BorderCol = new Color(0.62f, 0.63f, 0.78f);
    private static readonly Color FillCol = new Color(0.29f, 0.3f, 0.43f);
    private static readonly Color RowCol = new Color(0.37f, 0.39f, 0.55f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    private static readonly Color Green = new Color(0.3f, 0.72f, 0.3f);
    private static readonly Color Blue = new Color(0.22f, 0.45f, 0.86f);
    private static readonly Color Red = new Color(0.86f, 0.3f, 0.3f);
    private static readonly Vector2 C = new Vector2(0.5f, 0.5f), TC = new Vector2(0.5f, 1f), TL = new Vector2(0f, 1f), TR = new Vector2(1f, 1f);
    private static Font font;
    private static Sprite rounded;

    // ================================================================== Menu
    [MenuItem("Tools/UI System/1. Setup Scene", priority = 1)]
    public static UIManager SetupScene()
    {
        UIPrefabRegistry registry = GetOrCreateRegistry();
        Canvas canvas = FindOrCreateCanvas();
        EnsureEventSystem();
        EnsureRaycaster(canvas);

        var layers = new RectTransform[LayerNames.Length];
        for (int i = 0; i < LayerNames.Length; i++)
        {
            layers[i] = GetOrCreateLayer(canvas.transform, LayerNames[i]);
            layers[i].SetAsLastSibling();   // đúng thứ tự vẽ và nằm trên các phần HUD có sẵn
        }

        UIManager manager = Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            var go = new GameObject("UIManager");
            Undo.RegisterCreatedObjectUndo(go, "Create UIManager");
            manager = go.AddComponent<UIManager>();
        }

        Undo.RecordObject(manager, "Configure UIManager");
        manager.Configure(registry, layers[0], layers[1], layers[2], layers[3]);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Debug.Log($"UI System: UIManager dùng canvas '{canvas.name}' và registry '{registry.name}'.", manager);
        return manager;
    }

    [MenuItem("Tools/UI System/2. Create Sample UI", priority = 2)]
    public static void CreateSampleUI()
    {
        UIManager manager = SetupScene();
        LoadStyle();
        Directory.CreateDirectory(PrefabFolder);

        Register(manager.Registry, BuildMainMenuPrefab(), UILayer.Popup);
        Register(manager.Registry, BuildInventoryPrefab(), UILayer.Popup);
        CreateMenuButton(manager);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
    }

    [MenuItem("Tools/UI System/Register Selected Prefab(s)", priority = 20)]
    public static void RegisterSelected()
    {
        UIPrefabRegistry registry = GetOrCreateRegistry();
        foreach (GameObject go in Selection.gameObjects)
        {
            BaseUI ui = go.GetComponent<BaseUI>();
            if (ui == null || !PrefabUtility.IsPartOfPrefabAsset(go))
            {
                Debug.LogWarning($"UI System: '{go.name}' không phải prefab asset có component BaseUI.", go);
                continue;
            }
            Register(registry, ui, UILayer.Popup);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/UI System/Register Selected Prefab(s)", true)]
    private static bool ValidateRegisterSelected() =>
        Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<BaseUI>() != null;

    // ================================================================== Registry / scene
    public static UIPrefabRegistry GetOrCreateRegistry()
    {
        var registry = AssetDatabase.LoadAssetAtPath<UIPrefabRegistry>(RegistryPath);
        if (registry != null) return registry;

        string[] guids = AssetDatabase.FindAssets("t:UIPrefabRegistry");
        if (guids.Length > 0) return AssetDatabase.LoadAssetAtPath<UIPrefabRegistry>(AssetDatabase.GUIDToAssetPath(guids[0]));

        Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath));
        registry = ScriptableObject.CreateInstance<UIPrefabRegistry>();
        AssetDatabase.CreateAsset(registry, RegistryPath);
        AssetDatabase.SaveAssets();
        return registry;
    }

    public static bool Register(UIPrefabRegistry registry, BaseUI prefab, UILayer layer)
    {
        if (registry == null || prefab == null) return false;
        Undo.RecordObject(registry, "Register UI Prefab");
        bool added = registry.Register(prefab, layer);
        EditorUtility.SetDirty(registry);
        Debug.Log(added
            ? $"UI System: đã đăng ký {prefab.GetType().Name} vào lớp {layer}."
            : $"UI System: {prefab.GetType().Name} đã có trong registry.", prefab);
        return added;
    }

    private static Canvas FindOrCreateCanvas()
    {
        Canvas fallback = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas) continue;
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
            if (fallback == null) fallback = c;
        }
        if (fallback != null) return fallback;

        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // Canvas không có GraphicRaycaster thì không nút nào nhận được click/chạm.
    private static void EnsureRaycaster(Canvas canvas)
    {
        if (canvas.GetComponent<GraphicRaycaster>() != null) return;
        Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
        Debug.Log($"UI System: đã thêm GraphicRaycaster cho canvas '{canvas.name}'.", canvas);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        System.Type module = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (module != null) go.AddComponent(module);
        else go.AddComponent<StandaloneInputModule>();
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
    }

    private static RectTransform GetOrCreateLayer(Transform canvas, string layerName)
    {
        var existing = canvas.Find(layerName) as RectTransform;
        if (existing != null) return existing;

        var go = new GameObject(layerName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        Undo.RegisterCreatedObjectUndo(go, "Create UI Layer");
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        Stretch(rt, 0, 0, 0, 0);
        return rt;
    }

    private static void CreateMenuButton(UIManager manager)
    {
        Canvas canvas = FindOrCreateCanvas();
        Transform root = canvas.transform;
        if (root.Find("MenuButton") != null) return;

        RectTransform rt = NewUI("MenuButton", root);
        Place(rt, TR, TR, TR, new Vector2(-20, -210), new Vector2(150, 72));
        Image img = Img(rt, rounded, Blue, true, 1.4f);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        RectTransform label = NewUI("Label", rt); Stretch(label, 0, 0, 0, 0);
        Txt(label, "MENU", 30, Color.white, TextAnchor.MiddleCenter);
        rt.gameObject.AddComponent<MainMenuButton>();

        Transform background = root.Find(LayerNames[0]);
        if (background != null) rt.SetSiblingIndex(background.GetSiblingIndex());   // dưới các lớp UI quản lý
        Undo.RegisterCreatedObjectUndo(rt.gameObject, "Create Menu Button");
    }

    // ================================================================== Prefab mẫu
    private static MainMenuUI BuildMainMenuPrefab()
    {
        RectTransform root = NewRoot("MainMenuUI");
        var ui = root.gameObject.AddComponent<MainMenuUI>();
        root.gameObject.AddComponent<CanvasGroup>();
        Dim(root, ui);

        RectTransform window = NewUI("Window", root);
        Place(window, C, C, C, Vector2.zero, new Vector2(640, 620));
        RectTransform fill = Frame(window);

        RectTransform title = NewUI("Title", fill);
        Place(title, TC, TC, TC, new Vector2(0, -24), new Vector2(560, 70));
        Txt(title, "MENU", 52, Gold, TextAnchor.MiddleCenter);

        RectTransform best = NewUI("BestWave", fill);
        Place(best, TC, TC, TC, new Vector2(0, -104), new Vector2(560, 44));
        Text bestText = Txt(best, "Wave cao nhất: 0", 30, Color.white, TextAnchor.MiddleCenter);

        Button resume = Btn(fill, "ResumeButton", "TIẾP TỤC", Green, new Vector2(0, 10), new Vector2(460, 100), 36);
        Button stats = Btn(fill, "StatsButton", "CHỈ SỐ NHÂN VẬT", Blue, new Vector2(0, -120), new Vector2(460, 100), 34);
        Button close = CloseButton(fill);

        Set(ui, "resumeButton", resume);
        Set(ui, "statsButton", stats);
        Set(ui, "closeButton", close);
        Set(ui, "bestWaveText", bestText);
        Set(ui, "animatedContent", window);
        return SavePrefab<MainMenuUI>(root);
    }

    private static InventoryUI BuildInventoryPrefab()
    {
        RectTransform root = NewRoot("InventoryUI");
        var ui = root.gameObject.AddComponent<InventoryUI>();
        root.gameObject.AddComponent<CanvasGroup>();
        Dim(root, ui);

        RectTransform window = NewUI("Window", root);
        Place(window, C, C, C, Vector2.zero, new Vector2(860, 900));
        RectTransform fill = Frame(window);

        RectTransform title = NewUI("Title", fill);
        Place(title, TL, TL, TL, new Vector2(28, -20), new Vector2(640, 64));
        Text titleText = Txt(title, "KHO ĐỒ", 44, Gold, TextAnchor.MiddleLeft);
        Button close = CloseButton(fill);

        RectTransform scroll = NewUI("Scroll", fill);
        Stretch(scroll, 16, 16, 100, 16);
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = NewUI("Viewport", scroll);
        Stretch(viewport, 0, 0, 0, 0);
        Img(viewport, null, new Color(0, 0, 0, 0), true);   // vùng nhận thao tác kéo
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewUI("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8; layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport; scrollRect.content = content; scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped; scrollRect.scrollSensitivity = 30f;

        RectTransform row = NewUI("ItemTemplate", content);
        Img(row, rounded, RowCol, false, 1.5f);
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 76; element.minHeight = 76;
        RectTransform nameRt = NewUI("Name", row); Anchor(nameRt, 0f, 0.45f, 22, 0);
        RectTransform detailRt = NewUI("Detail", row); Anchor(detailRt, 0.45f, 0.68f, 0, 0);
        RectTransform valueRt = NewUI("Value", row); Anchor(valueRt, 0.68f, 1f, 0, 22);
        var view = row.gameObject.AddComponent<InventoryItemView>();
        Set(view, "nameText", Txt(nameRt, "Tên", 28, Color.white, TextAnchor.MiddleLeft));
        Set(view, "detailText", Txt(detailRt, "Cấp 0/10", 22, new Color(0.8f, 0.82f, 0.93f), TextAnchor.MiddleCenter, false));
        Set(view, "valueText", Txt(valueRt, "0", 28, new Color(0.62f, 0.92f, 1f), TextAnchor.MiddleRight));

        RectTransform empty = NewUI("EmptyText", fill);
        Place(empty, C, C, C, new Vector2(0, -40), new Vector2(600, 60));
        Text emptyText = Txt(empty, "Không có dữ liệu", 30, new Color(0.8f, 0.82f, 0.93f), TextAnchor.MiddleCenter, false);

        Set(ui, "titleText", titleText);
        Set(ui, "emptyText", emptyText);
        Set(ui, "listContent", content);
        Set(ui, "itemTemplate", view);
        Set(ui, "closeButton", close);
        Set(ui, "scrollRect", scrollRect);
        Set(ui, "animatedContent", window);
        return SavePrefab<InventoryUI>(root);
    }

    private static T SavePrefab<T>(RectTransform root) where T : BaseUI
    {
        string path = PrefabFolder + "/" + root.name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
        Object.DestroyImmediate(root.gameObject);
        Debug.Log("UI System: đã lưu prefab " + path, prefab);
        return prefab.GetComponent<T>();
    }

    // ================================================================== Dựng UI
    private static void LoadStyle()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/ui_rounded.png");
        if (rounded == null) rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static RectTransform NewRoot(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        Stretch(rt, 0, 0, 0, 0);
        return rt;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = C;
        rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
    }

    private static void Anchor(RectTransform rt, float minX, float maxX, float left, float right)
    {
        rt.anchorMin = new Vector2(minX, 0); rt.anchorMax = new Vector2(maxX, 1); rt.pivot = C;
        rt.offsetMin = new Vector2(left, 0); rt.offsetMax = new Vector2(-right, 0);
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast, float ppu = 1f)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast;
        if (sprite != null && sprite == rounded) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppu; }
        return img;
    }

    private static Text Txt(RectTransform rt, string text, int size, Color color, TextAnchor align, bool outline = true)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        if (outline)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = Ink; o.effectDistance = new Vector2(2.5f, -2.5f);
        }
        return t;
    }

    private static RectTransform Frame(RectTransform rt)
    {
        Img(rt, rounded, Ink, true);
        RectTransform border = NewUI("Border", rt); Stretch(border, 3, 3, 3, 3); Img(border, rounded, BorderCol, false);
        RectTransform fill = NewUI("Fill", border); Stretch(fill, 6, 6, 6, 6); Img(fill, rounded, FillCol, false);
        return fill;
    }

    private static Button Btn(RectTransform parent, string name, string label, Color color, Vector2 pos, Vector2 size, int fontSize)
    {
        RectTransform rt = NewUI(name, parent);
        Place(rt, C, C, C, pos, size);
        Image img = Img(rt, rounded, color, true, 1.3f);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        RectTransform text = NewUI("Label", rt); Stretch(text, 0, 0, 0, 0);
        Txt(text, label, fontSize, Color.white, TextAnchor.MiddleCenter);
        return button;
    }

    private static Button CloseButton(RectTransform parent)
    {
        RectTransform rt = NewUI("CloseButton", parent);
        Place(rt, TR, TR, TR, new Vector2(-16, -16), new Vector2(64, 64));
        Image img = Img(rt, rounded, Red, true, 1.6f);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        RectTransform text = NewUI("Label", rt); Stretch(text, 0, 0, 0, 0);
        Txt(text, "X", 32, Color.white, TextAnchor.MiddleCenter);
        return button;
    }

    // Nền tối toàn màn hình: chặn thao tác phía sau, bấm vào để đóng UI.
    private static void Dim(RectTransform root, BaseUI ui)
    {
        RectTransform dim = NewUI("Dim", root);
        Stretch(dim, 0, 0, 0, 0);
        Image img = Img(dim, null, new Color(0, 0, 0, 0.65f), true);
        var button = dim.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = img;
        UnityEventTools.AddPersistentListener(button.onClick, ui.CloseSelf);
    }

    private static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null) { Debug.LogError($"UI System: không tìm thấy trường '{property}' trên {target.GetType().Name}."); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
