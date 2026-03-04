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
    public float beamThickness = 0.18f;
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
        // 최신 참조 보정
        if (ship == null) ship = GetComponentInParent<ShipStats>();
        if (inst == null) inst = GetComponent<ModuleInstance>();

        // === [문제 1 해결] ===
        // 1) 함선에 "부착"된 모듈이어야 함 (부착 안된 월드 모듈은 ship == null)
        // 2) 플레이어 함선 소유 모듈이어야 입력으로 발사 가능
        if (ship == null || !ship.isPlayerShip)
        {
            SetBeamVisible(false);
            return;
        }

        // 입력은 ShipCombatInput만 사용 (스페이스바 직접 체크 제거: 중립 모듈 오발 방지)
        bool fireHeld = ShipCombatInput.FireHeld;
        if (!fireHeld)
        {
            SetBeamVisible(false);
            return;
        }

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

        float fireRate = Mathf.Max(0f, d.weaponFireRate);
        if (fireRate <= 0f)
        {
            SetBeamVisible(false);
            return;
        }

        // 발사 중에만 에너지(초당 소모)
        float costThisFrame = Mathf.Max(0f, d.powerUsePerSec) * Time.deltaTime;
        if (costThisFrame > 0f && !ship.TryConsumeBattery(costThisFrame))
        {
            SetBeamVisible(false);
            return;
        }

        Vector2 origin = muzzle ? (Vector2)muzzle.position : (Vector2)transform.position;
        Vector2 dir = GetForwardDir();

        // 자기 히트 방지(시작점을 살짝 앞으로)
        origin += dir * startOffset;

        float range = defaultRange;
        Vector2 endDefault = origin + dir * range;

        // RaycastAll로 충돌 전체 수집
        var hits = Physics2D.RaycastAll(origin, dir, range, hitMask);

        // === [문제 2 해결] ===
        // - 빔 끝점: "첫 번째 유효 충돌"을 사용 (시각적 일관성)
        // - 데미지 대상: hits 중 IDamageable이 있는 첫 번째 대상을 우선 선택
        RaycastHit2D firstHit = default;
        bool hasFirstHit = false;

        RaycastHit2D damageHit = default;
        bool hasDamageHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            // 내 배(플레이어 함선)의 콜라이더는 무시
            if (h.collider.transform.IsChildOf(ship.transform)) continue;

            // 첫 유효 히트(빔 끝점 용)
            if (!hasFirstHit)
            {
                firstHit = h;
                hasFirstHit = true;
            }

            // 데미지 줄 수 있는 대상 우선 선택
            var dmgTarget = h.collider.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                damageHit = h;
                hasDamageHit = true;
                break; // 첫 IDamageable을 우선 사용
            }
        }

        // 빔 끝점 결정
        Vector2 end = hasFirstHit ? firstHit.point : endDefault;

        // 빔 표시(매 프레임)
        UpdateBeam(origin, end);

        // 데미지 tick
        cd -= Time.deltaTime;
        if (cd > 0f) return;

        // (옵션) 샷당 소모
        float shotCost = Mathf.Max(0f, d.weaponPowerPerShot);
        if (shotCost > 0f && !ship.TryConsumeBattery(shotCost))
            return;

        float damage = Mathf.Max(0f, d.weaponDamage);

        // IDamageable 대상에만 데미지 적용
        if (hasDamageHit && damageHit.collider != null && damage > 0f)
        {
            var dmgTarget = damageHit.collider.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                Vector2 normal = damageHit.normal;
                if (normal.sqrMagnitude < 0.0001f) normal = -dir;
                dmgTarget.ApplyDamage(damage, damageHit.point, normal, gameObject);
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

        float z = muzzle ? muzzle.position.z : transform.position.z;
        beamT.position = new Vector3(origin.x, origin.y, z);

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        beamT.rotation = Quaternion.Euler(0f, 0f, angle);

        beamT.localScale = new Vector3(length, beamThickness, 1f);

        beamSR.color = beamColor;
        beamSR.sortingLayerName = beamSortingLayer;
        beamSR.sortingOrder = beamSortingOrder;
    }
}