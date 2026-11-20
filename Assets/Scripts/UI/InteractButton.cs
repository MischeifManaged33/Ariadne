using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// On screen interact button: tap to interact, hold to rewind the thread
[RequireComponent(typeof(Button))]
public class InteractButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField]
    private bool mobileOnly = true;

    private Button _button;
    private AriadneThread _thread;
    private bool _holding;

    private void Awake()
    {
        if (mobileOnly && !Application.isMobilePlatform) {
            gameObject.SetActive(false);
            return;
        }

        _button = GetComponent<Button>();
    }

    private void OnDisable() => Release();

    private void Update()
    {
        var interactor = PlayerInteractor.Active;
        var thread = ResolveThread();

        // Usable when there is something to open, or thread laid out to rewind
        _button.interactable = _holding ||
            (interactor != null && interactor.Target != null) ||
            (thread != null && thread.UsedThread > 0f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_holding || !_button.interactable)
            return;

        _holding = true;

        var interactor = PlayerInteractor.Active;
        if (interactor != null)
            interactor.Interact();

        var thread = ResolveThread();
        if (thread != null)
            thread.PressInteract();
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    private void Release()
    {
        if (!_holding)
            return;

        _holding = false;

        if (_thread != null)
            _thread.ReleaseInteract();
    }

    private AriadneThread ResolveThread()
    {
        if (_thread == null)
            _thread = FindAnyObjectByType<AriadneThread>();

        return _thread;
    }
}
