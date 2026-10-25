using System;
using UnityEngine;

/// <summary>
/// Enemy di chuyển về phía target và gây sát thương qua Trigger với cooldown riêng.
/// Có thể kế thừa và override Move / TryAttack để mở rộng AI.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [SerializeField, Min(0f)] private float stopDistance = 0.6f;   // Dừng lại khi đủ gần target

    [Header("Attack")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0.01f)] private float attackCooldown = 1f;

    private Rigidbody2D body;
    private Transform target;
    private IDamageable targetDamageable;
    private Component targetDamageableComponent;   // Dùng để kiểm tra đối tượng còn sống
    private float nextAttackTime;
    private bool isDying;

    /// <summary>Được gọi đúng một lần khi Enemy bị hủy (chết, hết scene, ...).</summary>
    public event Action<EnemyController> Despawned;

    public Transform Target => target;
    public bool IsDying => isDying;
    public float Damage => damage;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        // Body kinematic đứng yên sẽ ngủ (sleep) và Unity ngừng gửi OnTriggerStay2D,
        // khiến Enemy chỉ đánh được một lần. NeverSleep đảm bảo cooldown hoạt động liên tục.
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;
    }

    private void Reset()
    {
        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }
        if (TryGetComponent(out Collider2D col)) col.isTrigger = true;
    }

    /// <summary>EnemyManager gọi hàm này ngay sau khi spawn để gán mục tiêu.</summary>
    public void Initialize(Transform newTarget)
    {
        target = newTarget;
        targetDamageable = null;
        targetDamageableComponent = null;

        if (newTarget != null && newTarget.TryGetComponent(out IDamageable found))
        {
            targetDamageable = found;
            targetDamageableComponent = found as Component;
        }
    }

    /// <summary>WaveManager gọi ngay sau khi spawn để nhân sát thương / kích thước theo wave.</summary>
    public void ApplyScaling(float damageMultiplier, float sizeMultiplier, float speedMultiplier = 1f)
    {
        damage *= Mathf.Max(0f, damageMultiplier);
        moveSpeed *= Mathf.Max(0f, speedMultiplier);
        if (!Mathf.Approximately(sizeMultiplier, 1f)) transform.localScale *= sizeMultiplier;
    }

    /// <summary>Được EnemyHealth gọi khi hết máu. Dừng di chuyển/tấn công ngay rồi hủy object.</summary>
    public void Die()
    {
        if (isDying) return;
        isDying = true;
        Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        Move();
    }

    protected virtual void Move()
    {
        if (isDying || target == null) return;

        Vector2 current = body.position;
        Vector2 targetPosition = target.position;

        if ((targetPosition - current).sqrMagnitude <= stopDistance * stopDistance) return;

        body.MovePosition(Vector2.MoveTowards(current, targetPosition, moveSpeed * Time.fixedDeltaTime));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryAttack(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryAttack(other);
    }

    /// <summary>
    /// Cooldown được lưu dưới dạng mốc thời gian nên không cần Update/coroutine,
    /// và Player rời vùng Trigger rồi quay lại cũng không thể bỏ qua cooldown.
    /// </summary>
    protected virtual void TryAttack(Collider2D other)
    {
        if (isDying || Time.time < nextAttackTime) return;
        if (target == null || targetDamageableComponent == null) return;
        if (other.transform != target && !other.transform.IsChildOf(target)) return;

        targetDamageable.TakeDamage(damage);
        nextAttackTime = Time.time + attackCooldown;
    }

    private void OnDestroy()
    {
        Action<EnemyController> handler = Despawned;
        Despawned = null;
        handler?.Invoke(this);
    }
}
