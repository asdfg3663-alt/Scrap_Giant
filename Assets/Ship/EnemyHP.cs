using UnityEngine;

/// <summary>
/// 테스트용 간단 적 HP (나중에 적 시스템으로 교체)
/// </summary>
public class EnemyHP : MonoBehaviour, IDamageable
{
    public float hp = 30f;

    public void TakeDamage(float damage)
    {
        hp -= damage;
        if (hp <= 0f) Destroy(gameObject);
    }
}
