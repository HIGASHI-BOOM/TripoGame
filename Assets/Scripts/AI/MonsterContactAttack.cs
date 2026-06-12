using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MonsterContactAttack : MonoBehaviour
{
    [Tooltip("Tag that marks the player object this monster can attack.")]
    [SerializeField] private string targetTag = "Player";

    [Tooltip("Layers that can contain attack targets.")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Tooltip("Attack radius in meters around this trigger object.")]
    [SerializeField] private float attackRadius = 1.05f;

    [Tooltip("Seconds between repeated contact attacks against the same player.")]
    [SerializeField] private float attackCooldown = 1.1f;

    [Tooltip("Horizontal knockback speed applied to the player, in meters per second.")]
    [SerializeField] private float knockbackSpeed = 4.5f;

    [Tooltip("Upward launch speed applied to the player, in meters per second.")]
    [SerializeField] private float knockUpSpeed = 8.5f;

    [Tooltip("Point used as the source of the knockback direction. Defaults to this transform.")]
    [SerializeField] private Transform knockbackSource;

    [Tooltip("Animator that receives the attack trigger when contact damage fires.")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator trigger parameter fired when the monster hits the player.")]
    [SerializeField] private string attackTriggerParameter = "Attack";

    [Tooltip("Optional ceiling monster brain. When assigned, contact attacks are disabled while the monster is jumping.")]
    [SerializeField] private CeilingMonsterBrain ceilingMonsterBrain;

    [Tooltip("When enabled, successful contact attacks count toward the game fail condition.")]
    [SerializeField] private bool reportHitToGameFlow = true;

    private readonly Collider[] targetScanResults = new Collider[16];
    private float nextAttackTime;
    private int attackTriggerHash;

    private void Reset()
    {
        Collider attackCollider = GetComponent<Collider>();
        attackCollider.isTrigger = true;
    }

    private void Awake()
    {
        Collider attackCollider = GetComponent<Collider>();
        attackCollider.isTrigger = true;

        if (knockbackSource == null)
        {
            knockbackSource = transform;
        }

        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
        }

        if (animator == null && transform.root != null)
        {
            animator = transform.root.GetComponentInChildren<Animator>(true);
        }

        if (ceilingMonsterBrain == null)
        {
            ceilingMonsterBrain = GetComponentInParent<CeilingMonsterBrain>();
        }

        attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
    }

    private void Update()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            attackRadius,
            targetScanResults,
            targetLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            if (TryAttack(targetScanResults[i]))
            {
                return;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAttack(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryAttack(other);
    }

    private bool TryAttack(Collider other)
    {
        if (GameFlowController.HasInstance && !GameFlowController.Instance.IsGameRunning)
        {
            return false;
        }

        if (ceilingMonsterBrain != null && ceilingMonsterBrain.IsJumping)
        {
            return false;
        }

        if (Time.time < nextAttackTime)
        {
            return false;
        }

        ThirdPersonPlayerController player = other.GetComponentInParent<ThirdPersonPlayerController>();
        if (player == null || !player.CompareTag(targetTag))
        {
            return false;
        }

        Vector3 sourcePosition = knockbackSource != null ? knockbackSource.position : transform.position;
        player.ApplyLaunch(sourcePosition, knockbackSpeed, knockUpSpeed);
        if (ceilingMonsterBrain != null)
        {
            ceilingMonsterBrain.FaceTargetImmediately(player.transform.position);
            ceilingMonsterBrain.BeginAttackMoveLock();
        }

        if (animator != null)
        {
            animator.SetTrigger(attackTriggerHash);
        }

        if (reportHitToGameFlow)
        {
            GameFlowController.ReportPlayerHit(player);
        }

        nextAttackTime = Time.time + attackCooldown;
        return true;
    }
}
