using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(ShipStats))]
public class EnemyShipAI : MonoBehaviour
{
    enum BehaviorBand
    {
        Far,
        Mid,
        Close
    }

    [Header("Ranges")]
    public float closeRange = 20f;
    public float mediumRange = 30f;

    [Header("Movement")]
    public float maxSpeed = 12f;
    public float playerSpeedRatio = 0.12f;
    public float baseTurnTorque = 1.1f;
    public float thrustTurnTorqueMultiplier = 0.02f;
    public float turnTorqueScale = 0.1f;
    public float aimTurnMultiplier = 1.15f;
    public float maxThrustTorque = 8f;
    public float thrustTorqueSlowdownStartDegPerSec = 120f;
    public float maxAngularDegPerSec = 220f;
    public float thrustTorqueCompensation = 0.72f;
    public float highTorqueDrivePenalty = 0.45f;
    public float angularStabilityDamping = 2.25f;
    public float lowAngularStopStartRPM = 4f;
    public float lowAngularStopDamping = 5f;
    public float wanderForwardThrottle = 0.03f;
    public float idleBrake = 7f;
    public float retaliationDuration = 12f;
    [Range(0f, 1f)] public float engineDirectionThreshold = 0.75f;
    [Range(0f, 30f)] public float maxEngineGimbalDegrees = 10f;
    public bool useModuleCenterOfMass = true;
    public float minModuleMassForCOM = 0.001f;
    public bool syncRigidbodyMassWithShipStats = true;
    public float minRigidbodyMass = 0.01f;
    public bool syncRigidbodyInertiaWithShipStats = true;
    public float inertiaFromMassMultiplier = 0.015f;
    public float minRigidbodyInertia = 0.1f;

    [Header("Behavior Timing")]
    public float decisionInterval = 5f;
    public float wanderTurnDurationMin = 0.6f;
    public float wanderTurnDurationMax = 1.4f;
    public float wanderTurnAngle = 50f;
    [Range(0f, 1f)] public float midRangePlayerAimChance = 0.5f;
    [Range(0f, 1f)] public float closeRangeAttackChance = 0.5f;

    [Header("Combat")]
    public float attackRange = 20f;
    public float fireConeAngle = 14f;

    Rigidbody2D rb;
    ShipStats stats;
    ShipStats playerStats;
    ShipMovement playerMovement;
    Transform player;
    ShipStats retaliationTarget;
    Vector2 steeringDirection = Vector2.up;
    Vector2 engagementTargetPoint;
    float throttleCommand;
    float retaliationUntilTime;
    float nextBehaviorDecisionTime;
    float nextAttackRollTime;
    float wanderTurnUntilTime;
    float wanderHeadingDegrees;
    bool wantsToFire;
    bool shouldBrake;
    bool prefersAimTurn;
    bool midRangeTracksPlayer;
    bool closeRangeCanAttack;
    BehaviorBand activeBand = BehaviorBand.Far;
    float baseInertia;

    public bool WantsToFire => wantsToFire;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<ShipStats>();
        baseInertia = rb != null ? rb.inertia : 0.1f;
        wanderHeadingDegrees = rb != null ? rb.rotation : transform.eulerAngles.z;
    }

    public void Initialize(float desiredRange, float attackRange, float fireConeAngle, float maxSpeed)
    {
        mediumRange = Mathf.Max(attackRange, desiredRange);
        closeRange = Mathf.Max(1f, attackRange);
        this.attackRange = Mathf.Max(closeRange, attackRange);
        this.fireConeAngle = fireConeAngle;
        this.maxSpeed = maxSpeed;
    }

    public void OnAttackedBy(ShipStats attacker)
    {
        if (attacker == null || attacker == stats)
            return;

        retaliationTarget = attacker;
        retaliationUntilTime = Time.time + retaliationDuration;
    }

    void Update()
    {
        if (GameRuntimeState.GameplayBlocked)
        {
            wantsToFire = false;
            return;
        }

        player = WorldSpawnDirector.PlayerTransform;
        wantsToFire = false;
        playerStats = null;
        playerMovement = null;
        throttleCommand = 0f;
        shouldBrake = true;
        prefersAimTurn = false;
        steeringDirection = transform.up;

        ShipStats targetShip = ResolveCurrentTarget();
        if (targetShip == null)
            return;

        Transform target = targetShip.GetCoreTransform();
        if (target == null)
            return;

        playerStats = targetShip;
        playerMovement = targetShip.GetComponent<ShipMovement>();

        engagementTargetPoint = targetShip.GetNearestModulePoint(transform.position);
        Vector2 toTarget = engagementTargetPoint - (Vector2)transform.position;
        if (toTarget.sqrMagnitude <= 0.001f)
            return;

        ResetModuleOrientations();

        bool isRetaliating = retaliationTarget != null && targetShip == retaliationTarget;
        Vector2 targetDir = toTarget.normalized;
        Vector2 weaponAimSteerDir = targetDir;
        float engagementRange = Mathf.Max(attackRange, 1f);
        bool canFireAtTarget = false;

        if (TryGetBestWeaponAimSolution(engagementTargetPoint, out Vector2 solvedAimDir, out bool solvedCanFire, out float solvedRange))
        {
            weaponAimSteerDir = solvedAimDir;
            canFireAtTarget = solvedCanFire;
            engagementRange = Mathf.Max(1f, solvedRange);
        }

        EvaluateBehavior(toTarget, targetDir, weaponAimSteerDir, engagementRange, isRetaliating);

        bool mayAttack = isRetaliating || (activeBand == BehaviorBand.Close && closeRangeCanAttack);
        wantsToFire = mayAttack && canFireAtTarget;
    }

    void FixedUpdate()
    {
        if (GameRuntimeState.GameplayBlocked)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            return;
        }

        if (rb == null || stats == null)
            return;

        if (syncRigidbodyMassWithShipStats)
            rb.mass = Mathf.Max(minRigidbodyMass, stats.totalMass);

        if (syncRigidbodyInertiaWithShipStats)
        {
            float targetInertia = Mathf.Max(baseInertia, minRigidbodyInertia, rb.mass * Mathf.Max(0f, inertiaFromMassMultiplier));
            rb.inertia = targetInertia;
        }

        if (!stats.HasOperationalModuleType(ModuleType.Engine))
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            return;
        }

        float speedCap = ComputeSpeedCap();
        Vector2 targetDir = steeringDirection.sqrMagnitude > 0.001f ? steeringDirection.normalized : (Vector2)transform.up;
        float steeringAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg - 90f;
        float angleDelta = Mathf.DeltaAngle(rb.rotation, steeringAngle);
        float turnInput = Mathf.Clamp(angleDelta / 45f, -1f, 1f);

        float turnTorque = (baseTurnTorque + Mathf.Max(0f, stats.totalThrust) * thrustTurnTorqueMultiplier) * turnTorqueScale;
        if (prefersAimTurn)
            turnTorque *= aimTurnMultiplier;

        rb.AddTorque(turnInput * turnTorque, ForceMode2D.Force);

        if (shouldBrake)
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, idleBrake * Time.fixedDeltaTime);
        }
        else if (throttleCommand > 0f)
        {
            float steeringDot = Mathf.Clamp(Vector2.Dot(transform.up, targetDir), -1f, 1f);
            float drive = Mathf.Clamp01((steeringDot + 0.2f) * 0.5f);
            float angularSpeed = Mathf.Abs(rb.angularVelocity);
            float torqueThrottleScale = 1f - Mathf.InverseLerp(
                thrustTorqueSlowdownStartDegPerSec,
                Mathf.Max(thrustTorqueSlowdownStartDegPerSec + 1f, maxAngularDegPerSec),
                angularSpeed);
            drive *= Mathf.Clamp01(torqueThrottleScale);

            if (drive > 0f)
            {
                ModuleInstance[] modules = GetComponentsInChildren<ModuleInstance>(true);
                Vector2 centerOfMass = useModuleCenterOfMass
                    ? ShipThrustUtility.ComputeModuleCenterOfMass(modules, rb.worldCenterOfMass, minModuleMassForCOM)
                    : rb.worldCenterOfMass;

                ShipThrustUtility.DirectionalThrustResult thrust = ShipThrustUtility.BuildDirectionalThrust(
                    modules,
                    centerOfMass,
                    (Vector2)transform.up,
                    throttleCommand * drive,
                    engineDirectionThreshold,
                    requestedThrust => requestedThrust,
                    refreshEngineVfx: true,
                    maxGimbalDegrees: maxEngineGimbalDegrees);

                if (thrust.appliedThrust > 0f)
                {
                    float clampedTorque = Mathf.Clamp(thrust.torque, -Mathf.Abs(maxThrustTorque), Mathf.Abs(maxThrustTorque));
                    float torquePenalty = 1f - Mathf.InverseLerp(0f, Mathf.Max(0.01f, Mathf.Abs(maxThrustTorque)), Mathf.Abs(clampedTorque)) * Mathf.Clamp01(highTorqueDrivePenalty);
                    Vector2 compensatedForce = thrust.force * Mathf.Clamp01(torquePenalty);
                    float compensatedTorque = clampedTorque * (1f - Mathf.Clamp01(thrustTorqueCompensation));

                    rb.AddForce(compensatedForce, ForceMode2D.Force);
                    rb.AddTorque(compensatedTorque, ForceMode2D.Force);
                }
            }
        }

        if (rb.linearVelocity.magnitude > speedCap)
            rb.linearVelocity = rb.linearVelocity.normalized * speedCap;

        ApplyAngularStability();
        rb.angularVelocity = Mathf.Clamp(rb.angularVelocity, -Mathf.Abs(maxAngularDegPerSec), Mathf.Abs(maxAngularDegPerSec));
    }

    void ApplyAngularStability()
    {
        if (rb == null)
            return;

        bool precisionStabilize = shouldBrake || prefersAimTurn || throttleCommand <= 0.001f;
        if (!precisionStabilize)
            return;

        float damping = Mathf.Max(0f, angularStabilityDamping);
        float angularSpeedRpm = Mathf.Abs(rb.angularVelocity) * 60f / 360f;

        if (angularSpeedRpm <= Mathf.Max(lowAngularStopStartRPM, 0.01f) && lowAngularStopDamping > 0f)
        {
            float stopAssist = Mathf.Lerp(
                damping,
                Mathf.Max(damping, lowAngularStopDamping),
                1f - Mathf.InverseLerp(0f, Mathf.Max(lowAngularStopStartRPM, 0.01f), angularSpeedRpm));
            damping = Mathf.Max(damping, stopAssist);
        }

        if (damping > 0f)
            rb.angularVelocity = Mathf.MoveTowards(rb.angularVelocity, 0f, damping * Time.fixedDeltaTime);
    }

    void EvaluateBehavior(Vector2 toTarget, Vector2 targetDir, Vector2 weaponAimSteerDir, float engagementRange, bool isRetaliating)
    {
        float distance = toTarget.magnitude;

        if (isRetaliating)
        {
            activeBand = BehaviorBand.Close;
            steeringDirection = weaponAimSteerDir;
            prefersAimTurn = true;
            shouldBrake = distance <= engagementRange * 0.95f;
            throttleCommand = shouldBrake ? 0f : 0.45f;
            closeRangeCanAttack = true;
            return;
        }

        float desiredCloseRange = Mathf.Max(closeRange, engagementRange);
        float desiredMidRange = Mathf.Max(mediumRange, desiredCloseRange + 8f);

        BehaviorBand nextBand = distance <= desiredCloseRange
            ? BehaviorBand.Close
            : (distance <= desiredMidRange ? BehaviorBand.Mid : BehaviorBand.Far);

        if (nextBand != activeBand)
        {
            activeBand = nextBand;
            nextBehaviorDecisionTime = 0f;
            nextAttackRollTime = 0f;
            closeRangeCanAttack = false;
        }

        switch (activeBand)
        {
            case BehaviorBand.Far:
                RunFarBehavior(targetDir, weaponAimSteerDir, engagementRange, distance);
                break;

            case BehaviorBand.Mid:
                RunMidBehavior(targetDir, weaponAimSteerDir, engagementRange, distance);
                break;

            default:
                RunCloseBehavior(targetDir, weaponAimSteerDir, engagementRange, distance);
                break;
        }
    }

    void RunFarBehavior(Vector2 targetDir, Vector2 weaponAimSteerDir, float engagementRange, float distance)
    {
        if (Time.time >= nextBehaviorDecisionTime)
        {
            ScheduleWanderTurn();
            nextBehaviorDecisionTime = Time.time + Mathf.Max(0.5f, decisionInterval);
        }

        bool shouldUseAimLead = distance <= engagementRange * 1.5f;
        Vector2 wanderDir = HeadingToVector(wanderHeadingDegrees);
        steeringDirection = shouldUseAimLead
            ? weaponAimSteerDir
            : (Time.time < wanderTurnUntilTime ? wanderDir : targetDir);
        throttleCommand = Mathf.Max(wanderForwardThrottle, 0.6f);
        shouldBrake = false;
        prefersAimTurn = shouldUseAimLead;
        closeRangeCanAttack = false;
    }

    void RunMidBehavior(Vector2 targetDir, Vector2 weaponAimSteerDir, float engagementRange, float distance)
    {
        if (Time.time >= nextBehaviorDecisionTime)
        {
            midRangeTracksPlayer = Random.value < midRangePlayerAimChance;
            if (!midRangeTracksPlayer)
                ScheduleWanderTurn();

            nextBehaviorDecisionTime = Time.time + Mathf.Max(0.5f, decisionInterval);
        }

        if (midRangeTracksPlayer)
        {
            steeringDirection = weaponAimSteerDir;
            shouldBrake = distance <= engagementRange * 0.95f;
            throttleCommand = shouldBrake ? 0f : 0.35f;
            prefersAimTurn = true;
        }
        else
        {
            Vector2 wanderDir = HeadingToVector(wanderHeadingDegrees);
            steeringDirection = Time.time < wanderTurnUntilTime ? wanderDir : targetDir;
            throttleCommand = Time.time < wanderTurnUntilTime ? 0f : 0.3f;
            shouldBrake = false;
            prefersAimTurn = false;
        }

        closeRangeCanAttack = false;
    }

    void RunCloseBehavior(Vector2 targetDir, Vector2 weaponAimSteerDir, float engagementRange, float distance)
    {
        if (Time.time >= nextAttackRollTime)
        {
            closeRangeCanAttack = Random.value < closeRangeAttackChance;
            nextAttackRollTime = Time.time + Mathf.Max(0.5f, decisionInterval);
        }

        steeringDirection = weaponAimSteerDir;
        bool insideComfortRange = distance <= engagementRange * 0.9f;
        throttleCommand = insideComfortRange ? 0f : 0.28f;
        shouldBrake = insideComfortRange;
        prefersAimTurn = true;
    }

    void ScheduleWanderTurn()
    {
        float offset = Random.Range(-wanderTurnAngle, wanderTurnAngle);
        wanderHeadingDegrees = rb != null ? rb.rotation + offset : transform.eulerAngles.z + offset;
        wanderTurnUntilTime = Time.time + Random.Range(wanderTurnDurationMin, wanderTurnDurationMax);
    }

    static Vector2 HeadingToVector(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians + Mathf.PI * 0.5f), Mathf.Sin(radians + Mathf.PI * 0.5f));
    }

    ShipStats ResolveCurrentTarget()
    {
        if (retaliationTarget != null)
        {
            if (Time.time <= retaliationUntilTime && retaliationTarget.gameObject.activeInHierarchy)
                return retaliationTarget;

            retaliationTarget = null;
        }

        if (player == null)
            player = WorldSpawnDirector.PlayerTransform;

        return player != null ? player.GetComponent<ShipStats>() : null;
    }

    bool CanAnyLaserFireAt(Vector2 targetPosition)
    {
        WeaponLaser[] weapons = GetComponentsInChildren<WeaponLaser>(true);
        if (weapons == null || weapons.Length == 0)
            return false;

        float fallbackRange = Mathf.Max(0f, attackRange);
        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponLaser weapon = weapons[i];
            if (weapon == null || !weapon.isActiveAndEnabled)
                continue;

            ModuleInstance weaponModule = weapon.GetComponent<ModuleInstance>();
            if (weaponModule == null || weaponModule.data == null || weaponModule.data.weaponType != WeaponType.Laser)
                continue;

            if (weaponModule.hp <= 0 || weaponModule.GetWeaponFireRate() <= 0f)
                continue;

            Vector2 origin = weapon.GetMuzzleWorldPosition();
            Vector2 toTarget = targetPosition - origin;
            float distance = toTarget.magnitude;
            float weaponRange = Mathf.Max(fallbackRange, weapon.GetRange());
            if (distance > weaponRange || distance <= 0.001f)
                continue;

            float angleToTarget = Vector2.Angle(weapon.GetAimDirection(), toTarget / distance);
            if (angleToTarget <= fireConeAngle)
                return true;
        }

        return false;
    }

    bool TryGetBestWeaponAimSolution(Vector2 targetPosition, out Vector2 desiredShipUpDirection, out bool canFire, out float preferredRange)
    {
        desiredShipUpDirection = transform.up;
        canFire = false;
        preferredRange = Mathf.Max(attackRange, 1f);

        WeaponLaser[] weapons = GetComponentsInChildren<WeaponLaser>(true);
        if (weapons == null || weapons.Length == 0)
            return false;

        float bestScore = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponLaser weapon = weapons[i];
            if (weapon == null || !weapon.isActiveAndEnabled)
                continue;

            ModuleInstance weaponModule = weapon.GetComponent<ModuleInstance>();
            if (weaponModule == null || weaponModule.data == null || weaponModule.data.weaponType != WeaponType.Laser)
                continue;

            if (weaponModule.hp <= 0 || weaponModule.GetWeaponFireRate() <= 0f)
                continue;

            Vector2 origin = weapon.GetMuzzleWorldPosition();
            Vector2 toTarget = targetPosition - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f)
                continue;

            Vector2 targetDir = toTarget / distance;
            Vector2 aimDir = weapon.GetAimDirection();
            float angleToTarget = Vector2.Angle(aimDir, targetDir);
            float weaponRange = Mathf.Max(attackRange, weapon.GetRange());
            bool inRange = distance <= weaponRange;

            float weaponOffsetFromShipUp = Vector2.SignedAngle((Vector2)transform.up, aimDir);
            Vector2 shipUpForWeaponAim = Quaternion.Euler(0f, 0f, -weaponOffsetFromShipUp) * targetDir;

            float score = (inRange ? 1000f : 0f) - angleToTarget * 4f - Mathf.Max(0f, distance - weaponRange) * 2f;
            if (score <= bestScore)
                continue;

            bestScore = score;
            desiredShipUpDirection = shipUpForWeaponAim.normalized;
            canFire = inRange && angleToTarget <= fireConeAngle;
            preferredRange = Mathf.Max(6f, weaponRange * 0.85f);
            found = true;
        }

        return found;
    }

    void ResetModuleOrientations()
    {
        var attachments = GetComponentsInChildren<ModuleAttachment>(true);
        for (int i = 0; i < attachments.Length; i++)
        {
            var attachment = attachments[i];
            if (attachment == null)
                continue;

            attachment.transform.localRotation = Quaternion.Euler(0f, 0f, attachment.rot90 * 90f);
        }
    }

    float ComputeSpeedCap()
    {
        if (playerStats != null && playerMovement != null)
        {
            float mass = Mathf.Max(playerMovement.minRigidbodyMass, playerStats.totalMass);
            float accelEst = mass > 0f
                ? (Mathf.Max(0f, playerStats.totalThrust) / mass) * playerMovement.accelMultiplier
                : 0f;

            float playerMaxSpeed = Mathf.Max(0.1f, playerMovement.baseMaxSpeed + accelEst * playerMovement.maxSpeedFromAccelMultiplier);
            return Mathf.Clamp(playerMaxSpeed * playerSpeedRatio, 1.5f, Mathf.Max(1.5f, maxSpeed));
        }

        return Mathf.Max(1.5f, maxSpeed * playerSpeedRatio);
    }
}
