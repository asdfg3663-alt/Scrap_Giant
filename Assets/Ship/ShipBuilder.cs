using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ShipBuilder
/// - Captain Forever 스타일: "함선에 붙어있는 모든 모듈"의 4방 AttachPoint를 후보로 스냅
/// - 드래그 시작 시(롱프레스 + 이동) 기존 함선에서 분리
/// - 스냅 후보는 그리드(코어 기준)로 강제하여 대각선/오프셋/한쪽만 안 붙는 문제를 줄임
/// - 겹침(OverlapBox) 체크로 중복 부착 방지
/// </summary>
public class ShipBuilder : MonoBehaviour
{
    [Header("Refs")]
    public Transform shipRoot;      // 보통 ShipStats가 붙어있는 루트
    public ShipStats shipStats;
    public Camera cam;

    [Header("Core (recommended)")]
    public Module coreModule;       // 그리드 원점(좌표계)을 고정하기 위함

    [Header("Snap/Grid")]
    public float cellSize = 1f;
    public float snapDistance = 1.2f;
    [Tooltip("OverlapBox 크기(셀 대비 비율). 1x1 모듈이면 0.9~0.98 추천")]
    public float overlapScale = 0.95f;

    [Header("Masks")]
    public LayerMask pickMask;      // 모듈 선택용
    public LayerMask blockMask;     // 겹침 체크용

    [Header("Input")]
    public float longPressTime = 0.25f;
    public float dragStartDistance = 0.15f;

    [Header("Debug")]
    public bool drawDebug = false;

    Transform draggingTf;
    Module draggingMod;
    Collider2D draggingCol;

    bool pointerDown;
    float pressT;
    Vector2 pressWorld;
    Vector2 pointerWorldNow;
    Vector3 dragOffset;

    bool isDragging;
    bool wasAttachedAtDragStart;

    // 그리드 점유(코어 기준)
    readonly Dictionary<Vector2Int, Module> occupied = new();

    struct SnapCandidate
    {
        public bool valid;
        public Vector2Int grid;
        public int rot90;
        public Transform anchorModule;
        public Side anchorSide;

        public SnapCandidate(bool v, Vector2Int g, int r, Transform a, Side s)
        {
            valid = v; grid = g; rot90 = r; anchorModule = a; anchorSide = s;
        }
    }

    void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    void Start()
    {
        // shipRoot 자동 보정
        if (!shipRoot)
        {
            if (shipStats) shipRoot = shipStats.transform;
            else if (coreModule) shipRoot = coreModule.transform;
        }

        RebuildOccupiedMap();
    }

    void Update()
    {
        pointerWorldNow = MouseWorld();

        HandlePointer();
        if (isDragging)
            UpdateDrag();
    }

    void HandlePointer()
    {
        bool down = GetPointerDown();
        bool held = GetPointerHeld();
        bool up = GetPointerUp();

        if (down)
        {
            pointerDown = true;
            pressT = 0f;
            pressWorld = pointerWorldNow;

            draggingTf = TryPickModule(pointerWorldNow);
            draggingMod = draggingTf ? draggingTf.GetComponent<Module>() : null;
            draggingCol = draggingTf ? draggingTf.GetComponentInChildren<Collider2D>() : null;

            if (draggingTf)
            {
                dragOffset = draggingTf.position - (Vector3)pointerWorldNow;
                wasAttachedAtDragStart = IsModuleAttachedToShip(draggingTf);
            }
            else
            {
                wasAttachedAtDragStart = false;
            }
        }

        if (pointerDown && held)
        {
            pressT += Time.deltaTime;

            if (!isDragging && draggingTf)
            {
                float dist = Vector2.Distance(pressWorld, pointerWorldNow);

                // NOTE: 현재 UX = 롱프레스 + 이동 => 드래그 시작 (원하면 dist 조건 제거 가능)
                if (pressT >= longPressTime && dist >= dragStartDistance)
                {
                    isDragging = true;

                    if (wasAttachedAtDragStart)
                        DetachModule(draggingTf);
                }
            }
        }

        if (pointerDown && up)
        {
            if (isDragging)
                TryDrop();

            pointerDown = false;
            isDragging = false;
            draggingTf = null;
            draggingMod = null;
            draggingCol = null;
        }
    }

    void UpdateDrag()
    {
        if (!draggingTf) return;

        draggingTf.position = (Vector3)pointerWorldNow + dragOffset;

        var cand = FindBestSnapCandidate(pointerWorldNow, draggingTf);
        if (cand.valid)
        {
            draggingTf.position = GridToWorld(cand.grid);
            draggingTf.rotation = Quaternion.Euler(0, 0, cand.rot90 * 90f);
        }
    }

    void TryDrop()
    {
        if (!draggingTf) return;

        var cand = FindBestSnapCandidate(pointerWorldNow, draggingTf);
        if (!cand.valid) return;

        // 겹침 체크
        if (IsOccupied(cand.grid)) return;
        if (WouldOverlapAt(cand.grid, cand.rot90, draggingTf)) return;

        // TODO: 여기서 모듈별 룰(엔진은 뒤만, 코어 1개 등)을 추가 가능

        AttachModule(draggingTf, cand.grid, cand.rot90);
        RebuildOccupiedMap();
        if (shipStats) shipStats.Rebuild();
    }

    void AttachModule(Transform moduleTf, Vector2Int grid, int rot90)
    {
        if (!shipRoot) return;

        moduleTf.SetParent(shipRoot, true);
        moduleTf.position = GridToWorld(grid);
        moduleTf.rotation = Quaternion.Euler(0, 0, rot90 * 90f);

        SetModulePhysics(moduleTf, attachedToShip: true);

        // attachment metadata
        var att = moduleTf.GetComponent<ModuleAttachment>();
        if (!att) att = moduleTf.gameObject.AddComponent<ModuleAttachment>();
        att.shipRoot = shipRoot;
        att.gridPos = grid;
        att.rot90 = rot90;

        var mod = moduleTf.GetComponent<Module>();
        if (mod) occupied[grid] = mod;
    }

    void DetachModule(Transform moduleTf)
{
    moduleTf.SetParent(null, true);

    SetModulePhysics(moduleTf, attachedToShip: false);

    var mod = moduleTf.GetComponent<Module>();
    if (mod == null) return;

    var removeKeys = new List<Vector2Int>();
    foreach (var kv in occupied)
        if (kv.Value == mod) removeKeys.Add(kv.Key);

    for (int i = 0; i < removeKeys.Count; i++)
        occupied.Remove(removeKeys[i]);

    // 🔥 이 줄 추가
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
        // 붙어있어도 "픽(OverlapPoint)"이 되도록 simulated는 TRUE 유지
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        // 물리로 흔들리거나 밀리지 않게 고정
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }
    else
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        // 우주에 떠다니는 상태에서는 회전은 허용할지/금지할지 취향
        rb.constraints = RigidbodyConstraints2D.None;
    }
}

    // =========================
    // Snap logic (Captain Forever style)
    // =========================

    SnapCandidate FindBestSnapCandidate(Vector2 pointerWorld, Transform moduleTf)
    {
        if (!shipRoot) return new SnapCandidate(false, default, 0, null, Side.Up);

        var mod = moduleTf.GetComponent<Module>();
        if (!mod) return new SnapCandidate(false, default, 0, null, Side.Up);

        // 1) 함선에 붙은 모든 모듈 수집
        var anchors = shipRoot.GetComponentsInChildren<Module>(true);
        if (anchors == null || anchors.Length == 0)
            return new SnapCandidate(false, default, 0, null, Side.Up);

        float bestDist = float.MaxValue;
        SnapCandidate best = new SnapCandidate(false, default, 0, null, Side.Up);

        for (int i = 0; i < anchors.Length; i++)
        {
            var a = anchors[i];
            if (!a) continue;
            if (a.transform == moduleTf) continue; // 자기 자신은 제외

            // anchor의 그리드 좌표(코어 기준)
            Vector2Int aGrid = WorldToGrid(a.transform.position);

            // 4방 사이드 반복
            TrySide(a, Side.Up, aGrid);
            TrySide(a, Side.Down, aGrid);
            TrySide(a, Side.Left, aGrid);
            TrySide(a, Side.Right, aGrid);
        }

        return best;

        void TrySide(Module anchor, Side anchorSide, Vector2Int aGrid)
        {
            Transform ap = anchor.GetAttachPoint(anchorSide);
            if (!ap) return;

            // anchorSide의 월드 방향(90도 회전 고려)
            Vector2 normalWorld = (Vector2)anchor.transform.TransformDirection(Module.SideToDir(anchorSide));

            // 그리드 델타로 강제(대각선 방지)
            Vector2Int delta = WorldDirToGridDelta(normalWorld);
            Vector2Int targetGrid = aGrid + delta;

            Vector3 targetWorld = GridToWorld(targetGrid);

            float d = Vector2.Distance(pointerWorld, targetWorld);
            if (d > snapDistance) return;

            // 드래그 모듈이 "-normalWorld" 방향을 향하도록 회전(즉 붙는 면이 상대쪽을 보게)
            if (!TryComputeRotation90(mod, -normalWorld, out int rot90))
                return;

            if (IsOccupied(targetGrid)) return;
            if (WouldOverlapAt(targetGrid, rot90, moduleTf)) return;

            if (d < bestDist)
            {
                bestDist = d;
                best = new SnapCandidate(true, targetGrid, rot90, anchor.transform, anchorSide);
            }
        }
    }

    // 원하는 월드 방향을 향하도록 90도 회전값(0~3) 계산
    bool TryComputeRotation90(Module mod, Vector2 desiredWorldDir, out int rot90)
    {
        rot90 = 0;

        // attachableLocalSides 중 하나가 desiredWorldDir에 가장 가깝게 가도록 회전 선택
        if (mod.attachableLocalSides == null || mod.attachableLocalSides.Count == 0)
        {
            rot90 = 0;
            return true;
        }

        float best = -999f;
        int bestRot = 0;

        for (int r = 0; r < 4; r++)
        {
            float score = -999f;

            for (int i = 0; i < mod.attachableLocalSides.Count; i++)
            {
                Vector2 localDir = Module.SideToDir(mod.attachableLocalSides[i]);
                Vector2 rotated = Rotate90(localDir, r);
                float dot = Vector2.Dot(rotated.normalized, desiredWorldDir.normalized);
                if (dot > score) score = dot;
            }

            if (score > best)
            {
                best = score;
                bestRot = r;
            }
        }

        rot90 = bestRot;
        return true;
    }

    Vector2 Rotate90(Vector2 v, int rot90)
    {
        rot90 = ((rot90 % 4) + 4) % 4;
        return rot90 switch
        {
            0 => v,
            1 => new Vector2(-v.y, v.x),
            2 => new Vector2(-v.x, -v.y),
            3 => new Vector2(v.y, -v.x),
            _ => v
        };
    }

    Vector2Int WorldDirToGridDelta(Vector2 worldDir)
    {
        // 가장 큰 축으로 4방향 결정
        if (Mathf.Abs(worldDir.x) >= Mathf.Abs(worldDir.y))
            return new Vector2Int(worldDir.x >= 0 ? 1 : -1, 0);
        else
            return new Vector2Int(0, worldDir.y >= 0 ? 1 : -1);
    }

    // =========================
    // Picking / Overlap
    // =========================

    Transform TryPickModule(Vector2 world)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return null;

        var hit = Physics2D.OverlapPoint(world, pickMask);
        if (!hit) return null;

        // 모듈 루트가 콜라이더 자식일 수도 있으니 Module 컴포넌트 있는 상위로 끌어올림
        var m = hit.GetComponentInParent<Module>();
        return m ? m.transform : hit.transform;
    }

    bool WouldOverlapAt(Vector2Int grid, int rot90, Transform moduleTf)
    {
        Vector3 center = GridToWorld(grid);
        Vector2 size = Vector2.one * cellSize * overlapScale;

        // OverlapBoxAll로 받고, 자기 자신/자식 콜라이더 제외
        var hits = Physics2D.OverlapBoxAll(center, size, rot90 * 90f, blockMask);
        if (hits == null || hits.Length == 0) return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h) continue;

            // 드래그 중인 자신의 콜라이더는 무시
            if (draggingCol != null && h == draggingCol) continue;
            if (moduleTf != null && h.transform.IsChildOf(moduleTf)) continue;

            // 함선에 붙어있는 모듈이랑 겹치면 true
            return true;
        }

        return false;
    }

    // =========================
    // Attached 판단 / Grid
    // =========================

    bool IsModuleAttachedToShip(Transform tf)
    {
        if (!tf) return false;

        if (shipRoot && tf.IsChildOf(shipRoot)) return true;
        if (coreModule && tf.IsChildOf(coreModule.transform)) return true;
        if (shipStats && tf.GetComponentInParent<ShipStats>() == shipStats) return true;

        return false;
    }

    Vector3 GridOrigin()
    {
        // 코어 기준으로 좌표계 고정(함선 커져도 라운딩 흔들림 방지)
        if (coreModule) return coreModule.transform.position;
        if (shipRoot) return shipRoot.position;
        return Vector3.zero;
    }

    Vector2Int WorldToGrid(Vector3 world)
    {
        Vector3 o = GridOrigin();
        float x = (world.x - o.x) / cellSize;
        float y = (world.y - o.y) / cellSize;

        int gx = Mathf.RoundToInt(x);
        int gy = Mathf.RoundToInt(y);
        return new Vector2Int(gx, gy);
    }

    Vector3 GridToWorld(Vector2Int grid)
    {
        Vector3 o = GridOrigin();
        return new Vector3(o.x + grid.x * cellSize, o.y + grid.y * cellSize, 0f);
    }

    bool IsOccupied(Vector2Int grid) => occupied.ContainsKey(grid);

    void RebuildOccupiedMap()
    {
        occupied.Clear();
        if (!shipRoot) return;

        var mods = shipRoot.GetComponentsInChildren<Module>(true);
        for (int i = 0; i < mods.Length; i++)
        {
            var m = mods[i];
            if (!m) continue;

            Vector2Int g = WorldToGrid(m.transform.position);
            if (!occupied.ContainsKey(g))
                occupied.Add(g, m);
        }
    }

    // =========================
    // Input helpers
    // =========================

    Vector2 MouseWorld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            return cam.ScreenToWorldPoint(screen);
        }
#endif
        return cam.ScreenToWorldPoint(Input.mousePosition);
    }

    bool GetPointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) return Mouse.current.leftButton.wasPressedThisFrame;
#endif
        return Input.GetMouseButtonDown(0);
    }

    bool GetPointerHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) return Mouse.current.leftButton.isPressed;
#endif
        return Input.GetMouseButton(0);
    }

    bool GetPointerUp()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) return Mouse.current.leftButton.wasReleasedThisFrame;
#endif
        return Input.GetMouseButtonUp(0);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;
        if (!coreModule) return;

        Gizmos.color = Color.cyan;
        if (coreModule.apUp) Gizmos.DrawWireSphere(coreModule.apUp.position, 0.12f);
        if (coreModule.apDown) Gizmos.DrawWireSphere(coreModule.apDown.position, 0.12f);
        if (coreModule.apLeft) Gizmos.DrawWireSphere(coreModule.apLeft.position, 0.12f);
        if (coreModule.apRight) Gizmos.DrawWireSphere(coreModule.apRight.position, 0.12f);
    }
}
