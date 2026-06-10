using UnityEngine;

/// <summary>
/// Ranged attack component for the ceiling monster.
/// Spawns bullets from a prefab that fly toward the player and explode on contact.
/// Fires at a configurable cooldown while the monster is chasing and in range.
/// </summary>
[RequireComponent(typeof(CeilingMonsterBrain))]
public class CeilingMonsterAttack : MonoBehaviour
{
    private const string BulletPrefabResourcePath = "PF_CeilingMonster_Bullet";

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
    [Tooltip("Bullet prefab. If empty, loads from Resources/PF_CeilingMonster_Bullet.")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("Bullet travel speed in meters per second.")]
    [SerializeField] private float bulletSpeed = 10f;

    [Tooltip("How far below the monster the bullet spawns (to avoid hitting the ceiling).")]
    [SerializeField] private float spawnOffsetDown = 1.5f;

    // Fire cooldown timer
    private float cooldownTimer;
    private Transform cachedTarget;
    private GameObject loadedBulletPrefab;

    private void Awake()
    {
        if (brain == null)
            brain = GetComponent<CeilingMonsterBrain>();

        // Load bullet prefab from Resources as fallback
        if (bulletPrefab == null)
        {
            loadedBulletPrefab = Resources.Load<GameObject>(BulletPrefabResourcePath);
            if (loadedBulletPrefab == null)
                Debug.LogWarning("CeilingMonsterAttack: bullet prefab not assigned and not found at Resources/" + BulletPrefabResourcePath);
        }
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
        // Resolve prefab: Inspector assignment first, then Resources fallback
        GameObject prefab = bulletPrefab != null ? bulletPrefab : loadedBulletPrefab;
        if (prefab == null)
            return;

        // Spawn below the monster so the bullet doesn't start inside the ceiling
        Vector3 spawnPos = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.down * spawnOffsetDown;

        GameObject bulletGO = Instantiate(prefab, spawnPos, Quaternion.identity);
        bulletGO.transform.localScale = prefab.transform.lossyScale;

        CeilingMonsterBullet bullet = bulletGO.GetComponent<CeilingMonsterBullet>();
        if (bullet == null)
        {
            Destroy(bulletGO);
            return;
        }

        // Tag the bullet so it knows who fired it (avoids hitting the owner)
        bullet.SetOwner(transform);

        bullet.Initialize(targetPosition, bulletSpeed);
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
