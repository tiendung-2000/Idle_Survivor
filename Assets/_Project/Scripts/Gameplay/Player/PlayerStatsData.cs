using UnityEngine;

/// <summary>
/// Giá trị GỐC của các chỉ số Player. Chỉnh trong asset này để cân bằng game;
/// nâng cấp trong lúc chơi sẽ cộng thêm bằng modifier chứ không sửa asset.
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Game/Player Stats")]
public class PlayerStatsData : ScriptableObject
{
    [Header("Tấn công")]
    [Tooltip("Sát thương mỗi viên đạn (chưa tính chí mạng).")]
    [SerializeField, Min(0f)] private float damage = 10f;

    [Tooltip("Tốc độ đánh: số lần bắn mỗi giây. 5 = bắn mỗi 0.2 giây.")]
    [SerializeField, Min(0.1f)] private float attackSpeed = 5f;

    [Tooltip("Tầm đánh (world units). Chỉ nhắm và bắn Enemy trong tầm này; đạn cũng biến mất khi bay quá tầm.")]
    [SerializeField, Min(0.5f)] private float attackRange = 10f;

    [Tooltip("Tốc độ bay của đạn (units/giây).")]
    [SerializeField, Min(0.5f)] private float projectileSpeed = 12f;

    [Header("Chí mạng")]
    [Tooltip("Tỉ lệ chí mạng từ 0 đến 1. 0.1 = 10%.")]
    [SerializeField, Range(0f, 1f)] private float critChance = 0.1f;

    [Tooltip("Hệ số sát thương khi chí mạng. 1.5 = gây 150% sát thương thường.")]
    [SerializeField, Min(1f)] private float critMultiplier = 1.5f;

    [Header("Sinh tồn")]
    [Tooltip("Máu tối đa của Player.")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Tooltip("Máu hồi mỗi giây.")]
    [SerializeField, Min(0f)] private float healthRegen = 0f;

    [Tooltip("Giáp. Sát thương nhận vào x 100 / (100 + giáp): 100 giáp giảm 50%, 300 giáp giảm 75%.")]
    [SerializeField, Min(0f)] private float armor = 0f;

    [Header("Tài chính")]
    [Tooltip("Hệ số vàng khi tiêu diệt quái. 1 = bình thường, 2 = gấp đôi.")]
    [SerializeField, Min(0.1f)] private float goldMultiplier = 1f;

    [Tooltip("Tỉ lệ một quái rơi gấp đôi vàng, 0..1.")]
    [SerializeField, Range(0f, 1f)] private float goldBonusChance = 0f;

    public float GetBaseValue(StatType type)
    {
        switch (type)
        {
            case StatType.Damage: return damage;
            case StatType.AttackSpeed: return attackSpeed;
            case StatType.AttackRange: return attackRange;
            case StatType.ProjectileSpeed: return projectileSpeed;
            case StatType.CritChance: return critChance;
            case StatType.CritMultiplier: return critMultiplier;
            case StatType.MaxHealth: return maxHealth;
            case StatType.HealthRegen: return healthRegen;
            case StatType.Armor: return armor;
            case StatType.GoldMultiplier: return goldMultiplier;
            case StatType.GoldBonusChance: return goldBonusChance;
            default: return 0f;
        }
    }
}
