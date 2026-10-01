using System.Collections;
using UnityEngine;

// For all real world buttons, made it for future puzzles maybe perchance
[RequireComponent(typeof(Collider2D))]
public abstract class InteractableButton : MonoBehaviour, IInteractable
{
    [Header("Press Animation")]
    [SerializeField] private Transform buttonVisual;
    [SerializeField] private float pressDistance = 0.1f;
    [SerializeField] private float pressDuration = 0.15f;

    private bool isAnimating;

    public virtual bool CanInteract => isActiveAndEnabled && !isAnimating;

    protected virtual void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    public void Interact(Player player)
    {
        if (!CanInteract)
            return;

        OnPressed(player);

        if (buttonVisual != null)
            StartCoroutine(AnimateButton());
    }

    protected abstract void OnPressed(Player player);

    private IEnumerator AnimateButton()
    {
        isAnimating = true;

        Vector3 startingPosition = buttonVisual.localPosition;
        Vector3 pressedPosition =
            startingPosition + Vector3.down * pressDistance;

        float halfDuration = pressDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            buttonVisual.localPosition = Vector3.Lerp(
                startingPosition,
                pressedPosition,
                elapsed / halfDuration
            );

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            buttonVisual.localPosition = Vector3.Lerp(
                pressedPosition,
                startingPosition,
                elapsed / halfDuration
            );

            yield return null;
        }

        buttonVisual.localPosition = startingPosition;
        isAnimating = false;
    }
}
