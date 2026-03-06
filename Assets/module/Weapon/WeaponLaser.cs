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

    [Header("Beam Visual (Sprite)")]
    public float beamThickness = 0.12f;
    public int beamSortingOrder = 9999;
    public string beamSortingLayer = "Default";
    public Color beamColor = Color.red;

    [Header("Direction")]
    public ForwardAxis forwardAxis = ForwardAxis.Right;
    public bool flipDirection = false;

    float cd;
    ShipStats ship;
    ModuleInstance inst;

    GameObject beamGO;
    SpriteRenderer beamSR;
    Transform beamT;
    Sprite beamSprite;

    void Awake()
    {
        if (!muzzle) muzzle = transform;
        ship = GetComponentInParent<ShipStats>();
        inst = GetComponent<ModuleInstance>();

        EnsureBeam();
        SetBeamVisible(false);
    }

    void EnsureBeam()
    {
        if (beamGO != null) return;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        beamSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

        beamGO = new GameObject("LaserBeam");
        beamGO.transform.SetParent(transform, worldPositionStays: true);
        beamT = beamGO.transform;

        beamSR = beamGO.AddComponent<SpriteRenderer>();
        beamSR.sprite = beamSprite;

        Shader sh =
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Unlit/Texture");
        if (sh != null) beamSR.sharedMaterial = new Material(sh);

        beamSR.color = beamColor;
        beamSR.sortingLayerName = beamSortingLayer;
        beamSR.sortingOrder = beamSortingOrder;
    }

    void Update()
{
    // 입력은 무조건 전역값만 사용
    if (!ShipCombatInput.FireHeld)
    {
        SetBeamVisible(false);
        return;
    }

    // ✅ 핵심: 매 프레임 현재 부모 ship를 다시 계산해서 "플레이어쉽에 붙은 무기만" 발사
    ShipStats playerShip = ShipCombatInput.ActivePlayerShip;
    ShipStats myShipNow = GetComponentInParent<ShipStats>();

    if (playerShip == null || myShipNow == null || myShipNow != playerShip)
    {
        SetBeamVisible(false);
        return;
    }

    // 이후 로직은 myShipNow를 ship으로 사용
    ShipStats ship = myShipNow;

    if (inst == null) inst = GetComponent<ModuleInstance>();
    if (inst == null || inst.data == null)
    {
        SetBeamVisible(false);
        return;
    }

    var d = inst.data;
    if (d.weaponType != WeaponType.Laser)
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

    // 발사 중에만 에너지
    float costThisFrame = Mathf.Max(0f, inst.GetPowerUsePerSec()) * Time.deltaTime;
    if (costThisFrame > 0f && !ship.TryConsumeBattery(costThisFrame))
    {
        SetBeamVisible(false);
        return;
    }

    Vector2 origin = (Vector2)muzzle.position;
    Vector2 dir = GetForwardDir();
    origin += dir * startOffset;

    float range = defaultRange;
    Vector2 end = origin + dir * range;

    var hits = Physics2D.RaycastAll(origin, dir, range, hitMask);
    RaycastHit2D best = default;
    bool found = false;

    for (int i = 0; i < hits.Length; i++)
    {
        var h = hits[i];
        if (h.collider == null) continue;
        if (h.collider.transform.IsChildOf(ship.transform)) continue; // 내 배 무시

        best = h;
        found = true;
        break;
    }

    if (found) end = best.point;

    UpdateBeam(origin, end);

    cd -= Time.deltaTime;
    if (cd > 0f) return;

    float shotCost = Mathf.Max(0f, inst.GetWeaponPowerPerShot());
    if (shotCost > 0f && !ship.TryConsumeBattery(shotCost))
        return;

    float damage = Mathf.Max(0f, inst.GetWeaponDamage());

    if (found && best.collider != null && damage > 0f)
    {
        var dmgTarget = best.collider.GetComponentInParent<IDamageable>();
        if (dmgTarget != null)
        {
            Vector2 normal = best.normal;
            if (normal.sqrMagnitude < 0.0001f) normal = -dir;
            dmgTarget.ApplyDamage(damage, best.point, normal, gameObject);
        }
    }

    cd = 1f / fireRate;
}

    Vector2 GetForwardDir()
    {
        Vector2 dir = forwardAxis == ForwardAxis.Right ? (Vector2)muzzle.right : (Vector2)muzzle.up;
        if (flipDirection) dir = -dir;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        return dir.normalized;
    }

    void SetBeamVisible(bool on)
    {
        if (beamGO == null) return;
        if (beamGO.activeSelf != on) beamGO.SetActive(on);
    }

    void UpdateBeam(Vector2 origin, Vector2 end)
    {
        EnsureBeam();
        SetBeamVisible(true);

        Vector2 delta = end - origin;
        float length = delta.magnitude;
        if (length < 0.05f) length = 0.05f;

        float z = muzzle.position.z;
        beamT.position = new Vector3(origin.x, origin.y, z);

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        beamT.rotation = Quaternion.Euler(0f, 0f, angle);

        beamT.localScale = new Vector3(length, beamThickness, 1f);

        beamSR.color = beamColor;
        beamSR.sortingLayerName = beamSortingLayer;
        beamSR.sortingOrder = beamSortingOrder;
    }
}
