using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// WeaponLaser (Unity 6 / URP 대응 완성본)
/// - Space(또는 ShipCombatInput.FireHeld) 누르는 동안: 라인 렌더러 표시 + 히트스캔
/// - 데미지: weaponFireRate 기반 tick
/// - 전력 소모: 발사 중에만 powerUsePerSec * dt + (옵션) tick마다 weaponPowerPerShot
/// - 라인이 안 보이는 문제(URP 머티리얼/Sorting Layer/레이어 culling) 강제 해결
/// </summary>
[DisallowMultipleComponent]
public class WeaponLaser : MonoBehaviour
{
    [Header("Refs")]
    public Transform muzzle;
    public LayerMask hitMask = ~0;

    [Header("Raycast")]
    public float defaultRange = 20f;

    [Header("Visual (Force Visible)")]
    public bool useLineRenderer = true;
    public LineRenderer line;
    public float lineWidth = 0.25f;               // 일부러 크게 (안 보임 방지)
    public string sortingLayerName = "Default";   // 스프라이트와 같은 레이어로
    public int sortingOrder = 9999;

    [Header("Direction")]
    public bool useRightAsForward = true;
    public bool flipDirection = false;

    [Header("Debug")]
    public bool drawDebugRayInScene = false;

    float cd;
    ShipStats ship;
    ModuleInstance inst;

    void Awake()
    {
        if (!muzzle) muzzle = transform;

        ship = GetComponentInParent<ShipStats>();
        inst = GetComponent<ModuleInstance>();

        if (useLineRenderer)
            EnsureLine();
    }

    void EnsureLine()
    {
        if (line == null)
        {
            line = GetComponent<LineRenderer>();
            if (!line) line = gameObject.AddComponent<LineRenderer>();
        }

        // 라인이 카메라 컬링에 안 걸리게: 같은 레이어 사용
        line.gameObject.layer = gameObject.layer;

        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        // 2D 정렬 강제 (Sorting Layer까지)
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;
        line.alignment = LineAlignment.View;

        // 그림자/조명 영향 제거(URP에서 가끔 안 보이는 원인 제거)
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;

        // ✅ URP에서 "색/알파가 적용 안 돼서 안 보임" 방지: 머티리얼 강제 + 색 프로퍼티 동시 세팅
        if (line.sharedMaterial == null)
        {
            Shader sh =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color");

            if (sh != null)
                line.sharedMaterial = new Material(sh);
        }

        if (line.sharedMaterial != null)
        {
            // 흰색/알파1 강제
            Color c = Color.white;

            // URP Unlit 계열은 _BaseColor, 레거시는 _Color
            if (line.sharedMaterial.HasProperty("_BaseColor"))
                line.sharedMaterial.SetColor("_BaseColor", c);
            if (line.sharedMaterial.HasProperty("_Color"))
                line.sharedMaterial.SetColor("_Color", c);

            // 혹시 투명 블렌딩 꼬임 방지: 기본 렌더큐 유지
        }

        line.enabled = false;
    }

    void Update()
    {
        bool fireHeld = ShipCombatInput.FireHeld || Input.GetKey(KeyCode.Space);
        if (!fireHeld)
        {
            SetLineEnabled(false);
            return;
        }

        if (ship == null) ship = GetComponentInParent<ShipStats>();
        if (inst == null) inst = GetComponent<ModuleInstance>();

        if (inst == null || inst.data == null)
        {
            SetLineEnabled(false);
            return;
        }

        var d = inst.data;

        if (d.weaponType != WeaponType.Laser)
        {
            SetLineEnabled(false);
            return;
        }

        float fireRate = Mathf.Max(0f, d.weaponFireRate);
        if (fireRate <= 0f)
        {
            SetLineEnabled(false);
            return;
        }

        float damage = Mathf.Max(0f, d.weaponDamage);

        // 에너지: 발사 중에만
        float costPerSec = Mathf.Max(0f, d.powerUsePerSec);
        float costThisFrame = costPerSec * Time.deltaTime;

        if (ship != null && costThisFrame > 0f)
        {
            if (!ship.TryConsumeBattery(costThisFrame))
            {
                SetLineEnabled(false);
                return;
            }
        }

        // 레이캐스트 + 라인
        var hit = DoRaycast(defaultRange, out Vector3 origin3, out Vector3 end3);
        SetLine(origin3, end3);

        // 데미지 tick
        cd -= Time.deltaTime;
        if (cd > 0f) return;

        float shotCost = Mathf.Max(0f, d.weaponPowerPerShot);
        if (ship != null && shotCost > 0f)
        {
            if (!ship.TryConsumeBattery(shotCost))
                return;
        }

        if (hit.collider != null && damage > 0f)
        {
            var hp = hit.collider.GetComponent<EnemyHP>();
            if (hp != null) hp.TakeDamage(damage);
        }

        cd = 1f / fireRate;
    }

    Vector2 GetForwardDir()
    {
        Vector2 dir = useRightAsForward ? (Vector2)muzzle.right : (Vector2)muzzle.up;
        if (flipDirection) dir = -dir;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        return dir.normalized;
    }

    RaycastHit2D DoRaycast(float range, out Vector3 origin3, out Vector3 end3)
    {
        Vector2 origin = muzzle ? (Vector2)muzzle.position : (Vector2)transform.position;
        Vector2 dir = GetForwardDir();

        var hit = Physics2D.Raycast(origin, dir, range, hitMask);
        Vector2 end = (hit.collider != null) ? hit.point : origin + dir * range;

        // ✅ 라인렌더러는 3D 포지션이므로 z를 "스프라이트와 같은 평면"으로 고정
        float z = (muzzle != null) ? muzzle.position.z : transform.position.z;
        origin3 = new Vector3(origin.x, origin.y, z);
        end3 = new Vector3(end.x, end.y, z);

        if (drawDebugRayInScene)
            Debug.DrawRay(origin, dir * range, Color.red, 0.02f);

        return hit;
    }

    void SetLine(Vector3 a, Vector3 b)
    {
        if (!useLineRenderer) return;
        EnsureLine();

        // 런타임 중 값이 바뀌었을 수 있어 재강제
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        line.enabled = true;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }

    void SetLineEnabled(bool on)
    {
        if (!useLineRenderer) return;
        if (line == null) return;
        line.enabled = on;
    }
}