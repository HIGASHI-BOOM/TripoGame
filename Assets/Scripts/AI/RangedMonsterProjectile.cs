using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class RangedMonsterProjectile : MonoBehaviour
{
    [Tooltip("Projectile travel speed in meters per second.")]
    [SerializeField] private float speed = 12f;

    [Tooltip("Seconds before this projectile self-destructs if it hits nothing.")]
    [SerializeField] private float lifetime = 4f;

    [Tooltip("Radius used for continuous hit checks so fast shots do not tunnel through thin targets.")]
    [SerializeField] private float hitRadius = 0.18f;

    [Tooltip("Layers the projectile can hit.")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Tooltip("Tag used to identify the player target.")]
    [SerializeField] private string targetTag = "Player";

    [Tooltip("Horizontal knockback speed applied to the player when hit.")]
    [SerializeField] private float knockbackSpeed = 2.25f;

    [Tooltip("Upward launch speed applied to the player when hit.")]
    [SerializeField] private float knockUpSpeed = 2.25f;

    private Transform owner;
    private Vector3 direction = Vector3.forward;
    private float despawnTime;

    public void Launch(Vector3 launchDirection, Transform projectileOwner)
    {
        direction = launchDirection.sqrMagnitude > 0.001f ? launchDirection.normalized : transform.forward;
        owner = projectileOwner;
        despawnTime = Time.time + lifetime;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    public void SetSpeed(float projectileSpeed)
    {
        speed = Mathf.Max(0.1f, projectileSpeed);
    }

    private void Reset()
    {
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = hitRadius;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void Awake()
    {
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = hitRadius;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        despawnTime = Time.time + lifetime;
    }

    private void Update()
    {
        if (Time.time >= despawnTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 start = transform.position;
        Vector3 displacement = direction * (speed * Time.deltaTime);
        float distance = displacement.magnitude;

        if (distance > 0f && Physics.SphereCast(start, hitRadius, direction, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (!IsOwner(hit.transform))
            {
                HandleHit(hit.collider, hit.point);
                return;
            }
        }

        transform.position = start + displacement;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner(other.transform))
        {
            HandleHit(other, transform.position);
        }
    }

    private void HandleHit(Collider other, Vector3 hitPoint)
    {
        FirstPersonPlayerController player = other.GetComponentInParent<FirstPersonPlayerController>();
        if (player != null && player.CompareTag(targetTag))
        {
            player.ApplyLaunch(transform.position, knockbackSpeed, knockUpSpeed);
        }

        Destroy(gameObject);
    }

    private bool IsOwner(Transform candidate)
    {
        return owner != null && candidate != null && (candidate == owner || candidate.IsChildOf(owner));
    }
}
