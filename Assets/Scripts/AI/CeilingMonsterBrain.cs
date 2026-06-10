using UnityEngine;

/// <summary>
/// A ceiling-walking monster prototype that uses model inversion and positive gravity.
///
/// Physics approach (all forces applied in FixedUpdate):
///   - Upward AddForce (positive gravity) keeps the monster pressed to the ceiling
///   - A ceiling spring (critically damped) prevents Y-drift without fighting the physics engine
///   - Horizontal velocity drives patrol / chase movement along the ceiling plane
///   - The visual model is flipped 180� so it appears upside-down on the ceiling
///
/// State machine: Patrol (waypoints) / Chase (follow player below).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class CeilingMonsterBrain : MonoBehaviour
{
    public enum MonsterState
    {
        Patrol,
        Chase
    }

    [Header("Movement")]
    [Tooltip("Speed in meters per second while patrolling.")]
    [SerializeField] private float patrolSpeed = 2.2f;

    [Tooltip("Speed in meters per second while chasing.")]
    [SerializeField] private float chaseSpeed = 4.2f;

    [Tooltip("Horizontal acceleration (how quickly target speed is reached).")]
    [SerializeField] private float acceleration = 18f;

    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Tooltip("Upward gravity strength that keeps the monster stuck to the ceiling.")]
    [SerializeField] private float upwardGravity = 15f;

    [Header("Targeting")]
    [Tooltip("Tag for the player target.")]
    [SerializeField] private string targetTag = "Player";

    [Tooltip("Max distance for player detection.")]
    [SerializeField] private float detectionRadius = 10f;

    [Tooltip("Horizontal field of view in degrees.")]
    [SerializeField] private float fieldOfView = 130f;

    [Tooltip("Distance at which the monster loses the player.")]
    [SerializeField] private float loseSightRadius = 15f;

    [Tooltip("Height offset for line-of-sight checks toward the player.")]
    [SerializeField] private float eyeHeight = 1.35f;

    [Tooltip("Seconds between target scans.")]
    [SerializeField] private float sensingInterval = 0.15f;

    [Header("Ceiling")]
    [Tooltip("Layer mask for ceiling surfaces. Must NOT include the monster's own layer.")]
    [SerializeField] private LayerMask ceilingMask = 1;

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

    [Header("Patrol")]
    [Tooltip("Waypoints to patrol between.")]
    [SerializeField] private Transform[] patrolWaypoints;

    [Tooltip("Wait time at each waypoint.")]
    [SerializeField] private float patrolWaitTime = 1.25f;

    [Tooltip("Distance considered 'arrived at waypoint'.")]
    [SerializeField] private float patrolArrivalDistance = 0.5f;

    [Header("Visuals")]
    [Tooltip("Root of the visual model. Flipped 180� on Z. Auto-detects child 'Capsule_Visual'.")]
    [SerializeField] private Transform modelRoot;

    // Components
    private Rigidbody rb;
    private CapsuleCollider capsule;

    // State
    private MonsterState currentState = MonsterState.Patrol;
    private Transform currentTarget;
    private Vector3 lastTargetPosition;
    private int patrolIndex;
    private float waitTimer;
    private float sensingTimer;
    private float chaseRepathTimer;
    private Vector3 moveDestination;

    // Ceiling values
    private float ceilingSurfaceY;
    private float capsuleTopOffset;

    public MonsterState CurrentState => currentState;
    public Transform CurrentTarget => currentTarget;

    // =======================================================================
    // Unity Lifecycle
    // =======================================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

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

        // Flip the visual model 180 deg around Z so it appears upside-down.
        if (modelRoot == null)
        {
            Transform child = transform.Find("Capsule_Visual");
            if (child != null)
                modelRoot = child;
        }
        if (modelRoot != null)
            modelRoot.localRotation = Quaternion.Euler(0f, 0f, 180f);
    }

    private void Start()
    {
        rb.WakeUp();
        SnapToCeiling();

        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            moveDestination = patrolWaypoints[0].position;
            moveDestination.y = transform.position.y;
        }
        else
        {
            moveDestination = transform.position;
        }
    }

    private void Update()
    {
        UpdateSensing();

        switch (currentState)
        {
            case MonsterState.Patrol:
                TickPatrol();
                break;
            case MonsterState.Chase:
                TickChase();
                break;
        }
    }

    private void FixedUpdate()
    {
        // 1) Upward gravity — constant pull toward the ceiling
        Vector3 gravityForce = Vector3.up * (upwardGravity * rb.mass);
        rb.AddForce(gravityForce, ForceMode.Force);

        // 2) Ceiling spring correction — keeps Y at the correct ceiling offset
        ApplyCeilingSpring();

        // 3) Horizontal movement along the ceiling plane
        ApplyMovement();
    }

    // =======================================================================
    // Ceiling Contact
    // =======================================================================

    /// <summary>
    /// Immediately snap the monster into position below the ceiling surface.
    /// Called once in Start().
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
    /// Stiffness is low enough that grid-bar collisions don't block horizontal movement.
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

        // Dead zone: if displacement is very small, don't apply spring force
        // This stops the spring from fighting the upward gravity unnecessarily.
        if (Mathf.Abs(displacement) < 0.005f && Mathf.Abs(velocityY) < 0.05f)
            return;

        float springAccel = -ceilingSpringStiffness * displacement - ceilingSpringDamping * velocityY;
        rb.AddForce(Vector3.up * springAccel, ForceMode.Acceleration);
    }

    /// <summary>
    /// Target Y position for the monster's transform origin.
    /// Positions the monster so its capsule top is below the ceiling surface
    /// by the clearance amount.
    /// </summary>
    private float CeilingTargetY()
    {
        return ceilingSurfaceY - capsuleTopOffset - ceilingClearance;
    }

    /// <summary>
    /// Raycast upward to find the ceiling surface.
    /// The ray starts just below the top of the capsule so it does not originate
    /// inside the ceiling collider.
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
            lastTargetPosition = sensed.position;

            if (currentState != MonsterState.Chase)
                EnterChase();
            return;
        }

        if (currentTarget == null || currentState != MonsterState.Chase)
            return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist > loseSightRadius)
        {
            currentTarget = null;
            EnterPatrol();
        }
        else
        {
            lastTargetPosition = currentTarget.position;
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

        // Range check
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
    // Patrol State
    // =======================================================================

    private void EnterPatrol()
    {
        currentState = MonsterState.Patrol;
        waitTimer = 0f;
        PickNextPatrolDestination();
    }

    private void TickPatrol()
    {
        if (HasArrived())
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= patrolWaitTime)
            {
                waitTimer = 0f;
                PickNextPatrolDestination();
            }
        }
    }

    private void PickNextPatrolDestination()
    {
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            Transform wp = patrolWaypoints[patrolIndex % patrolWaypoints.Length];
            patrolIndex++;
            if (wp != null)
            {
                Vector3 dest = wp.position;
                dest.y = transform.position.y;
                moveDestination = dest;
                return;
            }
        }

        moveDestination = transform.position;
    }

    // =======================================================================
    // Chase State
    // =======================================================================

    private void EnterChase()
    {
        currentState = MonsterState.Chase;
        chaseRepathTimer = 0f;

        if (currentTarget != null)
            lastTargetPosition = currentTarget.position;

        UpdateChaseDestination();
    }

    private void TickChase()
    {
        if (currentTarget == null)
        {
            EnterPatrol();
            return;
        }

        chaseRepathTimer -= Time.deltaTime;
        if (chaseRepathTimer <= 0f)
        {
            chaseRepathTimer = Mathf.Max(0.02f, sensingInterval);
            UpdateChaseDestination();
        }
    }

    private void UpdateChaseDestination()
    {
        if (currentTarget != null)
            lastTargetPosition = currentTarget.position;

        Vector3 dest = lastTargetPosition;
        dest.y = transform.position.y;

        // If the player is directly below (destination ≈ monster's position),
        // orbit around them instead of stopping.
        Vector3 toDest = dest - transform.position;
        toDest.y = 0f;
        if (toDest.sqrMagnitude < 2.25f) // within 1.5m
        {
            // Perpendicular offset: cross product of forward and up
            Vector3 orbit = Vector3.Cross(transform.forward, Vector3.up).normalized * 3f;
            dest += orbit;
        }

        moveDestination = dest;
    }

    // =======================================================================
    // Movement
    // =======================================================================

    /// <summary>
    /// Apply horizontal velocity toward moveDestination (XZ only).
    /// Y velocity is left untouched — the ceiling spring handles Y.
    /// Called from FixedUpdate.
    /// </summary>
    private void ApplyMovement()
    {
        Vector3 toDest = moveDestination - transform.position;
        toDest.y = 0f;

        float speed = currentState == MonsterState.Chase ? chaseSpeed : patrolSpeed;
        Vector3 desired = Vector3.zero;

        if (toDest.sqrMagnitude > 0.01f)
        {
            desired = toDest.normalized * speed;

            Quaternion lookRot = Quaternion.LookRotation(toDest.normalized, Vector3.down);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, lookRot, rotationSpeed * Time.deltaTime);
        }

        // Set horizontal velocity, preserve Y (ceiling spring handles it)
        Vector3 vel = rb.linearVelocity;
        vel.x = Mathf.MoveTowards(vel.x, desired.x, acceleration * Time.fixedDeltaTime);
        vel.z = Mathf.MoveTowards(vel.z, desired.z, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = vel;
    }

    private bool HasArrived()
    {
        Vector3 a = transform.position;
        Vector3 b = moveDestination;
        a.y = b.y = 0f;
        return Vector3.Distance(a, b) <= patrolArrivalDistance;
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

        if (patrolWaypoints != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] == null) continue;
                Gizmos.DrawWireSphere(patrolWaypoints[i].position, 0.25f);
                Transform next = patrolWaypoints[(i + 1) % patrolWaypoints.Length];
                if (next != null)
                    Gizmos.DrawLine(patrolWaypoints[i].position, next.position);
            }
        }

        // Ceiling detection ray
        float topOff = capsule != null
            ? (capsule.center.y + capsule.height * 0.5f) * transform.localScale.y
            : 1f;
        Vector3 origin = transform.position + Vector3.up * (topOff - 0.1f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, Vector3.up * ceilingCheckDistance);

        Gizmos.color = currentState == MonsterState.Chase ? Color.red : Color.blue;
        Gizmos.DrawWireSphere(moveDestination, 0.3f);
        Gizmos.DrawLine(transform.position, moveDestination);
    }

    // =======================================================================
    // Validation
    // =======================================================================

    private void OnValidate()
    {
        patrolSpeed = Mathf.Max(0.1f, patrolSpeed);
        chaseSpeed = Mathf.Max(0.1f, chaseSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
        upwardGravity = Mathf.Max(0.1f, upwardGravity);
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
        loseSightRadius = Mathf.Max(detectionRadius, loseSightRadius);
        sensingInterval = Mathf.Max(0.02f, sensingInterval);
        ceilingClearance = Mathf.Max(0f, ceilingClearance);
        ceilingSpringStiffness = Mathf.Max(0f, ceilingSpringStiffness);
        ceilingSpringDamping = Mathf.Max(0f, ceilingSpringDamping);
        ceilingCheckDistance = Mathf.Max(0.5f, ceilingCheckDistance);
        patrolWaitTime = Mathf.Max(0f, patrolWaitTime);
        patrolArrivalDistance = Mathf.Max(0.05f, patrolArrivalDistance);
    }
}
