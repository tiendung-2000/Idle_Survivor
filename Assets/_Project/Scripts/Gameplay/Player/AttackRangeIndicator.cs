using UnityEngine;

/// <summary>
/// Vẽ vòng tròn nét đứt (lõi trắng, viền đen) thể hiện tầm đánh của Player.
/// Bán kính lấy từ PlayerStats (AttackRange) và tự vẽ lại khi chỉ số thay đổi (nâng cấp, buff),
/// không kiểm tra mỗi frame. Số nét được tính theo chu vi để mọi nét luôn dài bằng nhau.
/// Gắn trên một object con của Player cùng LineRenderer dùng material nét đứt (AttackRange.mat).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class AttackRangeIndicator : MonoBehaviour
{
    [Tooltip("Để trống = tự lấy PlayerStats ở object cha.")]
    [SerializeField] private PlayerStats stats;

    [Header("Hình dạng")]
    [Tooltip("Độ dày nét (đơn vị thế giới).")]
    [SerializeField, Min(0.01f)] private float lineWidth = 0.09f;
    [Tooltip("Chiều dài một nét cộng khoảng trống (đơn vị thế giới).")]
    [SerializeField, Min(0.05f)] private float dashLength = 0.45f;
    [Tooltip("Số điểm tạo nên vòng tròn. Càng nhiều càng tròn.")]
    [SerializeField, Range(16, 256)] private int segments = 128;

    [Header("Hiệu ứng")]
    [Tooltip("Xoay chậm cho vòng tròn trông sống động (độ/giây). 0 = đứng yên.")]
    [SerializeField] private float rotationSpeed = 8f;

    private LineRenderer line;
    private float builtRange = -1f;

    private void OnEnable()
    {
        Setup();
        if (stats != null) stats.StatChanged += HandleStatChanged;
        Rebuild(force: true);
    }

    private void OnDisable()
    {
        if (stats != null) stats.StatChanged -= HandleStatChanged;
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Setup();
        Rebuild(force: true);
    }

    private void Update()
    {
        if (Application.isPlaying && rotationSpeed != 0f) transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    private void Setup()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (stats == null) stats = GetComponentInParent<PlayerStats>();

        line.useWorldSpace = false;
        line.loop = true;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.TransformZ;
        line.numCapVertices = 0;
        line.widthMultiplier = lineWidth;
    }

    private void HandleStatChanged(StatType type, float value)
    {
        if (type == StatType.AttackRange) Rebuild(force: false);
    }

    /// <summary>Vẽ lại vòng tròn theo tầm đánh hiện tại.</summary>
    public void Rebuild(bool force)
    {
        if (line == null) return;

        float range = stats != null ? stats.GetValue(StatType.AttackRange) : 1f;
        if (!force && Mathf.Approximately(range, builtRange)) return;
        builtRange = range;

        // LineRenderer dùng toạ độ local: bù scale của object để bán kính đúng bằng tầm đánh thật.
        float scale = Mathf.Max(0.0001f, transform.lossyScale.x);
        float radius = range / scale;

        line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        // Một lần lặp texture = một nét + một khoảng trống. Số nguyên để chỗ nối vòng không bị lệch.
        int dashes = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * range / dashLength));
        line.textureScale = new Vector2(dashes, 1f);
    }
}
