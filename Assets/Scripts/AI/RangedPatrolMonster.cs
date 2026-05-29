using UnityEngine;

public class RangedPatrolMonster : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Optional explicit player target. If empty, the monster searches by Target Tag.")]
    [SerializeField] private Transform targetOverride;

    [Tooltip("Tag used when Target Override is empty.")]
    [SerializeField] private string targetTag = "Player";

    [Tooltip("Distance in meters where the monster can detect and attack the player.")]
    [SerializeField] private float detectionRange = 24f;

    [Tooltip("Horizontal field of view in degrees used before the monster notices the player.")]
    [SerializeField] private float fieldOfView = 120f;

    [Tooltip("Height in meters above the monster pivot used for line-of-sight checks.")]
    [SerializeField] private float eyeHeight = 1.25f;

    [Tooltip("Layers that can block line of sight, such as dungeon walls.")]
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Tooltip("Seconds between searches for a tagged target when Target Override is empty.")]
    [SerializeField] private float targetSearchInterval = 0.35f;

    [Header("Patrol")]
    [Tooltip("Ordered patrol waypoints. The monster walks back and forth through them.")]
    [SerializeField] private Transform[] patrolWaypoints;

    [Tooltip("Movement speed in meters per second while patrolling.")]
    [SerializeField] private float patrolSpeed = 1.7f;

    [Tooltip("Seconds to wait at each patrol waypoint.")]
    [SerializeField] private float waypointWaitTime = 0.75f;

    [Tooltip("Distance in meters considered close enough to a waypoint.")]
    [SerializeField] private float waypointArrivalDistance = 0.2f;

    [Header("Attack")]
    [Tooltip("Child transform where projectiles spawn. Defaults to this transform plus Muzzle Local Offset.")]
    [SerializeField] private Transform muzzle;

    [Tooltip("Fallback local-space projectile spawn offset when Muzzle is not assigned.")]
    [SerializeField] private Vector3 muzzleLocalOffset = new Vector3(0f, 1.2f, 0.45f);

    [Tooltip("Seconds between projectile shots while the player is visible.")]
    [SerializeField] private float fireCooldown = 1.35f;

    [Tooltip("Projectile speed in meters per second.")]
    [SerializeField] private float projectileSpeed = 13f;

    [Tooltip("How much the shot aims above the player's pivot, in meters.")]
    [SerializeField] private float targetAimHeight = 1.15f;

    [Tooltip("Projectile material. Leave empty to create a runtime orange emissive material.")]
    [SerializeField] private Material projectileMaterial;

    private Transform currentTarget;
    private Transform cachedTaggedTarget;
    private int waypointIndex;
    private int patrolDirection = 1;
    private float waitTimer;
    private float nextFireTime;
    private float nextTargetSearchTime;

    private void Update()
    {
        currentTarget = FindVisibleTarget();
        if (currentTarget != null)
        {
            FaceTarget(currentTarget.position);
            TryFireAt(currentTarget);
            return;
        }

        Patrol();
    }

    private Transform FindVisibleTarget()
    {
        Transform target = targetOverride != null ? targetOverride : FindTaggedTarget();
        if (target == null)
        {
            return null;
        }

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPoint = target.position + Vector3.up * targetAimHeight;
        Vector3 toTarget = targetPoint - eye;
        if (toTarget.magnitude > detectionRange)
        {
            return null;
        }

        Vector3 flatToTarget = toTarget;
        flatToTarget.y = 0f;
        if (flatToTarget.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(transform.forward, flatToTarget.normalized);
            if (angle > fieldOfView * 0.5f)
            {
                return null;
            }
        }

        if (Physics.Linecast(eye, targetPoint, out RaycastHit hit, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(target) && hit.transform != target)
            {
                return null;
            }
        }

        return target;
    }

    private Transform FindTaggedTarget()
    {
        if (cachedTaggedTarget != null && cachedTaggedTarget.CompareTag(targetTag))
        {
            return cachedTaggedTarget;
        }

        if (Time.time < nextTargetSearchTime)
        {
            return null;
        }

        nextTargetSearchTime = Time.time + Mathf.Max(0.05f, targetSearchInterval);
        GameObject targetObject = GameObject.FindGameObjectWithTag(targetTag);
        cachedTaggedTarget = targetObject != null ? targetObject.transform : null;
        return cachedTaggedTarget;
    }

    private void Patrol()
    {
        if (patrolWaypoints == null || patrolWaypoints.Length == 0)
        {
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        Transform waypoint = patrolWaypoints[Mathf.Clamp(waypointIndex, 0, patrolWaypoints.Length - 1)];
        Vector3 destination = waypoint.position;
        destination.y = transform.position.y;
        Vector3 toDestination = destination - transform.position;

        if (toDestination.magnitude <= waypointArrivalDistance)
        {
            AdvanceWaypoint();
            waitTimer = waypointWaitTime;
            return;
        }

        Vector3 direction = toDestination.normalized;
        transform.position += direction * (patrolSpeed * Time.deltaTime);
        FaceTarget(transform.position + direction);
    }

    private void AdvanceWaypoint()
    {
        if (patrolWaypoints.Length <= 1)
        {
            return;
        }

        waypointIndex += patrolDirection;
        if (waypointIndex >= patrolWaypoints.Length)
        {
            waypointIndex = patrolWaypoints.Length - 2;
            patrolDirection = -1;
        }
        else if (waypointIndex < 0)
        {
            waypointIndex = 1;
            patrolDirection = 1;
        }
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(toTarget.normalized, Vector3.up),
                540f * Time.deltaTime);
        }
    }

    private void TryFireAt(Transform target)
    {
        if (Time.time < nextFireTime)
        {
            return;
        }

        Vector3 origin = muzzle != null ? muzzle.position : transform.TransformPoint(muzzleLocalOffset);
        Vector3 aimPoint = target.position + Vector3.up * targetAimHeight;
        Vector3 direction = (aimPoint - origin).normalized;
        SpawnProjectile(origin, direction);
        nextFireTime = Time.time + fireCooldown;
    }

    private void SpawnProjectile(Vector3 origin, Vector3 direction)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Runtime_RangedMonster_Projectile";
        projectile.transform.position = origin;
        projectile.transform.localScale = Vector3.one * 0.34f;

        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = projectileMaterial != null ? projectileMaterial : CreateRuntimeProjectileMaterial();
        }

        RangedMonsterProjectile projectileMotion = projectile.AddComponent<RangedMonsterProjectile>();
        projectileMotion.SetSpeed(projectileSpeed);
        projectileMotion.Launch(direction, transform);
    }

    private Material CreateRuntimeProjectileMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader);
        material.name = "Runtime Ranged Monster Projectile";
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1f, 0.36f, 0.08f, 1f));
        if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(1f, 0.36f, 0.08f, 1f));
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", new Color(1f, 0.18f, 0.03f, 1f));
        return material;
    }

}
