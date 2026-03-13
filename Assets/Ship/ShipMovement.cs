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
    public float thrustToManualTurnTorque = 0.15f;
    public float baseManualTurnTorque = 0.0f;
    public float manualTurnTorqueScale = 0.5f;
    public float angularDamping = 0.0f;
    public float lowAngularStopStartRPM = 6f;
    public float lowAngularStopDamping = 6f;

    [Header("Engine Torque Model")]
    public bool useModuleCenterOfMass = true;
    public float minModuleMassForCOM = 0.001f;

    [Header("Rotation Limit (RPM)")]
    public float maxRPM = 30f;

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
        if (GameRuntimeState.GameplayBlocked)
        {
            AudioRuntime.SetEngineLoopActive(false);
            return;
        }

        if (stats == null || !stats.isPlayerShip)
        {
            AudioRuntime.SetEngineLoopActive(false);
            return;
        }

        float thrustInput = Input.GetAxisRaw("Vertical");
        float turnInput = Input.GetAxisRaw("Horizontal");

        float mass = Mathf.Max(minRigidbodyMass, stats.totalMass);
        float totalThrust = Mathf.Max(0f, stats.totalThrust);
        float effectiveTotalThrust = Mathf.Max(0f, stats.GetEffectiveTotalThrust());
        float thrustScale = totalThrust > 0f ? (effectiveTotalThrust / totalThrust) : 0f;

        if (syncRigidbodyMassWithShipStats)
            rb.mass = mass;

        float accelEst = (effectiveTotalThrust / mass) * accelMultiplier;
        float maxSpeed = Mathf.Max(0.1f, baseMaxSpeed + accelEst * maxSpeedFromAccelMultiplier);

        var modules = GetComponentsInChildren<ModuleInstance>(true);
        Vector2 com = useModuleCenterOfMass ? ComputeModuleCOM(modules) : rb.worldCenterOfMass;

        Vector2 sumForce = Vector2.zero;
        float sumTorque = 0f;
        bool engineLoopActive = false;

        if (thrustInput > 0f && effectiveTotalThrust > 0f)
        {
            stats.ConsumeFuelForThrust(Time.fixedDeltaTime);
            engineLoopActive = true;

            if (modules != null)
            {
                foreach (var module in modules)
                {
                    if (module == null || module.data == null)
                        continue;

                    float thrust = module.GetThrust() * thrustScale;
                    if (thrust <= 0f)
                        continue;

                    Vector2 dir = module.transform.up;
                    Vector2 force = dir * (thrust * thrustInput);
                    Vector2 r = (Vector2)module.transform.position - com;
                    float torque = r.x * force.y - r.y * force.x;

                    sumForce += force;
                    sumTorque += torque;
                }
            }

            rb.AddForce(sumForce, ForceMode2D.Force);
            rb.AddTorque(sumTorque, ForceMode2D.Force);
        }

        if (thrustInput < 0f)
        {
            float brakeForce = brakeForceOverride > 0f
                ? brakeForceOverride
                : (effectiveTotalThrust * Mathf.Max(0f, reverseMultiplier));

            if (brakeForce > 0f)
            {
                Vector2 velocity = rb.linearVelocity;
                if (velocity.sqrMagnitude > 0.000001f)
                {
                    Vector2 brakeDir = -velocity.normalized;
                    rb.AddForce(brakeDir * ((-thrustInput) * brakeForce), ForceMode2D.Force);
                }
            }

            if (rb.linearVelocity.magnitude < stopSnapSpeed)
                rb.linearVelocity = Vector2.zero;
        }

        float speed = rb.linearVelocity.magnitude;
        if (speed > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        float manualTurnTorque = (baseManualTurnTorque + effectiveTotalThrust * thrustToManualTurnTorque) * manualTurnTorqueScale;
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

        float maxAngularDegPerSec = Mathf.Max(0f, maxRPM) * 360f / 60f;
        if (maxAngularDegPerSec > 0f)
            rb.angularVelocity = Mathf.Clamp(rb.angularVelocity, -maxAngularDegPerSec, maxAngularDegPerSec);

        AudioRuntime.SetEngineLoopActive(engineLoopActive);
    }

    Vector2 ComputeModuleCOM(ModuleInstance[] modules)
    {
        if (modules == null || modules.Length == 0)
            return rb.worldCenterOfMass;

        float totalModuleMass = 0f;
        Vector2 weightedSum = Vector2.zero;

        foreach (var module in modules)
        {
            if (module == null || module.data == null)
                continue;

            float moduleMass = Mathf.Max(0f, module.GetMass());
            if (moduleMass < minModuleMassForCOM)
                continue;

            totalModuleMass += moduleMass;
            weightedSum += (Vector2)module.transform.position * moduleMass;
        }

        if (totalModuleMass <= 0.0001f)
            return rb.worldCenterOfMass;

        return weightedSum / totalModuleMass;
    }
}
