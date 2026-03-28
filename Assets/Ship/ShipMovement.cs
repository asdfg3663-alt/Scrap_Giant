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
    public bool syncRigidbodyInertiaWithShipStats = true;
    public float inertiaFromMassMultiplier = 0.05f;
    public float minRigidbodyInertia = 0.1f;

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
    public float sasAssistRangeMultiplier = 2f;

    [Header("Engine Torque Model")]
    public bool useModuleCenterOfMass = true;
    public float minModuleMassForCOM = 0.001f;
    [Range(0f, 1f)] public float engineDirectionThreshold = 0.75f;

    [Header("Rotation Limit (RPM)")]
    public float maxRPM = 30f;

    ShipStats stats;
    Rigidbody2D rb;
    float baseInertia;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<ShipStats>();
        baseInertia = rb != null ? rb.inertia : 0.1f;

        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.gravityScale = 0f;
    }

    void FixedUpdate()
    {
        if (GameRuntimeState.GameplayBlocked)
        {
            MobileShipInput.SetMoveVector(Vector2.zero);
            AudioRuntime.SetEngineLoopActive(false);
            return;
        }

        if (stats == null || !stats.isPlayerShip)
        {
            AudioRuntime.SetEngineLoopActive(false);
            return;
        }

        Vector2 mobileMove = MobileShipInput.MoveVector;
        GetMobileDriveAxes(mobileMove, out float mobileThrustInput, out float mobileTurnInput);

        float thrustInput = Mathf.Clamp(Input.GetAxisRaw("Vertical") + mobileThrustInput, -1f, 1f);
        float turnInput = Mathf.Clamp(Input.GetAxisRaw("Horizontal") + mobileTurnInput, -1f, 1f);

        float mass = Mathf.Max(minRigidbodyMass, stats.totalMass);
        float effectiveTotalThrust = Mathf.Max(0f, stats.GetEffectiveTotalThrust());

        if (syncRigidbodyMassWithShipStats)
            rb.mass = mass;

        if (syncRigidbodyInertiaWithShipStats)
        {
            float targetInertia = Mathf.Max(baseInertia, minRigidbodyInertia, mass * Mathf.Max(0f, inertiaFromMassMultiplier));
            rb.inertia = targetInertia;
        }

        float accelEst = (effectiveTotalThrust / mass) * accelMultiplier;
        float maxSpeed = Mathf.Max(0.1f, baseMaxSpeed + accelEst * maxSpeedFromAccelMultiplier);

        var modules = GetComponentsInChildren<ModuleInstance>(true);
        Vector2 com = useModuleCenterOfMass
            ? ShipThrustUtility.ComputeModuleCenterOfMass(modules, rb.worldCenterOfMass, minModuleMassForCOM)
            : rb.worldCenterOfMass;

        bool engineLoopActive = false;

        if (thrustInput > 0f && effectiveTotalThrust > 0f)
        {
            ShipThrustUtility.DirectionalThrustResult forwardThrust = ShipThrustUtility.BuildDirectionalThrust(
                modules,
                com,
                (Vector2)transform.up,
                thrustInput,
                engineDirectionThreshold,
                stats.GetEffectiveThrust);
            if (forwardThrust.appliedThrust > 0f)
            {
                stats.ConsumeFuelForThrust(forwardThrust.appliedThrust, Time.fixedDeltaTime);
                rb.AddForce(forwardThrust.force, ForceMode2D.Force);
                rb.AddTorque(forwardThrust.torque, ForceMode2D.Force);
                engineLoopActive = forwardThrust.hasActiveEngines;
            }
        }

        if (thrustInput < 0f)
        {
            ShipThrustUtility.DirectionalThrustResult reverseThrust = ShipThrustUtility.BuildDirectionalThrust(
                modules,
                com,
                -(Vector2)transform.up,
                -thrustInput,
                engineDirectionThreshold,
                stats.GetEffectiveThrust);
            if (reverseThrust.appliedThrust > 0f)
            {
                stats.ConsumeFuelForThrust(reverseThrust.appliedThrust, Time.fixedDeltaTime);
                rb.AddForce(reverseThrust.force, ForceMode2D.Force);
                rb.AddTorque(reverseThrust.torque, ForceMode2D.Force);
                engineLoopActive = reverseThrust.hasActiveEngines;
            }
            else
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
            float sasAssistThresholdRpm = Mathf.Max(lowAngularStopStartRPM, lowAngularStopStartRPM * Mathf.Max(1f, sasAssistRangeMultiplier));

            if (angularSpeedRpm <= sasAssistThresholdRpm && lowAngularStopDamping > 0f)
            {
                float assistBlend = 1f - Mathf.InverseLerp(lowAngularStopStartRPM, sasAssistThresholdRpm, angularSpeedRpm);
                float assistedDamping = Mathf.Lerp(lowAngularStopDamping * 0.5f, lowAngularStopDamping, Mathf.Clamp01(assistBlend));
                damping = Mathf.Max(damping, assistedDamping);
            }

            if (damping > 0f)
                rb.angularVelocity = Mathf.MoveTowards(rb.angularVelocity, 0f, damping * Time.fixedDeltaTime);
        }

        float maxAngularDegPerSec = Mathf.Max(0f, maxRPM) * 360f / 60f;
        if (maxAngularDegPerSec > 0f)
            rb.angularVelocity = Mathf.Clamp(rb.angularVelocity, -maxAngularDegPerSec, maxAngularDegPerSec);

        AudioRuntime.SetEngineLoopActive(engineLoopActive);
    }

    static void GetMobileDriveAxes(Vector2 mobileMove, out float thrustInput, out float turnInput)
    {
        thrustInput = 0f;
        turnInput = 0f;

        if (mobileMove.sqrMagnitude <= 0.0001f)
            return;

        float magnitude = Mathf.Clamp01(mobileMove.magnitude);
        float absAngleFromForward = Vector2.Angle(Vector2.up, mobileMove);
        float absAngleFromBackward = Vector2.Angle(Vector2.down, mobileMove);
        float turnSign = Mathf.Sign(mobileMove.x);

        if (mobileMove.y >= 0f)
        {
            if (absAngleFromForward <= 25f)
            {
                thrustInput = magnitude;
                return;
            }

            if (absAngleFromForward <= 35f)
            {
                float blend = Mathf.InverseLerp(25f, 35f, absAngleFromForward);
                thrustInput = magnitude;
                turnInput = turnSign * blend;
                return;
            }

            turnInput = turnSign * magnitude;
            return;
        }

        if (absAngleFromBackward <= 25f)
        {
            thrustInput = -magnitude;
            return;
        }

        if (absAngleFromBackward <= 35f)
        {
            float blend = Mathf.InverseLerp(25f, 35f, absAngleFromBackward);
            thrustInput = -magnitude;
            turnInput = turnSign * blend;
            return;
        }

        turnInput = turnSign * magnitude;
    }
}
