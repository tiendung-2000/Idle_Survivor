using System;
using System.Globalization;

/// <summary>
/// Rút gọn số lớn kiểu game idle: 950 -> "950", 4670 -> "4.67K", 92020000000 -> "92.02B".
/// Sau T là aa, ab, ac... Luôn dùng dấu chấm thập phân, không phụ thuộc ngôn ngữ máy.
/// </summary>
public static class NumberFormatter
{
    private static readonly string[] Suffixes = { "", "K", "M", "B", "T" };
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Format(double value)
    {
        if (double.IsNaN(value)) return "0";
        if (double.IsInfinity(value)) return value > 0 ? "∞" : "-∞";

        double abs = Math.Abs(value);
        if (abs < 1000d)
        {
            string pattern = abs < 10d ? "0.##" : abs < 100d ? "0.#" : "0";
            return value.ToString(pattern, Invariant);
        }

        int tier = (int)Math.Floor(Math.Log10(abs) / 3d);
        double scaled = value / Math.Pow(1000d, tier);
        if (Math.Abs(scaled) >= 999.995d)   // tránh hiển thị "1000.00K" do làm tròn
        {
            scaled /= 1000d;
            tier++;
        }
        return scaled.ToString("0.00", Invariant) + GetSuffix(tier);
    }

    private static string GetSuffix(int tier)
    {
        if (tier < Suffixes.Length) return Suffixes[tier];

        int n = tier - Suffixes.Length;
        int first = Math.Min(25, n / 26);
        return new string(new[] { (char)('a' + first), (char)('a' + n % 26) });
    }
}
