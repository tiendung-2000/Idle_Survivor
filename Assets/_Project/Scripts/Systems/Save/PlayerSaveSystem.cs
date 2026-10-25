using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lưu / tải tiến trình người chơi bằng PlayerPrefs:
///   player.stat.<TênChỉSố>  cấp nâng cấp của từng chỉ số (lưu theo tên nên thêm/đổi thứ tự enum không hỏng dữ liệu)
///   player.coins            số tiền
///   player.bestWave         wave cao nhất đã hoàn thành
/// Tự lưu ngay khi nâng cấp, định kỳ khi tiền thay đổi, và khi app bị ẩn/tắt (quan trọng trên Android).
/// </summary>
public class PlayerSaveSystem : MonoBehaviour
{
    private const string Prefix = "player.";
    private const string VersionKey = Prefix + "saveVersion";
    private const string CoinsKey = Prefix + "coins";
    private const string BestWaveKey = Prefix + "bestWave";
    private const string StatKeyPrefix = Prefix + "stat.";
    private const int SaveVersion = 1;

    [Header("References")]
    [SerializeField] private PlayerStatUpgrades upgrades;
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private WaveManager waveManager;

    [Header("Hành vi")]
    [Tooltip("Tiền thay đổi liên tục khi diệt quái nên chỉ ghi xuống đĩa mỗi N giây.")]
    [SerializeField, Min(1f)] private float autoSaveInterval = 5f;
    [Tooltip("Tắt để chơi thử trong Editor mà không nạp/ghi dữ liệu lưu.")]
    [SerializeField] private bool saveEnabled = true;

    private bool loaded;
    private bool isLoading;
    private bool dirty;
    private float nextSaveTime;

    public static bool HasSave => PlayerPrefs.HasKey(VersionKey);
    public static int BestWave => PlayerPrefs.GetInt(BestWaveKey, 0);

    private void OnEnable()
    {
        if (upgrades != null) upgrades.LevelChanged += HandleLevelChanged;
        if (wallet != null) wallet.Changed += HandleCoinsChanged;
        if (waveManager != null) waveManager.WaveCompleted += HandleWaveCompleted;
    }

    private void OnDisable()
    {
        if (upgrades != null) upgrades.LevelChanged -= HandleLevelChanged;
        if (wallet != null) wallet.Changed -= HandleCoinsChanged;
        if (waveManager != null) waveManager.WaveCompleted -= HandleWaveCompleted;
        if (dirty) SaveNow();
    }

    // Start chạy sau Awake của mọi object, nên PlayerStatUpgrades / CoinWallet đã sẵn sàng.
    // Các UI tự cập nhật qua sự kiện LevelChanged / Changed khi dữ liệu được nạp.
    private void Start()
    {
        if (saveEnabled) Load();
        loaded = true;
    }

    private void Update()
    {
        if (dirty && Time.unscaledTime >= nextSaveTime) SaveNow();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveNow();
    }

    private void OnApplicationQuit()
    {
        SaveNow();
    }

    public void Load()
    {
        if (!HasSave) return;

        isLoading = true;
        try
        {
            if (upgrades != null)
            {
                var levels = new List<StatLevelSave>();
                foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
                {
                    string key = StatKeyPrefix + stat;
                    if (PlayerPrefs.HasKey(key)) levels.Add(new StatLevelSave(stat, PlayerPrefs.GetInt(key)));
                }
                upgrades.ImportLevels(levels);   // tự kẹp theo cấp tối đa hiện tại
            }

            if (wallet != null) wallet.SetCoins(PlayerPrefs.GetInt(CoinsKey, wallet.Coins));
        }
        finally
        {
            isLoading = false;
            dirty = false;
        }
    }

    public void SaveNow()
    {
        if (!saveEnabled || !loaded || isLoading) return;

        PlayerPrefs.SetInt(VersionKey, SaveVersion);
        if (upgrades != null)
        {
            foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
                PlayerPrefs.SetInt(StatKeyPrefix + stat, upgrades.GetLevel(stat));
        }
        if (wallet != null) PlayerPrefs.SetInt(CoinsKey, wallet.Coins);
        PlayerPrefs.Save();

        dirty = false;
        nextSaveTime = Time.unscaledTime + autoSaveInterval;
    }

    /// <summary>Xóa toàn bộ dữ liệu lưu của người chơi. Trong lúc Play, phiên hiện tại sẽ không ghi lại nữa.</summary>
    [ContextMenu("Xóa dữ liệu lưu")]
    public void DeleteSave()
    {
        DeleteAllSavedData();
        if (Application.isPlaying) saveEnabled = false;   // tránh ghi đè lại khi thoát Play
        Debug.Log("PlayerSaveSystem: đã xóa dữ liệu lưu.");
    }

    public static void DeleteAllSavedData()
    {
        PlayerPrefs.DeleteKey(VersionKey);
        PlayerPrefs.DeleteKey(CoinsKey);
        PlayerPrefs.DeleteKey(BestWaveKey);
        foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
            PlayerPrefs.DeleteKey(StatKeyPrefix + stat);
        PlayerPrefs.Save();
    }

    private void HandleLevelChanged(StatType stat, int level)
    {
        if (!isLoading && loaded) SaveNow();   // nâng cấp là thao tác quan trọng: lưu ngay
    }

    private void HandleCoinsChanged(int coins, int delta)
    {
        if (isLoading || !loaded) return;
        if (!dirty) nextSaveTime = Mathf.Max(nextSaveTime, Time.unscaledTime + autoSaveInterval);
        dirty = true;
    }

    private void HandleWaveCompleted(int wave)
    {
        if (!saveEnabled || wave <= BestWave) return;
        PlayerPrefs.SetInt(BestWaveKey, wave);
        dirty = true;   // ghi xuống đĩa cùng lần lưu kế tiếp
    }
}
