using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Một dòng hiển thị trong InventoryUI.</summary>
public struct InventoryItem
{
    public string Name;
    public string Detail;
    public string Value;

    public InventoryItem(string name, string detail, string value)
    {
        Name = name;
        Detail = detail;
        Value = value;
    }
}

/// <summary>Dữ liệu cho InventoryUI. Tạo mới cho MỖI lần mở để UI luôn hiển thị số liệu hiện tại.</summary>
public sealed class InventoryUIData
{
    public string Title;
    public readonly List<InventoryItem> Items = new List<InventoryItem>();

    /// <summary>Danh sách chỉ số nhân vật: tên, cấp hiện tại và giá trị cuối cùng.</summary>
    public static InventoryUIData FromPlayer(PlayerStatUpgrades upgrades, PlayerStats stats)
    {
        var data = new InventoryUIData { Title = "CHỈ SỐ NHÂN VẬT" };
        if (upgrades == null || stats == null || upgrades.Data == null) return data;

        foreach (StatUpgradeConfig config in upgrades.Data.Upgrades)
        {
            data.Items.Add(new InventoryItem(
                config.displayName,
                "Cấp " + upgrades.GetLevel(config.stat) + "/" + config.maxLevel,
                config.Format(stats.GetValue(config.stat))));
        }
        return data;
    }
}

/// <summary>
/// Cửa sổ danh sách dùng dữ liệu truyền vào mỗi lần mở. Các dòng được dùng lại giữa các lần mở,
/// chỉ tạo thêm khi danh sách mới dài hơn; dòng thừa được ẩn đi.
/// </summary>
public class InventoryUI : BaseUI<InventoryUIData>
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text emptyText;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private InventoryItemView itemTemplate;
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scrollRect;

    private readonly List<InventoryItemView> views = new List<InventoryItemView>();

    protected override void OnCreate()
    {
        if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(CloseSelf);
    }

    protected override void OnOpenWithData(InventoryUIData data)
    {
        int count = data != null ? data.Items.Count : 0;
        if (titleText != null) titleText.text = data != null && !string.IsNullOrEmpty(data.Title) ? data.Title : "KHO ĐỒ";

        if (itemTemplate != null)
            while (views.Count < count) views.Add(Instantiate(itemTemplate, listContent));

        for (int i = 0; i < views.Count; i++)
        {
            bool used = i < count;
            views[i].gameObject.SetActive(used);
            if (used) views[i].Bind(data.Items[i]);
        }

        if (emptyText != null) emptyText.gameObject.SetActive(count == 0);
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }
}
