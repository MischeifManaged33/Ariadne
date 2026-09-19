using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class PressurePlate : MonoBehaviour
{
    [SerializeField]
    private PuzzleManager puzzleManager;
    [SerializeField]
    private SpriteRenderer plateRenderer;
    [SerializeField]
    private Color unpressedColor = Color.red;
    [SerializeField]
    private Color pressedColor = Color.green;

    private readonly HashSet<PushableBlock> blocksOnPlate = new();
    private bool isPressed;

    public bool IsPressed => isPressed;

    void Start()
    {
        puzzleManager.RegisterPlate(this);
        UpdateAppearance();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PushableBlock block = other.GetComponentInParent<PushableBlock>();

        if (block == null)
            return;

        blocksOnPlate.Add(block);
        UpdatePressedState();
    }

    private void OnTriggerExit2D (Collider2D other)
    {
        PushableBlock block = other.GetComponentInParent<PushableBlock>();

        if (block == null)
            return;

        blocksOnPlate.Remove(block);
        UpdatePressedState();
    }

    private void UpdatePressedState()
    {
        bool newPressedState = blocksOnPlate.Count > 0;

        if (newPressedState == isPressed)
            return;

        isPressed = newPressedState;
        UpdateAppearance();
        puzzleManager.CheckPuzzle();
    }

    private void UpdateAppearance ()
    {
        if (plateRenderer != null)
        {
            plateRenderer.color = isPressed ? pressedColor : unpressedColor;
        }
    }
}
