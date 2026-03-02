using UnityEngine;

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

    private ModuleInstance draggingModule;
    private Transform draggingTf;
    private Vector3 dragOffset;
    private int dragRot90;
    private bool pickedFromShip;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!shipRoot) shipRoot = transform;
        if (!shipStats) shipStats = shipRoot.GetComponent<ShipStats>();
    }

    void Update()
    {
        if (PressedThisFrame())
            TryPick();

        if (draggingTf != null)
        {
            DragMove();

            if (Input.GetKeyDown(rotateKey))
            {
                dragRot90 = (dragRot90 + 1) % 4;
                draggingTf.rotation = Quaternion.Euler(0, 0, -90f * dragRot90);
            }

            if (ReleasedThisFrame())
                DropAttach();
        }
    }

    void TryPick()
    {
        var world = MouseWorld();

        var col = Physics2D.OverlapPoint(world, pickMask);
        if (!col)
        {
            Debug.Log($"[ShipBuilder] Pick miss. world={world} mask={pickMask.value}");
            return;
        }

        var mi = col.GetComponentInParent<ModuleInstance>();
        if (!mi)
        {
            Debug.Log($"[ShipBuilder] Collider hit but no ModuleInstance. collider={col.name}");
            return;
        }

        // 이미 붙어있던 모듈도 다시 이동 가능하게
        pickedFromShip = mi.transform.IsChildOf(shipRoot);

        draggingModule = mi;
        draggingTf = mi.transform;
        dragOffset = draggingTf.position - world;

        var att = draggingTf.GetComponent<ModuleAttachment>();
        dragRot90 = att ? att.rot90 : 0;

        // 드래그 시작: 물리 영향 제거(중요)
        SetModulePhysics(draggingTf, attachedToShip: false);
        SetDragCollider(draggingTf, true);

        // 붙어있던 모듈이면, 부모를 잠깐 풀어서 자유롭게 이동하게
        if (pickedFromShip)
        {
            draggingTf.SetParent(null, true);
            if (shipStats) shipStats.Rebuild(); // 떼면 스탯 감소(원하면 제거 가능)
        }

        Debug.Log($"[ShipBuilder] Pick OK: {draggingTf.name} (fromShip={pickedFromShip})");
    }

    void DragMove()
    {
        draggingTf.position = MouseWorld() + dragOffset;

        var grid = WorldToGrid(draggingTf.position);
        var snapped = GridToWorld(grid);

        if (Vector3.Distance(snapped, draggingTf.position) <= snapRadius)
            draggingTf.position = snapped;
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

        // ★ 핵심: 붙는 순간 모듈 Rigidbody2D 시뮬레이션을 끈다 (떨어지는 현상 방지)
        SetDragCollider(draggingTf, false);
        SetModulePhysics(draggingTf, attachedToShip: true);

        if (shipStats) shipStats.Rebuild();

        Debug.Log($"[ShipBuilder] Attached: {draggingTf.name} grid={grid} totalThrust={shipStats?.totalThrust}");

        draggingModule = null;
        draggingTf = null;
        pickedFromShip = false;
    }

    void SetModulePhysics(Transform moduleTf, bool attachedToShip)
    {
        var rb = moduleTf.GetComponent<Rigidbody2D>();
        if (!rb) return;

        // 속도/회전 속도 초기화 (안 하면 '미끄러져 나감'이 남음)
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (attachedToShip)
        {
            // 배에 붙으면 개별 물리 시뮬레이션을 꺼서 "배의 일부"가 되게 함
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = false;
        }
        else
        {
            // 우주에 떠있을 때는 필요에 따라 켜도 됨
            // 드래그 중엔 안정적으로 움직이도록 Kinematic + simulated true 권장
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