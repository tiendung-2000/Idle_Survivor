using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Quản lý máu của Player. Máu tối đa lấy từ chỉ số MaxHealth của PlayerStats (nếu có).
/// Không phụ thuộc UI: UI chỉ cần đăng ký các event bên dưới.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Tooltip("Máu tối đa dùng khi Player không có PlayerStats. Nếu có PlayerStats thì chỉ số MaxHealth được ưu tiên.")]
    [FormerlySerializedAs("maxHealth")]
    [SerializeField, Min(1f)] private float fallbackMaxHealth = 100f;

    [Tooltip("Chỉ để quan sát khi chạy game. Giá trị được gán lại từ Max Health lúc khởi tạo.")]
    [SerializeField] private float currentHealth;

    private PlayerStats stats;
    private float lastMaxHealth;
    private bool isDead;

    /// <summary>Tham số: (máu hiện tại, máu tối đa).</summary>
    public event Action<float, float> OnHealthChanged;
    public event Action OnPlayerDied;

    public float MaxHealth => stats != null ? stats.GetValue(StatType.MaxHealth) : fallbackMaxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        TryGetComponent(out stats);
        lastMaxHealth = MaxHealth;
        currentHealth = lastMaxHealth;
    }

    private void OnEnable()
    {
        if (stats != null) stats.StatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        if (stats != null) stats.StatChanged -= HandleStatChanged;
    }

    /// <summary>Hằng số giáp: sát thương nhận vào x ArmorConstant / (ArmorConstant + giáp).</summary>
    public const float ArmorConstant = 100f;

    private void Update()
    {
        if (isDead || stats == null) return;

        float regen = stats.GetValue(StatType.HealthRegen);
        if (regen > 0f && currentHealth < MaxHealth) Heal(regen * Time.deltaTime);
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f) return;
        if (stats != null) damage *= ArmorConstant / (ArmorConstant + stats.GetValue(StatType.Armor));
        SetHealth(currentHealth - damage);
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        SetHealth(currentHealth + amount);
    }

    private void SetHealth(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, MaxHealth);
        if (Mathf.Approximately(clamped, currentHealth)) return;

        currentHealth = clamped;
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);

        if (currentHealth <= 0f) Die();
    }

    /// <summary>Máu tối đa tăng thì cộng phần chênh lệch vào máu hiện tại; giảm thì kẹp lại. Luôn báo UI cập nhật tỷ lệ.</summary>
    private void HandleStatChanged(StatType type, float value)
    {
        if (type != StatType.MaxHealth || isDead) return;

        float increase = Mathf.Max(0f, value - lastMaxHealth);
        lastMaxHealth = value;
        currentHealth = Mathf.Clamp(currentHealth + increase, 1f, value);
        OnHealthChanged?.Invoke(currentHealth, value);
    }

    private void Die()
    {
        if (isDead) return;   // Đảm bảo chỉ xử lý chết một lần
        isDead = true;
        OnPlayerDied?.Invoke();
    }
}
