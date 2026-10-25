using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bảng chỉ số tổng trên HUD: sát thương, hồi máu, hệ số vàng và thanh máu.
/// Chỉ lắng nghe PlayerStats / PlayerHealth, không điều khiển ngược lại.
/// </summary>
public class PlayerStatsHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats stats;
    [SerializeField] private PlayerHealth health;

    [Header("Chỉ số")]
    [SerializeField] private Text damageText;
    [SerializeField] private Text regenText;
    [SerializeField] private Text goldMultiplierText;

    [Header("Thanh máu")]
    [SerializeField] private RectTransform hpFill;   // co giãn theo tỉ lệ máu (anchorMax.x)
    [SerializeField] private Image hpFillImage;
    [SerializeField] private Text hpText;
    [SerializeField] private Color hpFullColor = new Color(0.42f, 0.82f, 0.26f);
    [SerializeField] private Color hpLowColor = new Color(0.9f, 0.25f, 0.2f);

    private void OnEnable()
    {
        if (stats != null) stats.StatChanged += HandleStatChanged;
        if (health != null) health.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (stats != null) stats.StatChanged -= HandleStatChanged;
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
    }

    private void Start()
    {
        RefreshStats();
        if (health != null) HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
    }

    private void HandleStatChanged(StatType type, float value)
    {
        RefreshStats();
    }

    private void RefreshStats()
    {
        if (stats == null) return;
        if (damageText != null) damageText.text = NumberFormatter.Format(stats.GetValue(StatType.Damage));
        if (regenText != null) regenText.text = NumberFormatter.Format(stats.GetValue(StatType.HealthRegen)) + "/s";
        if (goldMultiplierText != null) goldMultiplierText.text = "x" + NumberFormatter.Format(stats.GetValue(StatType.GoldMultiplier));
    }

    private void HandleHealthChanged(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (hpFill != null) hpFill.anchorMax = new Vector2(ratio, 1f);
        if (hpFillImage != null) hpFillImage.color = Color.Lerp(hpLowColor, hpFullColor, ratio);
        if (hpText != null)
            hpText.text = "HP " + NumberFormatter.Format(Mathf.Ceil(current)) + "/" + NumberFormatter.Format(Mathf.Ceil(max));
    }
}
