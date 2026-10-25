using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Một chỉ số: giá trị gốc + danh sách modifier, giá trị cuối được cache và kẹp trong [min, max].
/// </summary>
public class Stat
{
    private readonly List<StatModifier> modifiers = new List<StatModifier>();
    private readonly float min;
    private readonly float max;

    private float baseValue;
    private float cachedValue;
    private bool isDirty = true;

    public StatType Type { get; }

    /// <summary>Được gọi mỗi khi giá trị gốc hoặc danh sách modifier thay đổi.</summary>
    public event Action<Stat> Changed;

    public Stat(StatType type, float baseValue, float min, float max)
    {
        Type = type;
        this.min = min;
        this.max = max;
        this.baseValue = Mathf.Clamp(baseValue, min, max);
    }

    public float BaseValue
    {
        get => baseValue;
        set
        {
            float clamped = Mathf.Clamp(value, min, max);
            if (Mathf.Approximately(clamped, baseValue)) return;

            baseValue = clamped;
            MarkChanged();
        }
    }

    /// <summary>Giá trị cuối cùng sau khi áp dụng mọi modifier.</summary>
    public float Value
    {
        get
        {
            if (isDirty) Recalculate();
            return cachedValue;
        }
    }

    public IReadOnlyList<StatModifier> Modifiers => modifiers;

    public void AddModifier(StatModifier modifier)
    {
        if (modifier == null) return;

        modifiers.Add(modifier);
        MarkChanged();
    }

    public bool RemoveModifier(StatModifier modifier)
    {
        if (modifier == null || !modifiers.Remove(modifier)) return false;

        MarkChanged();
        return true;
    }

    /// <summary>Gỡ mọi modifier đến từ một nguồn (ví dụ một nâng cấp). Trả về số modifier đã gỡ.</summary>
    public int RemoveModifiersFromSource(object source)
    {
        int removed = modifiers.RemoveAll(m => m.Source == source);
        if (removed > 0) MarkChanged();
        return removed;
    }

    private void Recalculate()
    {
        float flat = 0f;
        float percentAdd = 0f;
        float percentMult = 1f;

        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier m = modifiers[i];
            switch (m.Mode)
            {
                case StatModifierMode.Flat: flat += m.Value; break;
                case StatModifierMode.PercentAdd: percentAdd += m.Value; break;
                case StatModifierMode.PercentMult: percentMult *= 1f + m.Value; break;
            }
        }

        cachedValue = Mathf.Clamp((baseValue + flat) * (1f + percentAdd) * percentMult, min, max);
        isDirty = false;
    }

    private void MarkChanged()
    {
        isDirty = true;
        Changed?.Invoke(this);
    }
}
