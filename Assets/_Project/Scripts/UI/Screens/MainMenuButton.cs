using UnityEngine;
using UnityEngine.UI;

/// <summary>Nút trên HUD để mở/đóng MainMenuUI.</summary>
[RequireComponent(typeof(Button))]
public class MainMenuButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        UIManager manager = UIManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("MainMenuButton: chưa có UIManager trong scene.", this);
            return;
        }
        manager.Toggle<MainMenuUI>();
    }
}
