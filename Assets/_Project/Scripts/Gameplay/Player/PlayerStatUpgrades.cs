using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Một dòng dữ liệu lưu game: chỉ số và cấp đã nâng.</summary>
[Serializable]
public struct StatLevelSave
{
    public StatType stat;
    public int level;

    public StatLevelSave(StatType stat, int level)
    {
        this.stat = stat;
        this.level = level;
    }
}

/// <summary>
/// Quản lý cấp nâng cấp chỉ số của Player. Mỗi cấp thêm một StatModifier (Source = component này)
/// vào PlayerStats, nên PlayerShooter / PlayerHealth tự nhận giá trị mới qua StatChanged.
/// Không giữ tiền: truyền số tiền hiện có vào TryUpgrade, hàm sẽ trừ nếu nâng thành công.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerStatUpgrades : MonoBehaviour
{
    [SerializeField] private StatUpgradeData data;

    [Tooltip("Hệ số độ khó áp lên giá nâng cấp. Để trống = dùng giá gốc trong StatUpgradeData.")]
    [SerializeField] private EconomySettings economy;

    [Tooltip("Cấp khởi đầu, tiện để thử cân bằng trong Editor. Dữ liệu lưu game nên nạp bằng ImportLevels.")]
    [SerializeField] private List<StatLevelSave> startingLevels = new List<StatLevelSave>();

    private PlayerStats stats;
    private List<StatModifier>[] levelModifiers;   // index = (int)StatType, mỗi phần tử = 1 cấp

    /// <summary>Tham số: (chỉ số, cấp mới).</summary>
    public event Action<StatType, int> LevelChanged;

    public StatUpgradeData Data => data;
    public EconomySettings Economy => economy;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (levelModifiers != null) return;

        stats = GetComponent<PlayerStats>();
        int count = Enum.GetValues(typeof(StatType)).Length;
        levelModifiers = new List<StatModifier>[count];
        for (int i = 0; i < count; i++) levelModifiers[i] = new List<StatModifier>();

        if (data == null) Debug.LogWarning("PlayerStatUpgrades: chưa gán StatUpgradeData, không thể nâng cấp.", this);

        foreach (StatLevelSave s in startingLevels) SetLevel(s.stat, s.level);
    }

    // ---------------- Truy vấn ----------------

    public StatUpgradeConfig GetConfig(StatType stat) => data != null ? data.Get(stat) : null;

    public int GetLevel(StatType stat)
    {
        EnsureInitialized();
        return levelModifiers[(int)stat].Count;
    }

    public int GetMaxLevel(StatType stat)
    {
        StatUpgradeConfig c = GetConfig(stat);
        return c != null ? c.maxLevel : 0;
    }

    public bool IsMaxLevel(StatType stat) => GetLevel(stat) >= GetMaxLevel(stat);

    /// <summary>Giá nâng cấp tiếp theo (đã nhân hệ số độ khó), -1 nếu đã max hoặc chỉ số không nâng được.</summary>
    public int GetUpgradeCost(StatType stat)
    {
        StatUpgradeConfig c = GetConfig(stat);
        if (c == null) return -1;

        int baseCost = c.GetCost(GetLevel(stat));
        return economy != null ? economy.ApplyCostMultiplier(baseCost) : baseCost;
    }

    public bool CanUpgrade(StatType stat, int currency)
    {
        int cost = GetUpgradeCost(stat);
        return cost >= 0 && currency >= cost;
    }

    /// <summary>Giá trị cuối của chỉ số nếu nâng thêm 1 cấp (để UI hiện "10 -> 12").</summary>
    public float PreviewNextLevelValue(StatType stat)
    {
        EnsureInitialized();
        Stat s = stats.GetStat(stat);
        StatUpgradeConfig c = GetConfig(stat);
        if (c == null || IsMaxLevel(stat)) return s.Value;

        float flat = 0f, percentAdd = 0f, percentMult = 1f;
        Accumulate(c.ModifierMode, c.valuePerLevel, ref flat, ref percentAdd, ref percentMult);
        foreach (StatModifier m in s.Modifiers) Accumulate(m.Mode, m.Value, ref flat, ref percentAdd, ref percentMult);

        PlayerStats.GetLimits(stat, out float min, out float max);
        return Mathf.Clamp((s.BaseValue + flat) * (1f + percentAdd) * percentMult, min, max);
    }

    private static void Accumulate(StatModifierMode mode, float value, ref float flat, ref float percentAdd, ref float percentMult)
    {
        switch (mode)
        {
            case StatModifierMode.Flat: flat += value; break;
            case StatModifierMode.PercentAdd: percentAdd += value; break;
            case StatModifierMode.PercentMult: percentMult *= 1f + value; break;
        }
    }

    // ---------------- Nâng cấp ----------------

    /// <summary>Nâng 1 cấp nếu đủ tiền; trừ tiền và trả về true khi thành công.</summary>
    public bool TryUpgrade(StatType stat, ref int currency)
    {
        if (!CanUpgrade(stat, currency)) return false;

        currency -= GetUpgradeCost(stat);
        AddLevel(stat);
        LevelChanged?.Invoke(stat, GetLevel(stat));
        return true;
    }

    /// <summary>Nâng 1 cấp, trả tiền từ ví. Trả về true khi thành công.</summary>
    public bool TryUpgrade(StatType stat, CoinWallet wallet)
    {
        if (wallet == null) return false;

        int cost = GetUpgradeCost(stat);
        if (cost < 0 || !wallet.TrySpend(cost)) return false;

        AddLevel(stat);
        LevelChanged?.Invoke(stat, GetLevel(stat));
        return true;
    }

    /// <summary>Đặt cấp trực tiếp (nạp save, cheat, test). Không tốn tiền.</summary>
    public void SetLevel(StatType stat, int level)
    {
        EnsureInitialized();
        level = Mathf.Clamp(level, 0, GetMaxLevel(stat));
        if (level == GetLevel(stat)) return;

        while (GetLevel(stat) < level) AddLevel(stat);
        while (GetLevel(stat) > level) RemoveLevel(stat);

        LevelChanged?.Invoke(stat, level);
    }

    public void ResetAllLevels()
    {
        foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType))) SetLevel(stat, 0);
    }

    // Mỗi cấp là 1 modifier riêng: cộng dồn đúng công thức và chỉ bắn 1 sự kiện StatChanged mỗi cấp
    // (PlayerHealth cộng phần máu tối đa tăng thêm vào máu hiện tại).
    private void AddLevel(StatType stat)
    {
        StatUpgradeConfig c = GetConfig(stat);
        if (c == null) return;

        var modifier = new StatModifier(c.valuePerLevel, c.ModifierMode, this);
        levelModifiers[(int)stat].Add(modifier);
        stats.AddModifier(stat, modifier);
    }

    private void RemoveLevel(StatType stat)
    {
        List<StatModifier> list = levelModifiers[(int)stat];
        if (list.Count == 0) return;

        StatModifier last = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
        stats.RemoveModifier(stat, last);
    }

    // ---------------- Lưu / tải ----------------

    public List<StatLevelSave> ExportLevels()
    {
        EnsureInitialized();
        var result = new List<StatLevelSave>();
        foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
        {
            int level = GetLevel(stat);
            if (level > 0) result.Add(new StatLevelSave(stat, level));
        }
        return result;
    }

    public void ImportLevels(IEnumerable<StatLevelSave> saved)
    {
        ResetAllLevels();
        if (saved == null) return;
        foreach (StatLevelSave s in saved) SetLevel(s.stat, s.level);
    }
}
