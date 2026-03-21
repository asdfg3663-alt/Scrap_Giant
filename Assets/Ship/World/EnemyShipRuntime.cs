using UnityEngine;

[DisallowMultipleComponent]
public class EnemyShipRuntime : MonoBehaviour
{
    public float detachLinearImpulse = 1.1f;
    public float detachAngularImpulse = 0.4f;
    [Range(0f, 1f)] public float powerPlantExplosionChance = 0.9f;

    ModuleInstance coreModule;
    bool coreDestroyed;

    public void Initialize(ModuleInstance core)
    {
        coreModule = core;
    }

    public void OnCoreDestroyed(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (coreDestroyed)
            return;

        coreDestroyed = true;

        var modules = GetComponentsInChildren<ModuleInstance>(true);
        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null || module == coreModule)
                continue;

            if (TryExplodePowerPlant(module, hitPoint, hitNormal))
                continue;

            ScatterModule(module.transform, hitPoint, hitNormal);
        }

        Destroy(gameObject);
    }

    bool TryExplodePowerPlant(ModuleInstance module, Vector2 hitPoint, Vector2 hitNormal)
    {
        if (module == null || module.data == null || module.data.type != ModuleType.Reactor)
            return false;

        if (Random.value > powerPlantExplosionChance)
            return false;

        AudioRuntime.PlayPowerPlantExplosion();

        ModuleHP moduleHp = module.GetComponent<ModuleHP>();
        if (moduleHp != null)
        {
            moduleHp.ForceDestroy(module.transform.position, hitNormal, awardResources: false);
            return true;
        }

        Destroy(module.gameObject);
        return true;
    }

    void ScatterModule(Transform moduleTransform, Vector2 hitPoint, Vector2 hitNormal)
    {
        moduleTransform.SetParent(null, true);

        var attachment = moduleTransform.GetComponent<ModuleAttachment>();
        if (attachment != null)
        {
            attachment.shipRoot = null;
            attachment.gridPos = default;
            attachment.rot90 = 0;
        }

        WorldSpawnDirector.NeutralizeDetachedModule(moduleTransform);

        var rb = moduleTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.None;

            Vector2 dir = (Random.insideUnitCircle.normalized + hitNormal).normalized;
            if (dir.sqrMagnitude < 0.001f)
                dir = ((Vector2)moduleTransform.position - hitPoint).normalized;
            if (dir.sqrMagnitude < 0.001f)
                dir = Random.insideUnitCircle.normalized;

            rb.linearVelocity *= 0.2f;
            rb.angularVelocity *= 0.2f;
            rb.AddForce(dir * detachLinearImpulse, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-detachAngularImpulse, detachAngularImpulse), ForceMode2D.Impulse);
        }

        var despawn = moduleTransform.GetComponent<WorldDistanceDespawn>();
        if (despawn == null)
            despawn = moduleTransform.gameObject.AddComponent<WorldDistanceDespawn>();

        despawn.axisLimit = WorldSpawnDirector.CurrentDespawnAxisLimit;
    }
}
