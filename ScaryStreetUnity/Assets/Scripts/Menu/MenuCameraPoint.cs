using UnityEngine;

// Where the title-screen camera sits (the front of the house). Set it with
// Tools > Scary Street > Set Menu Camera From Scene View.
public class MenuCameraPoint : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.95f, 0.76f, 0.19f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawFrustum(Vector3.zero, 50f, 3f, 0.3f, 16f / 9f);
    }
}
