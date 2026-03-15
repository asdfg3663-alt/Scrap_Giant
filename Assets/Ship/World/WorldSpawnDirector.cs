using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldSpawnDirector : MonoBehaviour
{
    const float DefaultStartingScrap = 240f;
    const int DefaultStartingAmmo = 120;
    const float DefaultFuelPerScrap = 10f;
    const float DefaultInitialFuelFillRatio = 1f;

    [Header("Prefab Refs")]
    public GameObject scrapVisualPrefab;
    public GameObject coreModulePrefab;
    public GameObject engineModulePrefab;
    public GameObject fuelTankModulePrefab;
    public GameObject laserModulePrefab;
    public GameObject powerPlantModulePrefab;
    public GameObject repairModulePrefab;
    public GameObject radiatorModulePrefab;

    [Header("Resource Economy")]
    public float startingScrap = DefaultStartingScrap;
    public int startingAmmo = DefaultStartingAmmo;
    public float fuelPerScrap = DefaultFuelPerScrap;
    [Range(0f, 1f)] public float initialFuelFillRatio = DefaultInitialFuelFillRatio;

    [Header("Population")]
    public int maxFloatingScraps = 5;
    public int maxEnemyShips = 5;
    public float spawnCheckInterval = 1.25f;
    public float enemyInitialSpawnDelay = 10f;

    [Header("Spawn Range")]
    public float minSpawnDistance = 30f;
    public float maxSpawnDistance = 100f;
    public float despawnAxisDistance = 100f;

    [Header("Floating Scrap")]
    public float scrapMassFromPlayerMassMultiplier = 1f;
    public float scrapDriftImpulse = 1.25f;

    [Header("Enemy Combat")]
    public float enemyDesiredRange = 30f;
    public float enemyAttackRange = 20f;
    public float enemyFireConeAngle = 24f;
    public float enemyMaxSpeed = 10f;
    public Color enemyOutlineColor = new Color(1f, 0.2f, 0.18f, 0.95f);
    public float enemyOutlinePixelSize = 7f;
    [Range(0f, 1f)] public float enemySaturationMultiplier = 0.65f;

    [Header("Threat Scaling")]
    public float threatScoreScale = 0.18f;
    public float threatLogBase = 2f;
    public float baseThreat = 1f;
    public float extraEngineThreatStep = 1.35f;
    public float extraFuelTankThreatStep = 1.7f;
    public float extraLaserThreatStep = 1.1f;
    public float moduleTierThreatStep = 1.8f;
    public float weaponTierThreatStep = 1.4f;
    public int maxExtraEngines = 4;
    public int maxExtraFuelTanks = 4;
    public int maxExtraLasers = 4;
    public int maxModuleUpgradeLevel = 4;
    public int maxLaserUpgradeLevel = 4;

    static WorldSpawnDirector instance;
    static Material enemyDesaturateMaterial;

    readonly List<FloatingScrap> floatingScraps = new();
    readonly List<EnemyShipRuntime> enemyShips = new();

    ShipStats playerShip;
    float nextSpawnCheckTime;
    float playerRegisteredTime;

    static readonly Vector2Int[] EngineSlots =
    {
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(1, -1),
        new Vector2Int(0, -2),
        new Vector2Int(-2, -1)
    };

    static readonly Vector2Int[] FuelTankSlots =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(2, 0),
        new Vector2Int(-2, 0),
        new Vector2Int(1, 1)
    };

    static readonly Vector2Int[] LaserSlots =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 2),
        new Vector2Int(2, 1)
    };

    static readonly Vector2Int[] SpecialSlots =
    {
        new Vector2Int(-2, 0),
        new Vector2Int(2, 0),
        new Vector2Int(-2, 1),
        new Vector2Int(2, 1),
        new Vector2Int(-2, -1),
        new Vector2Int(2, -1),
        new Vector2Int(-1, 2),
        new Vector2Int(1, 2),
        new Vector2Int(0, 3),
        new Vector2Int(-3, 0),
        new Vector2Int(3, 0),
        new Vector2Int(-1, -2),
        new Vector2Int(1, -2),
        new Vector2Int(0, -3)
    };

    static readonly Vector2Int[] connectionDirs =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public static Transform PlayerTransform => instance != null && instance.playerShip != null ? instance.playerShip.transform : null;
    public static float CurrentDespawnAxisLimit => instance != null ? instance.despawnAxisDistance : 100f;
    public static float GetStartingScrap() => ResolveInstance() != null ? Mathf.Max(0f, instance.startingScrap) : DefaultStartingScrap;
    public static int GetStartingAmmo() => ResolveInstance() != null ? Mathf.Max(0, instance.startingAmmo) : DefaultStartingAmmo;
    public static float GetFuelPerScrap() => ResolveInstance() != null ? Mathf.Max(0.01f, instance.fuelPerScrap) : DefaultFuelPerScrap;
    public static float GetInitialFuelFillRatio() => ResolveInstance() != null ? Mathf.Clamp01(instance.initialFuelFillRatio) : DefaultInitialFuelFillRatio;
    public static GameObject GetModulePrefabByType(ModuleType type) => ResolveInstance() != null ? instance.GetPrefabByType(type) : null;

    public static void RegisterPlayer(ShipStats ship)
    {
        if (ship == null)
            return;

        if (instance == null)
            instance = FindFirstObjectByType<WorldSpawnDirector>();

        if (instance == null)
        {
            var go = new GameObject("WorldSpawnDirector");
            instance = go.AddComponent<WorldSpawnDirector>();
        }

        instance.playerShip = ship;
        instance.playerRegisteredTime = Time.time;
        NeutralModuleSpawnDirector.EnsureForWorld(instance);
    }

    public static FloatingScrap GetNearestFloatingScrap(Vector3 origin)
    {
        return instance != null ? instance.FindNearest(instance.floatingScraps, origin) : null;
    }

    public static EnemyShipRuntime GetNearestEnemyShip(Vector3 origin)
    {
        return instance != null ? instance.FindNearest(instance.enemyShips, origin) : null;
    }

    static WorldSpawnDirector ResolveInstance()
    {
        if (instance == null)
            instance = FindFirstObjectByType<WorldSpawnDirector>();

        return instance;
    }

    GameObject GetPrefabByType(ModuleType type)
    {
        return type switch
        {
            ModuleType.Core => coreModulePrefab,
            ModuleType.Engine => engineModulePrefab,
            ModuleType.FuelTank => fuelTankModulePrefab,
            ModuleType.Reactor => powerPlantModulePrefab,
            ModuleType.Weapon => laserModulePrefab,
            ModuleType.Radiator => radiatorModulePrefab,
            ModuleType.Repair => repairModulePrefab,
            _ => null
        };
    }

    public static void NeutralizeDetachedModule(Transform moduleRoot)
    {
        if (moduleRoot == null)
            return;

        var renderers = moduleRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            if (renderer.name == "EnemyOutline")
            {
                Destroy(renderer.gameObject);
                continue;
            }

            var marker = renderer.GetComponent<EnemyVisualMarker>();
            if (marker != null)
            {
                marker.Restore(renderer);
                continue;
            }

            Transform orphanOutline = renderer.transform.Find("EnemyOutline");
            if (orphanOutline != null)
                Destroy(orphanOutline.gameObject);
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Update()
    {
        if (playerShip == null)
            return;

        if (Time.time < nextSpawnCheckTime)
            return;

        nextSpawnCheckTime = Time.time + Mathf.Max(0.25f, spawnCheckInterval);

        PruneLists();
        EnsureFloatingScraps();
        EnsureEnemyShips();
    }

    void EnsureFloatingScraps()
    {
        int count = CountNearby(floatingScraps);
        while (count < maxFloatingScraps)
        {
            if (!SpawnFloatingScrap())
                break;

            count++;
        }
    }

    void EnsureEnemyShips()
    {
        if (Time.time < playerRegisteredTime + Mathf.Max(0f, enemyInitialSpawnDelay))
            return;

        int count = CountNearby(enemyShips);
        while (count < maxEnemyShips)
        {
            if (!SpawnEnemyShip())
                break;

            count++;
        }
    }

    bool SpawnFloatingScrap()
    {
        if (scrapVisualPrefab == null || playerShip == null)
            return false;

        Vector2 spawnPosition = GetRandomSpawnPosition();
        float zRotation = Random.Range(0f, 360f);
        var scrapGO = Instantiate(scrapVisualPrefab, spawnPosition, Quaternion.Euler(0f, 0f, zRotation));
        scrapGO.name = "FloatingScrap";

        var scrap = scrapGO.GetComponent<FloatingScrap>();
        if (scrap == null)
            scrap = scrapGO.AddComponent<FloatingScrap>();

        var despawn = scrapGO.GetComponent<WorldDistanceDespawn>();
        if (despawn == null)
            despawn = scrapGO.AddComponent<WorldDistanceDespawn>();
        despawn.axisLimit = despawnAxisDistance;

        float playerMass = Mathf.Max(1f, playerShip.totalMass);
        int maxMass = Mathf.Max(1, Mathf.FloorToInt(playerMass * scrapMassFromPlayerMassMultiplier));
        scrap.Initialize(Random.Range(1, maxMass + 1));

        var rb = scrapGO.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.AddForce(Random.insideUnitCircle.normalized * scrapDriftImpulse, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-scrapDriftImpulse, scrapDriftImpulse), ForceMode2D.Impulse);
        }

        floatingScraps.Add(scrap);
        return true;
    }

    bool SpawnEnemyShip()
    {
        if (playerShip == null || coreModulePrefab == null || engineModulePrefab == null || fuelTankModulePrefab == null || laserModulePrefab == null)
            return false;

        float threat = EvaluateThreat(playerShip.totalScore);
        EnemyLoadout loadout = BuildLoadout(threat);
        if (!TryCreateEnemyAssemblyWithFallback(loadout, out EnemyLoadout resolvedLoadout, out List<EnemyModulePlacement> assembly))
            return false;

        Vector2 spawnPosition = GetRandomSpawnPosition();
        var root = new GameObject("EnemyShip");
        root.transform.position = spawnPosition;
        root.transform.rotation = Quaternion.identity;

        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.angularDamping = 0.1f;
        rb.linearDamping = 0.05f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var shipStats = root.AddComponent<ShipStats>();
        shipStats.isPlayerShip = false;
        shipStats.autoDetectPlayerByTag = false;

        var runtime = root.AddComponent<EnemyShipRuntime>();
        var ai = root.AddComponent<EnemyShipAI>();
        ai.Initialize(enemyDesiredRange, enemyAttackRange, enemyFireConeAngle, enemyMaxSpeed + threat + resolvedLoadout.moduleUpgradeLevel * 0.35f);

        var despawn = root.AddComponent<WorldDistanceDespawn>();
        despawn.axisLimit = despawnAxisDistance;

        ModuleInstance core = null;
        for (int i = 0; i < assembly.Count; i++)
        {
            EnemyModulePlacement placement = assembly[i];
            ModuleInstance instance = AttachModule(root.transform, placement.prefab, placement.gridPos, placement.rot90, placement.upgradeLevel);
            if (instance == null)
            {
                Destroy(root);
                return false;
            }

            if (placement.type == ModuleType.Core && core == null)
                core = instance;
        }

        if (core == null)
        {
            Destroy(root);
            return false;
        }

        IgnoreShipInternalCollisions(root.transform);
        ApplyEnemyVisualStyle(root.transform);
        shipStats.Rebuild();
        runtime.Initialize(core);

        if (playerShip != null)
        {
            Vector2 toPlayer = ((Vector2)playerShip.transform.position - spawnPosition).normalized;
            float angle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;
            root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        enemyShips.Add(runtime);
        return true;
    }

    ModuleInstance AttachModule(Transform shipRoot, GameObject prefab, Vector2Int gridPos, int rot90, int upgradeLevel)
    {
        if (prefab == null || shipRoot == null)
            return null;

        var moduleGO = Instantiate(prefab, shipRoot.position, shipRoot.rotation);
        moduleGO.transform.SetParent(shipRoot, false);
        moduleGO.transform.localPosition = new Vector3(gridPos.x, gridPos.y, 0f);
        moduleGO.transform.localRotation = Quaternion.Euler(0f, 0f, rot90 * 90f);

        var rb = moduleGO.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            rb.Sleep();
        }

        var attachment = moduleGO.GetComponent<ModuleAttachment>();
        if (attachment == null)
            attachment = moduleGO.AddComponent<ModuleAttachment>();
        attachment.shipRoot = shipRoot;
        attachment.gridPos = gridPos;
        attachment.rot90 = rot90;

        var instanceComponent = moduleGO.GetComponent<ModuleInstance>();
        if (instanceComponent != null && upgradeLevel > 0)
            instanceComponent.ApplyUpgrade(upgradeLevel);

        return instanceComponent;
    }

    void IgnoreShipInternalCollisions(Transform shipRoot)
    {
        var colliders = shipRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var a = colliders[i];
            if (!a)
                continue;

            for (int j = i + 1; j < colliders.Length; j++)
            {
                var b = colliders[j];
                if (!b)
                    continue;

                if (a.transform == shipRoot || b.transform == shipRoot)
                    continue;

                Physics2D.IgnoreCollision(a, b, true);
            }
        }
    }

    float EvaluateThreat(float currentScore)
    {
        float logBase = Mathf.Max(1.01f, threatLogBase);
        float scaledScore = Mathf.Max(0f, currentScore) * Mathf.Max(0.001f, threatScoreScale);
        return baseThreat + Mathf.Log(1f + scaledScore, logBase);
    }

    EnemyLoadout BuildLoadout(float threat)
    {
        int extraEngines = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1f) / Mathf.Max(0.01f, extraEngineThreatStep)), 0, maxExtraEngines);
        int extraFuelTanks = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1f) / Mathf.Max(0.01f, extraFuelTankThreatStep)), 0, maxExtraFuelTanks);
        int extraLasers = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1f) / Mathf.Max(0.01f, extraLaserThreatStep)), 0, maxExtraLasers);
        int moduleUpgradeLevel = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1f) / Mathf.Max(0.01f, moduleTierThreatStep)), 0, maxModuleUpgradeLevel);
        int weaponUpgradeLevel = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1f) / Mathf.Max(0.01f, weaponTierThreatStep)), 0, maxLaserUpgradeLevel);
        int reactorBudget = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1.25f) / 2.5f), 0, 2);
        int repairBudget = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 2f) / 3.25f), 0, 1);
        int radiatorBudget = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1f) / 2f), 0, 2);

        return new EnemyLoadout
        {
            engineCount = Mathf.Min(EngineSlots.Length, 1 + extraEngines + Mathf.Max(0, extraLasers / 2)),
            fuelTankCount = Mathf.Min(FuelTankSlots.Length, 1 + extraFuelTanks),
            laserCount = Mathf.Min(LaserSlots.Length, 1 + extraLasers),
            moduleUpgradeLevel = moduleUpgradeLevel,
            weaponUpgradeLevel = weaponUpgradeLevel,
            powerPlantCount = powerPlantModulePrefab != null && reactorBudget > 0 ? Random.Range(1, reactorBudget + 1) : 0,
            repairCount = repairModulePrefab != null ? Random.Range(0, repairBudget + 1) : 0,
            radiatorCount = radiatorModulePrefab != null && radiatorBudget > 0 ? Random.Range(1, radiatorBudget + 1) : 0
        };
    }

    bool TryCreateEnemyAssemblyWithFallback(EnemyLoadout loadout, out EnemyLoadout resolvedLoadout, out List<EnemyModulePlacement> placements)
    {
        resolvedLoadout = loadout;
        placements = null;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            if (TryCreateEnemyAssembly(resolvedLoadout, out placements))
                return true;

            if (!TryReduceLoadoutForAssembly(ref resolvedLoadout))
                break;
        }

        placements = null;
        return false;
    }

    bool TryCreateEnemyAssembly(EnemyLoadout loadout, out List<EnemyModulePlacement> placements)
    {
        placements = null;

        List<EnemyModuleRequest> requests = BuildEnemyModuleRequests(loadout);
        for (int attempt = 0; attempt < 48; attempt++)
        {
            if (TryCreateEnemyAssemblyAttempt(requests, out placements))
                return true;
        }

        return false;
    }

    List<EnemyModuleRequest> BuildEnemyModuleRequests(EnemyLoadout loadout)
    {
        var requests = new List<EnemyModuleRequest>
        {
            new EnemyModuleRequest(coreModulePrefab, ModuleType.Core, loadout.moduleUpgradeLevel)
        };

        AddRequests(requests, powerPlantModulePrefab, ModuleType.Reactor, loadout.powerPlantCount, loadout.moduleUpgradeLevel);
        AddRequests(requests, repairModulePrefab, ModuleType.Repair, loadout.repairCount, loadout.moduleUpgradeLevel);
        AddRequests(requests, radiatorModulePrefab, ModuleType.Radiator, loadout.radiatorCount, loadout.moduleUpgradeLevel);
        AddRequests(requests, engineModulePrefab, ModuleType.Engine, loadout.engineCount, loadout.moduleUpgradeLevel);
        AddRequests(requests, laserModulePrefab, ModuleType.Weapon, loadout.laserCount, Mathf.Clamp(loadout.weaponUpgradeLevel, 0, maxLaserUpgradeLevel));
        AddRequests(requests, fuelTankModulePrefab, ModuleType.FuelTank, loadout.fuelTankCount, loadout.moduleUpgradeLevel);
        return requests;
    }

    bool TryReduceLoadoutForAssembly(ref EnemyLoadout loadout)
    {
        if (loadout.fuelTankCount > 1)
        {
            loadout.fuelTankCount--;
            return true;
        }

        if (loadout.engineCount > 1)
        {
            loadout.engineCount--;
            return true;
        }

        if (loadout.laserCount > 1)
        {
            loadout.laserCount--;
            return true;
        }

        if (loadout.repairCount > 0)
        {
            loadout.repairCount--;
            return true;
        }

        if (loadout.powerPlantCount > 1)
        {
            loadout.powerPlantCount--;
            return true;
        }

        if (loadout.radiatorCount > 1)
        {
            loadout.radiatorCount--;
            return true;
        }

        if (loadout.powerPlantCount > 0)
        {
            loadout.powerPlantCount--;
            return true;
        }

        if (loadout.radiatorCount > 0)
        {
            loadout.radiatorCount--;
            return true;
        }

        return false;
    }

    void AddRequests(List<EnemyModuleRequest> requests, GameObject prefab, ModuleType type, int count, int upgradeLevel)
    {
        if (prefab == null || count <= 0)
            return;

        for (int i = 0; i < count; i++)
            requests.Add(new EnemyModuleRequest(prefab, type, upgradeLevel));
    }

    bool TryCreateEnemyAssemblyAttempt(List<EnemyModuleRequest> requests, out List<EnemyModulePlacement> placements)
    {
        placements = new List<EnemyModulePlacement>();
        var occupied = new Dictionary<Vector2Int, EnemyModulePlacement>();
        var openSockets = new List<EnemyOpenSocket>();

        Module coreModule = coreModulePrefab != null ? coreModulePrefab.GetComponent<Module>() : null;
        if (coreModule == null)
            return false;

        EnemyModulePlacement corePlacement = new EnemyModulePlacement(coreModulePrefab, ModuleType.Core, Vector2Int.zero, 0, 0);
        placements.Add(corePlacement);
        occupied.Add(Vector2Int.zero, corePlacement);
        AddOpenSocketsForPlacement(corePlacement, occupied, openSockets, connectedSide: null);

        for (int i = 1; i < requests.Count; i++)
        {
            if (!TryPlaceEnemyModule(requests[i], occupied, openSockets, out EnemyModulePlacement placement))
                return false;

            placements.Add(placement);
            occupied.Add(placement.gridPos, placement);
        }

        return true;
    }

    bool TryPlaceEnemyModule(
        EnemyModuleRequest request,
        Dictionary<Vector2Int, EnemyModulePlacement> occupied,
        List<EnemyOpenSocket> openSockets,
        out EnemyModulePlacement placement)
    {
        placement = default;

        Module module = request.prefab != null ? request.prefab.GetComponent<Module>() : null;
        if (module == null)
            return false;

        EnemyPlacementCandidate bestCandidate = default;
        bool found = false;

        for (int i = 0; i < openSockets.Count; i++)
        {
            EnemyOpenSocket socket = openSockets[i];
            Vector2Int targetGrid = socket.gridPos + SideToGridDelta(socket.side);
            if (occupied.ContainsKey(targetGrid))
                continue;

            if (HasExtraAdjacentNeighbors(targetGrid, socket.gridPos, occupied))
                continue;

            List<int> rotations = GetValidRotationsForConnection(module, OppositeSide(socket.side));
            for (int r = 0; r < rotations.Count; r++)
            {
                int rot90 = rotations[r];
                float score = ScorePlacement(request.type, targetGrid, socket.side) + Random.value * 0.25f;
                if (!found || score > bestCandidate.score)
                {
                    bestCandidate = new EnemyPlacementCandidate(socket, targetGrid, rot90, score);
                    found = true;
                }
            }
        }

        if (!found)
            return false;

        placement = new EnemyModulePlacement(request.prefab, request.type, bestCandidate.gridPos, bestCandidate.rot90, request.upgradeLevel);
        RemoveOpenSocket(openSockets, bestCandidate.socket);
        AddOpenSocketsForPlacement(placement, occupied, openSockets, OppositeSide(bestCandidate.socket.side));
        return true;
    }

    void AddOpenSocketsForPlacement(
        EnemyModulePlacement placement,
        Dictionary<Vector2Int, EnemyModulePlacement> occupied,
        List<EnemyOpenSocket> openSockets,
        Side? connectedSide)
    {
        Module module = placement.prefab != null ? placement.prefab.GetComponent<Module>() : null;
        if (module == null)
            return;

        AddOpenSocketIfAvailable(module.apUp, Side.Up);
        AddOpenSocketIfAvailable(module.apDown, Side.Down);
        AddOpenSocketIfAvailable(module.apLeft, Side.Left);
        AddOpenSocketIfAvailable(module.apRight, Side.Right);

        void AddOpenSocketIfAvailable(Transform attachPoint, Side localSide)
        {
            if (attachPoint == null)
                return;

            Side worldSide = RotateSide(localSide, placement.rot90);
            if (connectedSide.HasValue && worldSide == connectedSide.Value)
                return;

            Vector2Int targetGrid = placement.gridPos + SideToGridDelta(worldSide);
            if (occupied.ContainsKey(targetGrid))
                return;

            EnemyOpenSocket socket = new EnemyOpenSocket(placement.gridPos, worldSide);
            for (int i = 0; i < openSockets.Count; i++)
            {
                if (openSockets[i].gridPos == socket.gridPos && openSockets[i].side == socket.side)
                    return;
            }

            openSockets.Add(socket);
        }
    }

    void RemoveOpenSocket(List<EnemyOpenSocket> openSockets, EnemyOpenSocket socket)
    {
        for (int i = openSockets.Count - 1; i >= 0; i--)
        {
            if (openSockets[i].gridPos == socket.gridPos && openSockets[i].side == socket.side)
            {
                openSockets.RemoveAt(i);
                return;
            }
        }
    }

    bool HasExtraAdjacentNeighbors(Vector2Int gridPos, Vector2Int anchorGrid, Dictionary<Vector2Int, EnemyModulePlacement> occupied)
    {
        for (int i = 0; i < connectionDirs.Length; i++)
        {
            Vector2Int neighbor = gridPos + connectionDirs[i];
            if (neighbor == anchorGrid)
                continue;

            if (occupied.ContainsKey(neighbor))
                return true;
        }

        return false;
    }

    List<int> GetValidRotationsForConnection(Module module, Side desiredWorldConnectionSide)
    {
        var rotations = new List<int>(4);
        if (module == null)
            return rotations;

        for (int rot90 = 0; rot90 < 4; rot90++)
        {
            for (int i = 0; i < module.attachableLocalSides.Count; i++)
            {
                if (RotateSide(module.attachableLocalSides[i], rot90) != desiredWorldConnectionSide)
                    continue;

                rotations.Add(rot90);
                break;
            }
        }

        return rotations;
    }

    float ScorePlacement(ModuleType type, Vector2Int gridPos, Side anchorSide)
    {
        float score = 0f;
        switch (type)
        {
            case ModuleType.Engine:
                score += anchorSide == Side.Down ? 100f : (anchorSide == Side.Up ? -100f : 15f);
                score += -gridPos.y * 8f;
                break;

            case ModuleType.Weapon:
                score += anchorSide == Side.Up ? 100f : (anchorSide == Side.Down ? -100f : 10f);
                score += gridPos.y * 8f;
                break;

            case ModuleType.Radiator:
                score += (anchorSide == Side.Left || anchorSide == Side.Right) ? 60f : 10f;
                score += Mathf.Abs(gridPos.x) * 3f;
                break;

            case ModuleType.FuelTank:
                score += Mathf.Abs(gridPos.x) * 4f;
                score += gridPos.y <= 0 ? 8f : 2f;
                break;

            case ModuleType.Reactor:
            case ModuleType.Repair:
                score += -gridPos.sqrMagnitude * 2f;
                break;
        }

        return score;
    }

    static Side OppositeSide(Side side) => side switch
    {
        Side.Up => Side.Down,
        Side.Down => Side.Up,
        Side.Left => Side.Right,
        Side.Right => Side.Left,
        _ => Side.Up
    };

    static Side RotateSide(Side side, int rot90)
    {
        rot90 = ((rot90 % 4) + 4) % 4;
        return rot90 switch
        {
            0 => side,
            1 => side switch
            {
                Side.Up => Side.Left,
                Side.Left => Side.Down,
                Side.Down => Side.Right,
                Side.Right => Side.Up,
                _ => side
            },
            2 => OppositeSide(side),
            3 => side switch
            {
                Side.Up => Side.Right,
                Side.Right => Side.Down,
                Side.Down => Side.Left,
                Side.Left => Side.Up,
                _ => side
            },
            _ => side
        };
    }

    static Vector2Int SideToGridDelta(Side side) => side switch
    {
        Side.Up => Vector2Int.up,
        Side.Down => Vector2Int.down,
        Side.Left => Vector2Int.left,
        Side.Right => Vector2Int.right,
        _ => Vector2Int.zero
    };

    Vector2 GetRandomSpawnPosition()
    {
        Vector2 origin = playerShip != null ? (Vector2)playerShip.transform.position : Vector2.zero;
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 0.001f)
            dir = Vector2.right;

        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        return origin + dir * distance;
    }

    void PruneLists()
    {
        floatingScraps.RemoveAll(item => item == null);
        enemyShips.RemoveAll(item => item == null);
    }

    int CountNearby<T>(List<T> items) where T : Object
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null)
                continue;

            var component = items[i] as Component;
            if (component == null)
                continue;

            if (IsWithinPopulationWindow(component.transform.position))
                count++;
        }

        return count;
    }

    bool IsWithinPopulationWindow(Vector3 worldPosition)
    {
        if (playerShip == null)
            return false;

        Vector2 delta = worldPosition - playerShip.transform.position;
        return delta.sqrMagnitude <= maxSpawnDistance * maxSpawnDistance;
    }

    T FindNearest<T>(List<T> items, Vector3 origin) where T : Component
    {
        PruneLists();

        T nearest = null;
        float bestDistanceSq = float.MaxValue;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i] as T;
            if (item == null)
                continue;

            float distanceSq = (item.transform.position - origin).sqrMagnitude;
            if (distanceSq >= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            nearest = item;
        }

        return nearest;
    }

    void ApplyEnemyVisualStyle(Transform shipRoot)
    {
        if (shipRoot == null)
            return;

        var renderers = shipRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
                continue;

            if (renderer.name == "LaserBeam" || renderer.transform.parent != shipRoot)
                continue;

            var marker = renderer.GetComponent<EnemyVisualMarker>();
            if (marker == null)
                marker = renderer.gameObject.AddComponent<EnemyVisualMarker>();

            marker.Capture(renderer);
            renderer.color = marker.originalColor;
            ModuleInstance moduleInstance = renderer.GetComponentInParent<ModuleInstance>();
            bool preserveTierTint = moduleInstance != null && moduleInstance.CurrentTier > 1;
            if (preserveTierTint)
            {
                if (marker.hasOriginalMaterial)
                    renderer.sharedMaterial = marker.originalMaterial;
            }
            else
            {
                Material desaturateMaterial = GetEnemyDesaturateMaterial();
                if (desaturateMaterial != null)
                    renderer.sharedMaterial = desaturateMaterial;
            }
            marker.outlineRoot = EnsureOutline(renderer, marker.originalMaterial);
        }
    }

    Material GetEnemyDesaturateMaterial()
    {
        if (enemyDesaturateMaterial != null)
        {
            enemyDesaturateMaterial.SetFloat("_Saturation", enemySaturationMultiplier);
            return enemyDesaturateMaterial;
        }

        Shader shader = Shader.Find("Custom/EnemyDesaturateSprite");
        if (shader == null)
            return null;

        enemyDesaturateMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        enemyDesaturateMaterial.SetFloat("_Saturation", enemySaturationMultiplier);
        return enemyDesaturateMaterial;
    }

    Transform EnsureOutline(SpriteRenderer source, Material outlineMaterial)
    {
        if (source == null)
            return null;

        Transform existing = source.transform.Find("EnemyOutline");
        if (existing != null)
            return existing;

        var outlineRoot = new GameObject("EnemyOutline");
        outlineRoot.transform.SetParent(source.transform, false);
        outlineRoot.transform.localPosition = Vector3.zero;
        outlineRoot.transform.localRotation = Quaternion.identity;

        float pixelsPerUnit = source.sprite != null && source.sprite.pixelsPerUnit > 0f
            ? source.sprite.pixelsPerUnit
            : 128f;
        float offset = Mathf.Max(1f, enemyOutlinePixelSize) / pixelsPerUnit;

        Vector2[] directions =
        {
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f),
            new Vector2(-0.7071f, 0.7071f),
            new Vector2(-0.7071f, -0.7071f)
        };

        for (int i = 0; i < directions.Length; i++)
        {
            var segment = new GameObject("OutlinePart");
            segment.transform.SetParent(outlineRoot.transform, false);
            segment.transform.localPosition = (Vector3)(directions[i] * offset);
            segment.transform.localRotation = Quaternion.identity;

            var outline = segment.AddComponent<SpriteRenderer>();
            outline.sprite = source.sprite;
            outline.drawMode = SpriteDrawMode.Simple;
            outline.color = enemyOutlineColor;
            outline.sharedMaterial = outlineMaterial;
            outline.sortingLayerID = source.sortingLayerID;
            outline.sortingOrder = source.sortingOrder - 1;
            outline.maskInteraction = source.maskInteraction;
            outline.flipX = source.flipX;
            outline.flipY = source.flipY;
        }

        return outlineRoot.transform;
    }

    struct EnemyLoadout
    {
        public int engineCount;
        public int fuelTankCount;
        public int laserCount;
        public int moduleUpgradeLevel;
        public int weaponUpgradeLevel;
        public int powerPlantCount;
        public int repairCount;
        public int radiatorCount;
    }

    struct EnemyModuleRequest
    {
        public readonly GameObject prefab;
        public readonly ModuleType type;
        public readonly int upgradeLevel;

        public EnemyModuleRequest(GameObject prefab, ModuleType type, int upgradeLevel)
        {
            this.prefab = prefab;
            this.type = type;
            this.upgradeLevel = upgradeLevel;
        }
    }

    struct EnemyModulePlacement
    {
        public readonly GameObject prefab;
        public readonly ModuleType type;
        public readonly Vector2Int gridPos;
        public readonly int rot90;
        public readonly int upgradeLevel;

        public EnemyModulePlacement(GameObject prefab, ModuleType type, Vector2Int gridPos, int rot90, int upgradeLevel)
        {
            this.prefab = prefab;
            this.type = type;
            this.gridPos = gridPos;
            this.rot90 = rot90;
            this.upgradeLevel = upgradeLevel;
        }
    }

    struct EnemyOpenSocket
    {
        public readonly Vector2Int gridPos;
        public readonly Side side;

        public EnemyOpenSocket(Vector2Int gridPos, Side side)
        {
            this.gridPos = gridPos;
            this.side = side;
        }
    }

    struct EnemyPlacementCandidate
    {
        public readonly EnemyOpenSocket socket;
        public readonly Vector2Int gridPos;
        public readonly int rot90;
        public readonly float score;

        public EnemyPlacementCandidate(EnemyOpenSocket socket, Vector2Int gridPos, int rot90, float score)
        {
            this.socket = socket;
            this.gridPos = gridPos;
            this.rot90 = rot90;
            this.score = score;
        }
    }
}
