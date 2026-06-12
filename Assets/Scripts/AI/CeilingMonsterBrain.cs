using UnityEngine;

/// <summary>
/// A ceiling-dwelling monster that jumps between ceiling blocks.
///
/// Priority order when the player is spotted:
///   1. If a ceiling exists above the player AND jump is off cooldown → jump there
///      (arcs downward first, then floats up to the new ceiling via inverted gravity).
///   2. Otherwise → chase the player along the current ceiling block (cannot leave it).
///
/// When no player is detected → idle on the current ceiling.
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
    [Tooltip("Layer mask for ceiling surfaces. Must NOT include the monster's own layer.\nDefault = Navigation layer (8).")]
    [SerializeField] private LayerMask ceilingMask = 256; // 1 << 8 = Navigation

    [Tooltip("Layers that can block line-of-sight (walls, pillars, etc).")]
    [SerializeField] private LayerMask lineOfSightMask = 1;

    [Tooltip("Clearance below the ceiling surface to avoid grid bars / decoration colliders.")]
    [SerializeField] private float ceilingClearance = 0.15f;

    [Tooltip("Spring stiffness for the ceiling correction (higher = snappier).")]
    [SerializeField] private float ceilingSpringStiffness = 12f;

    [Tooltip("Spring damping for the ceiling correction (higher = less bounce).")]
    [SerializeField] private float ceilingSpringDamping = 5f;

    [Tooltip("Max raycast distance for ceiling detection.")]
    [SerializeField] private float ceilingCheckDistance = 5f;

    [Tooltip("Upward gravity strength that keeps the monster stuck to the ceiling (inverted, +9.8 = Earth gravity reversed).")]
    [SerializeField] private float upwardGravity = 9.8f;

    [Header("Jump")]
    [Tooltip("Cooldown between jumps in seconds.")]
    [SerializeField] private float jumpCooldown = 1f;

    [Tooltip("Max raycast distance when checking for a ceiling above the player (can be larger than ceilingCheckDistance for tall rooms).")]
    [SerializeField] private float jumpCheckDistance = 20f;

    [Tooltip("How far the monster dips downward during the jump arc (meters).")]
    [SerializeField] private float jumpDipHeight = 3f;

    [Header("Chase")]
    [Tooltip("Movement speed when chasing along the current ceiling (cannot leave the block).")]
    [SerializeField] private float chaseSpeed = 4.2f;

    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 360f;

    // Components
    private Rigidbody rb;
    private CapsuleCollider capsule;

    // State machine
    private enum MonsterAction { Idle, Chase, Jumping }
    private MonsterAction currentAction = MonsterAction.Idle;

    // State
    private Transform currentTarget;
    private float sensingTimer;
    private float jumpCooldownTimer;
    private Vector3 moveDestination;

    // Jump state (used while Jumping)
    private Vector3 jumpTargetPosition;
    private float jumpTargetCeilingY;
    private Vector3 jumpStartPosition;
    private float jumpTimer;
    private float jumpDuration;

    // Ceiling values
    private float ceilingSurfaceY;
    private float capsuleTopOffset;

    public Transform CurrentTarget => currentTarget;

    // =======================================================================
    // Unity Lifecycle
    // =======================================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        // Ensure Navigation layer (8) is always included in the ceiling mask,
        // even if the Inspector value was set before this code change.
        ceilingMask |= 1 << 8;

        // Physics setup: we control gravity manually
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.linearDamping = 2f;
        rb.angularDamping = 5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Capsule direction = Y axis
        capsule.direction = 1;

        // Frictionless physics material — prevents sticking when pressed against a surface
        PhysicsMaterial frictionless = new PhysicsMaterial("CeilingMonster_Frictionless");
        frictionless.dynamicFriction = 0f;
        frictionless.staticFriction = 0f;
        frictionless.frictionCombine = PhysicsMaterialCombine.Minimum;
        capsule.sharedMaterial = frictionless;

        // Precalculate capsule top offset from transform origin
        capsuleTopOffset = (capsule.center.y + capsule.height * 0.5f) * transform.localScale.y;
    }

    private void Start()
    {
        rb.WakeUp();
        SnapToCeiling();
        moveDestination = transform.position;
        Debug.Log($"[CeilingMonsterBrain] Started. ceilingMask={ceilingMask.value} (layers in mask: {string.Join(", ", GetLayerNames(ceilingMask))}), ceilingCheckDistance={ceilingCheckDistance}");
    }

    private void Update()
    {
        UpdateSensing();
    }

    private void FixedUpdate()
    {
        if (currentAction == MonsterAction.Jumping)
        {
            // ── Controlled jump arc ──────────────────────────────────────
            // During the jump we drive position directly along a calculated
            // parabola.  No gravity/spring — only MovePosition.
            jumpTimer += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(jumpTimer / jumpDuration);

            // Horizontal: simple lerp from start to target
            Vector3 pos = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, t);

            // Vertical arc: dips down in the middle, then rises back up.
            // The factor -4*t*(1-t) creates a parabola that peaks at t=0.5
            // with value -dipHeight at the midpoint.
            pos.y += -4f * jumpDipHeight * t * (1f - t);

            rb.MovePosition(pos);
            rb.linearVelocity = Vector3.zero;

            // Rotate toward the target direction (looking along the arc)
            Vector3 arcDir = jumpTargetPosition - jumpStartPosition;
            arcDir.y = 0f;
            if (arcDir.sqrMagnitude > 0.1f)
            {
                Quaternion lookRot = Quaternion.LookRotation(arcDir.normalized, Vector3.down);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, lookRot, rotationSpeed * Time.fixedDeltaTime);
            }

            // Land when the arc completes
            if (t >= 1f)
                LandOnCeiling();

            return;
        }

        // ── Normal state (Idle / Chase) ─────────────────────────────────
        // Upward gravity — constant pull toward the ceiling
        Vector3 gravityForce = Vector3.up * (upwardGravity * rb.mass);
        rb.AddForce(gravityForce, ForceMode.Force);

        // Ceiling spring correction — keeps Y at the correct ceiling offset
        ApplyCeilingSpring();

        // Chase movement along the ceiling (constrained to block bounds)
        ApplyConstrainedMovement();
    }

    // =======================================================================
    // Ceiling Contact
    // =======================================================================

    /// <summary>
    /// Immediately snap the monster into position below the ceiling surface.
    /// </summary>
    private void SnapToCeiling()
    {
        if (FindCeilingSurface(out float hitY))
        {
            ceilingSurfaceY = hitY;
            Vector3 pos = transform.position;
            pos.y = CeilingTargetY();
            rb.MovePosition(pos);
        }
    }

    /// <summary>
    /// Soft spring that keeps the monster near the target Y below the ceiling.
    /// F = -k * displacement - d * velocity
    /// </summary>
    private void ApplyCeilingSpring()
    {
        if (!FindCeilingSurface(out float hitY))
            return;

        ceilingSurfaceY = hitY;
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

    /// <summary>
    /// Raycast upward from the monster's position to find the ceiling surface.
    /// </summary>
    private bool FindCeilingSurface(out float hitY)
    {
        hitY = 0f;
        Vector3 rayOrigin = transform.position + Vector3.up * (capsuleTopOffset - 0.1f);
        if (Physics.Raycast(rayOrigin, Vector3.up, out RaycastHit hit,
                ceilingCheckDistance, ceilingMask, QueryTriggerInteraction.Ignore))
        {
            hitY = hit.point.y;
            return true;
        }
        return false;
    }

    // =======================================================================
    // Sensing / Detection
    // =======================================================================

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
            TryJumpOrChase();
            return;
        }

        // Still have a target but can't see them — check distance
        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            if (dist > loseSightRadius)
                currentTarget = null;
        }
    }

    private Transform FindVisibleTarget()
    {
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go == null)
            return null;

        Transform candidate = go.transform;
        if (!CanSeeTarget(candidate))
            return null;

        return candidate;
    }

    private bool CanSeeTarget(Transform candidate)
    {
        Vector3 toTarget = candidate.position - transform.position;

        if (toTarget.sqrMagnitude > detectionRadius * detectionRadius)
            return false;

        // Line-of-sight: from below the monster (looking down at the floor)
        Vector3 eye = transform.position + Vector3.down * eyeHeight;
        Vector3 targetPoint = candidate.position + Vector3.up * 1.0f;

        if (Physics.Linecast(eye, targetPoint, out RaycastHit hit,
                lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == candidate || hit.transform.IsChildOf(candidate);
        }

        return true;
    }

    // =======================================================================
    // Jump
    // =======================================================================

    /// <summary>
    /// Decide: jump to a ceiling above the player, or chase along the current one.
    ///   1. Ceiling above player + cooldown ready → jump (arc down, float up).
    ///   2. Otherwise → chase along the current ceiling block (can't leave it).
    /// </summary>
    private void TryJumpOrChase()
    {
        if (currentTarget == null)
            return;

        // Handle cooldown timer regardless
        if (jumpCooldownTimer > 0f)
        {
            jumpCooldownTimer -= Time.deltaTime;
            SetChaseDestination();
            return;
        }

        Vector3 playerPos = currentTarget.position;

        // Priority 1: jump to a ceiling above the player
        if (FindCeilingAtPosition(playerPos, out float ceilingY))
        {
            StartJumpToCeiling(playerPos, ceilingY);
            jumpCooldownTimer = jumpCooldown;
            return;
        }

        // Priority 2: chase along the current ceiling
        SetChaseDestination();
    }

    /// <summary>
    /// Point moveDestination at the player, projected onto the monster's ceiling plane.
    /// </summary>
    private void SetChaseDestination()
    {
        moveDestination = currentTarget.position;
        moveDestination.y = transform.position.y;
    }

    /// <summary>
    /// Start a controlled jump arc toward the ceiling above the player.
    /// The monster follows a pre-calculated parabola:
    ///   - Drops downward first (visible "jump off the ceiling")
    ///   - Rises back up to the target ceiling
    ///   - Lands and resumes chasing
    /// </summary>
    private void StartJumpToCeiling(Vector3 position, float ceilingY)
    {
        jumpTargetCeilingY = ceilingY;

        // Target position: at the player's XZ, at the correct Y below the target ceiling
        jumpTargetPosition = position;
        jumpTargetPosition.y = ceilingY - capsuleTopOffset - ceilingClearance;

        jumpStartPosition = transform.position;

        // Jump duration scales with horizontal distance (at chase speed)
        float horizontalDist = Vector3.Distance(
            new Vector3(jumpStartPosition.x, 0f, jumpStartPosition.z),
            new Vector3(jumpTargetPosition.x, 0f, jumpTargetPosition.z));
        jumpDuration = Mathf.Max(0.4f, horizontalDist / chaseSpeed);

        jumpTimer = 0f;
        currentAction = MonsterAction.Jumping;

        // Clear any residual velocity so physics doesn't fight the arc
        rb.linearVelocity = Vector3.zero;

        Debug.Log($"[CeilingMonsterBrain] Jumping from {jumpStartPosition} → {jumpTargetPosition} " +
                  $"(dist={horizontalDist:F1}m, duration={jumpDuration:F2}s, dip={jumpDipHeight:F1}m)");
    }

    /// <summary>
    /// (Unused — landing is handled by the timer in FixedUpdate.)
    /// Kept as a safe no-op in case external callers exist.
    /// </summary>
    private void CheckJumpLanding() { }

    /// <summary>
    /// Snap the monster onto the target ceiling and return to chase state.
    /// </summary>
    private void LandOnCeiling()
    {
        ceilingSurfaceY = jumpTargetCeilingY;

        // Snap to the correct Y for this ceiling
        Vector3 pos = transform.position;
        pos.y = CeilingTargetY();
        rb.MovePosition(pos);
        rb.linearVelocity = Vector3.zero;

        currentAction = MonsterAction.Chase;
        moveDestination = jumpTargetPosition;
        moveDestination.y = pos.y;

        Debug.Log($"[CeilingMonsterBrain] Landed on ceiling at Y={ceilingSurfaceY}");
    }

    /// <summary>
    /// Raycast upward from an arbitrary world position to find the ceiling surface Y.
    /// Uses jumpCheckDistance (longer) since ceilings can be far above the player.
    /// </summary>
    private bool FindCeilingAtPosition(Vector3 position, out float hitY)
    {
        hitY = 0f;
        Vector3 rayOrigin = position + Vector3.up * (capsuleTopOffset - 0.1f);
        return Physics.Raycast(rayOrigin, Vector3.up, out RaycastHit hit,
                jumpCheckDistance, ceilingMask, QueryTriggerInteraction.Ignore)
            ? (hitY = hit.point.y) == hit.point.y
            : false;
    }

    /// <summary>
    /// Move horizontally toward moveDestination, but only if the target XZ position
    /// still has a ceiling above it (checked at short range = ceilingCheckDistance).
    /// If the ceiling ends, the monster stops at the edge.
    /// </summary>
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

        // Use SHORT range (ceilingCheckDistance) for boundary detection —
        // we only want to know if there's a ceiling right above the new position,
        // not a ceiling way up high that the ray passes through a gap to reach.
        if (CeilingExistsAt(newPos))
        {
            rb.MovePosition(newPos);
        }
        // else: edge of ceiling — stop (can't leave the block)

        // Rotate to face movement direction
        Vector3 moveDir = toDest.normalized;
        Quaternion lookRot = Quaternion.LookRotation(moveDir, Vector3.down);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, lookRot, rotationSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Quick check: is there a ceiling within ceilingCheckDistance above this position?
    /// Used for movement boundary detection (short range).
    /// </summary>
    private bool CeilingExistsAt(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * (capsuleTopOffset - 0.1f);
        return Physics.Raycast(rayOrigin, Vector3.up, out _,
            ceilingCheckDistance, ceilingMask, QueryTriggerInteraction.Ignore);
    }

    // =======================================================================
    // Debug
    // =======================================================================

    private string GetLayerNames(LayerMask mask)
    {
        System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) != 0)
                names.Add(UnityEngine.LayerMask.LayerToName(i));
        }
        return names.Count > 0 ? string.Join(", ", names) : "(none)";
    }

    // =======================================================================
    // Gizmos
    // =======================================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, loseSightRadius);

        // Ceiling detection ray
        float topOff = capsule != null
            ? (capsule.center.y + capsule.height * 0.5f) * transform.localScale.y
            : 1f;
        Vector3 origin = transform.position + Vector3.up * (topOff - 0.1f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, Vector3.up * ceilingCheckDistance);
    }

    // =======================================================================
    // Validation
    // =======================================================================

    private void OnValidate()
    {
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        loseSightRadius = Mathf.Max(detectionRadius, loseSightRadius);
        sensingInterval = Mathf.Max(0.02f, sensingInterval);
        ceilingClearance = Mathf.Max(0f, ceilingClearance);
        ceilingSpringStiffness = Mathf.Max(0f, ceilingSpringStiffness);
        ceilingSpringDamping = Mathf.Max(0f, ceilingSpringDamping);
        ceilingCheckDistance = Mathf.Max(0.5f, ceilingCheckDistance);
        upwardGravity = Mathf.Max(0.1f, upwardGravity);
        jumpCooldown = Mathf.Max(0f, jumpCooldown);
        jumpCheckDistance = Mathf.Max(1f, jumpCheckDistance);
        jumpDipHeight = Mathf.Max(0.1f, jumpDipHeight);
        chaseSpeed = Mathf.Max(0.1f, chaseSpeed);
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
    }
}
