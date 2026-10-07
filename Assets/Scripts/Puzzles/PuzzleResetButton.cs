using UnityEngine;

public class PuzzleResetButton : InteractableButton
{
    [Header("References")]
    [SerializeField] private PuzzleManager puzzleManager;

    public override bool CanInteract => base.CanInteract && puzzleManager != null;

    protected override void OnPressed(Player player)
    {
        puzzleManager.ResetPuzzle();
    }
}
