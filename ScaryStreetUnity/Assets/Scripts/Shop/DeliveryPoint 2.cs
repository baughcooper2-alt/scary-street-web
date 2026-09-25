using UnityEngine;

// Optional: where the DoorDash driver starts (Street) and where they wait for you (Porch).
// Without these, DoorDashCourier uses the web build's front path.
public class DeliveryPoint : MonoBehaviour
{
    public enum Role { Street, Porch }
    public Role role;

    void OnDrawGizmos()
    {
        Gizmos.color = role == Role.Porch ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.6f, 0.5f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
    }
}
