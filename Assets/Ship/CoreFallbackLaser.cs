using UnityEngine;

[DisallowMultipleComponent]
public class CoreFallbackLaser : MonoBehaviour
{
    [Header("Fallback Weapon")]
    public LayerMask hitMask = ~0;
    public float dps = 1f;
    public float fireRate = 5f;
    public float range = 10f;
    public float startOffset = 0.4f;

    [Header("Damage Falloff")]
    public float fullDamageRange = 6f;
    [Range(0f, 1f)] public float minDamageMultiplierAtMaxRange = 0.65f;

    [Header("Beam Visual")]
    public float beamThickness = 0.08f;
    public int beamSortingOrder = 9998;
    public string beamSortingLayer = "Default";
    public Color beamColor = new Color(0.95f, 0.24f, 0.2f, 1f);

    float cooldown;
    GameObject beamGO;
    SpriteRenderer beamSR;
    Transform beamT;
    Sprite beamSprite;
    ShipStats ship;

    void Awake()
    {
        ship = GetComponent<ShipStats>();
        EnsureBeam();
        SetBeamVisible(false);
    }

    void Update()
    {
        if (GameRuntimeState.GameplayBlocked)
        {
            SetBeamVisible(false);
            return;
        }

        if (ship == null)
            ship = GetComponent<ShipStats>();

        if (ship == null || !ship.isPlayerShip)
        {
            SetBeamVisible(false);
            return;
        }

        if (ship.HasOperationalWeaponModules())
        {
            SetBeamVisible(false);
            return;
        }

        if (!ShipCombatInput.FireHeld || ShipCombatInput.ActivePlayerShip != ship)
        {
            SetBeamVisible(false);
            return;
        }

        Transform originTransform = ship.GetCoreTransform();
        Vector2 dir = originTransform != null ? (Vector2)originTransform.up : (Vector2)transform.up;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.up;
        dir.Normalize();

        Vector2 origin = (originTransform != null ? (Vector2)originTransform.position : (Vector2)transform.position) + dir * startOffset;
        Vector2 end = origin + dir * Mathf.Max(0f, range);

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, Mathf.Max(0f, range), hitMask);
        RaycastHit2D best = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null)
                continue;

            if (ShipCollisionHull2D.IsHullCollider(hit.collider))
                continue;

            if (hit.collider.transform.IsChildOf(ship.transform))
                continue;

            best = hit;
            found = true;
            break;
        }

        if (found)
            end = best.point;

        UpdateBeam(origin, end, originTransform != null ? originTransform.position.z : transform.position.z);
        AudioRuntime.RequestLaserLoop();

        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
            return;

        float damage = Mathf.Max(0f, dps) / Mathf.Max(1f, fireRate);
        if (damage <= 0f)
            return;

        if (found && best.collider != null)
        {
            float hitDistance = Vector2.Distance(origin, end);
            float damageMultiplier = GetDamageMultiplier(hitDistance);
            var target = best.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                Vector2 normal = best.normal;
                if (normal.sqrMagnitude < 0.0001f)
                    normal = -dir;

                target.ApplyDamage(damage * damageMultiplier, best.point, normal, gameObject);
            }
        }

        cooldown = 1f / Mathf.Max(0.01f, fireRate);
    }

    float GetDamageMultiplier(float distance)
    {
        float clampedRange = Mathf.Max(fullDamageRange, range);
        if (distance <= fullDamageRange || clampedRange <= fullDamageRange)
            return 1f;

        float t = Mathf.InverseLerp(fullDamageRange, clampedRange, distance);
        return Mathf.Lerp(1f, Mathf.Clamp01(minDamageMultiplierAtMaxRange), t);
    }

    void EnsureBeam()
    {
        if (beamGO != null)
            return;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);

        beamGO = new GameObject("FallbackLaserBeam");
        beamGO.transform.SetParent(transform, true);
        beamT = beamGO.transform;

        beamSR = beamGO.AddComponent<SpriteRenderer>();
        beamSR.sprite = beamSprite;

        Shader shader =
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Unlit/Texture");
        if (shader != null)
            beamSR.sharedMaterial = new Material(shader);

        beamSR.color = beamColor;
        beamSR.sortingLayerName = beamSortingLayer;
        beamSR.sortingOrder = beamSortingOrder;
    }

    void SetBeamVisible(bool visible)
    {
        if (beamGO != null && beamGO.activeSelf != visible)
            beamGO.SetActive(visible);
    }

    void UpdateBeam(Vector2 origin, Vector2 end, float zPosition)
    {
        EnsureBeam();
        SetBeamVisible(true);

        Vector2 delta = end - origin;
        float length = Mathf.Max(0.05f, delta.magnitude);

        beamT.position = new Vector3(origin.x, origin.y, zPosition);
        beamT.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        beamT.localScale = new Vector3(length, beamThickness, 1f);

        beamSR.color = beamColor;
        beamSR.sortingLayerName = beamSortingLayer;
        beamSR.sortingOrder = beamSortingOrder;
    }
}
