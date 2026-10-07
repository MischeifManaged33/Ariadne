using UnityEngine;
using UnityEngine.UI;

// On screen interact button
[RequireComponent(typeof(Button))]
public class InteractButton : MonoBehaviour
{
    [SerializeField]
    private bool mobileOnly = true;

    private Button _button;

    private void Awake()
    {
        if (mobileOnly && !Application.isMobilePlatform) {
            gameObject.SetActive(false);
            return;
        }

        _button = GetComponent<Button>();
        _button.onClick.AddListener(Press);
    }

    private void Update()
    {
        var interactor = PlayerInteractor.Active;
        _button.interactable = interactor != null && interactor.Target != null;
    }

    private void Press()
    {
        var interactor = PlayerInteractor.Active;
        if (interactor != null)
            interactor.Interact();
    }
}
