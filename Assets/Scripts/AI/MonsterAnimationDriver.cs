using UnityEngine;
using UnityEngine.AI;

public class MonsterAnimationDriver : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator that controls the monster model.")]
    [SerializeField] private Animator animator;

    [Tooltip("NavMeshAgent used to read movement speed.")]
    [SerializeField] private NavMeshAgent agent;

    [Tooltip("Monster brain used to read jump/link traversal state.")]
    [SerializeField] private MonsterBrain brain;

    [Header("Parameters")]
    [Tooltip("Animator float parameter driven by normalized movement speed from 0 to 1.")]
    [SerializeField] private string speedParameter = "Speed";

    [Tooltip("Animator bool parameter that is false while traversing jump/drop links.")]
    [SerializeField] private string groundedParameter = "Grounded";

    [Tooltip("Animator trigger fired when the monster starts an OffMeshLink jump/drop.")]
    [SerializeField] private string jumpTriggerParameter = "Jump";

    [Tooltip("Animator float parameter used by the Move state as an animation playback speed multiplier.")]
    [SerializeField] private string moveSpeedMultiplierParameter = "MoveSpeedMultiplier";

    [Tooltip("Agent speed that maps to Speed = 1 in the Animator.")]
    [SerializeField] private float fullSpeedMetersPerSecond = 4.2f;

    [Tooltip("Maximum playback multiplier applied to the run animation while speed boost is active.")]
    [SerializeField] private float maxMoveAnimationSpeedMultiplier = 2f;

    [Tooltip("How quickly the animation speed parameter follows the agent velocity.")]
    [SerializeField] private float speedDampTime = 0.08f;

    private int speedHash;
    private int groundedHash;
    private int jumpHash;
    private int moveSpeedMultiplierHash;
    private bool hasMoveSpeedMultiplierParameter;
    private MonsterBrain.MonsterState previousState;

    public Animator Animator
    {
        get { return animator; }
        set { animator = value; }
    }

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (brain == null)
        {
            brain = GetComponent<MonsterBrain>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        speedHash = Animator.StringToHash(speedParameter);
        groundedHash = Animator.StringToHash(groundedParameter);
        jumpHash = Animator.StringToHash(jumpTriggerParameter);
        moveSpeedMultiplierHash = Animator.StringToHash(moveSpeedMultiplierParameter);
        hasMoveSpeedMultiplierParameter = HasAnimatorFloatParameter(moveSpeedMultiplierParameter);

        if (brain != null)
        {
            previousState = brain.CurrentState;
        }
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        MonsterBrain.MonsterState state = brain != null ? brain.CurrentState : MonsterBrain.MonsterState.Patrol;
        bool isTraversingLink = state == MonsterBrain.MonsterState.TraversingLink;
        float speed = agent != null ? agent.velocity.magnitude : 0f;
        float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullSpeedMetersPerSecond));

        animator.SetFloat(speedHash, normalizedSpeed, speedDampTime, Time.deltaTime);
        animator.SetBool(groundedHash, !isTraversingLink);
        UpdateMovePlaybackSpeed(isTraversingLink);

        if (state == MonsterBrain.MonsterState.TraversingLink && previousState != MonsterBrain.MonsterState.TraversingLink)
        {
            animator.SetTrigger(jumpHash);
        }

        previousState = state;
    }

    private void OnValidate()
    {
        fullSpeedMetersPerSecond = Mathf.Max(0.01f, fullSpeedMetersPerSecond);
        maxMoveAnimationSpeedMultiplier = Mathf.Max(1f, maxMoveAnimationSpeedMultiplier);
        speedDampTime = Mathf.Max(0f, speedDampTime);
    }

    private void UpdateMovePlaybackSpeed(bool isTraversingLink)
    {
        if (!hasMoveSpeedMultiplierParameter)
        {
            return;
        }

        float multiplier = 1f;
        if (!isTraversingLink && brain != null)
        {
            multiplier = Mathf.Clamp(brain.SpeedMultiplier, 1f, maxMoveAnimationSpeedMultiplier);
        }

        animator.SetFloat(moveSpeedMultiplierHash, multiplier);
    }

    private bool HasAnimatorFloatParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName &&
                parameters[i].type == AnimatorControllerParameterType.Float)
            {
                return true;
            }
        }

        return false;
    }
}
