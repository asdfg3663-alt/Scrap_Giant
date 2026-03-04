using UnityEngine;

/// <summary>
/// 2D 우주선 이동(우주 물리)
/// - W: 엔진 추력방향으로 가속 (최대속도까지)
/// - S: 약한 역추력(브레이크) (기본추력보다 훨씬 약하게)
/// - A/D: 회전
///
/// 핵심:
/// - 엔진이 없으면 코어(루트) 방향(transform.up)
/// - 엔진이 있으면 "엔진들의 방향/추력"을 합쳐서 나온 결과 방향으로 가속
/// - W를 떼어도 자동 감속하지 않음(관성)
///
/// 중요:
/// - stats.totalMass 값이 커져도 Rigidbody2D.mass를 바꾸지 않으면,
///   AddForce의 가속이 질량에 따라 달라지지 않습니다.
///   => rb.mass 를 stats.totalMass로 동기화해야 "무거울수록 둔해짐"이 제대로 됩니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(ShipStats))]
public class ShipMovement : MonoBehaviour
{
    [Header("Rotation")]
    public float rotationSpeed = 180f;

    [Header("Space Movement")]
    [Tooltip("최대 속도(클램프). 우주라 무한가속이지만 게임성 때문에 상한을 둡니다.")]
    public float baseMaxSpeed = 8f;

    [Tooltip("thrust/mass(가속도) 기반으로 maxSpeed를 가산하는 계수 (원하면 0으로)")]
    public float maxSpeedFromAccelMultiplier = 6f;

    [Tooltip("가속(추력) 적용 계수 (thrust/mass에 곱해짐)")]
    public float accelMultiplier = 1f;

    [Header("Physics Sync")]
    [Tooltip("ShipStats.totalMass를 Rigidbody2D.mass에 동기화 (질량이 실제 물리에 반영되도록)")]
    public bool syncRigidbodyMassWithShipStats = true;

    [Tooltip("rb.mass에 적용할 최소 질량 (0이면 물리가 깨질 수 있어 방지용)")]
    public float minRigidbodyMass = 0.01f;

    [Header("Brake / Reverse")]
    [Tooltip("S키 브레이크(역추력) 힘. 0 이하이면 totalThrust*reverseMultiplier를 사용")]
    public float brakeForceOverride = 1f;

    [Tooltip("brakeForceOverride가 0 이하일 때만 사용: totalThrust에 곱해 역추력으로 사용")]
    public float reverseMultiplier = 0.25f;

    [Tooltip("미세 흔들림 방지용 속도 스냅(이 값 이하로 떨어지면 0으로)")]
    public float stopSnapSpeed = 0.05f;

    ShipStats stats;
    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<ShipStats>();

        // 우주 느낌: 자동 감속 금지 (drag는 0)
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        // 혹시 중력이 켜져있으면 꺼두기(프로젝트 설정에 따라 달라서 안전장치)
        rb.gravityScale = 0f;
    }

    void FixedUpdate()
    {
        if (stats == null) return;

        // 플레이어만 조종(잔해/적은 별도 AI에서)
        if (!stats.isPlayerShip) return;

        // 입력
        float thrustInput = Input.GetAxisRaw("Vertical");   // W=1, S=-1
        float turnInput   = Input.GetAxisRaw("Horizontal"); // D=1, A=-1

        // 현재 엔진 기반 추력 방향 계산
        Vector2 thrustDir = GetThrustDirection();

        // 총추력/질량 (ShipStats가 모듈 합산으로 계산한 값)
        float mass = Mathf.Max(minRigidbodyMass, stats.totalMass);
        float totalThrust = Mathf.Max(0f, stats.totalThrust);

        // ✅ 핵심 수정: Rigidbody2D.mass에 실제 질량을 반영해야
        // 같은 추력이라도 무거운 배가 덜 가속합니다.
        if (syncRigidbodyMassWithShipStats)
        {
            // 매 프레임 동기화(모듈이 붙었다 떼도 즉시 반영)
            rb.mass = mass;
        }

        // (참고용) ShipStats 기반 가속도 추정치 (디버그/최대속도 계산용)
        float accel = (totalThrust / mass) * accelMultiplier;

        // 최대속도 (원래 로직 유지: thrust/mass에 비례해서 조금 올라가게)
        float maxSpeed = Mathf.Max(0.1f, baseMaxSpeed + accel * maxSpeedFromAccelMultiplier);

        // --- 전진(W): 엔진 방향으로 가속 ---
        if (thrustInput > 0f && totalThrust > 0f)
        {
            // AddForce는 Rigidbody2D.mass를 고려해 가속도가 결정됨.
            Vector2 force = thrustDir * (thrustInput * totalThrust);
            rb.AddForce(force, ForceMode2D.Force);
        }

        // --- 감속/후진(S): 약한 역추력 ---
        if (thrustInput < 0f)
        {
            float brakeForce = brakeForceOverride > 0f
                ? brakeForceOverride
                : (totalThrust * Mathf.Max(0f, reverseMultiplier));

            if (brakeForce > 0f)
            {
                Vector2 force = -thrustDir * ((-thrustInput) * brakeForce);
                rb.AddForce(force, ForceMode2D.Force);
            }

            // 너무 느려지면 깔끔하게 정지 스냅
            if (rb.linearVelocity.magnitude < stopSnapSpeed)
                rb.linearVelocity = Vector2.zero;
        }

        // --- 최대속도 제한(관성 유지하되 상한만 둠) ---
        float speed = rb.linearVelocity.magnitude;
        if (speed > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        // --- 회전(A/D) ---
        rb.MoveRotation(rb.rotation - turnInput * rotationSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 엔진 모듈이 있으면: 각 엔진의 방향(transform.up) * 엔진추력을 가중합한 결과 방향
    /// 엔진이 없으면: 루트(transform.up)
    ///
    /// ※ 엔진의 "추력 방향"은 엔진 오브젝트의 up을 기준으로 합니다.
    /// (모듈을 회전/스냅해 붙이면 그 방향대로 날아감)
    /// </summary>
    Vector2 GetThrustDirection()
    {
        Vector2 sum = Vector2.zero;

        var modules = GetComponentsInChildren<ModuleInstance>(true);
        if (modules != null)
        {
            foreach (var m in modules)
            {
                if (m == null || m.data == null) continue;

                // "엔진" 판정: thrust가 0보다 크면 엔진 취급
                float t = m.data.thrust;
                if (t <= 0f) continue;

                sum += (Vector2)m.transform.up * t;
            }
        }

        if (sum.sqrMagnitude < 0.0001f)
            return transform.up;

        return sum.normalized;
    }
}