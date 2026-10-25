using System;
using UnityEngine;

/// <summary>
/// Máu của Enemy. Hết máu thì Enemy bị hủy thông qua EnemyController.Die().
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maxHealth = 30f;

    private float currentHealth;
    private bool isDead;

    /// <summary>Được gọi đúng một lần khi Enemy hết máu (trước khi bị hủy).</summary>
    public event Action<EnemyHealth> Died;

    /// <summary>Được gọi khi BẤT KỲ Enemy nào hết máu (tiền thưởng, thống kê...).</summary>
    public static event Action<EnemyHealth> AnyEnemyDied;

    // Xóa đăng ký cũ khi tắt Domain Reload trong Enter Play Mode Options.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyEnemyDied = null;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    /// <summary>Hệ số tiền thưởng của quái này (WaveManager đặt theo wave; boss cao hơn).</summary>
    public float CoinReward { get; private set; } = 1f;

    /// <summary>Nhân máu tối đa và tiền thưởng theo wave. Gọi ngay sau khi spawn.</summary>
    public void ApplyScaling(float healthMultiplier, float rewardMultiplier)
    {
        maxHealth = Mathf.Max(1f, maxHealth * healthMultiplier);
        currentHealth = maxHealth;
        CoinReward = Mathf.Max(0f, rewardMultiplier);
    }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (currentHealth > 0f) return;

        isDead = true;
        Died?.Invoke(this);
        AnyEnemyDied?.Invoke(this);

        if (TryGetComponent(out EnemyController controller)) controller.Die();
        else Destroy(gameObject);
    }
}
