using System.Collections;
using UnityEngine;

/// <summary>
/// Lớp gốc cho mọi UI do UIManager quản lý. Vòng đời (tách biệt với Awake/OnEnable/OnDisable của Unity):
///   OnCreate()      Đúng 1 lần sau khi instantiate. Đặt khởi tạo tốn kém và đăng ký sự kiện nút ở đây
///                   (không đặt ở OnOpen, nếu không listener sẽ bị nhân đôi mỗi lần mở lại).
///   OnOpen(data)    Mỗi lần mở, kể cả Open khi UI đang mở (để làm mới dữ liệu). Luôn nhận data MỚI của
///                   lần mở đó, null nếu không truyền, nên UI phải hiển thị theo data chứ không dựa vào data cũ.
///   OnClose()       Mỗi lần đóng, trước khi GameObject bị tắt (trước cả hiệu ứng đóng).
///
/// Bị script khác tác động trực tiếp:
///   SetActive(false) từ bên ngoài (hoặc object cha bị tắt) → coi như đã đóng: OnClose chạy, IsOpen = false.
///   SetActive(true)  từ bên ngoài → coi như được mở không kèm dữ liệu: OnOpen(null) chạy, IsOpen = true.
///   Destroy từ bên ngoài → UIManager bỏ instance khỏi cache; lần Open sau tạo lại từ prefab.
/// Lớp con override OnEnable / OnDisable / OnDestroy phải gọi base.
/// </summary>
[DisallowMultipleComponent]
public abstract class BaseUI : MonoBehaviour
{
    [Header("Hiệu ứng mở / đóng (tùy chọn)")]
    [Tooltip("Mờ dần và thu phóng nhẹ khi mở/đóng. Tự thêm CanvasGroup nếu thiếu. Chạy bằng thời gian thực nên vẫn hoạt động khi game tạm dừng.")]
    [SerializeField] private bool animate = true;
    [SerializeField, Min(0f)] private float animationDuration = 0.15f;
    [Tooltip("Phần được thu phóng khi mở/đóng, thường là khung cửa sổ. Để trống = chỉ mờ dần.")]
    [SerializeField] private RectTransform animatedContent;

    private UIManager manager;
    private CanvasGroup canvasGroup;
    private Coroutine transition;
    private bool isOpen;
    private bool isClosing;
    private bool changingActiveByManager;

    /// <summary>Đang mở. Trả về false khi đang chạy hiệu ứng đóng.</summary>
    public bool IsOpen => isOpen && !isClosing;
    public bool IsClosing => isClosing;
    protected UIManager Manager => manager;

    // ------------------------------------------------------------------ Lifecycle hooks
    protected virtual void OnCreate() { }
    protected virtual void OnOpen(object data) { }
    protected virtual void OnClose() { }

    /// <summary>Đóng chính UI này qua UIManager (gắn vào nút X, nền tối...).</summary>
    public void CloseSelf()
    {
        if (manager != null) manager.Close(GetType());
    }

    // ------------------------------------------------------------------ Gọi bởi UIManager
    internal void Create(UIManager owner)
    {
        manager = owner;
        canvasGroup = GetComponent<CanvasGroup>();
        if (animate && canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        OnCreate();
    }

    internal void Open(object data)
    {
        bool wasVisible = IsOpen;
        if (isClosing)   // mở lại giữa lúc đang chạy hiệu ứng đóng: hủy hiệu ứng, giữ nguyên instance
        {
            StopTransition();
            isClosing = false;
        }

        isOpen = true;
        if (!gameObject.activeSelf) SetActiveByManager(true);
        transform.SetAsLastSibling();   // nằm trên các UI khác cùng lớp

        OnOpen(data);
        if (!wasVisible) PlayTransition(true);
    }

    internal void Close(bool immediate)
    {
        if (!isOpen || isClosing) return;   // đóng lặp lại hoặc đang đóng: bỏ qua, không chạy hiệu ứng trùng

        OnClose();
        if (immediate || !CanAnimate)
        {
            FinishClose();
            return;
        }

        isClosing = true;
        PlayTransition(false);   // FinishClose() chạy khi hiệu ứng kết thúc
    }

    private void FinishClose()
    {
        StopTransition();
        isClosing = false;
        isOpen = false;
        if (gameObject.activeSelf) SetActiveByManager(false);   // KHÔNG Destroy: giữ lại để dùng lại
        ResetVisual();
    }

    private void SetActiveByManager(bool active)
    {
        changingActiveByManager = true;
        try { gameObject.SetActive(active); }
        finally { changingActiveByManager = false; }
    }

    // ------------------------------------------------------------------ Đồng bộ khi bị tác động từ bên ngoài
    protected virtual void OnEnable()
    {
        if (changingActiveByManager || manager == null || isOpen) return;
        isOpen = true;
        isClosing = false;
        OnOpen(null);
    }

    protected virtual void OnDisable()
    {
        if (changingActiveByManager || manager == null || !isOpen) return;

        bool closeAlreadyRan = isClosing;
        transition = null;   // Unity tự dừng coroutine khi object bị tắt
        isClosing = false;
        isOpen = false;
        if (!closeAlreadyRan) OnClose();
        ResetVisual();
    }

    protected virtual void OnDestroy()
    {
        if (manager != null) manager.Forget(this);
    }

    // ------------------------------------------------------------------ Hiệu ứng
    private bool CanAnimate =>
        animate && animationDuration > 0f && canvasGroup != null && Application.isPlaying && isActiveAndEnabled;

    private void PlayTransition(bool opening)
    {
        StopTransition();
        if (!CanAnimate)
        {
            if (opening) ResetVisual();
            else FinishClose();
            return;
        }

        if (opening && canvasGroup.alpha >= 1f) ApplyVisual(0f);   // mở mới: bắt đầu từ trong suốt
        transition = StartCoroutine(Animate(opening ? 1f : 0f));
    }

    private IEnumerator Animate(float target)
    {
        canvasGroup.blocksRaycasts = target > 0f;   // không nhận click trong lúc đóng
        float from = canvasGroup.alpha;
        float duration = animationDuration * Mathf.Abs(target - from);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            ApplyVisual(Mathf.Lerp(from, target, t / duration));
            yield return null;
        }

        ApplyVisual(target);
        transition = null;
        if (target <= 0f) FinishClose();
    }

    private void ApplyVisual(float value)
    {
        canvasGroup.alpha = value;
        if (animatedContent != null) animatedContent.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, value);
    }

    private void ResetVisual()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        if (animatedContent != null) animatedContent.localScale = Vector3.one;
    }

    private void StopTransition()
    {
        if (transition == null) return;
        StopCoroutine(transition);
        transition = null;
    }
}

/// <summary>
/// UI nhận dữ liệu có kiểu. Mỗi lần mở đều gọi OnOpenWithData với dữ liệu của lần mở đó;
/// mở không truyền dữ liệu thì nhận default(TData), không bao giờ dùng lại dữ liệu cũ.
/// </summary>
public abstract class BaseUI<TData> : BaseUI
{
    protected abstract void OnOpenWithData(TData data);

    protected sealed override void OnOpen(object data)
    {
        if (data == null)
        {
            OnOpenWithData(default);
        }
        else if (data is TData typed)
        {
            OnOpenWithData(typed);
        }
        else
        {
            Debug.LogWarning($"{GetType().Name}: dữ liệu kiểu {data.GetType().Name} không khớp {typeof(TData).Name}, dùng giá trị mặc định.", this);
            OnOpenWithData(default);
        }
    }
}
