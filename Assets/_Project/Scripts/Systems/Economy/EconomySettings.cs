using System;
using UnityEngine;

/// <summary>
/// Cấu hình kinh tế và độ khó. Chỉnh asset này để game dễ hơn hoặc khó hơn
/// (có thể chỉnh ngay khi đang Play, UI nâng cấp tự cập nhật giá).
/// </summary>
[CreateAssetMenu(fileName = "EconomySettings", menuName = "Game/Economy Settings")]
public class EconomySettings : ScriptableObject
{
    [Tooltip("Số tiền nhận được mỗi khi tiêu diệt 1 quái.")]
    [SerializeField, Min(0)] private int coinsPerKill = 1;

    [Tooltip("Hệ số độ khó nhân vào giá nâng cấp.\n1 = bình thường, 0.5 = rẻ một nửa (dễ), 2 = đắt gấp đôi (khó).")]
    [SerializeField, Range(0.1f, 5f)] private float upgradeCostMultiplier = 1f;

    [Tooltip("Số tiền có sẵn khi bắt đầu game.")]
    [SerializeField, Min(0)] private int startingCoins = 0;

    /// <summary>Gọi khi giá trị trong asset bị chỉnh (Editor).</summary>
    public event Action Changed;

    public int CoinsPerKill => coinsPerKill;
    public float UpgradeCostMultiplier => upgradeCostMultiplier;
    public int StartingCoins => startingCoins;

    /// <summary>Áp hệ số độ khó lên giá gốc trong bảng nâng cấp. Giá tối thiểu 1; giữ nguyên -1 (đã max).</summary>
    public int ApplyCostMultiplier(int baseCost)
    {
        if (baseCost < 0) return baseCost;
        return Mathf.Max(1, Mathf.RoundToInt(baseCost * upgradeCostMultiplier));
    }

    private void OnValidate()
    {
        Changed?.Invoke();
    }
}
