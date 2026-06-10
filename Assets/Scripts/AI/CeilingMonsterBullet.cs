using UnityEngine;

/// <summary>
/// Bullet fired by the ceiling monster.
///
/// Moves at constant speed toward the player's position (recorded at fire time).
/// On contact with anything other than its owner, triggers an explosion
/// (Physics.OverlapSphere) that knocks back any player in range, then destroys itself.
/// Self-destructs after a max lifetime if it never hits anything.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class CeilingMonsterBullet : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Constant speed toward the target in meters per second.")]
    [SerializeField] private float speed = 10f;

    [Header("Explosion")]
    [Tooltip("Radius of the explosion sphere.")]
    [SerializeField] private float explosionRadius = 3f;

    [Tooltip("Horizontal knockback applied to the player.")]
    [SerializeField] private float knockbackSpeed = 6f;

    [Tooltip("Upward launch applied to the player.")]
    [SerializeField] private float knockUpSpeed = 6f;

    [Tooltip("Layers the explosion can affect.")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Lifetime")]
    [Tooltip("Max seconds before the bullet self-destructs.")]
    [SerializeField] private float maxLifetime = 5f;

    [Header("Visual")]
    [Tooltip("Optional explosion prefab (e.g. particle system). If empty, a simple expanding sphere is spawned.")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Tooltip("Colour of the default runtime explosion sphere.")]
    [SerializeField] private Color explosionColor = new Color(1f, 0.5f, 0f, 0.6f);

    [Tooltip("How fast the explosion sphere expands.")]
    [SerializeField] private float explosionExpandSpeed = 8f;

    [Tooltip("How long the explosion sphere stays visible before being destroyed.")]
    [SerializeField] private float explosionDuration = 0.4f;

    // Direction calculated at fire time
    private Vector3 moveDirection;
    private float spawnTime;

    // The monster that fired this bullet (excluded from collision checks)
    private Transform owner;
    private bool hasOwner;

    // =======================================================================

    private void Awake()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.25f;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    /// <summary>
    /// Record the monster that fired this bullet so it doesn't hit itself.
    /// </summary>
    public void SetOwner(Transform ownerTransform)
    {
        owner = ownerTransform;
        hasOwner = owner != null;
    }

    /// <summary>
    /// Set the bullet's target and speed. Called by CeilingMonsterAttack after spawning.
    /// </summary>
    public void Initialize(Vector3 targetPosition, float bulletSpeed)
    {
        speed = bulletSpeed;

        // Build movement vector: from bullet's current position toward the player
        moveDirection = (targetPosition - transform.position).normalized;
        if (moveDirection.sqrMagnitude < 0.001f)
            moveDirection = Vector3.forward;

        transform.rotation = Quaternion.LookRotation(moveDirection);
        spawnTime = Time.time;
    }

    private void Update()
    {
        // Self-destruct on timeout
        if (Time.time >= spawnTime + maxLifetime)
        {
            Explode();
            return;
        }

        // Move toward target at constant speed
        Vector3 displacement = moveDirection * (speed * Time.deltaTime);
        float distance = displacement.magnitude;

        // SphereCast for continuous collision detection (prevents tunneling)
        if (distance > 0.01f)
        {
            float radius = GetComponent<SphereCollider>().radius * transform.localScale.x;
            if (Physics.SphereCast(transform.position, radius, moveDirection,
                    out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (!IsOwner(hit.transform))
                {
                    Explode();
                    return;
                }
            }
        }

        transform.position += displacement;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner(other.transform))
        {
            Explode();
        }
    }

    // =======================================================================
    // Owner check
    // =======================================================================

    /// <summary>
    /// Returns true if the given transform is the monster that fired this bullet.
    /// </summary>
    private bool IsOwner(Transform candidate)
    {
        if (!hasOwner || candidate == null)
            return false;

        return candidate == owner || candidate.IsChildOf(owner);
    }

    // =======================================================================
    // Explosion
    // =======================================================================

    private void Explode()
    {
        // 1) Apply knockback to all players in explosion radius
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, hitMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            // Try both player controller types
            ThirdPersonPlayerController tpPlayer = hits[i].GetComponentInParent<ThirdPersonPlayerController>();
            if (tpPlayer != null)
            {
                tpPlayer.ApplyLaunch(transform.position, knockbackSpeed, knockUpSpeed);
                continue;
            }

            FirstPersonPlayerController fpPlayer = hits[i].GetComponentInParent<FirstPersonPlayerController>();
            if (fpPlayer != null)
            {
                fpPlayer.ApplyLaunch(transform.position, knockbackSpeed, knockUpSpeed);
            }
        }

        // 2) Visual explosion effect
        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, explosionDuration);
        }
        else
        {
            SpawnDefaultExplosion();
        }

        // 3) Destroy the bullet
        Destroy(gameObject);
    }

    /// <summary>
    /// Create a simple expanding sphere as the explosion visual.
    /// </summary>
    private void SpawnDefaultExplosion()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "BulletExplosion";
        sphere.transform.position = transform.position;
        sphere.transform.localScale = Vector3.one * 0.1f;

        // Remove the collider so it doesn't interfere
        Destroy(sphere.GetComponent<SphereCollider>());

        // Set material
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                        ?? Shader.Find("Standard"));
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", explosionColor);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", explosionColor);

                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f); // Transparent

                renderer.sharedMaterial = mat;
            }
        }

        // Animate expansion and destroy
        ExplosionAnimator anim = sphere.AddComponent<ExplosionAnimator>();
        anim.Initialize(explosionRadius, explosionExpandSpeed, explosionDuration);
    }

    // =======================================================================
    // Gizmos
    // =======================================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        if (Application.isPlaying && moveDirection.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, moveDirection * 2f);
        }
    }

    // =======================================================================
    // Inner class: simple explosion expand + fade animation
    // =======================================================================

    private class ExplosionAnimator : MonoBehaviour
    {
        private float targetRadius;
        private float expandSpeed;
        private float duration;
        private float elapsed;
        private Vector3 startScale;
        private Vector3 targetScale;
        private Renderer cachedRenderer;

        public void Initialize(float radius, float expandSpd, float life)
        {
            targetRadius = radius;
            expandSpeed = expandSpd;
            duration = life;
            elapsed = 0f;
            startScale = transform.localScale;
            targetScale = Vector3.one * (radius * 2f);
            cachedRenderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Expand
            transform.localScale = Vector3.Lerp(startScale, targetScale,
                Mathf.Min(1f, elapsed * expandSpeed));

            // Fade alpha
            if (cachedRenderer != null && cachedRenderer.material != null)
            {
                Color c = cachedRenderer.material.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                cachedRenderer.material.color = c;
            }
        }
    }
}
