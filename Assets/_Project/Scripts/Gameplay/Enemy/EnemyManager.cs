using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Flags]
public enum SpawnSide
{
    None = 0,
    Top = 1 << 0,
    Bottom = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    All = Top | Bottom | Left | Right
}

/// <summary>
/// Sinh Enemy ngoài ranh giới màn hình theo chu kỳ, giới hạn số lượng tối đa.
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyController enemyPrefab;
    [SerializeField] private Transform player;
    [SerializeField] private Camera spawnCamera;   // Để trống = Camera.main

    [Header("Spawn Settings")]
    [SerializeField] private bool autoSpawn = true;
    [SerializeField, Min(0.05f)] private float spawnInterval = 1.5f;
    [SerializeField, Min(0)] private int maxEnemies = 20;
    [SerializeField, Min(0f)] private float spawnDistance = 1.5f;   // Khoảng cách ngoài mép màn hình (world units)
    [SerializeField] private SpawnSide spawnDirection = SpawnSide.All;

    private static readonly SpawnSide[] Sides =
    {
        SpawnSide.Top, SpawnSide.Bottom, SpawnSide.Left, SpawnSide.Right
    };

    private readonly HashSet<EnemyController> activeEnemies = new HashSet<EnemyController>();
    private WaitForSeconds spawnWait;
    private Coroutine spawnRoutine;
    private bool missingReferencesLogged;

    public int ActiveEnemyCount => activeEnemies.Count;
    public bool IsSpawning => spawnRoutine != null;
    public EnemyController EnemyPrefab => enemyPrefab;
    public int MaxEnemies => maxEnemies;

    private void Awake()
    {
        if (spawnCamera == null) spawnCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (autoSpawn) StartSpawning();
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    private void OnDestroy()
    {
        // Gỡ đăng ký để Enemy không gọi ngược vào Manager đã bị hủy.
        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null) enemy.Despawned -= HandleEnemyDespawned;
        }
        activeEnemies.Clear();
    }

    public void StartSpawning()
    {
        if (spawnRoutine != null || !isActiveAndEnabled) return;

        spawnWait = new WaitForSeconds(spawnInterval);
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnRoutine == null) return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    public void SetSpawning(bool enabled)
    {
        if (enabled) StartSpawning();
        else StopSpawning();
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null) enemy.Initialize(player);
        }
    }

    /// <summary>
    /// Tìm Enemy còn sống gần nhất (không tạo garbage). maxRange = 0 nghĩa là không giới hạn tầm.
    /// </summary>
    public bool TryGetNearestEnemy(Vector2 from, float maxRange, out EnemyController nearest)
    {
        nearest = null;
        float bestSqr = maxRange > 0f ? maxRange * maxRange : float.MaxValue;

        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy == null || enemy.IsDying) continue;

            float sqr = ((Vector2)enemy.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = enemy;
            }
        }
        return nearest != null;
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return spawnWait;
            TrySpawn();
        }
    }

    private bool TrySpawn() => TrySpawnEnemy(out _);

    /// <summary>
    /// Sinh một Enemy ngoài màn hình. Trả về false nếu đã đủ maxEnemies trên màn hình hoặc thiếu reference.
    /// WaveManager dùng hàm này để tự điều khiển số lượng quái mỗi wave.
    /// </summary>
    public bool TrySpawnEnemy(out EnemyController enemy)
    {
        enemy = null;
        if (activeEnemies.Count >= maxEnemies) return false;
        if (!HasValidReferences()) return false;

        SpawnSide side = PickSide();
        if (side == SpawnSide.None) return false;

        Vector3 position = GetSpawnPosition(side);
        enemy = Instantiate(enemyPrefab, position, Quaternion.identity, transform);

        enemy.Despawned += HandleEnemyDespawned;
        activeEnemies.Add(enemy);
        enemy.Initialize(player);
        return true;
    }

    private void HandleEnemyDespawned(EnemyController enemy)
    {
        enemy.Despawned -= HandleEnemyDespawned;
        activeEnemies.Remove(enemy);
    }

    private bool HasValidReferences()
    {
        if (spawnCamera == null) spawnCamera = Camera.main;

        bool valid = enemyPrefab != null && player != null && spawnCamera != null;
        if (valid)
        {
            missingReferencesLogged = false;
            return true;
        }

        if (!missingReferencesLogged)
        {
            missingReferencesLogged = true;
            Debug.LogWarning("EnemyManager: không thể spawn, thiếu reference: "
                + (enemyPrefab == null ? "[Enemy Prefab] " : "")
                + (player == null ? "[Player] " : "")
                + (spawnCamera == null ? "[Camera] " : ""), this);
        }
        return false;
    }

    private SpawnSide PickSide()
    {
        int mask = (int)spawnDirection;
        int count = 0;
        for (int i = 0; i < Sides.Length; i++)
        {
            if ((mask & (int)Sides[i]) != 0) count++;
        }
        if (count == 0) return SpawnSide.None;

        int pick = Random.Range(0, count);
        for (int i = 0; i < Sides.Length; i++)
        {
            if ((mask & (int)Sides[i]) == 0) continue;
            if (pick == 0) return Sides[i];
            pick--;
        }
        return SpawnSide.None;
    }

    /// <summary>
    /// Lấy điểm ngẫu nhiên trên mép viewport rồi đẩy ra ngoài theo hướng của camera,
    /// nên luôn nằm ngoài màn hình bất kể tỷ lệ khung hình hay orthographic size.
    /// </summary>
    private Vector3 GetSpawnPosition(SpawnSide side)
    {
        Transform camTransform = spawnCamera.transform;
        float depth = Mathf.Abs(camTransform.position.z - player.position.z);
        float t = Random.value;

        float viewX;
        float viewY;
        Vector3 outward;

        switch (side)
        {
            case SpawnSide.Top:
                viewX = t; viewY = 1f; outward = camTransform.up;
                break;
            case SpawnSide.Bottom:
                viewX = t; viewY = 0f; outward = -camTransform.up;
                break;
            case SpawnSide.Left:
                viewX = 0f; viewY = t; outward = -camTransform.right;
                break;
            default:
                viewX = 1f; viewY = t; outward = camTransform.right;
                break;
        }

        Vector3 world = spawnCamera.ViewportToWorldPoint(new Vector3(viewX, viewY, depth)) + outward * spawnDistance;
        world.z = player.position.z;
        return world;
    }
}
