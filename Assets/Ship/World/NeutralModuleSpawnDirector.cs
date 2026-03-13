using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NeutralModuleSpawnDirector : MonoBehaviour
{
    [Header("Spawn Timing")]
    public float minSpawnRollInterval = 5f;
    public float maxSpawnRollInterval = 30f;
    [Range(0f, 1f)] public float spawnChancePerRoll = 0.2f;

    [Header("Spawn Limits")]
    public int maxNeutralModules = 2;
    public float minSpawnDistance = 50f;
    public float maxSpawnDistance = 70f;
    public float despawnAxisDistance = 100f;

    [Header("Drift")]
    public float spawnDriftImpulse = 0.75f;

    static NeutralModuleSpawnDirector instance;

    readonly List<ModuleInstance> neutralModules = new();
    readonly List<GameObject> prefabPool = new();

    float nextSpawnRollTime;

    public static ModuleInstance GetNearestNeutralModule(Vector3 origin)
    {
        return instance != null ? instance.FindNearest(origin) : null;
    }

    public static void Unregister(ModuleInstance module)
    {
        if (instance == null || module == null)
            return;

        instance.neutralModules.Remove(module);
    }

    public static void EnsureForWorld(WorldSpawnDirector worldDirector)
    {
        if (instance == null)
            instance = FindFirstObjectByType<NeutralModuleSpawnDirector>();

        if (instance == null)
        {
            var go = new GameObject("NeutralModuleSpawnDirector");
            instance = go.AddComponent<NeutralModuleSpawnDirector>();
        }

        instance.SyncPrefabPool(worldDirector);
        instance.ScheduleNextRoll();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ScheduleNextRoll();
    }

    void Update()
    {
        if (WorldSpawnDirector.PlayerTransform == null)
            return;

        if (Time.time < nextSpawnRollTime)
            return;

        ScheduleNextRoll();
        PruneList();

        if (neutralModules.Count >= maxNeutralModules)
            return;

        if (prefabPool.Count == 0)
            SyncPrefabPool(FindFirstObjectByType<WorldSpawnDirector>());

        if (prefabPool.Count == 0 || Random.value > spawnChancePerRoll)
            return;

        SpawnNeutralModule();
    }

    void SyncPrefabPool(WorldSpawnDirector worldDirector)
    {
        prefabPool.Clear();
        if (worldDirector == null)
            return;

        AddPrefab(worldDirector.engineModulePrefab);
        AddPrefab(worldDirector.fuelTankModulePrefab);
        AddPrefab(worldDirector.laserModulePrefab);
        AddPrefab(worldDirector.powerPlantModulePrefab);
        AddPrefab(worldDirector.repairModulePrefab);
        AddPrefab(worldDirector.radiatorModulePrefab);
    }

    void AddPrefab(GameObject prefab)
    {
        if (prefab != null && !prefabPool.Contains(prefab))
            prefabPool.Add(prefab);
    }

    void SpawnNeutralModule()
    {
        if (prefabPool.Count == 0 || WorldSpawnDirector.PlayerTransform == null)
            return;

        GameObject prefab = prefabPool[Random.Range(0, prefabPool.Count)];
        Vector2 spawnPosition = GetRandomSpawnPosition();
        float rotation = Random.Range(0f, 360f);

        var moduleGO = Instantiate(prefab, spawnPosition, Quaternion.Euler(0f, 0f, rotation));
        moduleGO.name = $"Neutral_{prefab.name}";
        moduleGO.transform.SetParent(null, true);

        var attachment = moduleGO.GetComponent<ModuleAttachment>();
        if (attachment != null)
        {
            attachment.shipRoot = null;
            attachment.gridPos = default;
            attachment.rot90 = 0;
        }

        WorldSpawnDirector.NeutralizeDetachedModule(moduleGO.transform);

        var rb = moduleGO.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.None;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.AddForce(Random.insideUnitCircle.normalized * spawnDriftImpulse, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-spawnDriftImpulse, spawnDriftImpulse), ForceMode2D.Impulse);
        }

        var despawn = moduleGO.GetComponent<WorldDistanceDespawn>();
        if (despawn == null)
            despawn = moduleGO.AddComponent<WorldDistanceDespawn>();
        despawn.axisLimit = despawnAxisDistance;

        var module = moduleGO.GetComponent<ModuleInstance>();
        if (module != null)
            neutralModules.Add(module);
    }

    Vector2 GetRandomSpawnPosition()
    {
        Vector2 origin = WorldSpawnDirector.PlayerTransform != null
            ? (Vector2)WorldSpawnDirector.PlayerTransform.position
            : Vector2.zero;

        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.right;

        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        return origin + direction * distance;
    }

    void ScheduleNextRoll()
    {
        nextSpawnRollTime = Time.time + Random.Range(minSpawnRollInterval, maxSpawnRollInterval);
    }

    void PruneList()
    {
        neutralModules.RemoveAll(module =>
        {
            if (module == null)
                return true;

            var attachment = module.GetComponent<ModuleAttachment>();
            if (attachment != null && attachment.shipRoot != null)
                return true;

            var owningShip = module.GetComponentInParent<ShipStats>();
            return owningShip != null;
        });
    }

    ModuleInstance FindNearest(Vector3 origin)
    {
        PruneList();

        ModuleInstance nearest = null;
        float bestDistanceSq = float.MaxValue;
        for (int i = 0; i < neutralModules.Count; i++)
        {
            var module = neutralModules[i];
            if (module == null)
                continue;

            float distanceSq = (module.transform.position - origin).sqrMagnitude;
            if (distanceSq >= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            nearest = module;
        }

        return nearest;
    }
}
