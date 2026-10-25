/// <summary>
/// Các chỉ số của Player. Giá trị số được dùng làm index mảng và được serialize nên KHÔNG đổi số
/// của chỉ số đã có; chỉ thêm chỉ số mới ở cuối.
/// </summary>
public enum StatType
{
    Damage = 0,           // Sát thương mỗi viên đạn (chưa tính chí mạng)
    AttackSpeed = 1,      // Số lần bắn mỗi giây
    AttackRange = 2,      // Tầm đánh (world units)
    ProjectileSpeed = 3,  // Tốc độ đạn (units/giây)
    CritChance = 4,       // Tỉ lệ chí mạng, 0..1 (0.1 = 10%)
    CritMultiplier = 5,   // Hệ số sát thương chí mạng (1.5 = gây 150% sát thương)
    MaxHealth = 6,        // Máu tối đa
    HealthRegen = 7,      // Máu hồi mỗi giây
    Armor = 8,            // Giáp: 100 giáp = giảm 50% sát thương nhận vào
    GoldMultiplier = 9,   // Hệ số vàng khi tiêu diệt quái (2 = gấp đôi)
    GoldBonusChance = 10  // Tỉ lệ nhận gấp đôi vàng từ một quái, 0..1
}
