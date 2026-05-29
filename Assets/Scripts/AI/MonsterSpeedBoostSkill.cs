using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(MonsterBrain))]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterSpeedBoostSkill : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("References")]
    [Tooltip("Monster brain that owns the NavMeshAgent movement speeds.")]
    [SerializeField] private MonsterBrain brain;

    [Tooltip("NavMeshAgent used to read the monster's acceleration for skill VFX intensity.")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Activation")]
    [Tooltip("Automatically cast the speed boost while the monster is chasing the player.")]
    [SerializeField] private bool autoActivateWhileChasing = true;

    [Tooltip("Seconds before the first automatic speed boost can trigger after spawn.")]
    [SerializeField] private float firstUseDelay = 0.75f;

    [Tooltip("Seconds between speed boost casts.")]
    [SerializeField] private float cooldown = 6f;

    [Header("Effect")]
    [Tooltip("Movement speed multiplier applied while the skill is active. 2 means double speed.")]
    [SerializeField] private float speedMultiplier = 2f;

    [Tooltip("Seconds the speed boost stays active.")]
    [SerializeField] private float duration = 2.5f;

    [Tooltip("End the speed boost early if the monster stops chasing the player.")]
    [SerializeField] private bool cancelWhenNotChasing = true;

    [Header("VFX")]
    [Tooltip("Particle system played while the speed boost skill is active.")]
    [SerializeField] private ParticleSystem accelerationParticles;

    [Tooltip("Create a default acceleration particle trail at runtime if no particle system is assigned.")]
    [SerializeField] private bool createDefaultVfxWhenMissing = true;

    [Tooltip("Particle emission added per NavMeshAgent acceleration unit while the boost is active.")]
    [SerializeField] private float particleRatePerAcceleration = 2.75f;

    [Tooltip("Minimum and maximum particles per second allowed for the acceleration effect.")]
    [SerializeField] private Vector2 particleEmissionRange = new Vector2(30f, 120f);

    [Tooltip("How strongly acceleration affects particle playback speed while the boost is active.")]
    [SerializeField] private float particleSimulationSpeedPerAcceleration = 0.035f;

    private float cooldownTimer;
    private float activeTimer;
    private bool isActive;
    private Material runtimeParticleMaterial;

    public bool IsActive => isActive;

    private void Awake()
    {
        if (brain == null)
        {
            brain = GetComponent<MonsterBrain>();
        }

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (accelerationParticles == null)
        {
            Transform effectTransform = transform.Find("Skill_Acceleration_VFX");
            if (effectTransform != null)
            {
                accelerationParticles = effectTransform.GetComponent<ParticleSystem>();
            }
        }

        if (accelerationParticles == null && createDefaultVfxWhenMissing)
        {
            accelerationParticles = CreateDefaultAccelerationParticles();
        }
    }

    private void OnEnable()
    {
        cooldownTimer = Mathf.Max(0f, firstUseDelay);
        activeTimer = 0f;
        isActive = false;
        SetMultiplier(1f);
        StopAccelerationParticles(true);
    }

    private void OnDisable()
    {
        EndSkill(false);
    }

    private void OnDestroy()
    {
        if (runtimeParticleMaterial != null)
        {
            Destroy(runtimeParticleMaterial);
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (isActive)
        {
            activeTimer -= deltaTime;
            if (activeTimer <= 0f || ShouldCancelForState())
            {
                EndSkill(true);
            }
            else
            {
                UpdateAccelerationParticles();
            }

            return;
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= deltaTime;
            return;
        }

        if (autoActivateWhileChasing && brain != null && brain.CurrentState == MonsterBrain.MonsterState.Chase)
        {
            ActivateSkill();
        }
    }

    public void ActivateSkill()
    {
        if (isActive || brain == null)
        {
            return;
        }

        isActive = true;
        activeTimer = Mathf.Max(0.05f, duration);
        SetMultiplier(speedMultiplier);
        PlayAccelerationParticles();
    }

    public void EndSkill(bool startCooldown)
    {
        if (!isActive && brain != null && Mathf.Approximately(brain.SpeedMultiplier, 1f))
        {
            StopAccelerationParticles(false);
            return;
        }

        isActive = false;
        activeTimer = 0f;
        SetMultiplier(1f);
        StopAccelerationParticles(false);

        if (startCooldown)
        {
            cooldownTimer = Mathf.Max(0.05f, cooldown);
        }
    }

    private bool ShouldCancelForState()
    {
        if (!cancelWhenNotChasing || brain == null)
        {
            return false;
        }

        return brain.CurrentState != MonsterBrain.MonsterState.Chase
            && brain.CurrentState != MonsterBrain.MonsterState.TraversingLink;
    }

    private void PlayAccelerationParticles()
    {
        if (accelerationParticles == null)
        {
            return;
        }

        UpdateAccelerationParticles();
        accelerationParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        accelerationParticles.Play(true);
    }

    private void StopAccelerationParticles(bool clear)
    {
        if (accelerationParticles == null)
        {
            return;
        }

        ParticleSystemStopBehavior stopBehavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;
        accelerationParticles.Stop(true, stopBehavior);
    }

    private void UpdateAccelerationParticles()
    {
        if (accelerationParticles == null)
        {
            return;
        }

        float acceleration = agent != null ? agent.acceleration : 0f;
        float boostMultiplier = brain != null ? brain.SpeedMultiplier : speedMultiplier;
        float boostedAcceleration = Mathf.Max(0f, acceleration * Mathf.Max(1f, boostMultiplier));
        float minRate = Mathf.Min(particleEmissionRange.x, particleEmissionRange.y);
        float maxRate = Mathf.Max(particleEmissionRange.x, particleEmissionRange.y);

        ParticleSystem.EmissionModule emission = accelerationParticles.emission;
        emission.rateOverTime = Mathf.Clamp(boostedAcceleration * particleRatePerAcceleration, minRate, maxRate);

        ParticleSystem.MainModule main = accelerationParticles.main;
        main.simulationSpeed = Mathf.Max(0.1f, 1f + boostedAcceleration * particleSimulationSpeedPerAcceleration);
    }

    private ParticleSystem CreateDefaultAccelerationParticles()
    {
        GameObject effectObject = new GameObject("Skill_Acceleration_VFX");
        effectObject.transform.SetParent(transform, false);
        effectObject.transform.localPosition = new Vector3(0f, 0.42f, -0.48f);
        effectObject.transform.localRotation = Quaternion.Euler(-8f, 180f, 0f);

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        ConfigureDefaultAccelerationParticles(particles);
        return particles;
    }

    private void ConfigureDefaultAccelerationParticles(ParticleSystem particles)
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Clear(true);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 0.7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 5.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-25f * Mathf.Deg2Rad, 25f * Mathf.Deg2Rad);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.95f, 1f, 0.88f),
            new Color(1f, 0.55f, 0.18f, 0.76f));
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 180;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 72f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)18, (short)28, 1, 0.03f)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.28f;
        shape.length = 0.18f;
        shape.radiusThickness = 0.35f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
        velocity.z = new ParticleSystem.MinMaxCurve(0.55f, 1.4f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.3f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.62f, 0.2f), 0.58f),
                new GradientColorKey(new Color(1f, 0.9f, 0.55f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.08f),
                new GradientAlphaKey(0.72f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.18f, 1f),
            new Keyframe(1f, 0.15f));
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.18f);
        noise.frequency = 1.6f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.8f);

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.velocityScale = 0.35f;
            particleRenderer.lengthScale = 1.75f;
            particleRenderer.maxParticleSize = 0.45f;
            particleRenderer.material = GetOrCreateRuntimeParticleMaterial();
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private Material GetOrCreateRuntimeParticleMaterial()
    {
        if (runtimeParticleMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                return null;
            }

            runtimeParticleMaterial = new Material(shader)
            {
                name = "Runtime Crab Acceleration VFX"
            };
        }

        SetMaterialColor(runtimeParticleMaterial, new Color(0.65f, 0.95f, 1f, 0.8f));
        return runtimeParticleMaterial;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }

        if (material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, color * 1.35f);
        }
    }

    private void SetMultiplier(float multiplier)
    {
        if (brain != null)
        {
            brain.SpeedMultiplier = Mathf.Max(0.01f, multiplier);
        }
    }

    private void OnValidate()
    {
        firstUseDelay = Mathf.Max(0f, firstUseDelay);
        cooldown = Mathf.Max(0.05f, cooldown);
        speedMultiplier = Mathf.Max(0.01f, speedMultiplier);
        duration = Mathf.Max(0.05f, duration);
        particleRatePerAcceleration = Mathf.Max(0f, particleRatePerAcceleration);
        particleEmissionRange.x = Mathf.Max(0f, particleEmissionRange.x);
        particleEmissionRange.y = Mathf.Max(particleEmissionRange.x, particleEmissionRange.y);
        particleSimulationSpeedPerAcceleration = Mathf.Max(0f, particleSimulationSpeedPerAcceleration);
    }
}
