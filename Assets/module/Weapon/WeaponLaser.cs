using UnityEngine;

[DisallowMultipleComponent]
public class WeaponLaser : MonoBehaviour
{
    public enum ForwardAxis { Right, Up }

    [Header("Refs")]
    public Transform muzzle;
    public LayerMask hitMask = ~0;

    [Header("Raycast")]
    public float defaultRange = 20f;
    public float startOffset = 0.25f;

    [Header("Damage Falloff")]
    public float fullDamageRange = 5f;
    [Range(0f, 1f)] public float minDamageMultiplierAtMaxRange = 0.5f;

    [Header("Beam Visual (Sprite)")]
    public float beamThickness = 0.12f;
    public int beamSortingOrder = 9999;
    public string beamSortingLayer = "Default";
    public Color beamColor = Color.red;

    [Header("Direction")]
    public ForwardAxis forwardAxis = ForwardAxis.Right;
    public bool flipDirection = false;

    float cooldown;
    ModuleInstance inst;

    GameObject beamGO;
    SpriteRenderer beamSR;
    Transform beamT;
    Sprite beamSprite;

    void Awake()
    {
        if (!muzzle)
            muzzle = transform;

        inst = GetComponent<ModuleInstance>();
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

        var ship = GetComponentInParent<ShipStats>();
        if (ship == null)
        {
            SetBeamVisible(false);
            return;
        }

        if (!ShouldFire(ship))
        {
            SetBeamVisible(false);
            return;
        }

        if (inst == null)
            inst = GetComponent<ModuleInstance>();

        if (inst == null || inst.data == null || inst.data.weaponType != WeaponType.Laser)
        {
            SetBeamVisible(false);
            return;
        }

        float fireRate = Mathf.Max(0f, inst.GetWeaponFireRate());
        if (fireRate <= 0f)
        {
            SetBeamVisible(false);
            return;
        }

        if (ship.isPlayerShip)
        {
            float frameCost = Mathf.Max(0f, inst.GetPowerUsePerSec()) * Time.deltaTime;
            if (frameCost > 0f && !ship.TryConsumeBattery(frameCost))
            {
                SetBeamVisible(false);
                return;
            }
        }
        else
        {
            float frameCost = Mathf.Max(0f, inst.GetPowerUsePerSec()) * Time.deltaTime;
            if (!ship.CanFireWeaponsFromBattery() || (frameCost > 0f && !ship.TryConsumeWeaponBattery(frameCost)))
            {
                SetBeamVisible(false);
                return;
            }
        }

        inst.AddHeat(inst.GetDps() * Time.deltaTime);

        Vector2 dir = GetForwardDir();
        Vector2 origin = (Vector2)muzzle.position + dir * startOffset;
        Vector2 end = origin + dir * defaultRange;

        var hits = Physics2D.RaycastAll(origin, dir, defaultRange, hitMask);
        RaycastHit2D best = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(ship.transform))
                continue;

            best = hit;
            found = true;
            break;
        }

        if (found)
            end = best.point;

        UpdateBeam(origin, end);
        if (ship.isPlayerShip)
            AudioRuntime.RequestLaserLoop();

        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
            return;

        if (ship.isPlayerShip)
        {
            float shotCost = Mathf.Max(0f, inst.GetWeaponPowerPerShot());
            if (shotCost > 0f && !ship.TryConsumeBattery(shotCost))
                return;
        }
        else
        {
            float shotCost = Mathf.Max(0f, inst.GetWeaponPowerPerShot());
            if (!ship.CanFireWeaponsFromBattery() || (shotCost > 0f && !ship.TryConsumeWeaponBattery(shotCost)))
                return;
        }

        float hitDistance = found ? Vector2.Distance(origin, end) : defaultRange;
        float damageMultiplier = GetDamageMultiplier(hitDistance);
        float damage = Mathf.Max(0f, inst.GetEffectiveWeaponDamage()) * damageMultiplier;
        if (found && best.collider != null && damage > 0f)
        {
            var target = best.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                Vector2 normal = best.normal;
                if (normal.sqrMagnitude < 0.0001f)
                    normal = -dir;

                target.ApplyDamage(damage, best.point, normal, gameObject);
            }
        }

        cooldown = 1f / fireRate;
    }

    float GetDamageMultiplier(float distance)
    {
        float maxRange = Mathf.Max(fullDamageRange, defaultRange);
        if (distance <= fullDamageRange)
            return 1f;

        if (maxRange <= fullDamageRange)
            return 1f;

        float t = Mathf.InverseLerp(fullDamageRange, maxRange, distance);
        return Mathf.Lerp(1f, Mathf.Clamp01(minDamageMultiplierAtMaxRange), t);
    }

    bool ShouldFire(ShipStats ship)
    {
        if (ship.isPlayerShip)
            return ShipCombatInput.FireHeld && ShipCombatInput.ActivePlayerShip == ship;

        var enemyAI = ship.GetComponent<EnemyShipAI>();
        return enemyAI != null && enemyAI.WantsToFire;
    }

    Vector2 GetForwardDir()
    {
        Vector2 dir = forwardAxis == ForwardAxis.Right ? (Vector2)muzzle.right : (Vector2)muzzle.up;
        if (flipDirection)
            dir = -dir;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;

        return dir.normalized;
    }

    void EnsureBeam()
    {
        if (beamGO != null)
            return;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

        beamGO = new GameObject("LaserBeam");
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
        if (beamGO == null)
            return;

        if (beamGO.activeSelf != visible)
            beamGO.SetActive(visible);
    }

    void UpdateBeam(Vector2 origin, Vector2 end)
    {
        EnsureBeam();
        SetBeamVisible(true);

        Vector2 delta = end - origin;
        float length = Mathf.Max(0.05f, delta.magnitude);

        beamT.position = new Vector3(origin.x, origin.y, muzzle.position.z);
        beamT.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        beamT.localScale = new Vector3(length, beamThickness, 1f);

        beamSR.color = beamColor;
        beamSR.sortingLayerName = beamSortingLayer;
        beamSR.sortingOrder = beamSortingOrder;
    }
}
