using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// On screen dash button, fires on press so it can be tapped while steering
[RequireComponent(typeof(Button))]
public class DashButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField]
    private bool mobileOnly = true;
    [Tooltip("Optional filled image drawn over the button while on cooldown")]
    [SerializeField]
    private Image cooldownFill;

    private Button _button;

    private void Awake()
    {
        if (mobileOnly && !Application.isMobilePlatform) {
            gameObject.SetActive(false);
            return;
        }

        _button = GetComponent<Button>();

        if (cooldownFill != null) {
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.raycastTarget = false;
        }
    }

    private void Update()
    {
        var dash = PlayerDash.Active;
        _button.interactable = dash != null && dash.CanDash;

        if (cooldownFill != null)
            cooldownFill.fillAmount = dash != null ? dash.CooldownNormalized : 0f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        var dash = PlayerDash.Active;
        if (dash != null && _button.interactable)
            dash.TryDash();
    }
}
