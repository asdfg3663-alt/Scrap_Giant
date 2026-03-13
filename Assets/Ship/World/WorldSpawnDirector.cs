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
    [Range(0f, 1f)] public float enemySaturationMultiplier = 0.08f;

    [Header("Threat Scaling")]
    public float threatScoreScale = 0.18f;
    public float threatLogBase = 2f;
    public float baseThreat = 1f;
    public float extraEngineThreatStep = 1.35f;
    public float extraFuelTankThreatStep = 1.7f;
    public float extraLaserThreatStep = 1.1f;
    public float weaponTierThreatStep = 1.4f;
    public int maxExtraEngines = 4;
    public int maxExtraFuelTanks = 4;
    public int maxExtraLasers = 4;
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

    public static Transform PlayerTransform => instance != null && instance.playerShip != null ? instance.playerShip.transform : null;
    public static float CurrentDespawnAxisLimit => instance != null ? instance.despawnAxisDistance : 100f;
    public static float GetStartingScrap() => ResolveInstance() != null ? Mathf.Max(0f, instance.startingScrap) : DefaultStartingScrap;
    public static int GetStartingAmmo() => ResolveInstance() != null ? Mathf.Max(0, instance.startingAmmo) : DefaultStartingAmmo;
    public static float GetFuelPerScrap() => ResolveInstance() != null ? Mathf.Max(0.01f, instance.fuelPerScrap) : DefaultFuelPerScrap;
    public static float GetInitialFuelFillRatio() => ResolveInstance() != null ? Mathf.Clamp01(instance.initialFuelFillRatio) : DefaultInitialFuelFillRatio;

    public static void RegisterPlayer(ShipStats ship)
    {
        if (ship == null)
            return;

        if (instance == null)
            instance = FindObjectOfType<WorldSpawnDirector>();

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
            instance = FindObjectOfType<WorldSpawnDirector>();

        return instance;
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
        ai.Initialize(enemyDesiredRange, enemyAttackRange, enemyFireConeAngle, enemyMaxSpeed + threat);

        var despawn = root.AddComponent<WorldDistanceDespawn>();
        despawn.axisLimit = despawnAxisDistance;

        ModuleInstance core = AttachModule(root.transform, coreModulePrefab, Vector2Int.zero, 0);
        if (core == null)
        {
            Destroy(root);
            return false;
        }

        int weaponUpgradeLevel = Mathf.Clamp(loadout.weaponUpgradeLevel, 0, maxLaserUpgradeLevel);
        var occupied = new HashSet<Vector2Int> { Vector2Int.zero };

        for (int i = 0; i < loadout.engineCount && i < EngineSlots.Length; i++)
        {
            AttachModule(root.transform, engineModulePrefab, EngineSlots[i], 0);
            occupied.Add(EngineSlots[i]);
        }

        for (int i = 0; i < loadout.fuelTankCount && i < FuelTankSlots.Length; i++)
        {
            AttachModule(root.transform, fuelTankModulePrefab, FuelTankSlots[i], 0);
            occupied.Add(FuelTankSlots[i]);
        }

        for (int i = 0; i < loadout.laserCount && i < LaserSlots.Length; i++)
        {
            AttachModule(root.transform, laserModulePrefab, LaserSlots[i], weaponUpgradeLevel);
            occupied.Add(LaserSlots[i]);
        }

        AddSpecialModules(root.transform, occupied, loadout);

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

    ModuleInstance AttachModule(Transform shipRoot, GameObject prefab, Vector2Int gridPos, int upgradeLevel)
    {
        if (prefab == null || shipRoot == null)
            return null;

        var moduleGO = Instantiate(prefab, shipRoot.position, shipRoot.rotation);
        moduleGO.transform.SetParent(shipRoot, false);
        moduleGO.transform.localPosition = new Vector3(gridPos.x, gridPos.y, 0f);
        moduleGO.transform.localRotation = Quaternion.identity;

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
        attachment.rot90 = 0;

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
        int weaponUpgradeLevel = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1f) / Mathf.Max(0.01f, weaponTierThreatStep)), 0, maxLaserUpgradeLevel);
        int reactorBudget = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1.25f) / 2.5f), 0, 2);
        int repairBudget = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 2f) / 3.25f), 0, 1);
        int radiatorBudget = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, threat - 1f) / 2f), 0, 2);

        return new EnemyLoadout
        {
            engineCount = Mathf.Min(EngineSlots.Length, 1 + extraEngines + Mathf.Max(0, extraLasers / 2)),
            fuelTankCount = Mathf.Min(FuelTankSlots.Length, 1 + extraFuelTanks),
            laserCount = Mathf.Min(LaserSlots.Length, 1 + extraLasers),
            weaponUpgradeLevel = weaponUpgradeLevel,
            powerPlantCount = powerPlantModulePrefab != null ? Random.Range(0, reactorBudget + 1) : 0,
            repairCount = repairModulePrefab != null ? Random.Range(0, repairBudget + 1) : 0,
            radiatorCount = radiatorModulePrefab != null ? Random.Range(0, radiatorBudget + 1) : 0
        };
    }

    void AddSpecialModules(Transform shipRoot, HashSet<Vector2Int> occupied, EnemyLoadout loadout)
    {
        if (shipRoot == null)
            return;

        var availableSlots = new List<Vector2Int>(SpecialSlots.Length);
        for (int i = 0; i < SpecialSlots.Length; i++)
        {
            if (!occupied.Contains(SpecialSlots[i]))
                availableSlots.Add(SpecialSlots[i]);
        }

        ShuffleSlots(availableSlots);
        int slotIndex = 0;

        slotIndex = AddSpecialModuleGroup(shipRoot, powerPlantModulePrefab, loadout.powerPlantCount, availableSlots, occupied, slotIndex);
        slotIndex = AddSpecialModuleGroup(shipRoot, repairModulePrefab, loadout.repairCount, availableSlots, occupied, slotIndex);
        AddSpecialModuleGroup(shipRoot, radiatorModulePrefab, loadout.radiatorCount, availableSlots, occupied, slotIndex);
    }

    int AddSpecialModuleGroup(Transform shipRoot, GameObject prefab, int count, List<Vector2Int> availableSlots, HashSet<Vector2Int> occupied, int slotIndex)
    {
        if (prefab == null || count <= 0)
            return slotIndex;

        for (int i = 0; i < count && slotIndex < availableSlots.Count; i++, slotIndex++)
        {
            Vector2Int gridPos = availableSlots[slotIndex];
            if (occupied.Contains(gridPos))
                continue;

            AttachModule(shipRoot, prefab, gridPos, 0);
            occupied.Add(gridPos);
        }

        return slotIndex;
    }

    static void ShuffleSlots(List<Vector2Int> slots)
    {
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (slots[i], slots[swapIndex]) = (slots[swapIndex], slots[i]);
        }
    }

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
            Material desaturateMaterial = GetEnemyDesaturateMaterial();
            if (desaturateMaterial != null)
                renderer.sharedMaterial = desaturateMaterial;
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
        public int weaponUpgradeLevel;
        public int powerPlantCount;
        public int repairCount;
        public int radiatorCount;
    }
}
