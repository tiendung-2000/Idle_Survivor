using UnityEngine;

/// <summary>
/// Thông số cân bằng wave. Quái mạnh dần theo cấp số nhân: giá trị ở wave N = gốc x growth^(N-1).
/// Chuột phải asset > Log Wave Table để xem bảng độ khó các mốc wave.
/// </summary>
[CreateAssetMenu(fileName = "WaveSettings", menuName = "Game/Wave Settings")]
public class WaveSettings : ScriptableObject
{
    [Header("Số lượng quái mỗi wave")]
    [Tooltip("Số quái ở wave 1.")]
    [SerializeField, Min(1)] private int baseEnemyCount = 8;
    [Tooltip("Số quái tăng thêm mỗi wave (có thể là số lẻ, làm tròn xuống).")]
    [SerializeField, Min(0f)] private float enemiesAddedPerWave = 1.5f;
    [Tooltip("Số quái tối đa trong một wave.")]
    [SerializeField, Min(1)] private int maxEnemyCount = 40;

    [Header("Nhịp độ")]
    [Tooltip("Khoảng cách giữa hai lần spawn ở wave 1 (giây).")]
    [SerializeField, Min(0.05f)] private float baseSpawnInterval = 1.2f;
    [Tooltip("Mỗi wave giảm khoảng cách spawn bao nhiêu giây.")]
    [SerializeField, Min(0f)] private float spawnIntervalReductionPerWave = 0.02f;
    [SerializeField, Min(0.05f)] private float minSpawnInterval = 0.35f;
    [Tooltip("Thời gian nghỉ giữa hai wave (giây).")]
    [SerializeField, Min(0f)] private float timeBetweenWaves = 2.5f;

    [Header("Quái mạnh dần (nhân dồn mỗi wave)")]
    [Tooltip("1.08 = máu quái tăng 8% mỗi wave. Wave 10 ≈ x2, wave 30 ≈ x9.3, wave 50 ≈ x43.")]
    [SerializeField, Min(1f)] private float healthGrowth = 1.08f;
    [Tooltip("1.05 = sát thương quái tăng 5% mỗi wave.")]
    [SerializeField, Min(1f)] private float damageGrowth = 1.05f;
    [Tooltip("1.05 = tiền thưởng mỗi quái tăng 5% mỗi wave. Nên thấp hơn healthGrowth để người chơi phải nâng cấp khôn ngoan.")]
    [SerializeField, Min(1f)] private float rewardGrowth = 1.05f;

    [Tooltip("1.008 = quái nhanh hơn 0.8% mỗi wave, tối đa maxSpeedMultiplier lần tốc độ gốc.")]
    [SerializeField, Min(1f)] private float speedGrowth = 1.008f;
    [SerializeField, Min(1f)] private float maxSpeedMultiplier = 1.6f;

    [Header("Boss")]
    [Tooltip("Cứ mỗi N wave có một boss xuất hiện cuối wave. 0 = tắt boss.")]
    [SerializeField, Min(0)] private int bossEveryWaves = 10;
    [SerializeField, Min(1f)] private float bossHealthMultiplier = 12f;
    [SerializeField, Min(1f)] private float bossDamageMultiplier = 3f;
    [SerializeField, Min(1f)] private float bossRewardMultiplier = 15f;
    [SerializeField, Min(0.1f)] private float bossScale = 1.8f;
    [Tooltip("Boss đi chậm hơn quái thường: là một cuộc đua sát thương, người chơi thấy boss tiến tới và có thời gian phản ứng.")]
    [SerializeField, Range(0.1f, 2f)] private float bossSpeedMultiplier = 0.7f;
    [SerializeField] private Color bossTint = new Color(1f, 0.45f, 0.45f);

    public float TimeBetweenWaves => timeBetweenWaves;
    public float BossHealthMultiplier => bossHealthMultiplier;
    public float BossDamageMultiplier => bossDamageMultiplier;
    public float BossRewardMultiplier => bossRewardMultiplier;
    public float BossScale => bossScale;
    public float BossSpeedMultiplier => bossSpeedMultiplier;
    public Color BossTint => bossTint;

    public bool IsBossWave(int wave) => bossEveryWaves > 0 && wave > 0 && wave % bossEveryWaves == 0;

    public int GetEnemyCount(int wave) =>
        Mathf.Clamp(baseEnemyCount + Mathf.FloorToInt(enemiesAddedPerWave * (wave - 1)), 1, maxEnemyCount);

    public float GetSpawnInterval(int wave) =>
        Mathf.Max(minSpawnInterval, baseSpawnInterval - spawnIntervalReductionPerWave * (wave - 1));

    public float GetHealthMultiplier(int wave) => Mathf.Pow(healthGrowth, Mathf.Max(0, wave - 1));
    public float GetDamageMultiplier(int wave) => Mathf.Pow(damageGrowth, Mathf.Max(0, wave - 1));
    public float GetRewardMultiplier(int wave) => Mathf.Pow(rewardGrowth, Mathf.Max(0, wave - 1));
    public float GetSpeedMultiplier(int wave) => Mathf.Min(maxSpeedMultiplier, Mathf.Pow(speedGrowth, Mathf.Max(0, wave - 1)));

    [ContextMenu("Log Wave Table")]
    private void LogWaveTable()
    {
        foreach (int w in new[] { 1, 5, 10, 20, 30, 40, 50, 75, 100 })
        {
            Debug.Log("Wave " + w + (IsBossWave(w) ? " (BOSS)" : "") + ": " + GetEnemyCount(w) + " quái, spawn mỗi " +
                      GetSpawnInterval(w).ToString("0.00") + "s | máu x" + GetHealthMultiplier(w).ToString("0.0") +
                      " | sát thương x" + GetDamageMultiplier(w).ToString("0.0") + " | thưởng x" + GetRewardMultiplier(w).ToString("0.0") + " | tốc độ x" + GetSpeedMultiplier(w).ToString("0.00"));
        }
    }
}
