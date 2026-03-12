using UnityEngine;

[DisallowMultipleComponent]
public class EnemyShipRuntime : MonoBehaviour
{
    public float detachImpulse = 3.5f;

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

            ScatterModule(module.transform, hitPoint, hitNormal);
        }

        Destroy(gameObject);
    }

    void ScatterModule(Transform moduleTransform, Vector2 hitPoint, Vector2 hitNormal)
    {
        moduleTransform.SetParent(null, true);

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

            rb.AddForce(dir * detachImpulse, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-detachImpulse, detachImpulse), ForceMode2D.Impulse);
        }

        var despawn = moduleTransform.GetComponent<WorldDistanceDespawn>();
        if (despawn == null)
            despawn = moduleTransform.gameObject.AddComponent<WorldDistanceDespawn>();

        despawn.axisLimit = WorldSpawnDirector.CurrentDespawnAxisLimit;
    }
}
