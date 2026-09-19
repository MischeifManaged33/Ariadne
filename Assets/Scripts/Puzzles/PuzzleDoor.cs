using UnityEngine;

public class PuzzleDoor : MonoBehaviour
{
    [SerializeField]
    private Collider2D doorCollider;
    [SerializeField]
    private SpriteRenderer doorRenderer;

    private bool isOpen;

    public void Open()
    {
        if (isOpen)
            return;

        isOpen = true;

        doorCollider.enabled = false;
        doorRenderer.enabled = false;
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;

        doorCollider.enabled = true;
        doorRenderer.enabled = true;
    }
}
