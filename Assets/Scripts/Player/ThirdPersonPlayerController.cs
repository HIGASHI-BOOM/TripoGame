using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonPlayerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera used to make movement relative to the over-the-shoulder view.")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Animator that receives movement speed for the player model animation state machine.")]
    [SerializeField] private Animator modelAnimator;

    [Tooltip("Animator float parameter used to switch between Idle and Run. Expected value is normalized from 0 to 1.")]
    [SerializeField] private string speedParameter = "Speed";

    [Tooltip("Animator trigger parameter fired when the character starts a jump.")]
    [SerializeField] private string jumpTriggerParameter = "Jump";

    [Tooltip("Animator bool parameter that is true while the character is touching the ground.")]
    [SerializeField] private string groundedParameter = "Grounded";

    [Header("Movement")]
    [Tooltip("Horizontal move speed in meters per second.")]
    [SerializeField] private float moveSpeed = 5.5f;

    [Tooltip("How quickly the character turns toward movement direction. Lower values are snappier.")]
    [SerializeField] private float rotationSmoothTime = 0.08f;

    [Tooltip("Jump height in meters.")]
    [SerializeField] private float jumpHeight = 1.25f;

    [Tooltip("Gravity acceleration in meters per second squared.")]
    [SerializeField] private float gravity = -24f;

    [Header("External Forces")]
    [Tooltip("How quickly horizontal knockback slows down, in meters per second squared.")]
    [SerializeField] private float externalHorizontalDamping = 18f;

    [Header("Hit Feedback")]
    [Tooltip("Feedback played when the player receives an external hit or launch.")]
    [SerializeField] private PlayerHitFeedback hitFeedback;

    [Header("Facing")]
    [Tooltip("Show a small colored marker on the character front so facing direction is obvious in the prototype.")]
    [SerializeField] private bool showFacingMarker = true;

    private CharacterController characterController;
    private OverShoulderCameraController shoulderCamera;
    private Vector3 externalHorizontalVelocity;
    private float verticalVelocity;
    private float turnVelocity;
    private int speedParameterHash;
    private int jumpTriggerParameterHash;
    private int groundedParameterHash;

    public Transform CameraTransform
    {
        get => cameraTransform;
        set => cameraTransform = value;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null)
        {
            shoulderCamera = cameraTransform.GetComponent<OverShoulderCameraController>();
        }

        if (modelAnimator == null)
        {
            modelAnimator = GetComponentInChildren<Animator>(true);
        }

        if (hitFeedback == null)
        {
            hitFeedback = GetComponent<PlayerHitFeedback>();
        }

        if (hitFeedback == null)
        {
            hitFeedback = GetComponentInChildren<PlayerHitFeedback>(true);
        }

        if (hitFeedback == null)
        {
            hitFeedback = gameObject.AddComponent<PlayerHitFeedback>();
        }

        speedParameterHash = Animator.StringToHash(speedParameter);
        jumpTriggerParameterHash = Animator.StringToHash(jumpTriggerParameter);
        groundedParameterHash = Animator.StringToHash(groundedParameter);

        EnsureFacingMarker();
    }

    private void Update()
    {
        Vector2 moveInput = ReadMoveInput();
        Vector3 move = GetCameraRelativeMove(moveInput);
        bool jumpStarted = false;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (characterController.isGrounded && WasJumpPressed())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpStarted = true;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = move * moveSpeed + externalHorizontalVelocity;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
        externalHorizontalVelocity = Vector3.MoveTowards(externalHorizontalVelocity, Vector3.zero, externalHorizontalDamping * Time.deltaTime);
        UpdateAnimator(moveInput, jumpStarted, characterController.isGrounded);
        FaceMoveDirection(move);
    }

    private void FaceMoveDirection(Vector3 move)
    {
        if (move.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
            float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
        }
    }

    public void ApplyLaunch(Vector3 sourcePosition, float horizontalSpeed, float upwardSpeed)
    {
        Vector3 awayFromSource = transform.position - sourcePosition;
        awayFromSource.y = 0f;
        if (awayFromSource.sqrMagnitude < 0.001f)
        {
            awayFromSource = -transform.forward;
        }

        externalHorizontalVelocity = awayFromSource.normalized * Mathf.Max(0f, horizontalSpeed);
        verticalVelocity = Mathf.Max(verticalVelocity, upwardSpeed);
        UpdateAnimator(Vector2.zero, upwardSpeed > 0.01f, false);

        if (hitFeedback != null)
        {
            hitFeedback.PlayHitFeedback(sourcePosition);
        }
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            input += gamepad.leftStick.ReadValue();
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private Vector3 GetCameraRelativeMove(Vector2 input)
    {
        if (input.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        Quaternion basisRotation = shoulderCamera != null ? shoulderCamera.YawRotation : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 forward = basisRotation * Vector3.forward;
        Vector3 right = basisRotation * Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * input.y + right * input.x).normalized;
    }

    private void UpdateAnimator(Vector2 moveInput, bool jumpStarted, bool isGrounded)
    {
        if (modelAnimator == null)
        {
            return;
        }

        modelAnimator.SetFloat(speedParameterHash, moveInput.magnitude);
        modelAnimator.SetBool(groundedParameterHash, isGrounded);
        if (jumpStarted)
        {
            modelAnimator.SetTrigger(jumpTriggerParameterHash);
        }
    }

    private bool WasJumpPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
    }

    private void EnsureFacingMarker()
    {
        if (!showFacingMarker || HasFacingMarker())
        {
            return;
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Facing_Marker";
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0f, 0.35f, 0.62f);
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = new Vector3(0.2f, 0.2f, 0.65f);

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        Renderer markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            markerRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = new Color(1f, 0.82f, 0.12f, 1f)
            };
        }
    }

    private bool HasFacingMarker()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && child.name == "Facing_Marker")
            {
                return true;
            }
        }

        return false;
    }
}
