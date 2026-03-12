using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(ShipStats))]
public class EnemyShipAI : MonoBehaviour
{
    [Header("Movement")]
    public float desiredRange = 30f;
    public float rangeTolerance = 8f;
    public float maxSpeed = 12f;
    public float playerSpeedRatio = 0.12f;
    public float baseTurnTorque = 1.1f;
    public float thrustTurnTorqueMultiplier = 0.08f;
    public float aggressiveAimTurnMultiplier = 4.5f;
    public float approachThrottle = 0.035f;
    public float retreatThrottle = 0.02f;
    public float idleBrake = 7f;
    public float retaliationDuration = 12f;

    [Header("Combat")]
    public float attackRange = 14f;
    public float fireConeAngle = 14f;

    Rigidbody2D rb;
    ShipStats stats;
    ShipStats playerStats;
    ShipMovement playerMovement;
    Transform player;
    ShipStats retaliationTarget;
    bool wantsToFire;
    float retaliationUntilTime;

    public bool WantsToFire => wantsToFire;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<ShipStats>();
    }

    public void Initialize(float desiredRange, float attackRange, float fireConeAngle, float maxSpeed)
    {
        this.desiredRange = desiredRange;
        this.attackRange = attackRange;
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
        player = WorldSpawnDirector.PlayerTransform;
        wantsToFire = false;
        playerStats = null;
        playerMovement = null;

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

        float angleToTarget = Vector2.Angle(transform.up, toTarget.normalized);
        bool isRetaliating = retaliationTarget != null && targetShip == retaliationTarget;
        wantsToFire = angleToTarget <= fireConeAngle && (toTarget.magnitude <= attackRange || isRetaliating);
    }

    void FixedUpdate()
    {
        if (rb == null || stats == null)
            return;

        ShipStats targetShip = ResolveCurrentTarget();
        if (targetShip == null)
            return;

        Transform target = targetShip.GetCoreTransform();
        if (target == null)
            return;

        if (playerStats == null || playerStats != targetShip)
            playerStats = targetShip;

        if (playerMovement == null || playerMovement.gameObject != targetShip.gameObject)
            playerMovement = targetShip.GetComponent<ShipMovement>();

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return;

        Vector2 targetDir = toTarget / distance;
        bool isRetaliating = retaliationTarget != null && targetShip == retaliationTarget;
        bool preferDirectAim = isRetaliating || distance <= attackRange * 2f;
        Vector2 steeringDir = targetDir;

        float speedCap = ComputeSpeedCap();
        float steeringAngle = Mathf.Atan2(steeringDir.y, steeringDir.x) * Mathf.Rad2Deg - 90f;
        float angleDelta = Mathf.DeltaAngle(rb.rotation, steeringAngle);

        float turnInput = Mathf.Clamp(angleDelta / 30f, -1f, 1f);
        float turnTorque = baseTurnTorque + Mathf.Max(0f, stats.totalThrust) * thrustTurnTorqueMultiplier;
        if (preferDirectAim)
            turnTorque *= aggressiveAimTurnMultiplier;
        rb.AddTorque(-turnInput * turnTorque, ForceMode2D.Force);

        float facingDot = Mathf.Clamp(Vector2.Dot(transform.up, targetDir), -1f, 1f);
        float steeringDot = Mathf.Clamp(Vector2.Dot(transform.up, steeringDir), -1f, 1f);
        float thrust = Mathf.Max(1f, stats.totalThrust);
        float distanceError = distance - desiredRange;

        if (distanceError > rangeTolerance * 1.25f)
        {
            float drive = Mathf.Clamp01((steeringDot + 0.2f) * 0.5f);
            rb.AddForce((Vector2)transform.up * (thrust * approachThrottle * drive), ForceMode2D.Force);
        }
        else if (distanceError < -rangeTolerance)
        {
            float retreatDrive = Mathf.Clamp01((facingDot + 1f) * 0.5f);
            rb.AddForce(-(Vector2)transform.up * (thrust * retreatThrottle * retreatDrive), ForceMode2D.Force);
        }
        else
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, idleBrake * Time.fixedDeltaTime);
        }

        if (rb.linearVelocity.magnitude > speedCap)
            rb.linearVelocity = rb.linearVelocity.normalized * speedCap;
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

    void ResetModuleOrientations()
    {
        var attachments = GetComponentsInChildren<ModuleAttachment>(true);
        for (int i = 0; i < attachments.Length; i++)
        {
            var attachment = attachments[i];
            if (attachment == null)
                continue;

            attachment.transform.localRotation = Quaternion.identity;
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
