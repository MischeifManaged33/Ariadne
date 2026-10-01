using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Interact w the closes interactable obj in a radius
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Player player;

    [Header("Detection")]
    [SerializeField, Min(0f)]
    private float radius = 1f;
    [SerializeField]
    private LayerMask interactableLayers = ~0;

    public static PlayerInteractor Active { get; private set; }

    public IInteractable Target { get; private set; }

    private InputAction _interactAction;
    private ContactFilter2D _filter;
    private readonly List<Collider2D> _hits = new();

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();

        _interactAction = new InputAction("Interact", InputActionType.Button);
        _interactAction.AddBinding("<Keyboard>/e");
        _interactAction.AddBinding("<Gamepad>/buttonSouth");

        // Interactables usually sit on trigger colliders
        _filter = new ContactFilter2D { useTriggers = true };
        _filter.SetLayerMask(interactableLayers);
    }

    private void OnEnable()
    {
        _interactAction.Enable();
        Active = this;
    }

    private void OnDisable()
    {
        _interactAction.Disable();
        Target = null;

        if (Active == this)
            Active = null;
    }

    private void OnDestroy() => _interactAction?.Dispose();

    private void Update()
    {
        if (PauseMenu.IsPaused)
            return;

        Target = FindClosest();

        if (_interactAction.WasPressedThisFrame())
            Interact();
    }

    public bool Interact()
    {
        if (PauseMenu.IsPaused || Target == null || !Target.CanInteract)
            return false;

        Target.Interact(player);
        return true;
    }

    private IInteractable FindClosest()
    {
        Vector2 origin = transform.position;
        var count = Physics2D.OverlapCircle(origin, radius, _filter, _hits);

        IInteractable closest = null;
        var closestDistance = float.MaxValue;

        for (var i = 0; i < count; i++) {
            var hit = _hits[i];
            var candidate = hit.GetComponentInParent<IInteractable>();

            if (candidate == null || !candidate.CanInteract)
                continue;

            var distance = (hit.ClosestPoint(origin) - origin).sqrMagnitude;
            if (distance >= closestDistance)
                continue;

            closest = candidate;
            closestDistance = distance;
        }

        return closest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
