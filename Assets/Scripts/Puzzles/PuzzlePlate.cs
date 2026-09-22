using UnityEngine;

public class PuzzlePlate : MonoBehaviour
{
    [SerializeField] private SpriteRenderer plateRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pressedColor = Color.green;

    public Vector3Int Cell { get; private set; }
    public bool IsPressed { get; private set; }

    public void Initialize(Vector3Int cell)
    {
        Cell = cell;
        SetPressed(false);
    }

    public void SetPressed(bool pressed)
    {
        IsPressed = pressed;

        if (plateRenderer != null)
        {
            plateRenderer.color =
                pressed ? pressedColor : normalColor;
        }
    }
}