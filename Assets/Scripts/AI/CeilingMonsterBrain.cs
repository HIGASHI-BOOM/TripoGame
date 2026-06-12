using UnityEngine;

/// <summary>
/// A ceiling-dwelling monster that jumps between ceiling blocks.
///
/// Priority order when the player is spotted:
///   1. If a ceiling exists above the player and jump is off cooldown, jump there.
///   2. Otherwise, jump to an intermediate ceiling that gets closer to the player.
///   3. Otherwise, chase the player along the current ceiling block.
///
/// When no player is detected, idle on the current ceiling.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class CeilingMonsterBrain : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Tag for the player target.")]
    [SerializeField] private string targetTag = "Player";

    [Tooltip("Max distance for player detection.")]
    [SerializeField] private float detectionRadius = 10f;

    [Tooltip("Distance at which the monster loses the player.")]
    [SerializeField] private float loseSightRadius = 15f;

    [Tooltip("Height offset for line-of-sight checks toward the player.")]
    [SerializeField] private float eyeHeight = 1.35f;

    [Tooltip("Seconds between target scans.")]
    [SerializeField] private float sensingInterval = 0.15f;

    [Header("Ceiling")]
    [Tooltip("Layer mask for ceiling surfaces. Must NOT include the monster's own layer. Default includes Navigation layer (8).")]
    [SerializeField] private LayerMask ceilingMask = 256;

    [Tooltip("Layers that can block line-of-sight and jump paths, such as walls or pillars.")]
    [SerializeField] private LayerMask lineOfSightMask = 1;

    [Tooltip("Clearance below the ceiling surface to avoid grid bars or decoration colliders.")]
    [SerializeField] private float ceilingClearance = 0.15f;

    [Tooltip("Spring stiffness for the ceiling correction. Higher values snap harder to the ceiling.")]
    [SerializeField] private float ceilingSpringStiffness = 12f;

    [Tooltip("Spring damping for the ceiling correction. Higher values reduce bounce.")]
    [SerializeField] private float ceilingSpringDamping = 5f;

    [Tooltip("Max raycast distance for checking the ceiling directly above the monster.")]
    [SerializeField] private float ceilingCheckDistance = 5f;

    [Tooltip("Longer raycast distance used only to recover when the monster starts or drifts too far below the ceiling.")]
    [SerializeField] private float ceilingRecoveryCheckDistance = 8f;

    [Tooltip("Upward gravity strength that keeps the monster stuck to the ceiling.")]
    [SerializeField] private float upwardGravity = 9.8f;

    [Header("Jump")]
    [Tooltip("Cooldown between jumps in seconds.")]
    [SerializeField] private float jumpCooldown = 1f;

    [Tooltip("Max raycast distance when checking for a ceiling above the player or a sampled landing point.")]
    [SerializeField] private float jumpCheckDistance = 20f;

    [Tooltip("How far the monster dips downward during the jump arc, in meters.")]
    [SerializeField] private float jumpDipHeight = 3f;

    [Tooltip("Max horizontal distance allowed for any ceiling jump, in meters. Applies to direct and intermediate jumps.")]
    [SerializeField] private float maxJumpDistance = 16f;

    [Tooltip("Radius around the player used to search for intermediate ceiling platforms.")]
    [SerializeField] private float intermediateJumpSearchRadius = 10f;

    [Tooltip("Number of angle samples per search ring when looking for intermediate platforms.")]
    [SerializeField] private int intermediateJumpAngleSamples = 12;

    [Tooltip("Number of radial rings to sample around the player for intermediate platforms.")]
    [SerializeField] private int intermediateJumpRingCount = 3;

    [Tooltip("Minimum distance reduction required before using an intermediate platform jump.")]
    [SerializeField] private float intermediateJumpMinDistanceGain = 1.5f;

    [Header("Chase")]
    [Tooltip("Movement speed when chasing along the current ceiling.")]
    [SerializeField] private float chaseSpeed = 4.2f;

    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Attack")]
    [Tooltip("Seconds to stop chase movement after an attack starts. Tune to match the attack animation windup and release.")]
    [SerializeField] private float attackMoveLockDuration = 1.1f;

    [Header("Debug")]
    [Tooltip("Print ceiling monster behavior decisions to the Unity Console with this monster's name.")]
    [SerializeField] private bool enableBehaviorDebugLogs = true;

    [Tooltip("Number of recent behavior log lines kept for the debug panel.")]
    [SerializeField] private int debugLogCapacity = 16;

    [Header("Animation")]
    [Tooltip("Animator on the visual model. If empty, the first child Animator is used.")]
    [SerializeField] private Animator animator;

    [Tooltip("Bool parameter set true while chasing or jumping.")]
    [SerializeField] private string movingParameter = "IsMoving";

    [Tooltip("Bool parameter set true while the monster is in its jump arc.")]
    [SerializeField] private string jumpingParameter = "IsJumping";

    [Tooltip("Animator playback speed while jumping. Set to 2 for double-speed run animation during jumps.")]
    [SerializeField] private float jumpAnimationSpeed = 2f;

    private enum MonsterAction { Idle, Chase, Jumping }

    private struct CeilingJumpCandidate
    {
        public Vector3 Position;
        public float CeilingY;
        public float Score;
    }

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private MonsterAction currentAction = MonsterAction.Idle;
    private Transform currentTarget;
    private float sensingTimer;
    private float jumpCooldownTimer;
    private float attackMoveLockTimer;
    private Vector3 moveDestination;

    private Vector3 jumpTargetPosition;
    private float jumpTargetCeilingY;
    private Vector3 jumpStartPosition;
    private float jumpTimer;
    private float jumpDuration;

    private float ceilingSurfaceY;
    private float capsuleTopOffset;
    private int movingParameterHash;
    private int jumpingParameterHash;
    private float defaultAnimatorSpeed = 1f;
    private string lastDecision = "None";
    private string lastJumpReason = "None";
    private string lastCeilingHit = "None";
    private string lastLandingValidation = "Not checked";
    private string[] debugLogLines;
    private int debugLogWriteIndex;
    private int debugLogCount;

    public Transform CurrentTarget => currentTarget;
    public bool IsJumping => currentAction == MonsterAction.Jumping;
    public bool IsAttackMoveLocked => attackMoveLockTimer > 0f;
    public string DebugAction => IsAttackMoveLocked ? "Attacking" : currentAction.ToString();
    public string DebugLastDecision => lastDecision;
    public string DebugLastJumpReason => lastJumpReason;
    public string DebugLastCeilingHit => lastCeilingHit;
    public string DebugLastLandingValidation => lastLandingValidation;
    public Vector3 DebugMoveDestination => moveDestination;
    public Vector3 DebugJumpTargetPosition => jumpTargetPosition;
    public float DebugJumpTargetCeilingY => jumpTargetCeilingY;
    public float DebugCeilingSurfaceY => ceilingSurfaceY;
    public float DebugJumpProgress => jumpDuration > 0f ? Mathf.Clamp01(jumpTimer / jumpDuration) : 0f;
    public float DebugJumpDuration => jumpDuration;
    public float DebugJumpCooldownRemaining => jumpCooldownTimer;
    public float DebugAttackMoveLockRemaining => attackMoveLockTimer;
    public float DebugMaxJumpDistance => maxJumpDistance;
    public float DebugHorizontalDistanceToTarget => currentTarget != null ? HorizontalDistance(transform.position, currentTarget.position) : 0f;
    public Vector3 DebugLinearVelocity => rb != null ? rb.linearVelocity : Vector3.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        ceilingMask |= 1 << 8;

        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.linearDamping = 2f;
        rb.angularDamping = 5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        capsule.direction = 1;

        PhysicsMaterial frictionless = new PhysicsMaterial("CeilingMonster_Frictionless");
        frictionless.dynamicFriction = 0f;
        frictionless.staticFriction = 0f;
        frictionless.frictionCombine = PhysicsMaterialCombine.Minimum;
        capsule.sharedMaterial = frictionless;

        capsuleTopOffset = (capsule.center.y + capsule.height * 0.5f) * transform.localScale.y;
        debugLogCapacity = Mathf.Max(4, debugLogCapacity);
        debugLogLines = new string[debugLogCapacity];
        CacheAnimator();
    }

    private void Start()
    {
        rb.WakeUp();
        SnapToCeiling();
        moveDestination = transform.position;
        LogBehavior($"Started. ceilingMask={ceilingMask.value} layers={GetLayerNames(ceilingMask)}, ceilingCheckDistance={ceilingCheckDistance:F2}");
    }

    private void Update()
    {
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer = Mathf.Max(0f, jumpCooldownTimer - Time.deltaTime);

        if (attackMoveLockTimer > 0f)
            attackMoveLockTimer = Mathf.Max(0f, attackMoveLockTimer - Time.deltaTime);

        UpdateSensing();
        UpdateAnimatorState();
    }

    private void FixedUpdate()
    {
        if (currentAction == MonsterAction.Jumping)
        {
            TickJump();
            return;
        }

        rb.AddForce(Vector3.up * (upwardGravity * rb.mass), ForceMode.Force);
        ApplyCeilingSpring();

        if (IsAttackMoveLocked)
        {
            RotateTowardCurrentTarget();
        }
        else
        {
            ApplyConstrainedMovement();
        }
    }

    private void TickJump()
    {
        jumpTimer += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(jumpTimer / jumpDuration);

        Vector3 pos = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, t);
        pos.y += -4f * jumpDipHeight * t * (1f - t);

        rb.MovePosition(pos);
        rb.linearVelocity = Vector3.zero;

        Vector3 arcDir = jumpTargetPosition - jumpStartPosition;
        arcDir.y = 0f;
        if (arcDir.sqrMagnitude > 0.1f)
        {
            Quaternion lookRot = Quaternion.LookRotation(arcDir.normalized, Vector3.down);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, lookRot, rotationSpeed * Time.fixedDeltaTime);
        }

        if (t >= 1f)
            LandOnCeiling();
    }

    private void SnapToCeiling()
    {
        if (TryRestoreCeilingAttachment("start"))
        {
            Vector3 pos = transform.position;
            pos.y = CeilingTargetY();
            rb.MovePosition(pos);
            rb.position = pos;
            transform.position = pos;
            lastLandingValidation = $"Snap ok ceilingY={ceilingSurfaceY:F2}";
            LogBehavior($"SnapToCeiling hit ceilingY={ceilingSurfaceY:F2}, targetY={pos.y:F2}");
        }
        else
        {
            lastLandingValidation = "Snap failed: no ceiling";
            LogBehavior($"SnapToCeiling failed. No ceiling within {ceilingRecoveryCheckDistance:F2}m above {FormatVector(transform.position)}");
        }
    }

    private void ApplyCeilingSpring()
    {
        if (!TryRestoreCeilingAttachment("spring"))
            return;

        float targetY = CeilingTargetY();
        float displacement = transform.position.y - targetY;
        float velocityY = rb.linearVelocity.y;

        if (Mathf.Abs(displacement) < 0.005f && Mathf.Abs(velocityY) < 0.05f)
            return;

        float springAccel = -ceilingSpringStiffness * displacement - ceilingSpringDamping * velocityY;
        rb.AddForce(Vector3.up * springAccel, ForceMode.Acceleration);
    }

    private float CeilingTargetY()
    {
        return ceilingSurfaceY - capsuleTopOffset - ceilingClearance;
    }

    private bool FindCeilingSurface(out float hitY)
    {
        return FindCeilingSurface(ceilingCheckDistance, out hitY);
    }

    private bool FindCeilingSurface(float maxDistance, out float hitY)
    {
        hitY = 0f;
        Vector3 rayOrigin = transform.position + Vector3.up * (capsuleTopOffset - 0.1f);
        if (TryFindCeilingFrom(rayOrigin, maxDistance, out RaycastHit hit))
        {
            hitY = hit.point.y;
            return true;
        }

        return false;
    }

    private bool TryRestoreCeilingAttachment(string reason)
    {
        if (FindCeilingSurface(out float hitY))
        {
            ceilingSurfaceY = hitY;
            return true;
        }

        Vector3 recoveryOrigin = transform.position + Vector3.down * (capsuleTopOffset + 0.25f);
        float recoveryDistance = ceilingRecoveryCheckDistance + capsuleTopOffset + 0.25f;
        if (!TryFindCeilingFrom(recoveryOrigin, recoveryDistance, out RaycastHit recoveryHit))
            return false;

        float recoveryHitY = recoveryHit.point.y;
        ceilingSurfaceY = recoveryHitY;
        Vector3 pos = transform.position;
        pos.y = CeilingTargetY();
        rb.MovePosition(pos);
        rb.position = pos;
        transform.position = pos;
        rb.linearVelocity = Vector3.zero;
        lastLandingValidation = $"Recovered ceilingY={recoveryHitY:F2}";
        LogBehavior($"Recovered ceiling attachment during {reason}. ceilingY={recoveryHitY:F2}, targetY={pos.y:F2}");
        return true;
    }

    private void UpdateSensing()
    {
        sensingTimer -= Time.deltaTime;
        if (sensingTimer > 0f)
            return;

        sensingTimer = Mathf.Max(0.02f, sensingInterval);

        Transform sensed = FindVisibleTarget();
        if (sensed != null)
        {
            currentTarget = sensed;
            lastDecision = $"Saw target {sensed.name}";
            TryJumpOrChase();
            return;
        }

        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            if (dist > loseSightRadius)
            {
                LogBehavior($"Lost target {currentTarget.name}. distance={dist:F2}, loseSightRadius={loseSightRadius:F2}");
                currentTarget = null;
                currentAction = MonsterAction.Idle;
                lastDecision = "Lost target";
            }
        }
    }

    private Transform FindVisibleTarget()
    {
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go == null)
            return null;

        Transform candidate = go.transform;
        return CanSeeTarget(candidate) ? candidate : null;
    }

    private bool CanSeeTarget(Transform candidate)
    {
        Vector3 toTarget = candidate.position - transform.position;
        if (toTarget.sqrMagnitude > detectionRadius * detectionRadius)
            return false;

        Vector3 eye = transform.position + Vector3.down * eyeHeight;
        Vector3 targetPoint = candidate.position + Vector3.up;

        if (Physics.Linecast(eye, targetPoint, out RaycastHit hit,
                lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == candidate || hit.transform.IsChildOf(candidate);
        }

        return true;
    }

    private void TryJumpOrChase()
    {
        if (currentTarget == null || currentAction == MonsterAction.Jumping || IsAttackMoveLocked)
        {
            if (IsAttackMoveLocked)
                lastDecision = $"Attack move locked {attackMoveLockTimer:F2}s";
            return;
        }

        if (!TryRestoreCeilingAttachment("decision"))
        {
            currentAction = MonsterAction.Idle;
            lastDecision = "No current ceiling; skip jump";
            lastLandingValidation = $"No current ceiling within {ceilingCheckDistance:F2}m";
            LogBehavior($"Skip jump/chase decision: monster is not attached to a ceiling at {FormatVector(transform.position)}");
            return;
        }

        if (jumpCooldownTimer > 0f)
        {
            lastDecision = $"Jump cooldown {jumpCooldownTimer:F2}s; chase";
            SetChaseDestination();
            return;
        }

        Vector3 playerPos = currentTarget.position;

        if (FindCeilingAtPosition(playerPos, out float ceilingY) && IsWithinJumpDistance(playerPos))
        {
            if (StartJumpToCeiling(playerPos, ceilingY, "Direct player ceiling"))
            {
                jumpCooldownTimer = jumpCooldown;
                return;
            }
        }

        if (FindBestIntermediateCeiling(playerPos, out CeilingJumpCandidate candidate))
        {
            if (StartJumpToCeiling(candidate.Position, candidate.CeilingY, $"Intermediate score={candidate.Score:F2}"))
            {
                jumpCooldownTimer = jumpCooldown;
                return;
            }
        }

        lastDecision = "No valid jump; chase";
        SetChaseDestination();
    }

    private void SetChaseDestination()
    {
        if (currentTarget == null)
            return;

        currentAction = MonsterAction.Chase;
        moveDestination = currentTarget.position;
        moveDestination.y = transform.position.y;
    }

    private bool StartJumpToCeiling(Vector3 position, float ceilingY, string reason)
    {
        float horizontalDist = HorizontalDistance(transform.position, position);
        if (horizontalDist > maxJumpDistance)
        {
            lastDecision = $"Rejected jump too far {horizontalDist:F2}>{maxJumpDistance:F2}";
            LogBehavior(lastDecision);
            SetChaseDestination();
            return false;
        }

        Vector3 targetPosition = position;
        targetPosition.y = ceilingY - capsuleTopOffset - ceilingClearance;
        if (!ValidateJumpLanding(targetPosition, ceilingY, out string landingReason))
        {
            lastDecision = $"Rejected jump landing: {landingReason}";
            lastLandingValidation = lastDecision;
            LogBehavior($"{lastDecision}. reason={reason}, target={FormatVector(targetPosition)}");
            SetChaseDestination();
            return false;
        }

        jumpTargetCeilingY = ceilingY;
        jumpTargetPosition = targetPosition;
        jumpStartPosition = transform.position;

        jumpDuration = Mathf.Max(0.4f, horizontalDist / chaseSpeed);

        jumpTimer = 0f;
        currentAction = MonsterAction.Jumping;
        rb.linearVelocity = Vector3.zero;
        lastDecision = $"Jump start: {reason}";
        lastJumpReason = reason;
        lastLandingValidation = "In flight";

        LogBehavior($"Jump start reason={reason}, from={FormatVector(jumpStartPosition)}, target={FormatVector(jumpTargetPosition)}, ceilingY={ceilingY:F2}, dist={horizontalDist:F2}, duration={jumpDuration:F2}");
        return true;
    }

    private void LandOnCeiling()
    {
        ceilingSurfaceY = jumpTargetCeilingY;

        Vector3 pos = transform.position;
        pos.y = CeilingTargetY();
        rb.MovePosition(pos);
        rb.linearVelocity = Vector3.zero;

        currentAction = MonsterAction.Chase;
        moveDestination = jumpTargetPosition;
        moveDestination.y = pos.y;

        bool ceilingStillExists = FindCeilingAtPosition(pos, out float confirmedCeilingY);
        if (ceilingStillExists)
        {
            lastLandingValidation = $"Valid ceilingY={confirmedCeilingY:F2}";
            LogBehavior($"Jump landed. target={FormatVector(pos)}, confirmedCeilingY={confirmedCeilingY:F2}");
        }
        else
        {
            lastLandingValidation = "FAILED: no ceiling at landing";
            Debug.LogWarning($"{DebugPrefix()} Jump landed with no ceiling found above landing position {FormatVector(pos)}. LastHit={lastCeilingHit}");
        }
    }

    private bool FindCeilingAtPosition(Vector3 position, out float hitY)
    {
        hitY = 0f;
        Vector3 rayOrigin = position + Vector3.up * (capsuleTopOffset - 0.1f);
        if (TryFindCeilingFrom(rayOrigin, jumpCheckDistance, out RaycastHit hit))
        {
            hitY = hit.point.y;
            lastCeilingHit = $"{hit.collider.name} y={hit.point.y:F2} at {FormatVector(position)}";
            return true;
        }

        lastCeilingHit = $"No hit from {FormatVector(position)} range={jumpCheckDistance:F2}";
        return false;
    }

    private bool FindBestIntermediateCeiling(Vector3 playerPos, out CeilingJumpCandidate best)
    {
        best = default;

        float currentDistanceToPlayer = HorizontalDistance(transform.position, playerPos);
        float bestScore = float.PositiveInfinity;
        bool found = false;

        int ringCount = Mathf.Max(1, intermediateJumpRingCount);
        int angleSamples = Mathf.Max(4, intermediateJumpAngleSamples);
        float searchRadius = Mathf.Max(0.5f, intermediateJumpSearchRadius);

        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = searchRadius * ring / ringCount;
            float angleOffset = ring % 2 == 0 ? 0f : Mathf.PI / angleSamples;

            for (int i = 0; i < angleSamples; i++)
            {
                float angle = angleOffset + i * Mathf.PI * 2f / angleSamples;
                Vector3 candidatePos = playerPos + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                if (!FindCeilingAtPosition(candidatePos, out float ceilingY))
                    continue;

                candidatePos.y = ceilingY - capsuleTopOffset - ceilingClearance;
                if (!IsIntermediateJumpUseful(candidatePos, playerPos, currentDistanceToPlayer))
                    continue;

                float distanceToPlayer = HorizontalDistance(candidatePos, playerPos);
                float jumpDistance = HorizontalDistance(transform.position, candidatePos);
                float score = distanceToPlayer + jumpDistance * 0.25f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = new CeilingJumpCandidate
                    {
                        Position = candidatePos,
                        CeilingY = ceilingY,
                        Score = score
                    };
                    found = true;
                }
            }
        }

        return found;
    }

    private bool IsIntermediateJumpUseful(Vector3 candidatePos, Vector3 playerPos, float currentDistanceToPlayer)
    {
        if (!IsWithinJumpDistance(candidatePos))
            return false;

        float candidateDistanceToPlayer = HorizontalDistance(candidatePos, playerPos);
        if (currentDistanceToPlayer - candidateDistanceToPlayer < intermediateJumpMinDistanceGain)
            return false;

        return HasClearJumpPath(candidatePos);
    }

    private bool HasClearJumpPath(Vector3 candidatePos)
    {
        Vector3 start = transform.position + Vector3.down * 0.25f;
        Vector3 end = candidatePos + Vector3.down * 0.25f;

        if (!Physics.Linecast(start, end, out RaycastHit hit, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return true;

        return currentTarget != null && (hit.transform == currentTarget || hit.transform.IsChildOf(currentTarget));
    }

    private bool IsWithinJumpDistance(Vector3 destination)
    {
        float jumpDistance = HorizontalDistance(transform.position, destination);
        return jumpDistance >= 0.75f && jumpDistance <= maxJumpDistance;
    }

    private bool ValidateJumpLanding(Vector3 landingPosition, float expectedCeilingY, out string reason)
    {
        Vector3 rayOrigin = landingPosition + Vector3.up * (capsuleTopOffset - 0.1f);
        if (!TryFindCeilingFrom(rayOrigin, ceilingCheckDistance, out RaycastHit hit))
        {
            reason = $"no short ceiling hit from {FormatVector(landingPosition)}, check={ceilingCheckDistance:F2}";
            return false;
        }

        float ceilingDelta = Mathf.Abs(hit.point.y - expectedCeilingY);
        if (ceilingDelta > 0.2f)
        {
            reason = $"short hit ceiling mismatch hit={hit.point.y:F2}, expected={expectedCeilingY:F2}, collider={hit.collider.name}";
            return false;
        }

        reason = $"ok collider={hit.collider.name}, ceilingY={hit.point.y:F2}";
        return true;
    }

    public void BeginAttackMoveLock()
    {
        attackMoveLockTimer = Mathf.Max(attackMoveLockTimer, attackMoveLockDuration);
        lastDecision = $"Attack lock {attackMoveLockDuration:F2}s";
        LogBehavior($"Attack move lock started for {attackMoveLockDuration:F2}s");
    }

    private void ApplyConstrainedMovement()
    {
        if (currentTarget == null)
            return;

        Vector3 toDest = moveDestination - transform.position;
        toDest.y = 0f;

        if (toDest.sqrMagnitude < 0.1f)
            return;

        float maxStep = chaseSpeed * Time.fixedDeltaTime;
        Vector3 step = toDest.normalized * Mathf.Min(maxStep, toDest.magnitude);
        Vector3 newPos = rb.position + new Vector3(step.x, 0f, step.z);

        if (CeilingExistsAt(newPos))
        {
            rb.MovePosition(newPos);
        }
        else
        {
            currentAction = MonsterAction.Idle;
            lastDecision = "Chase blocked: no ceiling ahead";
            lastLandingValidation = $"No ceiling at chase step {FormatVector(newPos)}";
            return;
        }

        RotateTowardDirection(toDest.normalized);
    }

    private void RotateTowardCurrentTarget()
    {
        if (currentTarget == null)
            return;

        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        RotateTowardDirection(toTarget.normalized);
    }

    private void RotateTowardDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion lookRot = Quaternion.LookRotation(direction, Vector3.down);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, lookRot, rotationSpeed * Time.fixedDeltaTime);
    }

    public void FaceTargetImmediately(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.down);
    }

    private bool CeilingExistsAt(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * (capsuleTopOffset - 0.1f);
        return TryFindCeilingFrom(rayOrigin, ceilingCheckDistance, out _);
    }

    private bool TryFindCeilingFrom(Vector3 rayOrigin, float maxDistance, out RaycastHit bestHit)
    {
        bestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.up,
            maxDistance,
            ceilingMask,
            QueryTriggerInteraction.Ignore);

        if (hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsOwnCollider(hits[i].collider))
                continue;

            bestHit = hits[i];
            return true;
        }

        return false;
    }

    private bool IsOwnCollider(Collider candidate)
    {
        return candidate != null
            && (candidate.transform == transform || candidate.transform.IsChildOf(transform));
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private string GetLayerNames(LayerMask mask)
    {
        System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) != 0)
                names.Add(LayerMask.LayerToName(i));
        }

        return names.Count > 0 ? string.Join(", ", names) : "(none)";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, loseSightRadius);

        float topOff = capsule != null
            ? (capsule.center.y + capsule.height * 0.5f) * transform.localScale.y
            : 1f;
        Vector3 origin = transform.position + Vector3.up * (topOff - 0.1f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, Vector3.up * ceilingCheckDistance);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxJumpDistance);

        if (currentTarget != null)
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
            Gizmos.DrawWireSphere(currentTarget.position, intermediateJumpSearchRadius);
        }
    }

    private void OnValidate()
    {
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        loseSightRadius = Mathf.Max(detectionRadius, loseSightRadius);
        sensingInterval = Mathf.Max(0.02f, sensingInterval);
        ceilingClearance = Mathf.Max(0f, ceilingClearance);
        ceilingSpringStiffness = Mathf.Max(0f, ceilingSpringStiffness);
        ceilingSpringDamping = Mathf.Max(0f, ceilingSpringDamping);
        ceilingCheckDistance = Mathf.Max(0.5f, ceilingCheckDistance);
        ceilingRecoveryCheckDistance = Mathf.Max(ceilingCheckDistance, ceilingRecoveryCheckDistance);
        upwardGravity = Mathf.Max(0.1f, upwardGravity);
        jumpCooldown = Mathf.Max(0f, jumpCooldown);
        jumpCheckDistance = Mathf.Max(1f, jumpCheckDistance);
        jumpDipHeight = Mathf.Max(0.1f, jumpDipHeight);
        maxJumpDistance = Mathf.Max(0.5f, maxJumpDistance);
        intermediateJumpSearchRadius = Mathf.Max(0.5f, intermediateJumpSearchRadius);
        intermediateJumpAngleSamples = Mathf.Max(4, intermediateJumpAngleSamples);
        intermediateJumpRingCount = Mathf.Max(1, intermediateJumpRingCount);
        intermediateJumpMinDistanceGain = Mathf.Max(0f, intermediateJumpMinDistanceGain);
        chaseSpeed = Mathf.Max(0.1f, chaseSpeed);
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
        attackMoveLockDuration = Mathf.Max(0f, attackMoveLockDuration);
        debugLogCapacity = Mathf.Max(4, debugLogCapacity);
        jumpAnimationSpeed = Mathf.Max(0.1f, jumpAnimationSpeed);
    }

    private void CacheAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        movingParameterHash = Animator.StringToHash(movingParameter);
        jumpingParameterHash = Animator.StringToHash(jumpingParameter);
        defaultAnimatorSpeed = animator != null ? animator.speed : 1f;
    }

    private void UpdateAnimatorState()
    {
        if (animator == null)
            return;

        bool isJumping = currentAction == MonsterAction.Jumping;
        bool isMoving = isJumping
            || (!IsAttackMoveLocked && currentAction == MonsterAction.Chase && currentTarget != null
                && HorizontalDistance(transform.position, moveDestination) > 0.1f);

        animator.SetBool(movingParameterHash, isMoving);
        animator.SetBool(jumpingParameterHash, isJumping);
        animator.speed = isJumping ? jumpAnimationSpeed : defaultAnimatorSpeed;
    }

    public int CopyDebugLogLines(string[] destination)
    {
        if (destination == null || debugLogLines == null)
            return 0;

        int count = Mathf.Min(destination.Length, debugLogCount);
        for (int i = 0; i < count; i++)
        {
            int sourceIndex = (debugLogWriteIndex - count + i + debugLogLines.Length) % debugLogLines.Length;
            destination[i] = debugLogLines[sourceIndex];
        }

        return count;
    }

    private void LogBehavior(string message)
    {
        string line = $"{Time.time:F2}s {message}";
        if (debugLogLines != null && debugLogLines.Length > 0)
        {
            debugLogLines[debugLogWriteIndex] = line;
            debugLogWriteIndex = (debugLogWriteIndex + 1) % debugLogLines.Length;
            debugLogCount = Mathf.Min(debugLogCount + 1, debugLogLines.Length);
        }

        if (enableBehaviorDebugLogs)
            Debug.Log($"{DebugPrefix()} {message}");
    }

    private string DebugPrefix()
    {
        return $"[CeilingMonster:{name} t={Time.time:F2}]";
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }
}
