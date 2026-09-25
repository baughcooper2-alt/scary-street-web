#if UNITY_EDITOR   // lives outside an Editor folder, so keep it out of player builds
using UnityEditor;
using UnityEngine;

// Menu: Tools > Scary Street > Add Colliders To Selection
// Gives every mesh under the selected object a MeshCollider so the player can walk on floors and bump into walls.
public static class ScaryStreetTools
{
    [MenuItem("Tools/Scary Street/Add Colliders To Selection")]
    static void AddColliders()
    {
        int added = 0;
        foreach (var root in Selection.gameObjects)
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                added++;
            }
        Debug.Log($"Scary Street: added {added} mesh colliders.");
        EditorUtility.DisplayDialog("Scary Street", $"Added {added} colliders.", "OK");
    }

    [MenuItem("Tools/Scary Street/Add Colliders To Selection", true)]
    static bool Validate() => Selection.gameObjects.Length > 0;
}
#endif
