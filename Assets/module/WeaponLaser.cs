using UnityEngine;

/// <summary>
/// MVP 레이저 무기 (히트스캔)
/// - transform.up 방향으로 발사
/// - ModuleInstance.data 기반으로 damage/fireRate/power 소모
/// - 전력이 부족하면 발사하지 않음
/// </summary>
[DisallowMultipleComponent]
public class WeaponLaser : MonoBehaviour
{
    [Header("Raycast")]
    public float range = 12f;
    public LayerMask hitMask = ~0; // 필요하면 Enemy 레이어만 지정

    [Header("Visual (optional)")]
    public LineRenderer line;       // 넣으면 선으로 표시
    public float lineShowSec = 0.03f;

    ModuleInstance inst;
    ShipStats ship;
    float cd;

    void Awake()
    {
        inst = GetComponent<ModuleInstance>();
        ship = GetComponentInParent<ShipStats>();

        if (line != null)
        {
            line.enabled = false;
            line.positionCount = 2;
            line.useWorldSpace = true;
        }
    }

    void OnEnable()
    {
        cd = 0f;
    }

    void Update()
    {
        // 붙어있는 상태에서만 동작 (떼어져서 우주에 떠있으면 사격 안 함)
        if (ship == null)
        {
            ship = GetComponentInParent<ShipStats>();
            if (ship == null) return;
        }

        if (inst == null || inst.data == null) return;
        var d = inst.data;

        if (d.weaponType != WeaponType.Laser) return;

        float fireRate = Mathf.Max(0f, d.weaponFireRate);
        if (fireRate <= 0f) return;

        cd -= Time.deltaTime;

        // MVP: 자동사격(항상 발사) - 나중에 입력/적감지로 교체
        if (cd > 0f) return;

        // 전력 체크 (배터리에서 차감)
        float cost = Mathf.Max(0f, d.weaponPowerPerShot);
        if (cost > 0f && ship.energyCurrent < cost) return;

        ship.energyCurrent = Mathf.Clamp(ship.energyCurrent - cost, 0f, ship.energyMax);

        Fire(Mathf.Max(0f, d.weaponDamage));

        cd = 1f / fireRate;
    }

    void Fire(float damage)
    {
        Vector3 origin = transform.position;
        Vector3 dir = transform.up;

        var hit = Physics2D.Raycast(origin, dir, range, hitMask);

        Vector3 end = origin + dir * range;
        if (hit.collider != null)
        {
            end = hit.point;

            // 데미지 적용: EnemyHP가 있으면 사용, 아니면 IDamageable
            var hp = hit.collider.GetComponentInParent<EnemyHP>();
            if (hp != null)
            {
                hp.TakeDamage(damage);
            }
            else
            {
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null) dmg.TakeDamage(damage);
            }
        }

        Debug.DrawLine(origin, end, Color.red, 0.05f);

        if (line != null)
        {
            line.enabled = true;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            CancelInvoke(nameof(HideLine));
            Invoke(nameof(HideLine), lineShowSec);
        }
    }

    void HideLine()
    {
        if (line != null) line.enabled = false;
    }
}
