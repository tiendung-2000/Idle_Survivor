using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điều khiển wave: mỗi wave sinh một số quái nhất định qua EnemyManager, quái mạnh dần theo wave
/// và cứ vài wave có boss ở cuối. Wave xong khi mọi quái của wave đã bị hạ, nghỉ một chút rồi sang wave mới.
/// Khi Player chết, wave dừng lại (xử lý thua sẽ làm ở bước thiết kế vòng chơi).
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveSettings settings;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Bắt đầu")]
    [Tooltip("Wave bắt đầu khi vào game. Đặt cao để thử độ khó các wave sau.")]
    [SerializeField, Min(1)] private int startWave = 1;
    [SerializeField] private bool autoStart = true;

    [Header("Chỉ để quan sát khi chạy game")]
    [SerializeField] private int currentWave;
    [SerializeField] private int enemiesCleared;
    [SerializeField] private int enemiesTotal;

    private Coroutine waveRoutine;
    private int enemiesSpawned;
    private readonly HashSet<EnemyController> waveEnemies = new HashSet<EnemyController>();   // chỉ quái của wave hiện tại
    private bool isBossWave;
    private float baseEnemyHealth = 1f;
    private float baseEnemyDamage;

    /// <summary>Tham số: (số wave, wave có boss).</summary>
    public event Action<int, bool> WaveStarted;
    public event Action<int> WaveCompleted;
    /// <summary>Tham số: (số quái đã hạ, tổng số quái của wave).</summary>
    public event Action<int, int> ProgressChanged;
    /// <summary>Tham số: (quái vừa sinh, có phải boss).</summary>
    public event Action<EnemyController, bool> EnemySpawned;

    public int CurrentWave => currentWave;
    public int EnemiesTotal => enemiesTotal;
    public int EnemiesCleared => enemiesCleared;
    public bool IsBossWave => isBossWave;
    public bool IsRunning => waveRoutine != null;
    public WaveSettings Settings => settings;
    /// <summary>Boss đang sống của wave hiện tại, null nếu không có.</summary>
    public EnemyHealth CurrentBoss { get; private set; }

    /// <summary>Máu của một quái thường ở wave hiện tại (để hiển thị).</summary>
    public float CurrentEnemyHealth =>
        baseEnemyHealth * (settings != null ? settings.GetHealthMultiplier(Mathf.Max(1, currentWave)) : 1f);

    /// <summary>Sát thương của một quái thường ở wave hiện tại (để hiển thị).</summary>
    public float CurrentEnemyDamage =>
        baseEnemyDamage * (settings != null ? settings.GetDamageMultiplier(Mathf.Max(1, currentWave)) : 1f);

    private void Awake()
    {
        if (enemyManager != null && enemyManager.EnemyPrefab != null)
        {
            EnemyController prefab = enemyManager.EnemyPrefab;
            baseEnemyDamage = prefab.Damage;
            if (prefab.TryGetComponent(out EnemyHealth health)) baseEnemyHealth = health.MaxHealth;
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnPlayerDied -= HandlePlayerDied;
        StopWaves();
    }

    private void Start()
    {
        if (autoStart) StartWaves(startWave);
    }

    public void StartWaves(int fromWave)
    {
        if (settings == null || enemyManager == null)
        {
            Debug.LogWarning("WaveManager: thiếu WaveSettings hoặc EnemyManager.", this);
            return;
        }

        StopWaves();
        ClearWaveEnemies();
        enemyManager.StopSpawning();   // WaveManager tự điều khiển việc spawn
        currentWave = Mathf.Max(1, fromWave) - 1;
        waveRoutine = StartCoroutine(RunWaves());
    }

    public void StopWaves()
    {
        if (waveRoutine == null) return;
        StopCoroutine(waveRoutine);
        waveRoutine = null;
    }

    private IEnumerator RunWaves()
    {
        while (true)
        {
            currentWave++;
            yield return RunWave(currentWave);
            WaveCompleted?.Invoke(currentWave);
            if (settings.TimeBetweenWaves > 0f) yield return new WaitForSeconds(settings.TimeBetweenWaves);
        }
    }

    private IEnumerator RunWave(int wave)
    {
        isBossWave = settings.IsBossWave(wave);
        enemiesTotal = settings.GetEnemyCount(wave);
        enemiesSpawned = 0;
        enemiesCleared = 0;
        ClearWaveEnemies();

        WaveStarted?.Invoke(wave, isBossWave);
        ProgressChanged?.Invoke(0, enemiesTotal);

        var spawnWait = new WaitForSeconds(settings.GetSpawnInterval(wave));
        var retryWait = new WaitForSeconds(0.2f);

        while (enemiesSpawned < enemiesTotal)
        {
            bool spawnBoss = isBossWave && enemiesSpawned == enemiesTotal - 1;   // boss ra cuối wave
            if (enemyManager.TrySpawnEnemy(out EnemyController enemy))
            {
                Configure(enemy, wave, spawnBoss);
                enemiesSpawned++;
                yield return spawnWait;
            }
            else
            {
                yield return retryWait;   // màn hình đang đủ số quái tối đa
            }
        }

        while (waveEnemies.Count > 0) yield return null;
    }

    private void Configure(EnemyController enemy, int wave, bool boss)
    {
        float health = settings.GetHealthMultiplier(wave);
        float damage = settings.GetDamageMultiplier(wave);
        float reward = settings.GetRewardMultiplier(wave);
        float speed = settings.GetSpeedMultiplier(wave);
        float size = 1f;

        if (boss)
        {
            health *= settings.BossHealthMultiplier;
            damage *= settings.BossDamageMultiplier;
            reward *= settings.BossRewardMultiplier;
            size = settings.BossScale;
            speed *= settings.BossSpeedMultiplier;
            foreach (SpriteRenderer sprite in enemy.GetComponentsInChildren<SpriteRenderer>()) sprite.color *= settings.BossTint;
        }

        enemy.ApplyScaling(damage, size, speed);
        if (enemy.TryGetComponent(out EnemyHealth enemyHealth))
        {
            enemyHealth.ApplyScaling(health, reward);
            if (boss) CurrentBoss = enemyHealth;
        }

        waveEnemies.Add(enemy);
        enemy.Despawned += HandleEnemyDespawned;
        EnemySpawned?.Invoke(enemy, boss);
    }

    private void HandleEnemyDespawned(EnemyController enemy)
    {
        if (this == null) return;                 // scene đang đóng
        if (!waveEnemies.Remove(enemy)) return;   // quái của wave cũ, không tính

        if (CurrentBoss != null && CurrentBoss.gameObject == enemy.gameObject) CurrentBoss = null;
        enemiesCleared = Mathf.Min(enemiesTotal, enemiesCleared + 1);
        ProgressChanged?.Invoke(enemiesCleared, enemiesTotal);
    }

    private void ClearWaveEnemies()
    {
        foreach (EnemyController enemy in waveEnemies)
            if (enemy != null) enemy.Despawned -= HandleEnemyDespawned;
        waveEnemies.Clear();
        CurrentBoss = null;
    }

    private void HandlePlayerDied()
    {
        StopWaves();
    }
}
