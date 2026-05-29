using UnityEngine;
using UnityEngine.InputSystem;

public class ColliderRuntimeVisualizer : MonoBehaviour
{
    [Header("Controls")]
    [Tooltip("Show collider outlines while the game is running.")]
    [SerializeField] private bool visible;

    [Tooltip("Keyboard key used to toggle collider debug drawing at runtime.")]
    [SerializeField] private Key toggleKey = Key.F3;

    [Tooltip("Only draw while Unity is in Play Mode.")]
    [SerializeField] private bool playModeOnly = true;

    [Header("Filtering")]
    [Tooltip("Draw trigger colliders in the trigger color.")]
    [SerializeField] private bool includeTriggers = true;

    [Tooltip("Draw disabled colliders with a dim color.")]
    [SerializeField] private bool includeDisabledColliders;

    [Tooltip("Draw CharacterController components. These are not regular Collider components.")]
    [SerializeField] private bool includeCharacterControllers = true;

    [Tooltip("Layer mask for colliders shown by this visualizer.")]
    [SerializeField] private LayerMask layerMask = ~0;

    [Header("Colors")]
    [Tooltip("Color used for normal solid colliders.")]
    [SerializeField] private Color colliderColor = new Color(0.1f, 0.85f, 1f, 1f);

    [Tooltip("Color used for trigger colliders.")]
    [SerializeField] private Color triggerColor = new Color(1f, 0.72f, 0.12f, 1f);

    [Tooltip("Color used for CharacterController capsules.")]
    [SerializeField] private Color characterControllerColor = new Color(0.3f, 1f, 0.35f, 1f);

    [Tooltip("Color used for disabled colliders when they are included.")]
    [SerializeField] private Color disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);

    private const int CapsuleSegments = 24;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
        {
            visible = !visible;
        }
    }

    private void OnDrawGizmos()
    {
        if (!visible || (playModeOnly && !Application.isPlaying))
        {
            return;
        }

        DrawColliders();
        DrawCharacterControllers();
    }

    private void DrawColliders()
    {
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsInactive.Include);
        foreach (Collider collider in colliders)
        {
            if (collider == null || !IsIncluded(collider.gameObject.layer))
            {
                continue;
            }

            if (collider.isTrigger && !includeTriggers)
            {
                continue;
            }

            if (!collider.enabled && !includeDisabledColliders)
            {
                continue;
            }

            Gizmos.color = !collider.enabled ? disabledColor : collider.isTrigger ? triggerColor : colliderColor;
            DrawCollider(collider);
        }
    }

    private void DrawCharacterControllers()
    {
        if (!includeCharacterControllers)
        {
            return;
        }

        CharacterController[] controllers = FindObjectsByType<CharacterController>(FindObjectsInactive.Include);
        Gizmos.color = characterControllerColor;
        foreach (CharacterController controller in controllers)
        {
            if (controller == null || !controller.enabled || !IsIncluded(controller.gameObject.layer))
            {
                continue;
            }

            DrawCharacterController(controller);
        }
    }

    private bool IsIncluded(int layer)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private static void DrawCollider(Collider collider)
    {
        if (collider is BoxCollider boxCollider)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = boxCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            Gizmos.matrix = previousMatrix;
            return;
        }

        if (collider is SphereCollider sphereCollider)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(
                sphereCollider.transform.TransformPoint(sphereCollider.center),
                sphereCollider.transform.rotation,
                sphereCollider.transform.lossyScale);
            Gizmos.DrawWireSphere(Vector3.zero, sphereCollider.radius);
            Gizmos.matrix = previousMatrix;
            return;
        }

        if (collider is CapsuleCollider capsuleCollider)
        {
            DrawCapsuleCollider(capsuleCollider);
            return;
        }

        if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = meshCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireMesh(meshCollider.sharedMesh);
            Gizmos.matrix = previousMatrix;
            return;
        }

        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }

    private static void DrawCapsuleCollider(CapsuleCollider capsule)
    {
        Transform transform = capsule.transform;
        Vector3 axis = GetCapsuleAxis(capsule.direction);
        Vector3 worldAxis = transform.TransformDirection(axis).normalized;
        float radius = GetScaledRadius(capsule);
        float height = Mathf.Max(GetScaledHeight(capsule), radius * 2f);
        Vector3 center = transform.TransformPoint(capsule.center);
        float cylinderHalfHeight = Mathf.Max(0f, (height * 0.5f) - radius);
        Vector3 top = center + worldAxis * cylinderHalfHeight;
        Vector3 bottom = center - worldAxis * cylinderHalfHeight;

        DrawWireSphere(top, radius);
        DrawWireSphere(bottom, radius);

        Vector3 tangentA = Vector3.Cross(worldAxis, Vector3.up);
        if (tangentA.sqrMagnitude < 0.001f)
        {
            tangentA = Vector3.Cross(worldAxis, Vector3.right);
        }

        tangentA.Normalize();
        Vector3 tangentB = Vector3.Cross(worldAxis, tangentA).normalized;
        Gizmos.DrawLine(top + tangentA * radius, bottom + tangentA * radius);
        Gizmos.DrawLine(top - tangentA * radius, bottom - tangentA * radius);
        Gizmos.DrawLine(top + tangentB * radius, bottom + tangentB * radius);
        Gizmos.DrawLine(top - tangentB * radius, bottom - tangentB * radius);
    }

    private static void DrawCharacterController(CharacterController controller)
    {
        Vector3 center = controller.transform.TransformPoint(controller.center);
        Vector3 axis = controller.transform.up;
        float scaleX = Mathf.Abs(controller.transform.lossyScale.x);
        float scaleY = Mathf.Abs(controller.transform.lossyScale.y);
        float scaleZ = Mathf.Abs(controller.transform.lossyScale.z);
        float radius = controller.radius * Mathf.Max(scaleX, scaleZ);
        float height = Mathf.Max(controller.height * scaleY, radius * 2f);
        float cylinderHalfHeight = Mathf.Max(0f, (height * 0.5f) - radius);
        Vector3 top = center + axis * cylinderHalfHeight;
        Vector3 bottom = center - axis * cylinderHalfHeight;

        DrawWireSphere(top, radius);
        DrawWireSphere(bottom, radius);
        Gizmos.DrawLine(top + controller.transform.right * radius, bottom + controller.transform.right * radius);
        Gizmos.DrawLine(top - controller.transform.right * radius, bottom - controller.transform.right * radius);
        Gizmos.DrawLine(top + controller.transform.forward * radius, bottom + controller.transform.forward * radius);
        Gizmos.DrawLine(top - controller.transform.forward * radius, bottom - controller.transform.forward * radius);
    }

    private static Vector3 GetCapsuleAxis(int direction)
    {
        if (direction == 0)
        {
            return Vector3.right;
        }

        if (direction == 2)
        {
            return Vector3.forward;
        }

        return Vector3.up;
    }

    private static float GetScaledHeight(CapsuleCollider capsule)
    {
        Vector3 scale = capsule.transform.lossyScale;
        if (capsule.direction == 0)
        {
            return capsule.height * Mathf.Abs(scale.x);
        }

        if (capsule.direction == 2)
        {
            return capsule.height * Mathf.Abs(scale.z);
        }

        return capsule.height * Mathf.Abs(scale.y);
    }

    private static float GetScaledRadius(CapsuleCollider capsule)
    {
        Vector3 scale = capsule.transform.lossyScale;
        if (capsule.direction == 0)
        {
            return capsule.radius * Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        if (capsule.direction == 2)
        {
            return capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        return capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
    }

    private static void DrawWireSphere(Vector3 center, float radius)
    {
        DrawCircle(center, Vector3.up, radius);
        DrawCircle(center, Vector3.right, radius);
        DrawCircle(center, Vector3.forward, radius);
    }

    private static void DrawCircle(Vector3 center, Vector3 normal, float radius)
    {
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
        {
            tangent = Vector3.Cross(normal, Vector3.right);
        }

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
        Vector3 previous = center + tangent * radius;

        for (int i = 1; i <= CapsuleSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / CapsuleSegments;
            Vector3 next = center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
