using UnityEngine;
using UnityEngine.UI;

/// <summary>Menu tạm dừng: tiếp tục chơi, xem chỉ số nhân vật. Tạm dừng game trong lúc mở.</summary>
public class MainMenuUI : BaseUI
{
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button statsButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text bestWaveText;
    [SerializeField] private bool pauseGame = true;

    private PlayerStatUpgrades upgrades;
    private PlayerStats stats;
    private bool pausedByMe;
    private float previousTimeScale = 1f;

    protected override void OnCreate()
    {
        // Đăng ký listener ĐÚNG MỘT LẦN ở đây, không nhân đôi khi mở lại.
        if (resumeButton != null) resumeButton.onClick.AddListener(CloseSelf);
        if (closeButton != null) closeButton.onClick.AddListener(CloseSelf);
        if (statsButton != null) statsButton.onClick.AddListener(OpenStats);

        // Tìm một lần lúc tạo, không tìm lại mỗi lần mở.
        upgrades = FindAnyObjectByType<PlayerStatUpgrades>();
        if (upgrades != null) stats = upgrades.GetComponent<PlayerStats>();
    }

    protected override void OnOpen(object data)
    {
        if (bestWaveText != null) bestWaveText.text = "Wave cao nhất: " + PlayerSaveSystem.BestWave;

        // Open khi đang mở sẽ gọi lại OnOpen: chỉ lưu timeScale ở lần tạm dừng đầu tiên.
        if (pauseGame && !pausedByMe)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedByMe = true;
        }
    }

    protected override void OnClose()
    {
        if (!pausedByMe) return;
        Time.timeScale = previousTimeScale;
        pausedByMe = false;
    }

    private void OpenStats()
    {
        Manager.Open<InventoryUI>(InventoryUIData.FromPlayer(upgrades, stats));
    }
}
