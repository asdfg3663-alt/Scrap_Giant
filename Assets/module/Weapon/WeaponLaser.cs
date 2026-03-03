using UnityEngine;

/// <summary>
/// 레이저 무기 (히트스캔)
/// - 스페이스바(또는 모바일 버튼) 누르는 동안만 발사
/// - muzzle 위치/방향(muzzle.up)으로 발사
/// - ModuleInstance.data 기반 스탯 사용 (Laser만)
/// - ShipStats.energyCurrent에서 발사당 전력 소모
/// </summary>
[DisallowMultipleComponent]
public class WeaponLaser : MonoBehaviour
{
    [Header("Refs")]
    public Transform muzzle;              // 발사 위치 (없으면 transform 사용)
    public LayerMask hitMask = ~0;        // 맞출 레이어(나중에 Enemy만)

    [Header("Raycast")]
    public float defaultRange = 12f;      // data에 range가 없다면 이 값 사용

    [Header("Debug")]
    public bool drawDebugRay = true;

    float cd;
    ShipStats ship;
    ModuleInstance inst;

    void Awake()
    {
        if (!muzzle) muzzle = transform;
        ship = GetComponentInParent<ShipStats>();
        inst = GetComponent<ModuleInstance>();
    }

    void Update()
    {
        // 0) 입력: 누르는 동안만 발사
        bool fireHeld = (ShipCombatInput.FireHeld || Input.GetKey(KeyCode.Space));
if (!fireHeld) return;

        // 1) 붙어있는 상태(ShipStats 아래)에서만 동작
        if (ship == null)
        {
            ship = GetComponentInParent<ShipStats>();
            if (ship == null) return;
        }

        if (inst == null || inst.data == null) return;
        var d = inst.data;

        // 2) Laser 무기만 처리
        if (d.weaponType != WeaponType.Laser) return;

        // 3) 스탯
        float fireRate = Mathf.Max(0f, d.weaponFireRate);
        if (fireRate <= 0f) return;

        float damage = Mathf.Max(0f, d.weaponDamage);
        float cost = Mathf.Max(0f, d.weaponPowerPerShot);
        float range = defaultRange; // 필요하면 ModuleData에 range 추가해도 됨

        // 4) 쿨다운
        cd -= Time.deltaTime;
        if (cd > 0f) return;

        // 5) 전력 소비 시도 (부족하면 발사 안 함)
if (cost > 0f)
{
    if (!ship.TryConsumeBattery(cost))
        return;
}

// 6) 스페이스 홀드(발사 중)일 때만 초당 소모
float costPerSec = d.powerUsePerSec;                 // <= 이거 사용
float costThisFrame = costPerSec * Time.deltaTime;   // 프레임당 소모량

if (costThisFrame > 0f)
{
    if (!ship.TryConsumeBattery(costThisFrame))
        return; // 에너지 부족 -> 그 프레임은 발사 금지
}


        // 7) 발사
        FireOnce(damage, range);

        cd = 1f / fireRate;
    }

    void FireOnce(float damage, float range)
    {
        Vector2 origin = muzzle.position;
        Vector2 dir = muzzle.up;

        var hit = Physics2D.Raycast(origin, dir, range, hitMask);

        if (hit.collider != null)
        {
            // EnemyHP 우선
            var hp = hit.collider.GetComponent<EnemyHP>();
            if (hp) hp.TakeDamage(damage);
            else
            {
                // 혹시 나중에 IDamageable 인터페이스 쓸 거면 여기에 추가하면 됨
                // var dmg = hit.collider.GetComponent<IDamageable>();
                // if (dmg != null) dmg.TakeDamage(damage);
            }
        }

        if (drawDebugRay)
            Debug.DrawRay(origin, dir * range, Color.red, 0.05f);
    }
}