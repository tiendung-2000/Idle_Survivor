using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý tập trung vòng đời UI, mỗi kiểu UI có đúng một instance.
///   Open   Chưa tạo: Instantiate prefab ĐÚNG MỘT LẦN, gắn vào lớp đã đăng ký, gọi OnCreate.
///          Đã tạo: dùng lại instance trong cache và SetActive(true). Luôn gọi OnOpen(data) với data mới.
///   Close  Gọi OnClose rồi SetActive(false). KHÔNG Destroy; instance ở lại cache để mở lại.
///
/// Phạm vi theo scene: project chỉ có một scene và không dùng DontDestroyOnLoad, nên UIManager và các UI con
/// bị hủy cùng scene. Instance và cache được dọn trong OnDestroy để không giữ tham chiếu cũ.
/// Bộ nhớ: UI đã đóng vẫn giữ các asset nó tham chiếu. Với UI nặng ít dùng, gọi Release&lt;T&gt;() để hủy hẳn.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]   // sẵn sàng trước các script gọi Open trong Awake/Start của chúng
public class UIManager : MonoBehaviour
{
    [SerializeField] private UIPrefabRegistry registry;

    [Header("Lớp Canvas (theo UILayer)")]
    [SerializeField] private RectTransform backgroundLayer;
    [SerializeField] private RectTransform contentLayer;
    [SerializeField] private RectTransform popupLayer;
    [SerializeField] private RectTransform overlayLayer;

    public static UIManager Instance { get; private set; }

    private readonly Dictionary<Type, BaseUI> instances = new Dictionary<Type, BaseUI>();
    private readonly List<BaseUI> closeBuffer = new List<BaseUI>();   // dùng lại cho CloseAll, không cấp phát mỗi lần

    public UIPrefabRegistry Registry => registry;
    /// <summary>Số UI đang được quản lý (đã tạo và chưa bị hủy).</summary>
    public int CreatedCount => instances.Count;

    // Khi tắt Domain Reload trong Enter Play Mode Options, xóa Instance cũ từ phiên Play trước.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("UIManager: scene đã có một UIManager khác, gỡ bỏ bản trùng này.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        ValidateReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        instances.Clear();
    }

    /// <summary>Gán cấu hình bằng code (công cụ Editor, test).</summary>
    public void Configure(UIPrefabRegistry newRegistry, RectTransform background, RectTransform content, RectTransform popup, RectTransform overlay)
    {
        registry = newRegistry;
        backgroundLayer = background;
        contentLayer = content;
        popupLayer = popup;
        overlayLayer = overlay;
    }

    // ================================================================== API
    public T Open<T>(object data = null) where T : BaseUI => Open(typeof(T), data) as T;

    public BaseUI Open(Type uiType, object data = null)
    {
        BaseUI ui = GetOrCreate(uiType);
        if (ui != null) ui.Open(data);
        return ui;
    }

    /// <summary>Đóng UI. UI chưa từng được tạo hoặc đang đóng thì bỏ qua, không lỗi.</summary>
    public void Close<T>() where T : BaseUI => Close(typeof(T));

    public void Close(Type uiType)
    {
        if (TryGetLive(uiType, out BaseUI ui)) ui.Close(false);
    }

    /// <summary>Đang mở thì đóng; đang đóng hoặc chưa tạo thì mở.</summary>
    public T Toggle<T>(object data = null) where T : BaseUI
    {
        if (!IsOpen<T>()) return Open<T>(data);
        Close<T>();
        return Get<T>();
    }

    public bool IsOpen<T>() where T : BaseUI => IsOpen(typeof(T));
    public bool IsOpen(Type uiType) => TryGetLive(uiType, out BaseUI ui) && ui.IsOpen;

    /// <summary>Đã được tạo và vẫn được quản lý (kể cả khi đang ẩn).</summary>
    public bool IsCreated<T>() where T : BaseUI => TryGetLive(typeof(T), out _);

    /// <summary>Instance đã tạo, null nếu chưa tạo. Không tạo mới.</summary>
    public T Get<T>() where T : BaseUI => TryGetLive(typeof(T), out BaseUI ui) ? (T)ui : null;

    /// <summary>Đóng mọi UI đang mở. Không hủy instance, không xóa cache hay registry.</summary>
    public void CloseAll(bool immediate = false)
    {
        // Chép ra buffer trước: OnClose của một UI có thể mở UI khác và làm thay đổi Dictionary.
        closeBuffer.Clear();
        foreach (BaseUI ui in instances.Values)
            if (ui != null && ui.IsOpen) closeBuffer.Add(ui);

        for (int i = 0; i < closeBuffer.Count; i++) closeBuffer[i].Close(immediate);
        closeBuffer.Clear();
    }

    /// <summary>Hủy hẳn instance để giải phóng bộ nhớ (không dùng cho việc đóng thông thường).</summary>
    public void Release<T>() where T : BaseUI
    {
        if (!TryGetLive(typeof(T), out BaseUI ui)) return;

        ui.Close(true);
        instances.Remove(typeof(T));
        if (Application.isPlaying) Destroy(ui.gameObject);
        else DestroyImmediate(ui.gameObject);
    }

    // ================================================================== Nội bộ
    /// <summary>BaseUI gọi khi bị Destroy (từ bên ngoài hoặc khi scene unload) để cache không giữ tham chiếu chết.</summary>
    internal void Forget(BaseUI ui)
    {
        if (ui == null) return;
        Type type = ui.GetType();
        if (instances.TryGetValue(type, out BaseUI cached) && cached == ui) instances.Remove(type);
    }

    private bool TryGetLive(Type uiType, out BaseUI ui)
    {
        if (uiType != null && instances.TryGetValue(uiType, out ui))
        {
            if (ui != null) return true;
            instances.Remove(uiType);   // đã bị hủy ở nơi khác
        }
        ui = null;
        return false;
    }

    private BaseUI GetOrCreate(Type uiType)
    {
        if (TryGetLive(uiType, out BaseUI existing)) return existing;

        if (uiType == null || !typeof(BaseUI).IsAssignableFrom(uiType))
        {
            Debug.LogError($"UIManager: '{uiType?.Name ?? "null"}' không phải là BaseUI.", this);
            return null;
        }
        if (registry == null)
        {
            Debug.LogError($"UIManager: chưa gán UIPrefabRegistry, không thể mở {uiType.Name}.", this);
            return null;
        }
        if (!registry.TryGet(uiType, out UIPrefabRegistry.Entry entry))
        {
            Debug.LogError($"UIManager: chưa đăng ký prefab cho {uiType.Name} trong '{registry.name}'. " +
                           "Dùng Tools > UI System > Register Selected Prefab(s) hoặc thêm vào registry.", this);
            return null;
        }

        RectTransform parent = GetLayer(entry.layer);
        BaseUI ui = Instantiate(entry.prefab, parent, false);
        ui.name = entry.prefab.name;
        ui.gameObject.SetActive(false);   // ẩn đến khi Open() xong khởi tạo

        instances.Add(uiType, ui);
        ui.Create(this);
        return ui;
    }

    private RectTransform GetLayer(UILayer layer)
    {
        RectTransform target;
        switch (layer)
        {
            case UILayer.Background: target = backgroundLayer; break;
            case UILayer.Content: target = contentLayer; break;
            case UILayer.Overlay: target = overlayLayer; break;
            default: target = popupLayer; break;
        }
        if (target != null) return target;

        RectTransform fallback = popupLayer != null ? popupLayer : contentLayer != null ? contentLayer : overlayLayer;
        Debug.LogWarning($"UIManager: chưa gán lớp {layer}, dùng lớp thay thế '{(fallback != null ? fallback.name : name)}'.", this);
        return fallback != null ? fallback : (RectTransform)transform;
    }

    private void ValidateReferences()
    {
        if (registry == null) Debug.LogWarning("UIManager: chưa gán UIPrefabRegistry. Chạy Tools > UI System > 1. Setup Scene.", this);
        if (backgroundLayer == null || contentLayer == null || popupLayer == null || overlayLayer == null)
            Debug.LogWarning("UIManager: còn lớp Canvas chưa được gán. Chạy Tools > UI System > 1. Setup Scene.", this);
    }
}
