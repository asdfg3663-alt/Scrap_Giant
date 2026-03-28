using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = Vector2.zero;

    [Header("Follow Feel")]
    public float smoothTime = 0.18f;
    public float maxSpeed = 999f;

    [Header("Zoom")]
    public float baselineViewMultiplier = 2f;
    public float minimumAutoSize = 10f;
    public float scrollZoomSpeed = 0.12f;
    public float pinchZoomSensitivity = 0.005f;
    public float minZoomMultiplier = 0.5f;
    public float maxZoomMultiplier = 2f;
    public float zoomSmoothTime = 0.15f;
    public float moduleCountMaxForZoom = 100f;
    public float moduleCountBaseThreshold = 10f;
    public float maxAutoWidthMultiplier = 2f;
    public float backgroundOverscan = 1.35f;

    Vector3 followVelocity;
    Camera cam;
    ShipStats trackedShip;
    float baselineOrthoSize;
    float userZoomMultiplier = 1f;
    float zoomVelocity;
    SpriteRenderer backgroundRenderer;
    Vector3 backgroundBaseScale = Vector3.one;
    bool backgroundCached;

    void Awake()
    {
        cam = GetComponent<Camera>();
        baselineOrthoSize = Mathf.Max(minimumAutoSize, cam.orthographicSize * baselineViewMultiplier);
    }

    void LateUpdate()
    {
        ResolveTarget();
        HandleZoomInput();

        if (target == null)
            return;

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref followVelocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime);

        float autoSize = ComputeAutoSize();
        float desiredSize = Mathf.Clamp(autoSize * userZoomMultiplier, autoSize * minZoomMultiplier, autoSize * maxZoomMultiplier);
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, desiredSize, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.deltaTime);
        FitBackgroundToViewport();
    }

    void ResolveTarget()
    {
        if (trackedShip == null)
        {
            if (target != null)
                trackedShip = target.GetComponentInParent<ShipStats>() ?? target.GetComponent<ShipStats>();

            if (trackedShip == null && WorldSpawnDirector.PlayerTransform != null)
                trackedShip = WorldSpawnDirector.PlayerTransform.GetComponent<ShipStats>();
        }

        if (trackedShip == null)
            return;

        target = trackedShip.GetCoreTransform();
    }

    float ComputeAutoSize()
    {
        if (trackedShip == null)
            return baselineOrthoSize;

        float moduleCount = Mathf.Max(1f, trackedShip.GetInstalledModuleCount());
        float normalized = Mathf.InverseLerp(moduleCountBaseThreshold, moduleCountMaxForZoom, moduleCount);
        float widthMultiplier = Mathf.Lerp(1f, maxAutoWidthMultiplier, normalized);
        return Mathf.Max(minimumAutoSize, baselineOrthoSize * widthMultiplier);
    }

    void HandleZoomInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            userZoomMultiplier *= 1f - scroll * scrollZoomSpeed;
            userZoomMultiplier = Mathf.Clamp(userZoomMultiplier, minZoomMultiplier, maxZoomMultiplier);
        }

        if (!TryGetPinchTouches(out Touch touch0, out Touch touch1))
            return;
        Vector2 previous0 = touch0.position - touch0.deltaPosition;
        Vector2 previous1 = touch1.position - touch1.deltaPosition;

        float previousDistance = Vector2.Distance(previous0, previous1);
        float currentDistance = Vector2.Distance(touch0.position, touch1.position);
        if (previousDistance <= 0.001f || currentDistance <= 0.001f)
            return;

        float pinchRatio = previousDistance / currentDistance;
        float pinchDelta = Mathf.Lerp(1f, pinchRatio, pinchZoomSensitivity * 100f);
        userZoomMultiplier = Mathf.Clamp(userZoomMultiplier * pinchDelta, minZoomMultiplier, maxZoomMultiplier);
    }

    bool TryGetPinchTouches(out Touch touch0, out Touch touch1)
    {
        touch0 = default;
        touch1 = default;

        int found = 0;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                continue;

            if (found == 0)
                touch0 = touch;
            else if (found == 1)
                touch1 = touch;

            found++;
            if (found >= 2)
                return true;
        }

        return false;
    }

    void FitBackgroundToViewport()
    {
        if (cam == null)
            return;

        if (!backgroundCached)
        {
            Transform backgroundTransform = transform.Find("Backgrounds");
            if (backgroundTransform != null)
            {
                backgroundRenderer = backgroundTransform.GetComponent<SpriteRenderer>();
                backgroundBaseScale = backgroundTransform.localScale;
            }

            backgroundCached = true;
        }

        if (backgroundRenderer == null || backgroundRenderer.sprite == null)
            return;

        float viewportHeight = cam.orthographicSize * 2f * backgroundOverscan;
        float viewportWidth = viewportHeight * cam.aspect;

        Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f)
            return;

        float scale = Mathf.Max(viewportWidth / spriteSize.x, viewportHeight / spriteSize.y);
        backgroundRenderer.transform.localScale = new Vector3(
            backgroundBaseScale.x * scale,
            backgroundBaseScale.y * scale,
            backgroundBaseScale.z);
    }
}
