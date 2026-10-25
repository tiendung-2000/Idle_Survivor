/// <summary>
/// Đối tượng có thể nhận sát thương. Enemy chỉ phụ thuộc interface này, không phụ thuộc PlayerHealth.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}
