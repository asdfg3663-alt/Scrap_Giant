using UnityEngine;

/// <summary>
/// ShipMovement (2D)
/// - 전진: 엔진 추력 방향으로 AddForce
/// - 회전: 토크 기반(AddTorque) => 질량/모양/추력의 영향 반영
/// - 모양 영향: 모듈 질량 분포로 관성(inertia) 근사 계산
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(ShipStats))]
public class ShipMovement : MonoBehaviour
{
    [Header("Linear Movement")]
    public float baseMaxSpeed = 8f;
    public float maxSpeedFromAccelMultiplier = 6f;
    public float accelMultiplier = 1f;

    [Header("Physics Sync")]
    public bool syncRigidbodyMassWithShipStats = true;
    public float minRigidbodyMass = 0.01f;

    [Header("Brake / Reverse")]
    public float brakeForceOverride = 1f;
    public float reverseMultiplier = 0.25f;
    public float stopSnapSpeed = 0.05f;

    [Header("Rotation (Torque-based)")]
    [Tooltip("기본 회전 토크 (추력 0이어도 최소 조향감을 줄지 여부)")]
    public float baseTurnTorque = 0.0f;

    [Tooltip("전체추력(totalThrust)을 회전 토크로 변환하는 계수. 높을수록 추력이 클 때 더 잘 돌아감.")]
    public float thrustToTurnTorque = 1.0f;

    [Tooltip("관성(모양) 계산 결과에 곱하는 스케일. 너무 둔하면 낮추고, 너무 민감하면 높이세요.")]
    public float inertiaScale = 1.0f;

    [Tooltip("아주 작은 관성 값 방지용 최소값")]
    public float minInertia = 0.05f;

    [Tooltip("회전 입력이 없을 때의 자연 감속(우주 느낌이면 0~작게)")]
    public float angularDamping = 0.0f;

    [Tooltip("엔진이 없을 때도 회전 가능하게 할지(코어 자체 RCS 같은 느낌)")]
    public bool allowTurnWithoutEngines = true;

    ShipStats stats;
    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<ShipStats>();

        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.gravityScale = 0f;
    }

    void FixedUpdate()
    {
        if (stats == null) return;
        if (!stats.isPlayerShip) return;

        float thrustInput = Input.GetAxisRaw("Vertical");   // W=1, S=-1
        float turnInput   = Input.GetAxisRaw("Horizontal"); // D=1, A=-1

        // --- Mass / Thrust ---
        float mass = Mathf.Max(minRigidbodyMass, stats.totalMass);
        float totalThrust = Mathf.Max(0f, stats.totalThrust);

        if (syncRigidbodyMassWithShipStats)
            rb.mass = mass;

        // --- Thrust direction (engine-weighted) ---
        Vector2 thrustDir = GetThrustDirection();

        // --- Linear accel estimate (for maxSpeed only) ---
        float accel = (totalThrust / mass) * accelMultiplier;
        float maxSpeed = Mathf.Max(0.1f, baseMaxSpeed + accel * maxSpeedFromAccelMultiplier);

        // --- Forward thrust ---
        if (thrustInput > 0f && totalThrust > 0f)
        {
            Vector2 force = thrustDir * (thrustInput * totalThrust);
            rb.AddForce(force, ForceMode2D.Force);
        }

        // --- Brake / reverse ---
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

            if (rb.linearVelocity.magnitude < stopSnapSpeed)
                rb.linearVelocity = Vector2.zero;
        }

        // --- Clamp max speed ---
        float speed = rb.linearVelocity.magnitude;
        if (speed > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        // =========================
        // Rotation: torque-based
        // =========================

        // 1) 모양/질량 분포로 관성(inertia) 근사 계산
        float inertia = ComputeInertiaApprox();
        inertia = Mathf.Max(minInertia, inertia * Mathf.Max(0.0001f, inertiaScale));

        // 2) 회전 토크: 기본 + (전체추력 기반)
        //    - 추력 클수록 더 잘 돈다
        float availableTurnTorque = baseTurnTorque + totalThrust * thrustToTurnTorque;

        // 엔진이 하나도 없으면 totalThrust=0일 가능성이 큼 -> allowTurnWithoutEngines=false면 회전 금지
        if (!allowTurnWithoutEngines && totalThrust <= 0.0001f)
            availableTurnTorque = 0f;

        // 3) 입력 -> 토크 적용
        if (Mathf.Abs(turnInput) > 0.0001f && availableTurnTorque > 0f)
        {
            // AddTorque는 Rigidbody2D의 관성/각가속에 반영됨
            // (관성은 Unity 내부값도 있지만, 우리는 "모양 관성"을 직접 반영하려고
            //  availableTurnTorque를 inertia로 나눠 토크를 조정)
            float torque = (-turnInput) * (availableTurnTorque / inertia);

            rb.AddTorque(torque, ForceMode2D.Force);
        }
        else
        {
            // 입력 없을 때 약한 각속도 감쇠(우주 느낌이면 0으로)
            if (angularDamping > 0f)
                rb.angularVelocity = Mathf.MoveTowards(rb.angularVelocity, 0f, angularDamping * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// 엔진 모듈이 있으면: 각 엔진 방향(transform.up) * thrust 가중합 방향
    /// 엔진이 없으면: 루트(transform.up)
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

                float t = m.data.thrust;
                if (t <= 0f) continue;

                sum += (Vector2)m.transform.up * t;
            }
        }

        if (sum.sqrMagnitude < 0.0001f)
            return transform.up;

        return sum.normalized;
    }

    /// <summary>
    /// 모듈들의 질량과 중심으로부터의 거리로 2D 관성(I = Σ m r^2) 근사.
    /// - 모양이 길게 퍼질수록 r이 커져 I 증가 => 회전 둔해짐
    /// - 질량이 커질수록 I 증가 => 회전 둔해짐
    ///
    /// COM(질량중심)을 먼저 구해서 중심 기준 거리로 계산하면 더 정확해짐.
    /// </summary>
    float ComputeInertiaApprox()
    {
        var modules = GetComponentsInChildren<ModuleInstance>(true);
        if (modules == null || modules.Length == 0)
            return 1f;

        // 1) COM 구하기(질량중심)
        float totalM = 0f;
        Vector2 com = Vector2.zero;

        foreach (var m in modules)
        {
            if (m == null || m.data == null) continue;

            float mm = Mathf.Max(0f, m.data.mass);
            if (mm <= 0f) continue;

            totalM += mm;
            com += (Vector2)m.transform.position * mm;
        }

        if (totalM <= 0.0001f)
            return 1f;

        com /= totalM;

        // 2) I = Σ m r^2
        float I = 0f;
        foreach (var m in modules)
        {
            if (m == null || m.data == null) continue;

            float mm = Mathf.Max(0f, m.data.mass);
            if (mm <= 0f) continue;

            float r = Vector2.Distance((Vector2)m.transform.position, com);
            I += mm * r * r;
        }

        // 최소값 보정
        return Mathf.Max(0.0001f, I);
    }
}