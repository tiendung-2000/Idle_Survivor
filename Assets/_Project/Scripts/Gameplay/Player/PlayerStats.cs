using System;
using UnityEngine;

/// <summary>
/// Chỉ số runtime của Player: giá trị gốc lấy từ PlayerStatsData, cộng modifier từ nâng cấp.
/// Các hệ thống khác (bắn, máu...) chỉ đọc giá trị qua GetValue và lắng nghe StatChanged.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [SerializeField] private PlayerStatsData data;

#if UNITY_EDITOR
    [Serializable]
    private struct DebugEntry
    {
        public StatType stat;
        public float value;
    }

    [Header("Giá trị hiện tại (chỉ để quan sát khi chạy game)")]
    [SerializeField] private DebugEntry[] debugValues;
#endif

    private Stat[] stats;

    /// <summary>Tham số: (chỉ số, giá trị cuối mới).</summary>
    public event Action<StatType, float> StatChanged;

    private void Awake()
    {
        EnsureInitialized();
    }

    public Stat GetStat(StatType type)
    {
        EnsureInitialized();
        return stats[(int)type];
    }

    public float GetValue(StatType type)
    {
        return GetStat(type).Value;
    }

    public void AddModifier(StatType type, StatModifier modifier)
    {
        GetStat(type).AddModifier(modifier);
    }

    public bool RemoveModifier(StatType type, StatModifier modifier)
    {
        return GetStat(type).RemoveModifier(modifier);
    }

    /// <summary>Gỡ mọi modifier của một nguồn trên tất cả chỉ số.</summary>
    public void RemoveModifiersFromSource(object source)
    {
        EnsureInitialized();
        for (int i = 0; i < stats.Length; i++) stats[i].RemoveModifiersFromSource(source);
    }

    // Khởi tạo lười để thứ tự Awake giữa các component không gây lỗi (PlayerHealth đọc MaxHealth trong Awake).
    private void EnsureInitialized()
    {
        if (stats != null) return;

        PlayerStatsData source = data;
        bool createdFallback = false;
        if (source == null)
        {
            Debug.LogWarning("PlayerStats: chưa gán PlayerStatsData, dùng giá trị mặc định.", this);
            source = ScriptableObject.CreateInstance<PlayerStatsData>();
            createdFallback = true;
        }

        StatType[] types = (StatType[])Enum.GetValues(typeof(StatType));
        stats = new Stat[types.Length];

        for (int i = 0; i < types.Length; i++)
        {
            StatType type = types[i];
            GetLimits(type, out float min, out float max);

            var stat = new Stat(type, source.GetBaseValue(type), min, max);
            stat.Changed += HandleStatChanged;
            stats[(int)type] = stat;
        }

        if (createdFallback) Destroy(source);
        RefreshDebug();
    }

    private void HandleStatChanged(Stat stat)
    {
        RefreshDebug();
        StatChanged?.Invoke(stat.Type, stat.Value);
    }

    /// <summary>Giới hạn an toàn cho từng chỉ số, tránh nâng cấp làm game hỏng (chia cho 0, tỉ lệ > 100%...).</summary>
    public static void GetLimits(StatType type, out float min, out float max)
    {
        switch (type)
        {
            case StatType.AttackSpeed: min = 0.1f; max = 50f; break;
            case StatType.AttackRange: min = 0.5f; max = 100f; break;
            case StatType.ProjectileSpeed: min = 0.5f; max = 100f; break;
            case StatType.CritChance: min = 0f; max = 1f; break;
            case StatType.CritMultiplier: min = 1f; max = 20f; break;
            case StatType.MaxHealth: min = 1f; max = 1000000f; break;
            case StatType.HealthRegen: min = 0f; max = 1000000f; break;
            case StatType.Armor: min = 0f; max = 1000000f; break;
            case StatType.GoldMultiplier: min = 0.1f; max = 1000000f; break;
            case StatType.GoldBonusChance: min = 0f; max = 1f; break;
            default: min = 0f; max = 1000000f; break;   // Damage
        }
    }

    private void RefreshDebug()
    {
#if UNITY_EDITOR
        if (debugValues == null || debugValues.Length != stats.Length) debugValues = new DebugEntry[stats.Length];
        for (int i = 0; i < stats.Length; i++)
        {
            debugValues[i].stat = stats[i].Type;
            debugValues[i].value = stats[i].Value;
        }
#endif
    }
}
