using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thanh nâng cấp dưới đáy màn hình: mỗi ô (tab) là một nhóm chỉ số (Tấn công / Phòng thủ / Tài chính).
/// Bấm ô để mở bảng nâng cấp của nhóm đó, bấm lại để đóng. Các dòng được tạo tự động từ StatUpgradeData.
/// </summary>
public class StatUpgradePanel : MonoBehaviour
{
    [Serializable]
    public class CategoryTab
    {
        public StatCategory category;
        public string title = "TẤN CÔNG";
        public Button button;
        public Image background;
        [Tooltip("Chấm đỏ: số nâng cấp trong nhóm đang đủ tiền mua.")]
        public GameObject badgeRoot;
        public Text badgeText;
    }

    [Header("References")]
    [SerializeField] private PlayerStatUpgrades upgrades;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private CoinWallet wallet;

    [Header("Bảng nâng cấp")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Text panelTitle;
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private StatUpgradeRow rowTemplate;

    [Header("Các ô nâng cấp")]
    [SerializeField] private CategoryTab[] tabs = new CategoryTab[0];
    [SerializeField] private Color tabNormalColor = new Color(0.43f, 0.45f, 0.6f);
    [SerializeField] private Color tabSelectedColor = new Color(0.62f, 0.65f, 0.84f);

    [Header("Hành vi")]
    [SerializeField] private bool pauseWhileOpen;

    private readonly List<StatUpgradeRow> rows = new List<StatUpgradeRow>();
    private readonly List<StatCategory> rowCategories = new List<StatCategory>();
    private EconomySettings economy;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private StatCategory currentCategory;

    public bool IsOpen => isOpen;
    public StatCategory CurrentCategory => currentCategory;

    private void Awake()
    {
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (upgrades != null) economy = upgrades.Economy;

        foreach (CategoryTab tab in tabs)
        {
            if (tab.button == null) continue;
            StatCategory category = tab.category;
            tab.button.onClick.AddListener(() => HandleTabClicked(category));
        }
    }

    private void OnEnable()
    {
        if (wallet != null) wallet.Changed += HandleWalletChanged;
        if (upgrades != null) upgrades.LevelChanged += HandleLevelChanged;
        if (economy != null) economy.Changed += RefreshAll;
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= HandleWalletChanged;
        if (upgrades != null) upgrades.LevelChanged -= HandleLevelChanged;
        if (economy != null) economy.Changed -= RefreshAll;
        if (isOpen && pauseWhileOpen) Time.timeScale = previousTimeScale;
    }

    private void Start()
    {
        BuildRows();
        if (panelRoot != null) panelRoot.SetActive(false);
        RefreshAll();
    }

    private void HandleTabClicked(StatCategory category)
    {
        if (isOpen && category == currentCategory) Close();
        else Open(category);
    }

    public void Open(StatCategory category)
    {
        currentCategory = category;

        for (int i = 0; i < rows.Count; i++)
            rows[i].gameObject.SetActive(rowCategories[i] == category);

        CategoryTab tab = FindTab(category);
        if (panelTitle != null && tab != null) panelTitle.text = tab.title;

        if (!isOpen)
        {
            isOpen = true;
            if (panelRoot != null) panelRoot.SetActive(true);
            if (pauseWhileOpen)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
        }

        RefreshAll();
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;   // về đầu danh sách
        }
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (pauseWhileOpen) Time.timeScale = previousTimeScale;
        RefreshAll();
    }

    private CategoryTab FindTab(StatCategory category)
    {
        foreach (CategoryTab tab in tabs)
            if (tab.category == category) return tab;
        return null;
    }

    private void BuildRows()
    {
        if (upgrades == null || upgrades.Data == null || rowTemplate == null || rowContainer == null)
        {
            Debug.LogWarning("StatUpgradePanel: thiếu reference, không tạo được bảng nâng cấp.", this);
            return;
        }

        foreach (StatUpgradeConfig config in upgrades.Data.Upgrades)
        {
            StatUpgradeRow row = Instantiate(rowTemplate, rowContainer);
            row.name = "Row_" + config.stat;
            row.Setup(config.stat, HandleUpgradeClicked);
            rows.Add(row);
            rowCategories.Add(config.category);
        }
    }

    private void HandleUpgradeClicked(StatType stat)
    {
        upgrades.TryUpgrade(stat, wallet);   // LevelChanged / wallet.Changed sẽ gọi RefreshAll
    }

    private void HandleWalletChanged(int coins, int delta) => RefreshAll();
    private void HandleLevelChanged(StatType stat, int level) => RefreshAll();

    private void RefreshAll()
    {
        if (upgrades == null || stats == null) return;

        int coins = wallet != null ? wallet.Coins : 0;
        var affordable = new int[Enum.GetValues(typeof(StatCategory)).Length];

        for (int i = 0; i < rows.Count; i++)
        {
            StatUpgradeRow row = rows[i];
            StatUpgradeConfig config = upgrades.GetConfig(row.Stat);
            if (config == null) continue;

            int cost = upgrades.GetUpgradeCost(row.Stat);
            bool canAfford = cost >= 0 && coins >= cost;
            if (canAfford) affordable[(int)config.category]++;

            row.Refresh(config, upgrades.GetLevel(row.Stat), stats.GetValue(row.Stat),
                upgrades.PreviewNextLevelValue(row.Stat), cost, canAfford);
        }

        foreach (CategoryTab tab in tabs)
        {
            bool selected = isOpen && tab.category == currentCategory;
            int count = affordable[(int)tab.category];
            if (tab.background != null) tab.background.color = selected ? tabSelectedColor : tabNormalColor;
            if (tab.badgeRoot != null) tab.badgeRoot.SetActive(count > 0 && !selected);
            if (tab.badgeText != null) tab.badgeText.text = count.ToString();
        }
    }
}
