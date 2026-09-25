using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A real planar mirror (like the web build's): for each player camera that's close and can see it, a reflection
// camera renders the scene from the reflected viewpoint into a texture that the mirror shader shows. Your own
// body is drawn in reflections even in first person (see PlayerLayers). Mirrors are built at runtime from their
// size; Tools > Scary Street > Set Up Mirrors places the web build's three.
public class Mirror : MonoBehaviour
{
    public Vector2 size = new Vector2(0.6f, 0.72f);
    public float maxDistance = 10f;
    [Tooltip("Longest side of the reflection texture, in pixels.")]
    public int resolution = 1024;
    public Color frameColor = new Color(0.17f, 0.17f, 0.18f);

    class View { public Camera cam; public RenderTexture rt; }
    readonly Dictionary<Camera, View> views = new Dictionary<Camera, View>();
    Renderer surface;
    Material mat;

    void Awake()
    {
        var shader = Shader.Find("ScaryStreet/Mirror");
        if (!shader) { Debug.LogWarning("Mirror: shader ScaryStreet/Mirror not found.", this); enabled = false; return; }

        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(frame.GetComponent<Collider>());
        frame.name = "Frame";
        frame.transform.SetParent(transform, false);
        frame.transform.localPosition = new Vector3(0, 0, -0.018f);
        frame.transform.localScale = new Vector3(size.x + 0.08f, size.y + 0.08f, 0.03f);
        frame.GetComponent<Renderer>().material.color = frameColor;

        var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(glass.GetComponent<Collider>());
        glass.name = "Glass";
        glass.layer = PlayerLayers.Mirror;
        glass.transform.SetParent(transform, false);
        glass.transform.localPosition = new Vector3(0, 0, 0.004f);
        glass.transform.localRotation = Quaternion.Euler(0, 180f, 0);   // a Quad faces -Z; turn it to face the room (+Z)
        glass.transform.localScale = new Vector3(size.x, size.y, 1f);
        surface = glass.GetComponent<Renderer>();
        surface.shadowCastingMode = ShadowCastingMode.Off;
        mat = new Material(shader);
        surface.sharedMaterial = mat;
    }

    void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCamera;

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        foreach (var v in views.Values) if (v.cam) v.cam.enabled = false;
    }

    void OnDestroy()
    {
        foreach (var v in views.Values) { if (v.cam) Destroy(v.cam.gameObject); if (v.rt) { v.rt.Release(); Destroy(v.rt); } }
        if (mat) Destroy(mat);
    }

    // Each camera sees its own reflection; reflection cameras (and anyone too far away) see a dark glass.
    void BeforeCamera(ScriptableRenderContext ctx, Camera camera)
    {
        if (!mat) return;
        mat.SetTexture("_ReflectionTex", views.TryGetValue(camera, out var v) && v.cam && v.cam.enabled ? v.rt : Texture2D.blackTexture);
    }

    void LateUpdate()
    {
        if (!surface) return;
        Vector3 n = transform.forward, p = transform.position;
        foreach (var player in Players.All)
        {
            var cam = player ? player.GetComponentInChildren<Camera>() : null;
            if (!cam) continue;
            if (!views.TryGetValue(cam, out var view)) views[cam] = view = MakeView(cam);

            Vector3 eye = cam.transform.position;
            bool active = cam.enabled && Vector3.Distance(eye, p) < maxDistance && Vector3.Dot(eye - p, n) > 0.01f
                          && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), surface.bounds);
            view.cam.enabled = active;
            if (!active) continue;

            // texture sized like the player's view
            float aspect = cam.pixelWidth / (float)Mathf.Max(1, cam.pixelHeight);
            int w = aspect >= 1 ? resolution : Mathf.RoundToInt(resolution * aspect), h = aspect >= 1 ? Mathf.RoundToInt(resolution / aspect) : resolution;
            if (!view.rt || view.rt.width != w || view.rt.height != h)
            {
                if (view.rt) { view.rt.Release(); Destroy(view.rt); }
                view.rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "MirrorReflection", antiAliasing = 2 };
                view.cam.targetTexture = view.rt;
            }

            // reflect the viewer across the mirror plane
            var plane = new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, p));
            var reflect = Reflection(plane);
            view.cam.transform.SetPositionAndRotation(reflect.MultiplyPoint(eye), cam.transform.rotation);
            view.cam.fieldOfView = cam.fieldOfView;
            view.cam.nearClipPlane = cam.nearClipPlane; view.cam.farClipPlane = cam.farClipPlane;
            view.cam.worldToCameraMatrix = cam.worldToCameraMatrix * reflect;
            // clip everything behind the glass, then flip x so triangles keep their winding (the shader flips it back)
            var clip = CameraSpacePlane(view.cam.worldToCameraMatrix, p, n, 0.02f);
            view.cam.projectionMatrix = Matrix4x4.Scale(new Vector3(-1, 1, 1)) * cam.CalculateObliqueMatrix(clip);
            view.cam.depth = cam.depth - 1;
        }
    }

    View MakeView(Camera player)
    {
        var go = new GameObject($"MirrorCam ({name})");
        go.transform.SetParent(transform, false);
        var c = go.AddComponent<Camera>();
        c.enabled = false;
        c.cullingMask = PlayerLayers.MirrorMask;
        c.clearFlags = player.clearFlags; c.backgroundColor = player.backgroundColor;
        c.allowMSAA = false;
        var data = c.GetUniversalAdditionalCameraData();
        data.renderShadows = false;
        data.renderPostProcessing = false;
        data.requiresDepthTexture = false; data.requiresColorTexture = false;
        return new View { cam = c };
    }

    static Matrix4x4 Reflection(Vector4 pl)
    {
        var m = Matrix4x4.identity;
        m.m00 = 1 - 2 * pl.x * pl.x; m.m01 = -2 * pl.x * pl.y; m.m02 = -2 * pl.x * pl.z; m.m03 = -2 * pl.w * pl.x;
        m.m10 = -2 * pl.y * pl.x; m.m11 = 1 - 2 * pl.y * pl.y; m.m12 = -2 * pl.y * pl.z; m.m13 = -2 * pl.w * pl.y;
        m.m20 = -2 * pl.z * pl.x; m.m21 = -2 * pl.z * pl.y; m.m22 = 1 - 2 * pl.z * pl.z; m.m23 = -2 * pl.w * pl.z;
        return m;
    }

    static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, Vector3 pos, Vector3 normal, float offset)
    {
        Vector3 cp = worldToCamera.MultiplyPoint(pos + normal * offset);
        Vector3 cn = worldToCamera.MultiplyVector(normal).normalized;
        return new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn));
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.6f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.3f);
    }
}
