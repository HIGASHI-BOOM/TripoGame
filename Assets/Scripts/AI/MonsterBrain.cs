using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterBrain : MonoBehaviour
{
    public enum MonsterState
    {
        Patrol,
        Search,
        Chase,
        TraversingLink
    }

    [Header("Targeting")]
    [Tooltip("Optional explicit target. If empty, the monster searches for the nearest GameObject with Target Tag.")]
    [SerializeField] private Transform targetOverride;

    [Tooltip("Tag used when Target Override is empty.")]
    [SerializeField] private string targetTag = "Player";

    [Tooltip("Layers that can contain detectable targets.")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Tooltip("Distance in meters where the monster can first detect the target.")]
    [SerializeField] private float detectionRadius = 10f;

    [Tooltip("Horizontal field of view in degrees used for target detection.")]
    [SerializeField] private float fieldOfView = 120f;

    [Tooltip("Distance in meters where the monster gives up chasing a target it can no longer see.")]
    [SerializeField] private float loseSightRadius = 14f;

    [Tooltip("Height in meters above the monster pivot used for line-of-sight checks.")]
    [SerializeField] private float eyeHeight = 1.35f;

    [Tooltip("Layers that can block line of sight. Include level geometry and exclude triggers used only for gameplay volumes.")]
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Tooltip("Seconds between expensive target scans.")]
    [SerializeField] private float sensingInterval = 0.15f;

    [Header("Patrol")]
    [Tooltip("Ordered patrol points. If empty, the monster samples random patrol destinations around its spawn position.")]
    [SerializeField] private Transform[] patrolWaypoints;

    [Tooltip("Maximum random patrol radius in meters when no waypoint list is assigned.")]
    [SerializeField] private float randomPatrolRadius = 7f;

    [Tooltip("Seconds to wait after reaching a patrol destination.")]
    [SerializeField] private float patrolWaitTime = 1.25f;

    [Tooltip("Speed in meters per second while patrolling or searching.")]
    [SerializeField] private float patrolSpeed = 2.2f;

    [Header("Search")]
    [Tooltip("Seconds spent searching the last known target position before returning to patrol.")]
    [SerializeField] private float searchDuration = 4f;

    [Tooltip("Distance in meters considered close enough to the last known target position.")]
    [SerializeField] private float searchArrivalDistance = 0.75f;

    [Header("Chase")]
    [Tooltip("Speed in meters per second while chasing the target.")]
    [SerializeField] private float chaseSpeed = 4.2f;

    [Tooltip("Distance in meters to keep from the target while chasing.")]
    [SerializeField] private float chaseStoppingDistance = 1.3f;

    [Tooltip("Seconds between chase destination updates.")]
    [SerializeField] private float chaseRepathInterval = 0.12f;

    [Header("Jump Links")]
    [Tooltip("Seconds used to traverse NavMesh OffMeshLinks. This drives the prototype jump/drop animation arc.")]
    [SerializeField] private float offMeshLinkDuration = 0.45f;

    [Tooltip("Extra arc height in meters added while traversing an OffMeshLink.")]
    [SerializeField] private float offMeshLinkArcHeight = 1.15f;

    [Tooltip("Maximum distance in meters used to snap the monster onto the NavMesh when enabled.")]
    [SerializeField] private float navMeshSnapDistance = 2f;

    private NavMeshAgent agent;
    private MonsterState currentState = MonsterState.Patrol;
    private MonsterState stateBeforeLink = MonsterState.Patrol;
    private Transform currentTarget;
    private Vector3 spawnPosition;
    private Vector3 currentPatrolDestination;
    private Vector3 lastKnownTargetPosition;
    private int patrolIndex;
    private float patrolWaitTimer;
    private float searchTimer;
    private float sensingTimer;
    private float chaseRepathTimer;
    private Coroutine offMeshTraversal;
    private float speedMultiplier = 1f;
    private readonly Collider[] targetScanResults = new Collider[32];

    public MonsterState CurrentState
    {
        get { return currentState; }
    }

    public Transform CurrentTarget
    {
        get { return currentTarget; }
    }

    public float SpeedMultiplier
    {
        get { return speedMultiplier; }
        set
        {
            float multiplier = Mathf.Max(0.01f, value);
            if (Mathf.Approximately(speedMultiplier, multiplier))
            {
                return;
            }

            speedMultiplier = multiplier;
            ApplyCurrentStateSpeed();
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.autoTraverseOffMeshLink = false;
        agent.autoRepath = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        spawnPosition = transform.position;
    }

    private void OnEnable()
    {
        SnapToNavMeshIfNeeded();
        EnterPatrol();
    }

    private void Update()
    {
        if (!agent.isOnNavMesh)
        {
            return;
        }

        UpdateSensing();

        if (agent.isOnOffMeshLink && offMeshTraversal == null)
        {
            offMeshTraversal = StartCoroutine(TraverseOffMeshLink());
        }

        if (currentState == MonsterState.TraversingLink)
        {
            return;
        }

        switch (currentState)
        {
            case MonsterState.Patrol:
                TickPatrol();
                break;
            case MonsterState.Search:
                TickSearch();
                break;
            case MonsterState.Chase:
                TickChase();
                break;
        }
    }

    private void UpdateSensing()
    {
        sensingTimer -= Time.deltaTime;
        if (sensingTimer > 0f)
        {
            return;
        }

        sensingTimer = Mathf.Max(0.02f, sensingInterval);
        Transform sensedTarget = FindVisibleTarget();

        if (sensedTarget != null)
        {
            currentTarget = sensedTarget;
            lastKnownTargetPosition = sensedTarget.position;
            if (currentState != MonsterState.Chase)
            {
                EnterChase();
            }
            return;
        }

        if (currentTarget == null || currentState != MonsterState.Chase)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToTarget > loseSightRadius)
        {
            EnterSearch(currentTarget.position);
            currentTarget = null;
        }
        else
        {
            lastKnownTargetPosition = currentTarget.position;
        }
    }

    private Transform FindVisibleTarget()
    {
        if (targetOverride != null && CanSeeTarget(targetOverride))
        {
            return targetOverride;
        }

        if (string.IsNullOrWhiteSpace(targetTag))
        {
            return null;
        }

        int candidateCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectionRadius,
            targetScanResults,
            targetLayers,
            QueryTriggerInteraction.Ignore);

        Transform bestTarget = null;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < candidateCount; i++)
        {
            Collider candidateCollider = targetScanResults[i];
            if (candidateCollider == null)
            {
                continue;
            }

            Transform candidate = FindTaggedParent(candidateCollider.transform);
            if (candidate == null)
            {
                continue;
            }

            if (!CanSeeTarget(candidate))
            {
                continue;
            }

            float distanceSqr = (candidate.position - transform.position).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private Transform FindTaggedParent(Transform candidate)
    {
        while (candidate != null)
        {
            if (candidate.CompareTag(targetTag))
            {
                return candidate;
            }

            candidate = candidate.parent;
        }

        return null;
    }

    private bool CanSeeTarget(Transform candidate)
    {
        Vector3 toTarget = candidate.position - transform.position;
        toTarget.y = 0f;

        float detectionRadiusSqr = detectionRadius * detectionRadius;
        if (toTarget.sqrMagnitude > detectionRadiusSqr)
        {
            return false;
        }

        if (toTarget.sqrMagnitude > 0.01f)
        {
            float angle = Vector3.Angle(transform.forward, toTarget.normalized);
            if (angle > fieldOfView * 0.5f)
            {
                return false;
            }
        }

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPoint = candidate.position + Vector3.up * eyeHeight;
        RaycastHit hit;
        if (Physics.Linecast(eye, targetPoint, out hit, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == candidate || hit.transform.IsChildOf(candidate);
        }

        return true;
    }

    private void EnterPatrol()
    {
        currentState = MonsterState.Patrol;
        currentTarget = null;
        searchTimer = 0f;
        patrolWaitTimer = 0f;
        agent.isStopped = false;
        SetAgentSpeed(patrolSpeed);
        agent.stoppingDistance = 0.2f;
        PickNextPatrolDestination();
    }

    private void TickPatrol()
    {
        if (!HasArrived(agent.stoppingDistance + 0.25f))
        {
            return;
        }

        patrolWaitTimer += Time.deltaTime;
        if (patrolWaitTimer >= patrolWaitTime)
        {
            patrolWaitTimer = 0f;
            PickNextPatrolDestination();
        }
    }

    private void EnterSearch(Vector3 lastSeenPosition)
    {
        currentState = MonsterState.Search;
        lastKnownTargetPosition = lastSeenPosition;
        searchTimer = searchDuration;
        agent.isStopped = false;
        SetAgentSpeed(patrolSpeed);
        agent.stoppingDistance = searchArrivalDistance;
        SetDestinationOnNavMesh(lastKnownTargetPosition);
    }

    private void TickSearch()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f || HasArrived(searchArrivalDistance))
        {
            EnterPatrol();
        }
    }

    private void EnterChase()
    {
        currentState = MonsterState.Chase;
        chaseRepathTimer = 0f;
        agent.isStopped = false;
        SetAgentSpeed(chaseSpeed);
        agent.stoppingDistance = chaseStoppingDistance;
    }

    private void TickChase()
    {
        if (currentTarget == null)
        {
            EnterSearch(lastKnownTargetPosition);
            return;
        }

        chaseRepathTimer -= Time.deltaTime;
        if (chaseRepathTimer <= 0f)
        {
            chaseRepathTimer = Mathf.Max(0.02f, chaseRepathInterval);
            lastKnownTargetPosition = currentTarget.position;
            SetDestinationOnNavMesh(lastKnownTargetPosition);
        }
    }

    private void PickNextPatrolDestination()
    {
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            Transform waypoint = patrolWaypoints[patrolIndex % patrolWaypoints.Length];
            patrolIndex++;
            if (waypoint != null && SetDestinationOnNavMesh(waypoint.position))
            {
                currentPatrolDestination = waypoint.position;
                return;
            }
        }

        Vector3 sampledPosition;
        if (TrySampleRandomPatrolPosition(out sampledPosition))
        {
            currentPatrolDestination = sampledPosition;
            agent.SetDestination(sampledPosition);
        }
    }

    private bool TrySampleRandomPatrolPosition(out Vector3 sampledPosition)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * randomPatrolRadius;
            Vector3 candidate = spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, navMeshSnapDistance, agent.areaMask))
            {
                sampledPosition = hit.position;
                return true;
            }
        }

        sampledPosition = spawnPosition;
        return SetDestinationOnNavMesh(spawnPosition);
    }

    private bool SetDestinationOnNavMesh(Vector3 position)
    {
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(position, out hit, navMeshSnapDistance, agent.areaMask))
        {
            return false;
        }

        return agent.SetDestination(hit.position);
    }

    private bool HasArrived(float extraDistance)
    {
        if (agent.pathPending)
        {
            return false;
        }

        if (!agent.hasPath)
        {
            return true;
        }

        return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, extraDistance);
    }

    private IEnumerator TraverseOffMeshLink()
    {
        stateBeforeLink = currentState;
        currentState = MonsterState.TraversingLink;

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        Vector3 startPosition = transform.position;
        Vector3 endPosition = linkData.endPos + Vector3.up * agent.baseOffset;
        float duration = Mathf.Max(0.05f, offMeshLinkDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
            position.y += Mathf.Sin(t * Mathf.PI) * offMeshLinkArcHeight;
            transform.position = position;
            agent.nextPosition = position;
            elapsed += Time.deltaTime;
            yield return null;
        }

        agent.Warp(endPosition);
        agent.CompleteOffMeshLink();
        offMeshTraversal = null;
        currentState = stateBeforeLink;
        ApplyCurrentStateSpeed();
    }

    private void SnapToNavMeshIfNeeded()
    {
        if (agent.isOnNavMesh)
        {
            return;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, navMeshSnapDistance, agent.areaMask))
        {
            agent.Warp(hit.position);
        }
    }

    private void ApplyCurrentStateSpeed()
    {
        if (agent == null)
        {
            return;
        }

        switch (currentState)
        {
            case MonsterState.Chase:
                SetAgentSpeed(chaseSpeed);
                break;
            case MonsterState.Patrol:
            case MonsterState.Search:
            case MonsterState.TraversingLink:
                SetAgentSpeed(patrolSpeed);
                break;
        }
    }

    private void SetAgentSpeed(float baseSpeed)
    {
        if (agent != null)
        {
            agent.speed = Mathf.Max(0.01f, baseSpeed) * speedMultiplier;
        }
    }

    private void OnValidate()
    {
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
        loseSightRadius = Mathf.Max(detectionRadius, loseSightRadius);
        sensingInterval = Mathf.Max(0.02f, sensingInterval);
        randomPatrolRadius = Mathf.Max(0.1f, randomPatrolRadius);
        patrolWaitTime = Mathf.Max(0f, patrolWaitTime);
        patrolSpeed = Mathf.Max(0.1f, patrolSpeed);
        searchDuration = Mathf.Max(0.1f, searchDuration);
        searchArrivalDistance = Mathf.Max(0.05f, searchArrivalDistance);
        chaseSpeed = Mathf.Max(0.1f, chaseSpeed);
        chaseStoppingDistance = Mathf.Max(0f, chaseStoppingDistance);
        chaseRepathInterval = Mathf.Max(0.02f, chaseRepathInterval);
        offMeshLinkDuration = Mathf.Max(0.05f, offMeshLinkDuration);
        offMeshLinkArcHeight = Mathf.Max(0f, offMeshLinkArcHeight);
        navMeshSnapDistance = Mathf.Max(0.1f, navMeshSnapDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, loseSightRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.05f, currentPatrolDestination + Vector3.up * 0.05f);

        if (patrolWaypoints == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        for (int i = 0; i < patrolWaypoints.Length; i++)
        {
            if (patrolWaypoints[i] == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(patrolWaypoints[i].position, 0.25f);
            Transform next = patrolWaypoints[(i + 1) % patrolWaypoints.Length];
            if (next != null)
            {
                Gizmos.DrawLine(patrolWaypoints[i].position, next.position);
            }
        }
    }
}
