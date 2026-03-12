using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHP : MonoBehaviour, IDamageable
{
    public int maxHP = 10;
    public int currentHP = 10;

    [Header("Death")]
    public GameObject explosionVfxPrefab;

    void Awake()
    {
        if (currentHP <= 0)
            currentHP = maxHP;
    }

    public void ApplyDamage(float amount, Vector2 hitPoint, Vector2 hitNormal, GameObject attacker)
    {
        int damage = Mathf.CeilToInt(Mathf.Max(0f, amount));
        if (damage <= 0)
            return;

        currentHP -= damage;
        if (currentHP <= 0)
            Die(hitPoint);
    }

    void Die(Vector2 hitPoint)
    {
        if (explosionVfxPrefab)
            Instantiate(explosionVfxPrefab, hitPoint, Quaternion.identity);

        WorldResourceUtility.AwardScrap(Mathf.Max(1, maxHP));
        Destroy(gameObject);
    }
}
