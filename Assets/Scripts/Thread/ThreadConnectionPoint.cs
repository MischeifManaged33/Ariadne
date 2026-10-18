using UnityEngine;
using UnityEngine.Events;

public class ThreadConnectionPoint : MonoBehaviour
{
    [Header("Connection")]
    [Tooltip(
        "Optional child marker at the exact knot position. " +
        "Defaults to this Transform."
    )]
    [SerializeField] private Transform tiePosition;

    [SerializeField, Min(0.01f)]
    private float interactionRadius = 0.75f;

    [Tooltip("Optional sprite/VFX object shown while tied.")]
    [SerializeField] private GameObject tiedVisual;

    [Header("Events")]
    [SerializeField] private UnityEvent onTied = new UnityEvent();
    [SerializeField] private UnityEvent onUntied = new UnityEvent();

    public Vector3 Position =>
        tiePosition != null
            ? tiePosition.position
            : transform.position;

    public bool IsTied => Owner != null;

    public AriadneThread Owner { get; private set; }

    private void OnEnable()
    {
        UpdateVisual();
    }

    public static ThreadConnectionPoint FindNearestAvailable(
        Vector3 playerPosition)
    {
        ThreadConnectionPoint nearest = null;
        float bestDistance = float.PositiveInfinity;

        foreach (ThreadConnectionPoint point in
                 Object.FindObjectsByType<ThreadConnectionPoint>(
                     FindObjectsSortMode.None))
        {
            if (!point.isActiveAndEnabled || point.IsTied)
                continue;

            float distance =
                ((Vector2)(point.Position - playerPosition))
                .sqrMagnitude;

            if (distance >
                point.interactionRadius * point.interactionRadius ||
                distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            nearest = point;
        }

        return nearest;
    }

    public bool IsInRange(Vector3 playerPosition)
    {
        return Vector2.Distance(playerPosition, Position)
            <= interactionRadius;
    }

    public bool TryAttach(AriadneThread thread)
    {
        if (thread == null || !isActiveAndEnabled || IsTied)
            return false;

        Owner = thread;

        UpdateVisual();
        onTied?.Invoke();

        return Owner == thread && isActiveAndEnabled;
    }

    public void Detach(AriadneThread thread)
    {
        if (Owner != thread)
            return;

        Owner = null;

        UpdateVisual();
        onUntied?.Invoke();
    }

    private void OnDisable()
    {
        if (Owner == null)
            return;

        AriadneThread previousOwner = Owner;
        Owner = null;

        previousOwner.ForgetConnection(this);

        UpdateVisual();
        onUntied?.Invoke();
    }

    private void UpdateVisual()
    {
        // Only toggle a separate visual object.
        if (tiedVisual != null &&
            tiedVisual != gameObject &&
            !transform.IsChildOf(tiedVisual.transform))
        {
            tiedVisual.SetActive(IsTied);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsTied ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(Position, interactionRadius);
    }
}