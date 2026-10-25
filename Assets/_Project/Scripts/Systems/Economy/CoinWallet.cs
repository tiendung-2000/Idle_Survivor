using System;
using UnityEngine;

/// <summary>
/// Ví tiền của người chơi. Mỗi quái bị tiêu diệt (EnemyHealth.AnyEnemyDied) cho
/// coinsPerKill x Hệ số vàng tiền, có tỉ lệ nhân đôi. Phần lẻ được cộng dồn sang lần sau.
/// </summary>
public class CoinWallet : MonoBehaviour
{
    [SerializeField] private EconomySettings settings;

    [Tooltip("Lấy Hệ số vàng và Tỉ lệ vàng x2 từ đây. Để trống = không có thưởng thêm.")]
    [SerializeField] private PlayerStats playerStats;

    [Tooltip("Chỉ để quan sát khi chạy game.")]
    [SerializeField] private int coins;

    private double pendingFraction;

    /// <summary>Tham số: (số tiền hiện tại, lượng thay đổi: dương = nhận, âm = tiêu).</summary>
    public event Action<int, int> Changed;

    public int Coins => coins;
    public EconomySettings Settings => settings;

    private void Awake()
    {
        coins = settings != null ? settings.StartingCoins : 0;
    }

    private void OnEnable()
    {
        EnemyHealth.AnyEnemyDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        EnemyHealth.AnyEnemyDied -= HandleEnemyDied;
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        double reward = (settings != null ? settings.CoinsPerKill : 1) * (enemy != null ? enemy.CoinReward : 1f);
        if (playerStats != null)
        {
            reward *= playerStats.GetValue(StatType.GoldMultiplier);
            if (UnityEngine.Random.value < playerStats.GetValue(StatType.GoldBonusChance)) reward *= 2d;
        }
        AddReward(reward);
    }

    /// <summary>Cộng tiền có phần lẻ (ví dụ 1.3): phần nguyên cộng ngay, phần lẻ để dành.</summary>
    public void AddReward(double amount)
    {
        if (amount <= 0d) return;
        pendingFraction += amount;
        int whole = (int)Math.Floor(pendingFraction);
        pendingFraction -= whole;
        Add(whole);
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        Changed?.Invoke(coins, amount);
    }

    /// <summary>Đặt số tiền trực tiếp (dùng khi nạp dữ liệu lưu).</summary>
    public void SetCoins(int amount)
    {
        amount = Mathf.Max(0, amount);
        int delta = amount - coins;
        coins = amount;
        Changed?.Invoke(coins, delta);
    }

    public bool CanAfford(int amount) => amount >= 0 && coins >= amount;

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount)) return false;
        coins -= amount;
        Changed?.Invoke(coins, -amount);
        return true;
    }
}
