using UnityEngine;

/// <summary>
/// Ranged attack component for the ceiling monster.
/// Spawns bullets that fly toward the player and explode on contact.
/// Fires at a configurable cooldown while the monster is chasing and in range.
/// </summary>
[RequireComponent(typeof(CeilingMonsterBrain))]
public class CeilingMonsterAttack : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Ceiling monster brain — reads chase state and target.")]
    [SerializeField] private CeilingMonsterBrain brain;

    [Tooltip("Optional muzzle transform where bullets spawn. If empty, bullets spawn below the monster.")]
    [SerializeField] private Transform muzzle;

    [Header("Firing")]
    [Tooltip("Max distance to the player for the monster to open fire.")]
    [SerializeField] private float fireRange = 15f;

    [Tooltip("Min distance — the monster stops firing when the player is too close.")]
    [SerializeField] private float minFireRange = 2f;

    [Tooltip("Seconds between shots.")]
    [SerializeField] private float fireCooldown = 2f;

    [Header("Bullet")]
    [Tooltip("Bullet prefab. If empty, a default sphere bullet is created at runtime.")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("Bullet travel speed in meters per second.")]
    [SerializeField] private float bulletSpeed = 10f;

    [Tooltip("How far below the monster the bullet spawns (to avoid hitting the ceiling).")]
    [SerializeField] private float spawnOffsetDown = 1.5f;

    // Fire cooldown timer
    private float cooldownTimer;
    private Transform cachedTarget;

    private void Awake()
    {
        if (brain == null)
            brain = GetComponent<CeilingMonsterBrain>();
    }

    private void OnEnable()
    {
        cooldownTimer = 0f;
    }

    private void Update()
    {
        // Only fire while chasing a target
        if (brain.CurrentState != CeilingMonsterBrain.MonsterState.Chase)
            return;

        Transform target = brain.CurrentTarget;
        if (target == null)
            return;

        cachedTarget = target;

        // Distance check
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > fireRange || dist < minFireRange)
            return;

        // Cooldown
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer > 0f)
            return;

        Fire(target.position);
        cooldownTimer = fireCooldown;
    }

    // =======================================================================

    private void Fire(Vector3 targetPosition)
    {
        // Spawn below the monster so the bullet doesn't start inside the ceiling
        Vector3 spawnPos = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.down * spawnOffsetDown;

        GameObject bulletGO;
        if (bulletPrefab != null)
        {
            bulletGO = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            bulletGO = CreateDefaultBullet(spawnPos);
        }

        CeilingMonsterBullet bullet = bulletGO.GetComponent<CeilingMonsterBullet>();
        if (bullet == null)
            bullet = bulletGO.AddComponent<CeilingMonsterBullet>();

        // Tag the bullet so it knows who fired it (avoids hitting the owner)
        bullet.SetOwner(transform);

        bullet.Initialize(targetPosition, bulletSpeed);
    }

    /// <summary>
    /// Create a visible sphere bullet at runtime when no prefab is assigned.
    /// </summary>
    private GameObject CreateDefaultBullet(Vector3 position)
    {
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "CeilingMonster_Bullet";
        bullet.transform.position = position;
        bullet.transform.localScale = Vector3.one * 0.35f;

        // Remove the collider from CreatePrimitive — CeilingMonsterBullet adds its own
        DestroyImmediate(bullet.GetComponent<SphereCollider>());

        // Add our bullet script
        bullet.AddComponent<CeilingMonsterBullet>();

        // Set material
        Renderer renderer = bullet.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                        ?? Shader.Find("Standard"));
            if (mat != null)
            {
                Color color = new Color(1f, 0.3f, 0.05f);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);

                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 0.5f);
                }

                renderer.sharedMaterial = mat;
            }
        }

        // Add a point light for glow effect
        Light light = bullet.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.4f, 0.1f);
        light.intensity = 0.6f;
        light.range = 4f;

        return bullet;
    }

    // =======================================================================
    // Gizmos
    // =======================================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, fireRange);

        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, minFireRange);

        if (muzzle != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(muzzle.position, 0.15f);
            Gizmos.DrawRay(muzzle.position, muzzle.forward * 1.5f);
        }
    }

    // =======================================================================
    // Validation
    // =======================================================================

    private void OnValidate()
    {
        fireRange = Mathf.Max(0.1f, fireRange);
        minFireRange = Mathf.Max(0f, minFireRange);
        fireCooldown = Mathf.Max(0.05f, fireCooldown);
        bulletSpeed = Mathf.Max(0.1f, bulletSpeed);
        spawnOffsetDown = Mathf.Max(0f, spawnOffsetDown);
    }
}
