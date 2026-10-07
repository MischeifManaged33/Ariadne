public interface IInteractable
{
    // Interface for interaction
    bool CanInteract { get; }
    void Interact(Player player);
}
