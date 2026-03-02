using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ShipBuilder : MonoBehaviour
{
    [Header("Refs")]
    public Transform shipRoot;          // PlayerShip
    public ShipStats shipStats;         // Rebuild()
    public Camera cam;

    [Header("Grid")]
    public float cellSize = 1f;
    public float snapRadius = 0.6f;
    public LayerMask pickMask;          // ShipModule 체크

    [Header("Input")]
    public KeyCode rotateKey = KeyCode.R;

    [Header("Drag Rules")]
    public float longPress = 0.25f;         // ★ 꾹 누르기 시간
    public float dragStartDistance = 0.15f; // ★ 이만큼 움직여야 드래그 시작(클릭 오동작 방지)

    private ModuleInstance candidateModule;  // 마우스 다운 시 후보
    private Transform draggingTf;            // 실제 드래그 대상
    private Vector3 dragOffset;
    private int dragRot90;
    private bool pickedFromShip;

    private float pressT;
    private Vector2 pressWorld;
    private bool dragStarted;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!shipRoot) shipRoot = transform;
        if (!shipStats) shipStats = shipRoot.GetComponent<ShipStats>();
    }

    void Update()
    {
        // UI 위에서는 빌더 입력 무시 (UI 얽힘 방지)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (PressedThisFrame())
            BeginPress();

        if (candidateModule != null && Holding())
            HoldToStartDrag();

        if (draggingTf != null)
        {
            DragMove();

            if (Input.GetKeyDown(rotateKey))
            {
                dragRot90 = (dragRot90 + 1) % 4;
                draggingTf.rotation = Quaternion.Euler(0, 0, -90f * dragRot90);
            }

            if (ReleasedThisFrame())
                EndDragDrop();
        }

        // 클릭만 하고 뗀 경우: 아무 일도 안 함(Attach 금지)
        if (candidateModule != null && ReleasedThisFrame() && !dragStarted)
        {
            candidateModule = null;
        }
    }

    void BeginPress()
    {
        pressT = 0f;
        dragStarted = false;

        pressWorld = MouseWorld();

        var col = Physics2D.OverlapPoint(pressWorld, pickMask);
        if (!col)
        {
            candidateModule = null;
            return;
        }

        var mi = col.GetComponentInParent<ModuleInstance>();
        if (!mi)
        {
            candidateModule = null;
            return;
        }

        candidateModule = mi;
    }

    void HoldToStartDrag()
    {
        pressT += Time.deltaTime;

        Vector2 now = MouseWorld();
        bool movedEnough = Vector2.Distance(now, pressWorld) >= dragStartDistance;

        // ★ 롱프레스 + 이동이 만족될 때만 드래그 시작
        if (!dragStarted && pressT >= longPress && movedEnough)
        {
            StartDragging(candidateModule, now);
            candidateModule = null;
            dragStarted = true;
        }
    }

    void StartDragging(ModuleInstance mi, Vector2 worldNow)
    {
        pickedFromShip = mi.transform.IsChildOf(shipRoot);

        draggingTf = mi.transform;
        dragOffset = draggingTf.position - (Vector3)worldNow;

        var att = draggingTf.GetComponent<ModuleAttachment>();
        dragRot90 = att ? att.rot90 : 0;

        // 드래그 시작: 물리 영향 제거(중요)
        SetModulePhysics(draggingTf, attachedToShip: false);
        SetDragCollider(draggingTf, true);

        // ★ 드래그가 시작된 경우에만 "분리"
        if (pickedFromShip)
        {
            draggingTf.SetParent(null, true);
            if (shipStats) shipStats.Rebuild();
        }
    }

    void DragMove()
    {
        draggingTf.position = MouseWorld() + dragOffset;

        var grid = WorldToGrid(draggingTf.position);
        var snapped = GridToWorld(grid);

        if (Vector3.Distance(snapped, draggingTf.position) <= snapRadius)
            draggingTf.position = snapped;
    }

    void EndDragDrop()
    {
        // 드래그 시작이 안 된 상태면 여기로 올 수 없음(방어)
        if (draggingTf == null) return;

        DropAttach();

        draggingTf = null;
        pickedFromShip = false;
        dragStarted = false;
    }

    void DropAttach()
    {
        var grid = WorldToGrid(draggingTf.position);

        // 부착
        draggingTf.SetParent(shipRoot, true);
        draggingTf.localPosition = new Vector3(grid.x * cellSize, grid.y * cellSize, 0f);
        draggingTf.localRotation = Quaternion.Euler(0, 0, -90f * dragRot90);

        var att = draggingTf.GetComponent<ModuleAttachment>();
        if (!att) att = draggingTf.gameObject.AddComponent<ModuleAttachment>();
        att.shipRoot = shipRoot;
        att.gridPos = grid;
        att.rot90 = dragRot90;

        // 붙는 순간 Rigidbody2D 시뮬레이션을 끈다 (떨어지는 현상 방지)
        SetDragCollider(draggingTf, false);
        SetModulePhysics(draggingTf, attachedToShip: true);

        if (shipStats) shipStats.Rebuild();
    }

    void SetModulePhysics(Transform moduleTf, bool attachedToShip)
    {
        var rb = moduleTf.GetComponent<Rigidbody2D>();
        if (!rb) return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (attachedToShip)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = false;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
        }
    }

    Vector3 MouseWorld()
    {
        var p = ReadMousePosition();
        p.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(p);
    }

    Vector3 ReadMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }

    bool PressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool ReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }

    bool Holding()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    Vector2Int WorldToGrid(Vector3 world)
    {
        var local = shipRoot.InverseTransformPoint(world);
        return new Vector2Int(
            Mathf.RoundToInt(local.x / cellSize),
            Mathf.RoundToInt(local.y / cellSize)
        );
    }

    Vector3 GridToWorld(Vector2Int grid)
    {
        var local = new Vector3(grid.x * cellSize, grid.y * cellSize, 0f);
        return shipRoot.TransformPoint(local);
    }

    void SetDragCollider(Transform t, bool drag)
    {
        var col = t.GetComponentInChildren<Collider2D>();
        if (col) col.isTrigger = drag; // 드래그 중 겹침 허용(임시)
    }
}