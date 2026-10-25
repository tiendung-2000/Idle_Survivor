using UnityEngine;
using UnityEngine.UI;

/// <summary>Hiển thị số tiền trên HUD, nảy nhẹ mỗi lần nhận tiền.</summary>
public class CoinCounter : MonoBehaviour
{
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private Text valueText;
    [SerializeField] private RectTransform punchTarget;
    [SerializeField, Min(1f)] private float punchScale = 1.25f;
    [SerializeField, Min(0.01f)] private float punchDuration = 0.15f;

    private float punchTimer;

    private void OnEnable()
    {
        if (wallet != null) wallet.Changed += HandleChanged;
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= HandleChanged;
    }

    private void Start()
    {
        if (wallet != null) SetText(wallet.Coins);
    }

    private void HandleChanged(int coins, int delta)
    {
        SetText(coins);
        if (delta > 0) punchTimer = punchDuration;
    }

    private void SetText(int coins)
    {
        if (valueText != null) valueText.text = NumberFormatter.Format(coins);
    }

    private void Update()
    {
        if (punchTarget == null) return;

        if (punchTimer > 0f)
        {
            punchTimer -= Time.unscaledDeltaTime;   // vẫn chạy khi game tạm dừng
            float t = Mathf.Clamp01(punchTimer / punchDuration);
            punchTarget.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, t);
        }
        else if (punchTarget.localScale != Vector3.one)
        {
            punchTarget.localScale = Vector3.one;
        }
    }
}
