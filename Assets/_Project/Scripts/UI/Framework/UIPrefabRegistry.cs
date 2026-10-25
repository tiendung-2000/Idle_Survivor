using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bảng đăng ký prefab UI. Khóa là KIỂU của component BaseUI trên prefab (ví dụ InventoryUI),
/// nên không có đường dẫn hay chuỗi viết cứng rải rác trong code.
/// Danh sách được chuyển thành Dictionary một lần (lười, ở lần tra cứu đầu tiên).
/// </summary>
[CreateAssetMenu(fileName = "UIPrefabRegistry", menuName = "Game/UI/UI Prefab Registry")]
public class UIPrefabRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Prefab có component kế thừa BaseUI. Kiểu của component đó là khóa để mở UI.")]
        public BaseUI prefab;
        [Tooltip("Lớp Canvas mà UI được gắn vào khi tạo.")]
        public UILayer layer = UILayer.Popup;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<Type, Entry> lookup;

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGet(Type uiType, out Entry entry)
    {
        EnsureLookup();
        return lookup.TryGetValue(uiType, out entry);
    }

    /// <summary>Thêm prefab vào bảng (dùng cho công cụ Editor và test). Trả về false nếu kiểu đã có.</summary>
    public bool Register(BaseUI prefab, UILayer layer)
    {
        if (prefab == null) return false;
        EnsureLookup();

        Type type = prefab.GetType();
        if (lookup.ContainsKey(type)) return false;

        var entry = new Entry { prefab = prefab, layer = layer };
        entries.Add(entry);
        lookup.Add(type, entry);
        return true;
    }

    private void OnEnable() => lookup = null;
    private void OnValidate() => lookup = null;

    private void EnsureLookup()
    {
        if (lookup != null) return;

        lookup = new Dictionary<Type, Entry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"UIPrefabRegistry '{name}': mục #{i} chưa gán prefab, bỏ qua.", this);
                continue;
            }

            Type type = entry.prefab.GetType();
            if (lookup.ContainsKey(type))
            {
                Debug.LogWarning($"UIPrefabRegistry '{name}': {type.Name} được đăng ký nhiều lần, dùng mục đầu tiên.", this);
                continue;
            }
            lookup.Add(type, entry);
        }
    }
}
