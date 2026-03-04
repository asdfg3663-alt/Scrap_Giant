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

    private ModuleInstance inst;

    void Awake()
    {
        inst = GetComponent<ModuleInstance>();
        if (inst != null)
        {
            // data.maxHP -> inst.maxHp/hp 동기화
            inst.SyncFromDataIfNeeded(forceReset: false);

            // 만약 기존에 hp가 10으로 박혀서 꼬였던 경우를 방지: hp가 0이거나 비정상이면 max로
            if (inst.hp <= 0) inst.hp = inst.maxHp;
            inst.hp = Mathf.Clamp(inst.hp, 0, inst.maxHp);
        }
    }

    // ✅ IDamageable 규격 그대로 유지
    public void ApplyDamage(float amount, Vector2 hitPoint, Vector2 hitNormal, GameObject attacker)
    {
        if (inst == null)
            inst = GetComponent<ModuleInstance>();

        if (inst == null)
        {
            // 안전장치: ModuleInstance가 없으면 데미지 무시(프리팹 설정 오류)
            return;
        }

        // 혹시 data가 나중에 들어왔으면 반영
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
}