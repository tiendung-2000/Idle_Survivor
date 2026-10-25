using UnityEngine;
using UnityEngine.UI;

/// <summary>Hiển thị một InventoryItem.</summary>
public class InventoryItemView : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text detailText;
    [SerializeField] private Text valueText;

    public void Bind(InventoryItem item)
    {
        if (nameText != null) nameText.text = item.Name;
        if (detailText != null) detailText.text = item.Detail;
        if (valueText != null) valueText.text = item.Value;
    }
}
