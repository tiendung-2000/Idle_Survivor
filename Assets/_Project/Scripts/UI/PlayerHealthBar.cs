using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thanh máu HUD của Player. Chỉ lắng nghe event của PlayerHealth, không điều khiển ngược lại.
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private RectTransform fill;       // Thanh màu, co giãn theo tỷ lệ máu (qua anchorMax.x)
    [SerializeField] private Image fillImage;
    [SerializeField] private Text valueText;           // Tùy chọn: hiển thị "70 / 100"

    [Header("Colors")]
    [SerializeField] private Color fullColor = new Color(0.2f, 0.85f, 0.3f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.15f, 0.15f);

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void Start()
    {
        // Start chạy sau mọi Awake nên PlayerHealth chắc chắn đã khởi tạo máu.
        if (playerHealth != null) UpdateView(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        UpdateView(current, max);
    }

    private void UpdateView(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (fill != null) fill.anchorMax = new Vector2(ratio, 1f);
        if (fillImage != null) fillImage.color = Color.Lerp(lowColor, fullColor, ratio);
        if (valueText != null) valueText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
    }
}
