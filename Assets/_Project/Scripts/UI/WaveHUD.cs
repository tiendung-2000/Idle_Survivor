using UnityEngine;
using UnityEngine.UI;

/// <summary>Bảng wave góc trên phải: số wave, sức mạnh quái hiện tại và tiến độ wave.</summary>
public class WaveHUD : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;

    [Header("Chữ")]
    [SerializeField] private Text waveText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text enemyDamageText;
    [SerializeField] private Text enemyHealthText;

    [Header("Thanh tiến độ")]
    [SerializeField] private RectTransform progressFill;   // co giãn theo anchorMax.x
    [SerializeField] private Text progressText;

    [Header("Màu")]
    [SerializeField] private Color normalWaveColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private Color bossWaveColor = new Color(1f, 0.42f, 0.38f);

    private void OnEnable()
    {
        if (waveManager == null) return;
        waveManager.WaveStarted += HandleWaveStarted;
        waveManager.ProgressChanged += HandleProgress;
        waveManager.WaveCompleted += HandleWaveCompleted;
    }

    private void OnDisable()
    {
        if (waveManager == null) return;
        waveManager.WaveStarted -= HandleWaveStarted;
        waveManager.ProgressChanged -= HandleProgress;
        waveManager.WaveCompleted -= HandleWaveCompleted;
    }

    private void Start()
    {
        if (waveManager == null) return;
        if (waveManager.CurrentWave > 0) HandleWaveStarted(waveManager.CurrentWave, waveManager.IsBossWave);
        HandleProgress(waveManager.EnemiesCleared, waveManager.EnemiesTotal);
    }

    private void HandleWaveStarted(int wave, bool boss)
    {
        if (waveText != null)
        {
            waveText.text = "WAVE " + wave;
            waveText.color = boss ? bossWaveColor : normalWaveColor;
        }
        if (enemyDamageText != null) enemyDamageText.text = NumberFormatter.Format(waveManager.CurrentEnemyDamage);
        if (enemyHealthText != null) enemyHealthText.text = NumberFormatter.Format(waveManager.CurrentEnemyHealth);
    }

    private void HandleProgress(int cleared, int total)
    {
        float ratio = total > 0 ? Mathf.Clamp01((float)cleared / total) : 0f;
        if (progressFill != null) progressFill.anchorMax = new Vector2(ratio, 1f);
        if (progressText != null) progressText.text = cleared + "/" + total;

        if (statusText == null) return;
        if (waveManager.IsBossWave && cleared < total)
        {
            statusText.text = "BOSS!";
            statusText.color = bossWaveColor;
        }
        else
        {
            statusText.text = "Còn " + Mathf.Max(0, total - cleared) + " quái";
            statusText.color = Color.white;
        }
    }

    private void HandleWaveCompleted(int wave)
    {
        if (statusText == null) return;
        statusText.text = "Hoàn thành!";
        statusText.color = new Color(0.5f, 0.95f, 0.45f);
    }
}
