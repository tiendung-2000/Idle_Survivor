using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum FireMode
{
    AutoNearestEnemy,   // Tự động bắn Enemy gần nhất
    ManualPointer       // Bắn theo vị trí chuột / điểm chạm
}

/// <summary>
/// Bắn đạn (prefab) từ Player. Mặc định tự nhắm Enemy gần nhất và bắn liên tục
/// cho đến khi Enemy bị tiêu diệt; có thể chuyển sang bắn theo chuột / chạm màn hình.
/// Mọi chỉ số chiến đấu (sát thương, tốc độ đánh, tầm đánh, chí mạng...) lấy từ PlayerStats.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerShooter : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private Transform firePoint;          // Để trống = bắn từ vị trí Player

    [Header("Fire Settings")]
    [SerializeField] private FireMode fireMode = FireMode.AutoNearestEnemy;

    [Header("Auto Target")]
    [SerializeField] private EnemyManager enemyManager;

    [Header("Manual Fire (Mouse / Touch)")]
    [SerializeField] private bool holdToFire = true;       // Giữ chuột/ngón tay để bắn liên tục
    [SerializeField] private bool ignoreUI = true;         // Không bắn khi bấm vào UI

    private Camera cam;
    private PlayerHealth health;
    private PlayerStats stats;
    private float nextFireTime;
    private bool warnedMissingPrefab;

    private void Awake()
    {
        cam = Camera.main;
        TryGetComponent(out health);

        stats = GetComponent<PlayerStats>();
        if (stats == null)
        {
            Debug.LogError("PlayerShooter: thiếu PlayerStats trên Player.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (Time.time < nextFireTime) return;
        if (health != null && health.IsDead) return;   // Player chết thì ngừng bắn

        Vector2 direction;
        bool hasDirection = fireMode == FireMode.AutoNearestEnemy
            ? TryGetAutoDirection(out direction)
            : TryGetManualDirection(out direction);
        if (!hasDirection) return;

        Fire(direction);
        nextFireTime = Time.time + 1f / stats.GetValue(StatType.AttackSpeed);   // AttackSpeed = số lần bắn mỗi giây
    }

    private Vector3 GetOrigin()
    {
        return firePoint != null ? firePoint.position : transform.position;
    }

    private void Fire(Vector2 direction)
    {
        if (bulletPrefab == null)
        {
            if (!warnedMissingPrefab)
            {
                warnedMissingPrefab = true;
                Debug.LogWarning("PlayerShooter: chưa gán Bullet Prefab!", this);
            }
            return;
        }

        // Tính sát thương và chí mạng cho từng viên đạn
        float damage = stats.GetValue(StatType.Damage);
        bool isCrit = Random.value < stats.GetValue(StatType.CritChance);
        if (isCrit) damage *= stats.GetValue(StatType.CritMultiplier);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Bullet bullet = Instantiate(bulletPrefab, GetOrigin(), Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, stats.GetValue(StatType.ProjectileSpeed), damage, isCrit, stats.GetValue(StatType.AttackRange));
    }

    /// <summary>Hướng tới Enemy gần nhất trong tầm đánh. Mỗi lần chọn lại nên tự chuyển sang Enemy kế tiếp khi Enemy cũ chết.</summary>
    private bool TryGetAutoDirection(out Vector2 direction)
    {
        direction = default;
        if (enemyManager == null) return false;

        Vector3 origin = GetOrigin();
        float range = stats.GetValue(StatType.AttackRange);
        if (!enemyManager.TryGetNearestEnemy(origin, range, out EnemyController enemy)) return false;

        Vector2 toEnemy = (Vector2)enemy.transform.position - (Vector2)origin;
        if (toEnemy.sqrMagnitude < 0.0001f) return false;

        direction = toEnemy.normalized;
        return true;
    }

    private bool TryGetManualDirection(out Vector2 direction)
    {
        direction = default;
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        if (!TryGetPointer(out Vector2 screenPos, out int fingerId)) return false;
        if (ignoreUI && IsPointerOverUI(fingerId)) return false;

        Vector3 origin = GetOrigin();
        Vector3 target = cam.ScreenToWorldPoint(screenPos);
        target.z = origin.z;

        Vector2 toTarget = target - origin;
        if (toTarget.sqrMagnitude < 0.0001f) return false;

        direction = toTarget.normalized;
        return true;
    }

    /// <summary>Lấy vị trí chuột hoặc chạm (hỗ trợ cả Input System mới và cũ).</summary>
    private bool TryGetPointer(out Vector2 screenPos, out int fingerId)
    {
        screenPos = default;
        fingerId = -1;

#if ENABLE_INPUT_SYSTEM
        Pointer pointer = Pointer.current;   // Chuột hoặc màn hình cảm ứng
        if (pointer == null) return false;

        bool pressed = holdToFire
            ? pointer.press.isPressed
            : pointer.press.wasPressedThisFrame;
        if (!pressed) return false;

        screenPos = pointer.position.ReadValue();
        return true;
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            bool touched = holdToFire
                ? touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled
                : touch.phase == TouchPhase.Began;
            if (!touched) return false;

            screenPos = touch.position;
            fingerId = touch.fingerId;
            return true;
        }

        bool clicked = holdToFire ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (!clicked) return false;

        screenPos = Input.mousePosition;
        return true;
#endif
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        return fingerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(fingerId)
            : EventSystem.current.IsPointerOverGameObject();
    }
}
