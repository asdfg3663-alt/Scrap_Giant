using UnityEngine;

[DisallowMultipleComponent]
public class ModuleHP : MonoBehaviour, IDamageable
{
    [Header("Death VFX")]
    public GameObject explosionVfxPrefab;

    [Header("Legacy Drop Fields")]
    public GameObject scrapPrefab;
    public int scrapMin = 0;
    public int scrapMax = 2;
    public float scrapImpulse = 2.5f;

    ModuleInstance inst;
    ShipStats parentShip;

    void Awake()
    {
        parentShip = GetComponentInParent<ShipStats>();
        inst = GetComponent<ModuleInstance>();
        if (inst != null)
        {
            inst.SyncFromDataIfNeeded(forceReset: false);

            if (inst.hp <= 0)
                inst.hp = inst.maxHp;

            inst.hp = Mathf.Clamp(inst.hp, 0, inst.maxHp);
        }
    }

    public void ApplyDamage(float amount, Vector2 hitPoint, Vector2 hitNormal, GameObject attacker)
    {
        if (inst == null)
            inst = GetComponent<ModuleInstance>();

        if (inst == null)
            return;

        if (inst.data != null && inst.maxHp <= 0)
            inst.SyncFromDataIfNeeded(forceReset: true);

        int damage = Mathf.CeilToInt(Mathf.Max(0f, amount));
        if (damage <= 0)
            return;

        NotifyDefender(attacker);

        inst.hp -= damage;
        if (inst.hp > 0)
        {
            var currentShip = GetComponentInParent<ShipStats>();
            if (currentShip != null && inst.maxHp > 0 && inst.hp < inst.maxHp * 0.5f)
                currentShip.QueueRepair(inst);
            return;
        }

        inst.hp = 0;
        Die(hitPoint, hitNormal);
    }

    void Die(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (explosionVfxPrefab)
            Instantiate(explosionVfxPrefab, hitPoint, Quaternion.identity);

        var currentShip = GetComponentInParent<ShipStats>();
        bool isPlayerOwned = currentShip != null && currentShip.isPlayerShip;

        if (!isPlayerOwned && inst != null)
            WorldResourceUtility.AwardScrapFromMass(inst.GetMass());

        if (!isPlayerOwned && inst != null && inst.data != null && inst.data.type == ModuleType.Core)
        {
            var enemyRuntime = currentShip != null ? currentShip.GetComponent<EnemyShipRuntime>() : GetComponentInParent<EnemyShipRuntime>();
            if (enemyRuntime != null)
                enemyRuntime.OnCoreDestroyed(hitPoint, hitNormal);
        }

        Destroy(gameObject);
    }

    void NotifyDefender(GameObject attacker)
    {
        var defenderShip = GetComponentInParent<ShipStats>();
        if (defenderShip == null || defenderShip.isPlayerShip || attacker == null)
            return;

        var attackerShip = attacker.GetComponentInParent<ShipStats>();
        if (attackerShip == null || attackerShip == defenderShip)
            return;

        var enemyAI = defenderShip.GetComponent<EnemyShipAI>();
        if (enemyAI != null)
            enemyAI.OnAttackedBy(attackerShip);
    }

    void OnDestroy()
    {
        if (parentShip != null)
            parentShip.ScheduleRebuild();
    }
}
