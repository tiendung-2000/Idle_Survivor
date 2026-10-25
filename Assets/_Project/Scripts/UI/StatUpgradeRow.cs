using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Một dòng trong bảng nâng cấp: tên, cấp, giá trị hiện tại -> sau nâng cấp, nút mua.</summary>
public class StatUpgradeRow : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text valueText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Text costText;
    [SerializeField] private GameObject costIcon;

    [Header("Màu nút")]
    [SerializeField] private Color affordableColor = new Color(0.22f, 0.68f, 0.32f);
    [SerializeField] private Color tooExpensiveColor = new Color(0.32f, 0.33f, 0.36f);
    [SerializeField] private Color maxedColor = new Color(0.78f, 0.6f, 0.16f);

    private Action<StatType> clicked;
    private float costLeftOffset = -1f;

    public StatType Stat { get; private set; }

    public void Setup(StatType stat, Action<StatType> onUpgradeClicked)
    {
        Stat = stat;
        clicked = onUpgradeClicked;
        upgradeButton.onClick.RemoveAllListeners();
        upgradeButton.onClick.AddListener(() => clicked?.Invoke(Stat));
    }

    public void Refresh(StatUpgradeConfig config, int level, float currentValue, float nextValue, int cost, bool canAfford)
    {
        bool maxed = cost < 0;

        nameText.text = config.displayName;
        levelText.text = "Cấp " + level + " / " + config.maxLevel;
        valueText.text = maxed
            ? config.Format(currentValue)
            : config.Format(currentValue) + "  →  " + config.Format(nextValue);

        costText.text = maxed ? "TỐI ĐA" : NumberFormatter.Format(cost);
        if (costIcon != null) costIcon.SetActive(!maxed);

        // Khi ẩn icon, kéo chữ về giữa nút
        RectTransform costRect = costText.rectTransform;
        if (costLeftOffset < 0f) costLeftOffset = costRect.offsetMin.x;
        costRect.offsetMin = new Vector2(maxed ? -costRect.offsetMax.x : costLeftOffset, costRect.offsetMin.y);

        upgradeButton.interactable = !maxed && canAfford;
        if (buttonImage != null) buttonImage.color = maxed ? maxedColor : (canAfford ? affordableColor : tooExpensiveColor);
    }
}
