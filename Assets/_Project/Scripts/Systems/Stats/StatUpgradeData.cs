using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cách một chỉ số tăng theo cấp. Mỗi cấp thêm 1 StatModifier vào chỉ số tương ứng.</summary>
public enum StatGrowthType
{
    Flat,           // Mỗi cấp cộng thẳng. 2 = +2 sát thương mỗi cấp
    PercentOfBase,  // Mỗi cấp cộng % (cộng dồn). 0.05 = +5%/cấp, cấp 10 = +50%
    Compound        // Mỗi cấp nhân %. 0.05 = x1.05/cấp, cấp 10 = x1.63
}

/// <summary>Nhóm nâng cấp, tương ứng một ô (tab) trên thanh nâng cấp.</summary>
public enum StatCategory
{
    Attack = 0,   // Tấn công
    Defense = 1,  // Phòng thủ
    Economy = 2   // Tài chính
}

/// <summary>Cấu hình nâng cấp cho MỘT chỉ số.</summary>
[Serializable]
public class StatUpgradeConfig
{
    public StatType stat;
    public StatCategory category = StatCategory.Attack;
    public string displayName;

    [Header("Hiển thị")]
    [Tooltip("Hiển thị dạng % (0.1 -> 10%).")]
    public bool showAsPercent;
    [Tooltip("Chữ đứng trước giá trị, ví dụ \"x\" cho hệ số vàng.")]
    public string valuePrefix;
    [Tooltip("Chữ đứng sau giá trị, ví dụ \"/s\" cho hồi máu.")]
    public string valueSuffix;

    [Header("Tăng trưởng")]
    public StatGrowthType growthType = StatGrowthType.Flat;

    [Tooltip("Lượng tăng mỗi cấp. Flat: số tuyệt đối. PercentOfBase / Compound: 0.05 = 5%.")]
    public float valuePerLevel = 1f;

    [Min(0)] public int maxLevel = 10;

    [Header("Giá nâng cấp = baseCost x costMultiplier ^ cấp hiện tại")]
    [Min(0)] public int baseCost = 100;
    [Min(1f)] public float costMultiplier = 1.15f;

    /// <summary>Giá để nâng từ currentLevel lên currentLevel + 1. Trả về -1 nếu đã max.</summary>
    public int GetCost(int currentLevel)
    {
        if (currentLevel >= maxLevel) return -1;
        return Mathf.RoundToInt(baseCost * Mathf.Pow(costMultiplier, currentLevel));
    }

    /// <summary>Tổng tiền để nâng từ cấp 0 lên max (dùng khi cân bằng kinh tế).</summary>
    public long GetTotalCostToMax()
    {
        long total = 0;
        for (int i = 0; i < maxLevel; i++) total += GetCost(i);
        return total;
    }

    /// <summary>Kiểu modifier mà mỗi cấp tạo ra.</summary>
    public StatModifierMode ModifierMode
    {
        get
        {
            switch (growthType)
            {
                case StatGrowthType.PercentOfBase: return StatModifierMode.PercentAdd;
                case StatGrowthType.Compound: return StatModifierMode.PercentMult;
                default: return StatModifierMode.Flat;
            }
        }
    }

    public string Format(float value)
    {
        string number = showAsPercent ? NumberFormatter.Format(value * 100f) + "%" : NumberFormatter.Format(value);
        return valuePrefix + number + valueSuffix;
    }
}

/// <summary>
/// Bảng cân bằng nâng cấp chỉ số. Giá trị GỐC vẫn nằm trong PlayerStatsData;
/// asset này quy định mỗi cấp tăng bao nhiêu, tối đa bao nhiêu cấp, giá mỗi cấp và chỉ số thuộc ô nâng cấp nào.
/// </summary>
[CreateAssetMenu(fileName = "PlayerStatUpgrades", menuName = "Game/Player Stat Upgrades")]
public class StatUpgradeData : ScriptableObject
{
    [SerializeField] private List<StatUpgradeConfig> upgrades = new List<StatUpgradeConfig>();

    public IReadOnlyList<StatUpgradeConfig> Upgrades => upgrades;

    public StatUpgradeConfig Get(StatType stat)
    {
        for (int i = 0; i < upgrades.Count; i++)
            if (upgrades[i].stat == stat) return upgrades[i];
        return null;
    }

    // Unity gọi khi tạo asset mới hoặc bấm Reset trong Inspector.
    private void Reset()
    {
        upgrades = CreateDefaults();
    }

    [ContextMenu("Reset To Default Values")]
    private void ResetToDefaults()
    {
        upgrades = CreateDefaults();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Log Balance Table")]
    private void LogBalanceTable()
    {
        foreach (StatUpgradeConfig c in upgrades)
        {
            Debug.Log("[" + c.category + "] " + c.displayName + ": " + c.growthType + " " + c.valuePerLevel + "/cấp, tối đa " + c.maxLevel +
                      " cấp | giá cấp 1 = " + c.GetCost(0) + ", cấp cuối = " + c.GetCost(c.maxLevel - 1) +
                      ", tổng = " + c.GetTotalCostToMax().ToString("N0"));
        }
    }

    /// <summary>Giá trị khởi điểm. Giá tính bằng tiền (mặc định 1 tiền / quái), trước khi nhân hệ số độ khó.</summary>
    public static List<StatUpgradeConfig> CreateDefaults()
    {
        return new List<StatUpgradeConfig>
        {
            // Cân bằng bằng mô phỏng (Tools > Balance > Simulate Run). Sát thương và Máu tối đa tăng dạng nhân (Compound)
            // và không giới hạn thực tế: là trục sức mạnh dài hạn. Các nâng cấp phụ có giới hạn để tránh bùng nổ giữa game.
            //   nhóm                  chỉ số                     tên hiển thị            %      trước sau    kiểu tăng                    /cấp    max  giá  x giá
            Make(StatCategory.Attack,  StatType.Damage,          "Sát thương",          false, "",  "",   StatGrowthType.Compound,      0.07f,  200,  3, 1.10f),
            Make(StatCategory.Attack,  StatType.AttackSpeed,     "Tốc độ đánh",         false, "",  "/s", StatGrowthType.PercentOfBase, 0.04f,   25, 10, 1.18f),
            Make(StatCategory.Attack,  StatType.AttackRange,     "Tầm đánh",            false, "",  "",   StatGrowthType.Flat,          0.3f,    10, 12, 1.25f),
            Make(StatCategory.Attack,  StatType.ProjectileSpeed, "Tốc độ đạn",          false, "",  "",   StatGrowthType.PercentOfBase, 0.08f,   10,  6, 1.25f),
            Make(StatCategory.Attack,  StatType.CritChance,      "Tỉ lệ chí mạng",      true,  "",  "",   StatGrowthType.Flat,          0.015f,  20, 15, 1.20f),
            Make(StatCategory.Attack,  StatType.CritMultiplier,  "Sát thương chí mạng", true,  "",  "",   StatGrowthType.Flat,          0.1f,    25, 15, 1.17f),

            Make(StatCategory.Defense, StatType.MaxHealth,       "Máu tối đa",          false, "",  "",   StatGrowthType.Compound,      0.07f,  200,  3, 1.10f),
            Make(StatCategory.Defense, StatType.HealthRegen,     "Hồi máu",             false, "",  "/s", StatGrowthType.Compound,      0.07f,  170,  8, 1.11f),
            Make(StatCategory.Defense, StatType.Armor,           "Giáp",                false, "",  "",   StatGrowthType.Flat,          4f,      50, 10, 1.15f),

            Make(StatCategory.Economy, StatType.GoldMultiplier,  "Hệ số vàng",          false, "x", "",   StatGrowthType.Flat,          0.05f,   30, 15, 1.17f),
            Make(StatCategory.Economy, StatType.GoldBonusChance, "Tỉ lệ vàng x2",       true,  "",  "",   StatGrowthType.Flat,          0.02f,   25, 20, 1.18f),
        };
    }

    private static StatUpgradeConfig Make(StatCategory category, StatType stat, string name, bool percent,
        string prefix, string suffix, StatGrowthType growth, float perLevel, int maxLevel, int baseCost, float costMultiplier)
    {
        return new StatUpgradeConfig
        {
            category = category, stat = stat, displayName = name, showAsPercent = percent,
            valuePrefix = prefix, valueSuffix = suffix, growthType = growth,
            valuePerLevel = perLevel, maxLevel = maxLevel, baseCost = baseCost, costMultiplier = costMultiplier
        };
    }
}
