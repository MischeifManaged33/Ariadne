using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class PuzzleResetButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PuzzleManager puzzleManager;
    [SerializeField] private Transform buttonVisual;

    [Header("Interaction")]
    [SerializeField] private float pressDistance = 0.1f;
    [SerializeField] private float pressDuration = 0.15f;

    private InputAction interactAction;
    private bool playerNearby;
    private bool isAnimating;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;

        interactAction = new InputAction(
            "Interact",
            InputActionType.Button
        );

        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonSouth");
    }

    private void OnEnable()
    {
        interactAction.Enable();
    }

    private void OnDisable()
    {
        interactAction.Disable();
    }

    private void OnDestroy()
    {
        interactAction?.Dispose();
    }

    private void Update()
    {
        if (!playerNearby || isAnimating)
            return;

        if (!interactAction.WasPressedThisFrame())
            return;

        puzzleManager.ResetPuzzle();

        if (buttonVisual != null)
            StartCoroutine(AnimateButton());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
            playerNearby = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
            playerNearby = false;
    }

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