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
    public float wanderForwardThrottle = 0.03f;
    public float idleBrake = 7f;
    public float retaliationDuration = 12f;
    [Range(0f, 1f)] public float engineDirectionThreshold = 0.75f;
    public bool useModuleCenterOfMass = true;
    public float minModuleMassForCOM = 0.001f;

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

    public bool WantsToFire => wantsToFire;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<ShipStats>();
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

        Vector2 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.001f)
            return;

        ResetModuleOrientations();

        bool isRetaliating = retaliationTarget != null && targetShip == retaliationTarget;
        EvaluateBehavior(toTarget, isRetaliating);

        bool mayAttack = isRetaliating || (activeBand == BehaviorBand.Close && closeRangeCanAttack);
        wantsToFire = mayAttack && CanAnyLaserFireAt(target.position);
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
                    requestedThrust => requestedThrust);

                if (thrust.appliedThrust > 0f)
                {
                    rb.AddForce(thrust.force, ForceMode2D.Force);
                    rb.AddTorque(thrust.torque, ForceMode2D.Force);
                }
            }
        }

        if (rb.linearVelocity.magnitude > speedCap)
            rb.linearVelocity = rb.linearVelocity.normalized * speedCap;
    }

    void EvaluateBehavior(Vector2 toTarget, bool isRetaliating)
    {
        float distance = toTarget.magnitude;
        Vector2 targetDir = distance > 0.001f ? toTarget / distance : (Vector2)transform.up;

        if (isRetaliating)
        {
            activeBand = BehaviorBand.Close;
            steeringDirection = targetDir;
            prefersAimTurn = true;
            shouldBrake = true;
            closeRangeCanAttack = true;
            return;
        }

        BehaviorBand nextBand = distance <= closeRange
            ? BehaviorBand.Close
            : (distance <= mediumRange ? BehaviorBand.Mid : BehaviorBand.Far);

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
                RunFarBehavior();
                break;

            case BehaviorBand.Mid:
                RunMidBehavior(targetDir);
                break;

            default:
                RunCloseBehavior(targetDir);
                break;
        }
    }

    void RunFarBehavior()
    {
        if (Time.time >= nextBehaviorDecisionTime)
        {
            ScheduleWanderTurn();
            nextBehaviorDecisionTime = Time.time + Mathf.Max(0.5f, decisionInterval);
        }

        Vector2 wanderDir = HeadingToVector(wanderHeadingDegrees);
        steeringDirection = Time.time < wanderTurnUntilTime ? wanderDir : (Vector2)transform.up;
        throttleCommand = Time.time < wanderTurnUntilTime ? 0f : wanderForwardThrottle;
        shouldBrake = false;
        prefersAimTurn = false;
        closeRangeCanAttack = false;
    }

    void RunMidBehavior(Vector2 targetDir)
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
            steeringDirection = targetDir;
            throttleCommand = 0f;
            shouldBrake = true;
            prefersAimTurn = true;
        }
        else
        {
            Vector2 wanderDir = HeadingToVector(wanderHeadingDegrees);
            steeringDirection = Time.time < wanderTurnUntilTime ? wanderDir : (Vector2)transform.up;
            throttleCommand = Time.time < wanderTurnUntilTime ? 0f : wanderForwardThrottle;
            shouldBrake = false;
            prefersAimTurn = false;
        }

        closeRangeCanAttack = false;
    }

    void RunCloseBehavior(Vector2 targetDir)
    {
        if (Time.time >= nextAttackRollTime)
        {
            closeRangeCanAttack = Random.value < closeRangeAttackChance;
            nextAttackRollTime = Time.time + Mathf.Max(0.5f, decisionInterval);
        }

        steeringDirection = targetDir;
        throttleCommand = 0f;
        shouldBrake = true;
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
