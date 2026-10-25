using UnityEngine;

/// <summary>
/// Viên đạn bay thẳng bằng Rigidbody2D, gây sát thương cho Enemy khi chạm và tự hủy.
/// Sát thương, tốc độ và tầm bay do PlayerShooter truyền vào lúc bắn (lấy từ PlayerStats).
/// Gắn script này lên Bullet Prefab (cần Rigidbody2D + Collider2D Trigger).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Hiển thị chí mạng")]
    [SerializeField] private Color critColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField, Min(1f)] private float critScale = 1.6f;

    private Rigidbody2D body;
    private float damage;
    private bool hasHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    /// <param name="maxDistance">Quãng đường tối đa; đạn tự hủy khi bay quá tầm đánh.</param>
    public void Launch(Vector2 direction, float speed, float damage, bool isCrit, float maxDistance)
    {
        this.damage = damage;
        if (isCrit) ApplyCritVisual();

        body.linearVelocity = direction * speed;
        Destroy(gameObject, (maxDistance + 1f) / Mathf.Max(0.1f, speed));
    }

    private void ApplyCritVisual()
    {
        transform.localScale *= critScale;
        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) sprite.color = critColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        // Chỉ gây sát thương cho Enemy (Player cũng là IDamageable nên không dùng interface ở đây).
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy == null || enemy.IsDead) return;

        hasHit = true;   // Tránh gây sát thương hai lần nếu chạm nhiều collider cùng bước vật lý
        enemy.TakeDamage(damage);
        Destroy(gameObject);
    }
}
