using UnityEngine;

[DisallowMultipleComponent]
public class ModuleHP : MonoBehaviour, IDamageable
{
    [Header("Death VFX")]
    public GameObject explosionVfxPrefab;

    [Header("Scrap Drop")]
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

        int dmg = Mathf.CeilToInt(Mathf.Max(0f, amount));
        if (dmg <= 0) return;

        inst.hp -= dmg;
        if (inst.hp <= 0)
        {
            inst.hp = 0;
            Die(hitPoint, hitNormal);
        }
    }

    void Die(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (explosionVfxPrefab)
            Instantiate(explosionVfxPrefab, hitPoint, Quaternion.identity);

        if (scrapPrefab)
        {
            int n = Random.Range(scrapMin, scrapMax + 1);
            for (int i = 0; i < n; i++)
            {
                var go = Instantiate(scrapPrefab, hitPoint, Quaternion.identity);
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb)
                {
                    Vector2 dir = (Random.insideUnitCircle.normalized + hitNormal).normalized;
                    rb.AddForce(dir * scrapImpulse, ForceMode2D.Impulse);
                }
            }
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (parentShip != null)
            parentShip.ScheduleRebuild();
    }
}
