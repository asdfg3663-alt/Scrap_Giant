using UnityEngine;

[DisallowMultipleComponent]
public class ModuleHP : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public int maxHP = 10;
    public int currentHP = 10;

    [Header("Optional: Auto from ModuleData")]
    public bool autoFromModuleData = true;

    [Header("Death VFX")]
    public GameObject explosionVfxPrefab;

    [Header("Scrap Drop")]
    public GameObject scrapPrefab;     // 떨어질 Scrap 프리팹(콜라이더/리짓바디 있으면 좋음)
    public int scrapMin = 0;
    public int scrapMax = 2;
    public float scrapImpulse = 2.5f;

    ModuleInstance inst;

    void Awake()
    {
        inst = GetComponent<ModuleInstance>();

        // ModuleData의 Max HP를 자동 반영(기획대로: "모듈 내구도 0이면 잔해")
        if (autoFromModuleData && inst != null && inst.data != null)
        {
            if (inst.data.maxHP > 0)
                maxHP = inst.data.maxHP;
        }

        // 초기화
        if (currentHP <= 0 || currentHP > maxHP)
            currentHP = maxHP;
    }

    // ✅ 우리 프로젝트 IDamageable 규격(이미 존재하는 인터페이스)에 정확히 맞춤
    public void ApplyDamage(float amount, Vector2 hitPoint, Vector2 hitNormal, GameObject attacker)
    {
        int dmg = Mathf.CeilToInt(Mathf.Max(0f, amount));
        if (dmg <= 0) return;

        currentHP -= dmg;

        if (currentHP <= 0)
            Die(hitPoint, hitNormal);
    }

    void Die(Vector2 hitPoint, Vector2 hitNormal)
    {
        // 폭발 VFX
        if (explosionVfxPrefab)
            Instantiate(explosionVfxPrefab, hitPoint, Quaternion.identity);

        // Scrap 드랍
        if (scrapPrefab)
        {
            int n = Random.Range(scrapMin, scrapMax + 1);
            for (int i = 0; i < n; i++)
            {
                var go = Instantiate(scrapPrefab, hitPoint, Quaternion.identity);

                // 퍼지기(있으면)
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb)
                {
                    Vector2 dir = (Random.insideUnitCircle.normalized + hitNormal).normalized;
                    rb.AddForce(dir * scrapImpulse, ForceMode2D.Impulse);
                }
            }
        }

        // 모듈 파괴
        Destroy(gameObject);
    }
}