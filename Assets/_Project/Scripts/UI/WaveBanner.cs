using UnityEngine;
using UnityEngine.UI;

/// <summary>Chữ lớn giữa màn hình khi bắt đầu wave ("WAVE 5", "BOSS XUẤT HIỆN!"), tự hiện rồi mờ dần.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class WaveBanner : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Color normalColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private Color bossColor = new Color(1f, 0.42f, 0.38f);
    [SerializeField, Min(0.2f)] private float duration = 1.8f;

    private CanvasGroup group;
    private RectTransform rect;
    private float timer;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rect = (RectTransform)transform;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void OnEnable()
    {
        if (waveManager != null) waveManager.WaveStarted += HandleWaveStarted;
    }

    private void OnDisable()
    {
        if (waveManager != null) waveManager.WaveStarted -= HandleWaveStarted;
    }

    private void HandleWaveStarted(int wave, bool boss)
    {
        if (titleText != null)
        {
            titleText.text = "WAVE " + wave;
            titleText.color = boss ? bossColor : normalColor;
        }
        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(boss);
            subtitleText.text = "BOSS XUẤT HIỆN!";
            subtitleText.color = bossColor;
        }
        timer = duration;
    }

    private void Update()
    {
        if (timer <= 0f) return;

        timer -= Time.unscaledDeltaTime;
        float t = 1f - Mathf.Clamp01(timer / duration);   // 0 -> 1

        // Hiện nhanh trong 15% đầu, giữ, rồi mờ dần ở 30% cuối
        group.alpha = t < 0.15f ? t / 0.15f : t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
        rect.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(1.4f, 1f, t / 0.15f) : 1f);

        if (timer <= 0f) group.alpha = 0f;
    }
}
