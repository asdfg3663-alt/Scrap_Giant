using UnityEngine;

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

    [Header("Manual Turning (RCS/Gyro)")]
    public float thrustToManualTurnTorque = 1.0f;
    public float baseManualTurnTorque = 0.0f;
    public float angularDamping = 0.0f;
    [Tooltip("이 RPM 이하로 느려지면 아래의 저속 감쇠를 사용합니다.")]
    public float lowAngularStopStartRPM = 6f;
    [Tooltip("저속 회전 구간에서 0RPM으로 천천히 수렴시키는 감쇠량(deg/sec^2).")]
    public float lowAngularStopDamping = 6f;

    [Header("Engine Torque Model")]
    public bool useModuleCenterOfMass = true;
    public float minModuleMassForCOM = 0.001f;

    [Header("Rotation Limit (RPM)")]
    [Tooltip("최대 회전속도(RPM). 예: 60RPM = 초당 1바퀴(360deg/s)")]
    public float maxRPM = 60f;

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

        float mass = Mathf.Max(minRigidbodyMass, stats.totalMass);
        float totalThrust = Mathf.Max(0f, stats.totalThrust);

        if (syncRigidbodyMassWithShipStats)
            rb.mass = mass;

        float accelEst = (totalThrust / mass) * accelMultiplier;
        float maxSpeed = Mathf.Max(0.1f, baseMaxSpeed + accelEst * maxSpeedFromAccelMultiplier);

        var modules = GetComponentsInChildren<ModuleInstance>(true);
        Vector2 com = useModuleCenterOfMass ? ComputeModuleCOM(modules) : rb.worldCenterOfMass;

        Vector2 sumForce = Vector2.zero;
        float sumTorque = 0f;

        // ===== Forward thrust (W) =====
        if (thrustInput > 0f && totalThrust > 0f)
        {
            if (modules != null)
            {
                foreach (var m in modules)
                {
                    if (m == null || m.data == null) continue;

                    // 전제: m.data.thrust (엔진 추력)
                    float t = m.GetThrust();
                    if (t <= 0f) continue;

                    Vector2 dir = (Vector2)m.transform.up;
                    Vector2 force = dir * (t * thrustInput);
                    Vector2 r = (Vector2)m.transform.position - com;

                    float torque = r.x * force.y - r.y * force.x;

                    sumForce += force;
                    sumTorque += torque;
                }
            }

            rb.AddForce(sumForce, ForceMode2D.Force);
            rb.AddTorque(sumTorque, ForceMode2D.Force);
        }

        // ===== Brake / reverse (S) =====
        if (thrustInput < 0f)
        {
            float brakeForce = brakeForceOverride > 0f
                ? brakeForceOverride
                : (totalThrust * Mathf.Max(0f, reverseMultiplier));

            if (brakeForce > 0f)
            {
                Vector2 v = rb.linearVelocity;
                if (v.sqrMagnitude > 0.000001f)
                {
                    Vector2 brakeDir = -v.normalized;
                    rb.AddForce(brakeDir * ((-thrustInput) * brakeForce), ForceMode2D.Force);
                }
            }

            if (rb.linearVelocity.magnitude < stopSnapSpeed)
                rb.linearVelocity = Vector2.zero;
        }

        // ===== Clamp max linear speed =====
        float speed = rb.linearVelocity.magnitude;
        if (speed > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        // ===== Manual turning (A/D) =====
        float manualTurnTorque = baseManualTurnTorque + totalThrust * thrustToManualTurnTorque;
        if (Mathf.Abs(turnInput) > 0.0001f && manualTurnTorque > 0f)
        {
            rb.AddTorque(-turnInput * manualTurnTorque, ForceMode2D.Force);
        }
        else
        {
            float angularSpeedRpm = Mathf.Abs(rb.angularVelocity) * 60f / 360f;
            float damping = angularDamping;

            if (angularSpeedRpm <= lowAngularStopStartRPM && lowAngularStopDamping > 0f)
                damping = lowAngularStopDamping;

            if (damping > 0f)
                rb.angularVelocity = Mathf.MoveTowards(rb.angularVelocity, 0f, damping * Time.fixedDeltaTime);
        }

        // ===== Clamp max angular speed (RPM limit) =====
        // Rigidbody2D.angularVelocity 단위는 deg/sec
        float maxAngularDegPerSec = Mathf.Max(0f, maxRPM) * 360f / 60f; // RPM -> deg/s
        if (maxAngularDegPerSec > 0f)
        {
            rb.angularVelocity = Mathf.Clamp(rb.angularVelocity, -maxAngularDegPerSec, maxAngularDegPerSec);
        }
    }

    Vector2 ComputeModuleCOM(ModuleInstance[] modules)
    {
        if (modules == null || modules.Length == 0)
            return rb.worldCenterOfMass;

        float totalM = 0f;
        Vector2 sum = Vector2.zero;

        foreach (var m in modules)
        {
            if (m == null || m.data == null) continue;

            // 전제: m.data.mass (모듈 질량)
            float mm = Mathf.Max(0f, m.GetMass());
            if (mm < minModuleMassForCOM) continue;

            totalM += mm;
            sum += (Vector2)m.transform.position * mm;
        }

        if (totalM <= 0.0001f)
            return rb.worldCenterOfMass;

        return sum / totalM;
    }
}
