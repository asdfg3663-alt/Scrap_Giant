using UnityEngine;

public interface IDamageable
{
    void ApplyDamage(float amount, Vector2 hitPoint, Vector2 hitNormal, GameObject attacker);
}