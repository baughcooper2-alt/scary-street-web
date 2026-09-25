using UnityEngine;

// Anything the player can use with F (gamepad X) while looking at it: doors, the DoorDash courier...
public interface IInteractable
{
    string Prompt { get; }               // shown as "F  <prompt>"
    bool CanInteract { get; }
    void Interact(GameObject player);
}
