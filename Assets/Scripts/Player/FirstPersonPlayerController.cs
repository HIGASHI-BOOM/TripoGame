using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonPlayerController : MonoBehaviour
{
    [Header("View")]
    [Tooltip("Camera mounted at eye height for the first-person view.")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Camera local Y position in meters, measured from the player pivot on the floor.")]
    [SerializeField] private float eyeHeight = 1.62f;

    [Tooltip("Mouse look sensitivity in degrees per pixel.")]
    [SerializeField] private float mouseSensitivity = 0.075f;

    [Tooltip("Gamepad look sensitivity in degrees per second.")]
    [SerializeField] private float gamepadLookSensitivity = 120f;

    [Tooltip("Lowest pitch angle in degrees.")]
    [SerializeField] private float minPitch = -78f;

    [Tooltip("Highest pitch angle in degrees.")]
    [SerializeField] private float maxPitch = 78f;

    [Header("Movement")]
    [Tooltip("Walking speed in meters per second.")]
    [SerializeField] private float moveSpeed = 5.25f;

    [Tooltip("Running speed in meters per second while Shift is held.")]
    [SerializeField] private float runSpeed = 7.25f;

    [Tooltip("Jump height in meters.")]
    [SerializeField] private float jumpHeight = 1.25f;

    [Tooltip("Gravity acceleration in meters per second squared.")]
    [SerializeField] private float gravity = -24f;

    [Tooltip("Small downward velocity applied while grounded so slopes and floor contacts stay stable.")]
    [SerializeField] private float groundedStickVelocity = -2f;

    private CharacterController characterController;
    private PlayerHitFeedback hitFeedback;
    private Vector3 externalHorizontalVelocity;
    private float pitch;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        hitFeedback = GetComponent<PlayerHitFeedback>();
        if (hitFeedback == null)
        {
            hitFeedback = GetComponentInChildren<PlayerHitFeedback>(true);
        }
    }

    private void OnEnable()
    {
        LockCursor(true);
    }

    private void Update()
    {
        HandleCursorToggle();
        UpdateLook();
        UpdateMovement();
    }

    private void UpdateLook()
    {
        Vector2 lookInput = Vector2.zero;

        Mouse mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            lookInput += mouse.delta.ReadValue() * mouseSensitivity;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            lookInput += gamepad.rightStick.ReadValue() * (gamepadLookSensitivity * Time.deltaTime);
        }

        transform.Rotate(Vector3.up, lookInput.x, Space.World);
        pitch = Mathf.Clamp(pitch - lookInput.y, minPitch, maxPitch);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void UpdateMovement()
    {
        Vector2 moveInput = ReadMoveInput();
        Vector3 move = transform.forward * moveInput.y + transform.right * moveInput.x;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickVelocity;
        }

        if (characterController.isGrounded && WasJumpPressed())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        float speed = IsRunning() ? runSpeed : moveSpeed;
        Vector3 velocity = move * speed + externalHorizontalVelocity;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
        externalHorizontalVelocity = Vector3.MoveTowards(externalHorizontalVelocity, Vector3.zero, 18f * Time.deltaTime);
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

    private bool IsRunning()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.leftStickButton.isPressed;
    }

    private static void HandleCursorToggle()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            LockCursor(false);
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            LockCursor(true);
        }
    }

    private static void LockCursor(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }
}
