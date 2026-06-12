using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-100)]
public class OverShoulderCameraController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Character transform the camera follows.")]
    [SerializeField] private Transform target;

    [Tooltip("World-space height above the target pivot, in meters.")]
    [SerializeField] private float targetHeight = 1.45f;

    [Header("Shoulder Framing")]
    [Tooltip("Distance behind the target in meters.")]
    [SerializeField] private float followDistance = 4.25f;

    [Tooltip("Horizontal shoulder offset in meters. Positive values put the camera over the right shoulder.")]
    [SerializeField] private float shoulderOffset = 0.85f;

    [Tooltip("Vertical camera lift above the target look point, in meters.")]
    [SerializeField] private float cameraHeight = 0.55f;

    [Tooltip("Distance where the camera center ray converges with the character's forward aim, in meters.")]
    [SerializeField] private float aimConvergenceDistance = 18f;

    [Tooltip("When enabled, horizontal mouse or right-stick look rotates the target with the camera. Ignored for the player controller, which faces movement direction.")]
    [SerializeField] private bool rotateTargetWithCameraYaw = false;

    [Header("Lens")]
    [Tooltip("Camera vertical field of view in degrees. Higher values show more of the scene but add stronger perspective distortion.")]
    [SerializeField] private float cameraFieldOfView = 58f;

    [Header("Input")]
    [Tooltip("Mouse look sensitivity in degrees per pixel. Lower this if the crosshair feels too twitchy.")]
    [SerializeField] private float mouseSensitivity = 0.075f;

    [Tooltip("Gamepad look sensitivity in degrees per second.")]
    [SerializeField] private float gamepadSensitivity = 110f;

    [Tooltip("How quickly raw mouse/gamepad look input is smoothed. Smaller values feel more responsive; larger values reduce jitter.")]
    [SerializeField] private float lookSmoothTime = 0.025f;

    [Tooltip("Lowest camera pitch angle in degrees.")]
    [SerializeField] private float minPitch = -25f;

    [Tooltip("Highest camera pitch angle in degrees.")]
    [SerializeField] private float maxPitch = 55f;

    [Header("Smoothing")]
    [Tooltip("How quickly the camera moves to the desired shoulder position.")]
    [SerializeField] private float positionSmoothTime = 0.06f;

    [Tooltip("When enabled, the camera moves closer to the target to avoid clipping through level geometry.")]
    [SerializeField] private bool enableCollisionAvoidance = true;

    [Tooltip("Layer mask used to keep the camera from clipping through level geometry.")]
    [SerializeField] private LayerMask collisionMask = ~0;

    [Tooltip("Radius in meters used when sweeping the camera against level geometry.")]
    [SerializeField] private float collisionRadius = 0.25f;

    [Tooltip("Small gap in meters kept between the camera and collision surfaces.")]
    [SerializeField] private float collisionBuffer = 0.15f;

    [Tooltip("Closest distance in meters the camera can move behind the shoulder pivot when obstructed.")]
    [SerializeField] private float minimumCollisionDistance = 0.75f;

    private Camera controlledCamera;
    private Vector3 followVelocity;
    private Vector2 smoothedLookInput;
    private Vector2 lookInputVelocity;
    private float yaw;
    private float pitch = 14f;

    public float AimYaw => yaw;
    public Quaternion AimRotation => Quaternion.Euler(pitch, yaw, 0f);
    public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        ApplyCameraLens();
        yaw = transform.eulerAngles.y;
    }

    private void OnEnable()
    {
        LockCursor(true);
    }

    private void Update()
    {
        ReadLookInput();
        ApplyTargetYaw();
        HandleCursorToggle();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Quaternion aimRotation = AimRotation;
        Quaternion yawRotation = YawRotation;
        Vector3 targetPoint = target.position + Vector3.up * targetHeight;
        Vector3 shoulderPivot = targetPoint + yawRotation * Vector3.right * shoulderOffset + Vector3.up * cameraHeight;

        Vector3 desiredPosition = shoulderPivot - aimRotation * Vector3.forward * followDistance;
        if (enableCollisionAvoidance)
        {
            desiredPosition = ResolveCameraCollision(shoulderPivot, desiredPosition);
        }
        Vector3 aimPoint = targetPoint + aimRotation * Vector3.forward * aimConvergenceDistance;

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, positionSmoothTime);
        transform.rotation = Quaternion.LookRotation(aimPoint - transform.position, Vector3.up);
    }

    private void ReadLookInput()
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
            lookInput += gamepad.rightStick.ReadValue() * (gamepadSensitivity * Time.deltaTime);
        }

        smoothedLookInput = Vector2.SmoothDamp(smoothedLookInput, lookInput, ref lookInputVelocity, lookSmoothTime);
        yaw += smoothedLookInput.x;
        pitch = Mathf.Clamp(pitch - smoothedLookInput.y, minPitch, maxPitch);
    }

    private void ApplyTargetYaw()
    {
        if (!rotateTargetWithCameraYaw || target == null || target.GetComponent<ThirdPersonPlayerController>() != null)
        {
            return;
        }

        target.rotation = YawRotation;
    }

    private void HandleCursorToggle()
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

    private Vector3 ResolveCameraCollision(Vector3 pivotPoint, Vector3 desiredPosition)
    {
        Vector3 toCamera = desiredPosition - pivotPoint;
        float distance = toCamera.magnitude;
        if (distance < 0.01f)
        {
            return desiredPosition;
        }

        Vector3 cameraDirection = toCamera.normalized;
        RaycastHit[] hits = Physics.SphereCastAll(pivotPoint, collisionRadius, cameraDirection, distance, collisionMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (target != null && hit.transform.IsChildOf(target))
                {
                    continue;
                }

                float resolvedDistance = Mathf.Max(minimumCollisionDistance, hit.distance - collisionBuffer);
                return pivotPoint + cameraDirection * Mathf.Min(resolvedDistance, distance);
            }
        }

        return desiredPosition;
    }

    private static void LockCursor(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }

    private void ApplyCameraLens()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        if (controlledCamera == null)
        {
            return;
        }

        controlledCamera.orthographic = false;
        controlledCamera.fieldOfView = cameraFieldOfView;
    }

    private void OnValidate()
    {
        targetHeight = Mathf.Max(0f, targetHeight);
        followDistance = Mathf.Max(0.1f, followDistance);
        cameraFieldOfView = Mathf.Clamp(cameraFieldOfView, 1f, 179f);
        aimConvergenceDistance = Mathf.Max(0.1f, aimConvergenceDistance);
        mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
        gamepadSensitivity = Mathf.Max(0f, gamepadSensitivity);
        lookSmoothTime = Mathf.Max(0f, lookSmoothTime);
        if (maxPitch < minPitch)
        {
            maxPitch = minPitch;
        }
        positionSmoothTime = Mathf.Max(0f, positionSmoothTime);
        collisionRadius = Mathf.Max(0f, collisionRadius);
        collisionBuffer = Mathf.Max(0f, collisionBuffer);
        minimumCollisionDistance = Mathf.Max(0.01f, minimumCollisionDistance);

        ApplyCameraLens();
    }
}
