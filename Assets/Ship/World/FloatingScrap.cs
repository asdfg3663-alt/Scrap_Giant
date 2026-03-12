using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class FloatingScrap : MonoBehaviour, IDamageable
{
    [SerializeField] float mass = 1f;
    [SerializeField] int currentHP = 1;

    Rigidbody2D rb;
    SpriteRenderer sr;

    public float Mass => mass;
    public int CurrentHP => currentHP;
    public int MaxHP => Mathf.Max(1, Mathf.CeilToInt(mass));
    public string DisplayName => "Floating Scrap";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        ApplyMass(mass);
    }

    public void Initialize(float scrapMass)
    {
        ApplyMass(scrapMass);

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.angularDamping = 0.05f;
            rb.linearDamping = 0.05f;
        }
    }

    public void ApplyDamage(float amount, Vector2 hitPoint, Vector2 hitNormal, GameObject attacker)
    {
        int damage = Mathf.CeilToInt(Mathf.Max(0f, amount));
        if (damage <= 0)
            return;

        currentHP -= damage;
        if (currentHP <= 0)
            Die();
    }

    void ApplyMass(float scrapMass)
    {
        mass = Mathf.Max(1f, scrapMass);
        currentHP = Mathf.Max(1, Mathf.CeilToInt(mass));

        float scale = Mathf.Clamp(0.75f + mass * 0.04f, 0.8f, 1.75f);
        transform.localScale = new Vector3(scale, scale, 1f);

        if (sr != null)
            sr.color = Color.Lerp(new Color(0.72f, 0.72f, 0.74f, 1f), new Color(0.93f, 0.71f, 0.3f, 1f), Mathf.InverseLerp(1f, 25f, mass));
    }

    void Die()
    {
        WorldResourceUtility.AwardScrapFromMass(mass);
        Destroy(gameObject);
    }
}
