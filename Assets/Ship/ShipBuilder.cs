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
    int activeTouchPointerId = int.MinValue;
    Vector2 activePointerScreen;

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

    struct RigidbodyState2D
    {
        public Rigidbody2D body;
        public Vector2 position;
        public float rotation;
        public Vector2 linearVelocity;
        public float angularVelocity;
        public bool wasSleeping;
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

    void OnTransformChildrenChanged()
    {
        RebuildOccupiedMap();
    }

    void Update()
    {
        if (GameRuntimeState.GameplayBlocked)
            return;

        pointerWorldNow = ScreenToWorld(MouseScreen());

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
            pointerWorldNow = ScreenToWorld(activePointerScreen);
            pointerDown = true;
            pressT = 0f;
            pressWorld = pointerWorldNow;

            draggingTf = TryPickModule(pointerWorldNow, activeTouchPointerId);
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
            pointerWorldNow = ScreenToWorld(activePointerScreen);
            pressT += Time.deltaTime;

            if (!isDragging && draggingTf)
            {
                float dist = Vector2.Distance(pressWorld, pointerWorldNow);

                // NOTE: 현재 UX = 롱프레스 + 이동 => 드래그 시작 (원하면 dist 조건 제거 가능)
                if (pressT >= longPressTime && dist >= dragStartDistance)
                {
                    if (wasAttachedAtDragStart)
                        DetachModule(draggingTf);

                    isDragging = true;
                    BeginDragPreview(draggingTf);
                    AudioRuntime.BeginModuleDrag();
                }
            }
        }

        if (pointerDown && up)
        {
            pointerWorldNow = ScreenToWorld(activePointerScreen);
            bool dropped = false;
            if (isDragging)
            {
                dropped = TryDrop();
                if (!dropped)
                    dropped = TryStoreDraggedModule(activePointerScreen);
            }

            if (isDragging && draggingTf && !dropped)
                EndDetachedDrag(draggingTf);

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
            var frame = GridFrame();
            draggingTf.rotation = (frame ? frame.rotation : Quaternion.identity) * Quaternion.Euler(0, 0, cand.rot90 * 90f);
        }
    }

    bool TryDrop()
    {
        if (!draggingTf) return false;

        var cand = FindBestSnapCandidate(pointerWorldNow, draggingTf);
        if (!cand.valid) return false;

        // 겹침 체크
        if (IsOccupied(cand.grid)) return false;

        // TODO: 여기서 모듈별 룰(엔진은 뒤만, 코어 1개 등)을 추가 가능

        AttachModule(draggingTf, cand.grid, cand.rot90);
        RebuildOccupiedMap();
        if (shipStats) shipStats.Rebuild();
        return true;
    }

    bool TryStoreDraggedModule(Vector2 screenPoint)
    {
        if (!draggingTf)
            return false;

        var hud = PlayerHudRuntime.Instance;
        if (hud == null || !hud.TryStoreDetachedModule(draggingTf, screenPoint))
            return false;

        AudioRuntime.EndModuleDrag();
        return true;
    }

    void AttachModule(Transform moduleTf, Vector2Int grid, int rot90)
    {
        if (!shipRoot) return;

        var shipBodies = CaptureShipBodyStates();
        var frame = GridFrame();
        Vector3 targetPosition = GridToWorld(grid);
        Quaternion targetRotation = (frame ? frame.rotation : Quaternion.identity) * Quaternion.Euler(0, 0, rot90 * 90f);

        SetModulePhysics(moduleTf, attachedToShip: true);
        if (shipStats != null && shipStats.isPlayerShip)
            WorldSpawnDirector.NeutralizeDetachedModule(moduleTf);

        IgnoreCollisionsWithShip(moduleTf, true);
        moduleTf.SetParent(shipRoot, true);
        moduleTf.SetPositionAndRotation(targetPosition, targetRotation);
        Physics2D.SyncTransforms();

        // 내부 모듈끼리 충돌로 인한 '계속 밀림' 방지
        // attachment metadata
        var att = moduleTf.GetComponent<ModuleAttachment>();
        if (!att) att = moduleTf.gameObject.AddComponent<ModuleAttachment>();
        att.shipRoot = shipRoot;
        att.gridPos = grid;
        att.rot90 = rot90;

        var moduleInstance = moduleTf.GetComponent<ModuleInstance>();
        if (moduleInstance != null)
            NeutralModuleSpawnDirector.Unregister(moduleInstance);

        RestoreShipBodyStates(shipBodies);
        AudioRuntime.EndModuleDrag();
        AudioRuntime.PlayModuleAttach();



        var mod = moduleTf.GetComponent<Module>();
        if (mod) occupied[grid] = mod;
    }

    void DetachModule(Transform moduleTf)
{
    // ship에 붙어있던 동안 무시했던 내부 충돌 복구
    var shipBodies = CaptureShipBodyStates();

    moduleTf.SetParent(null, true);
    SetModulePhysics(moduleTf, attachedToShip: false);
    Physics2D.SyncTransforms();
    IgnoreCollisionsWithShip(moduleTf, false);
    RestoreShipBodyStates(shipBodies);

    var attachment = moduleTf.GetComponent<ModuleAttachment>();
    if (attachment != null)
    {
        attachment.shipRoot = null;
        attachment.gridPos = default;
        attachment.rot90 = 0;
    }

    var mod = moduleTf.GetComponent<Module>();
    if (mod == null) return;

    var removeKeys = new List<Vector2Int>();
    foreach (var kv in occupied)
        if (kv.Value == mod) removeKeys.Add(kv.Key);

    for (int i = 0; i < removeKeys.Count; i++)
        occupied.Remove(removeKeys[i]);

    RefreshShipStateAfterStructureChange();

    // 🔥 이 줄 추가
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
        rb.Sleep();
    }
    else
    {
        var moduleInstance = moduleTf.GetComponent<ModuleInstance>();
        if (moduleInstance != null)
            rb.mass = Mathf.Max(0.01f, moduleInstance.GetMass());

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        rb.gravityScale = 0f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;

        // 우주에 떠다니는 상태에서는 회전은 허용할지/금지할지 취향
        rb.constraints = RigidbodyConstraints2D.None;
        rb.WakeUp();
    }
}

    void BeginDragPreview(Transform moduleTf)
    {
        var rb = moduleTf ? moduleTf.GetComponent<Rigidbody2D>() : null;
        if (!rb) return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        rb.simulated = false;
    }

    public bool BeginInventoryDrag(GameObject modulePrefab, int upgradeLevel, Vector2 screenPosition, int pointerId = int.MinValue)
    {
        if (modulePrefab == null || cam == null || isDragging)
            return false;

        Vector3 spawnWorld = cam.ScreenToWorldPoint(screenPosition);
        spawnWorld.z = 0f;

        GameObject moduleGO = Instantiate(modulePrefab, spawnWorld, Quaternion.identity);
        Transform moduleTf = moduleGO.transform;

        ModuleInstance moduleInstance = moduleGO.GetComponent<ModuleInstance>();
        if (moduleInstance != null && upgradeLevel > 0)
            moduleInstance.ApplyUpgrade(upgradeLevel);

        PrepareDetachedModule(moduleTf);

        pointerWorldNow = spawnWorld;
        pressWorld = spawnWorld;
        pressT = longPressTime;
        dragOffset = Vector3.zero;
        pointerDown = true;
        activeTouchPointerId = pointerId >= 0 ? pointerId : int.MinValue;
        activePointerScreen = screenPosition;
        isDragging = true;
        wasAttachedAtDragStart = false;
        draggingTf = moduleTf;
        draggingMod = moduleTf.GetComponent<Module>();
        draggingCol = moduleTf.GetComponentInChildren<Collider2D>();

        BeginDragPreview(moduleTf);
        UpdateDrag();
        AudioRuntime.BeginModuleDrag();
        return true;
    }

    void EndDetachedDrag(Transform moduleTf)
    {
        if (!moduleTf) return;

        SetModulePhysics(moduleTf, attachedToShip: false);
        Physics2D.SyncTransforms();
        AudioRuntime.EndModuleDrag();
    }

    RigidbodyState2D[] CaptureShipBodyStates()
    {
        var states = new List<RigidbodyState2D>(2);
        AddBodyState(shipRoot ? shipRoot.GetComponent<Rigidbody2D>() : null, states);
        AddBodyState(coreModule ? coreModule.GetComponent<Rigidbody2D>() : null, states);
        return states.ToArray();
    }

    void AddBodyState(Rigidbody2D rb, List<RigidbodyState2D> states)
    {
        if (!rb) return;

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].body == rb)
                return;
        }

        states.Add(new RigidbodyState2D
        {
            body = rb,
            position = rb.position,
            rotation = rb.rotation,
            linearVelocity = rb.linearVelocity,
            angularVelocity = rb.angularVelocity,
            wasSleeping = rb.IsSleeping()
        });
    }

    void RestoreShipBodyStates(RigidbodyState2D[] states)
    {
        if (states == null) return;

        for (int i = 0; i < states.Length; i++)
        {
            var state = states[i];
            if (!state.body) continue;

            state.body.position = state.position;
            state.body.rotation = state.rotation;
            state.body.linearVelocity = state.linearVelocity;
            state.body.angularVelocity = state.angularVelocity;

            if (state.wasSleeping) state.body.Sleep();
            else state.body.WakeUp();
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

            // 코어(그리드 프레임) 로컬로 변환해서 4방향 델타 결정
            var frame = GridFrame();
            Vector2 normalLocal = frame ? (Vector2)frame.InverseTransformDirection(normalWorld) : normalWorld;

            // 그리드 델타로 강제(대각선 방지) - 로컬 기준
            Vector2Int delta = LocalDirToGridDelta(normalLocal);
            Vector2Int targetGrid = aGrid + delta;

            Vector3 targetWorld = GridToWorld(targetGrid);

            float d = Vector2.Distance(pointerWorld, targetWorld);
            if (d > snapDistance) return;

            // 드래그 모듈이 "그리드 로컬 기준 -delta" 방향을 향하도록 회전(붙는 면이 상대쪽을 보게)
            Vector2 desiredLocalDir = new Vector2(-delta.x, -delta.y);
            if (!TryComputeRotation90(mod, desiredLocalDir, out int rot90))
                return;

            if (IsOccupied(targetGrid)) return;

            if (d < bestDist)
            {
                bestDist = d;
                best = new SnapCandidate(true, targetGrid, rot90, anchor.transform, anchorSide);
            }
        }
    }

    // 원하는 월드 방향을 향하도록 90도 회전값(0~3) 계산
    bool TryComputeRotation90(Module mod, Vector2 desiredLocalDir, out int rot90)
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
                float dot = Vector2.Dot(rotated.normalized, desiredLocalDir.normalized);
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

    Vector2Int LocalDirToGridDelta(Vector2 localDir)
    {
        // 코어(그리드 프레임) 로컬 기준으로 4방향 결정
        if (Mathf.Abs(localDir.x) >= Mathf.Abs(localDir.y))
            return new Vector2Int(localDir.x >= 0 ? 1 : -1, 0);
        else
            return new Vector2Int(0, localDir.y >= 0 ? 1 : -1);
    }
// =========================
    // Picking / Overlap
    // =========================

    Transform TryPickModule(Vector2 world, int pointerId = int.MinValue)
    {
        if (EventSystem.current != null)
        {
            bool pointerOverUi = pointerId != int.MinValue
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
            if (pointerOverUi)
                return null;
        }

        var hits = Physics2D.OverlapPointAll(world, pickMask);
        if (hits == null || hits.Length == 0) return null;

        // 모듈 루트가 콜라이더 자식일 수도 있으니 Module 컴포넌트 있는 상위로 끌어올림
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (!hit || ShipCollisionHull2D.IsHullCollider(hit))
                continue;

            var m = hit.GetComponentInParent<Module>();
            if (m != null && !CanManipulateModule(m.transform))
                return null;

            return m ? m.transform : hit.transform;
        }

        return null;
    }

    bool CanManipulateModule(Transform moduleTf)
    {
        if (!moduleTf)
            return false;

        var ownerShip = moduleTf.GetComponentInParent<ShipStats>();
        if (ownerShip == null)
            return true;

        if (shipStats == null)
            return ownerShip.isPlayerShip;

        return ownerShip == shipStats || ownerShip.isPlayerShip;
    }

    bool WouldOverlapAt(Vector2Int grid, int rot90, Transform moduleTf)
    {
        Vector3 center = GridToWorld(grid);
        Vector2 size = Vector2.one * cellSize * overlapScale;

        // OverlapBoxAll로 받고, 자기 자신/자식 콜라이더 제외
        var hits = Physics2D.OverlapBoxAll(center, size, ((GridFrame() ? GridFrame().eulerAngles.z : 0f) + rot90 * 90f), blockMask);
        if (hits == null || hits.Length == 0) return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h) continue;

            // 드래그 중인 자신의 콜라이더는 무시
            if (draggingCol != null && h == draggingCol) continue;
            if (moduleTf != null && h.transform.IsChildOf(moduleTf)) continue;
            if (!h.enabled) continue;
            if (ShipCollisionHull2D.IsHullCollider(h)) continue;
            if (ShouldIgnoreCurrentShipCollider(h, moduleTf)) continue;

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

    Transform GridFrame()
    {
        // 코어 모듈이 "본체"이므로 그 회전을 그리드 축으로 사용
        // (코어가 없으면 shipRoot를 fallback)
        if (coreModule) return coreModule.transform;
        if (shipRoot) return shipRoot;
        return null;
    }

    Vector2Int WorldToGrid(Vector3 world)
    {
        var frame = GridFrame();
        if (!frame)
        {
            int gx0 = Mathf.RoundToInt(world.x / cellSize);
            int gy0 = Mathf.RoundToInt(world.y / cellSize);
            return new Vector2Int(gx0, gy0);
        }

        // 코어(또는 shipRoot) 로컬 좌표로 변환 후 그리드 스냅
        Vector3 local = frame.InverseTransformPoint(world);
        int gx = Mathf.RoundToInt(local.x / cellSize);
        int gy = Mathf.RoundToInt(local.y / cellSize);
        return new Vector2Int(gx, gy);
    }

    Vector3 GridToWorld(Vector2Int grid)
    {
        var frame = GridFrame();
        Vector3 local = new Vector3(grid.x * cellSize, grid.y * cellSize, 0f);

        if (!frame)
            return local;

        // 로컬 그리드를 다시 월드로 변환 (코어 회전 반영)
        return frame.TransformPoint(local);
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

    public void RefreshOccupiedMapNow()
    {
        RebuildOccupiedMap();
        Physics2D.SyncTransforms();
    }

    void RefreshShipStateAfterStructureChange()
    {
        RebuildOccupiedMap();
        Physics2D.SyncTransforms();
        if (shipStats) shipStats.Rebuild();
    }

    // =========================
    // Input helpers
    // =========================

    Vector2 MouseWorld()
    {
        Vector2 screen = MouseScreen();
        return ScreenToWorld(screen);
    }

    Vector2 ScreenToWorld(Vector2 screen)
    {
        if (cam == null)
            return screen;

        Vector3 world = cam.ScreenToWorldPoint(screen);
        return new Vector2(world.x, world.y);
    }

    Vector2 MouseScreen()
    {
        if (activeTouchPointerId != int.MinValue)
        {
            if (TryGetTouchPosition(activeTouchPointerId, out Vector2 touchScreen))
            {
                activePointerScreen = touchScreen;
                return touchScreen;
            }

            if (Input.touchCount > 0)
            {
                activePointerScreen = Input.GetTouch(0).position;
                return activePointerScreen;
            }

            return activePointerScreen;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            activePointerScreen = Mouse.current.position.ReadValue();
            return activePointerScreen;
        }
#endif
        activePointerScreen = Input.mousePosition;
        return activePointerScreen;
    }

    bool GetPointerDown()
    {
        if (TryGetTouchBegan(out int touchPointerId, out Vector2 touchScreen))
        {
            activeTouchPointerId = touchPointerId;
            activePointerScreen = touchScreen;
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            activeTouchPointerId = int.MinValue;
            activePointerScreen = Mouse.current.position.ReadValue();
            return true;
        }
#endif
        if (Input.GetMouseButtonDown(0))
        {
            activeTouchPointerId = int.MinValue;
            activePointerScreen = Input.mousePosition;
            return true;
        }

        return false;
    }

    bool ShouldIgnoreCurrentShipCollider(Collider2D collider, Transform moduleTf)
    {
        if (collider == null || shipRoot == null)
            return false;

        if (moduleTf != null && collider.transform.IsChildOf(moduleTf))
            return true;

        ShipStats ownerShip = collider.GetComponentInParent<ShipStats>();
        if (ownerShip == null || shipStats == null || ownerShip != shipStats)
            return false;

        return true;
    }

    bool GetPointerHeld()
    {
        if (activeTouchPointerId != int.MinValue)
        {
            if (TryGetTouchPosition(activeTouchPointerId, out Vector2 touchScreen))
            {
                activePointerScreen = touchScreen;
                return true;
            }

            if (Input.touchCount > 0)
            {
                activePointerScreen = Input.GetTouch(0).position;
                return true;
            }

            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            activePointerScreen = Mouse.current.position.ReadValue();
            return Mouse.current.leftButton.isPressed;
        }
#endif
        activePointerScreen = Input.mousePosition;
        return Input.GetMouseButton(0);
    }

    bool GetPointerUp()
    {
        if (activeTouchPointerId != int.MinValue)
        {
            if (TryGetTouchEnded(activeTouchPointerId, out Vector2 touchScreen))
            {
                activePointerScreen = touchScreen;
                activeTouchPointerId = int.MinValue;
                return true;
            }

            if (Input.touchCount == 0)
            {
                activeTouchPointerId = int.MinValue;
                return true;
            }

            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            return true;
#endif
        return Input.GetMouseButtonUp(0);
    }

    bool TryGetTouchBegan(out int pointerId, out Vector2 screenPosition)
    {
        pointerId = int.MinValue;
        screenPosition = default;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != UnityEngine.TouchPhase.Began)
                continue;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                continue;

            pointerId = touch.fingerId;
            screenPosition = touch.position;
            return true;
        }

        return false;
    }

    bool TryGetTouchPosition(int pointerId, out Vector2 screenPosition)
    {
        screenPosition = default;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != pointerId)
                continue;

            if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
                return false;

            screenPosition = touch.position;
            return true;
        }

        return false;
    }

    bool TryGetTouchEnded(int pointerId, out Vector2 screenPosition)
    {
        screenPosition = default;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != pointerId)
                continue;

            if (touch.phase != UnityEngine.TouchPhase.Ended && touch.phase != UnityEngine.TouchPhase.Canceled)
                return false;

            screenPosition = touch.position;
            return true;
        }

        return false;
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


void IgnoreCollisionsWithShip(Transform moduleTf, bool ignore)
{
    if (!moduleTf) return;

    // shipRoot가 없으면 core를 기준으로라도 잡음
    Transform root = shipRoot ? shipRoot : (coreModule ? coreModule.transform : null);
    if (!root) return;

    var myCols = moduleTf.GetComponentsInChildren<Collider2D>(true);
    if (myCols == null || myCols.Length == 0) return;

    var shipCols = root.GetComponentsInChildren<Collider2D>(true);
    if (shipCols == null || shipCols.Length == 0) return;

    // 자기 자신 콜라이더는 제외하고 shipRoot 아래 기존 콜라이더들과 충돌 무시/복구
    for (int i = 0; i < myCols.Length; i++)
    {
        var a = myCols[i];
        if (!a) continue;

        for (int j = 0; j < shipCols.Length; j++)
        {
            var b = shipCols[j];
            if (!b) continue;

            // 같은 콜라이더면 스킵
            if (a == b) continue;

            // 같은 모듈 내부끼리면 스킵(자기 자신/자식)
            if (b.transform.IsChildOf(moduleTf)) continue;

            Physics2D.IgnoreCollision(a, b, ignore);
        }
    }
}

    void PrepareDetachedModule(Transform moduleTf)
    {
        if (!moduleTf)
            return;

        ModuleAttachment attachment = moduleTf.GetComponent<ModuleAttachment>();
        if (attachment != null)
        {
            attachment.shipRoot = null;
            attachment.gridPos = default;
            attachment.rot90 = 0;
        }

        SetModulePhysics(moduleTf, attachedToShip: false);
        WorldSpawnDirector.NeutralizeDetachedModule(moduleTf);

        WorldDistanceDespawn despawn = moduleTf.GetComponent<WorldDistanceDespawn>();
        if (despawn == null)
            despawn = moduleTf.gameObject.AddComponent<WorldDistanceDespawn>();
        despawn.axisLimit = WorldSpawnDirector.CurrentDespawnAxisLimit;

        ModuleInstance moduleInstance = moduleTf.GetComponent<ModuleInstance>();
        if (moduleInstance != null)
            NeutralModuleSpawnDirector.Unregister(moduleInstance);
    }


}
