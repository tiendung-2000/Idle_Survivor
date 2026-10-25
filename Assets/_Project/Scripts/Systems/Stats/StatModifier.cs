/// <summary>
/// Cách một modifier tác động lên chỉ số.
/// Công thức: Giá trị = (Base + tổng Flat) x (1 + tổng PercentAdd) x tích (1 + PercentMult)
/// </summary>
public enum StatModifierMode
{
    Flat,         // Cộng thẳng: +5 sát thương
    PercentAdd,   // Cộng dồn theo %: 0.2 = +20% (các modifier loại này cộng với nhau rồi mới nhân)
    PercentMult   // Nhân riêng từng cái: 0.5 = x1.5 (các modifier loại này nhân chồng lên nhau)
}

/// <summary>
/// Một thay đổi chỉ số (từ nâng cấp, vật phẩm, buff...). Source dùng để gỡ toàn bộ modifier của cùng một nguồn.
/// </summary>
public sealed class StatModifier
{
    public float Value { get; }
    public StatModifierMode Mode { get; }
    public object Source { get; }

    public StatModifier(float value, StatModifierMode mode, object source = null)
    {
        Value = value;
        Mode = mode;
        Source = source;
    }
}
