using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHitFeedback : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Flash")]
    [Tooltip("Renderers that flash when the player is hit. Leave empty to use all non-particle renderers under this player.")]
    [SerializeField] private Renderer[] targetRenderers;

    [Tooltip("Optional material used during the flash. Leave empty to create a runtime white unlit material.")]
    [SerializeField] private Material flashMaterial;

    [Tooltip("Color applied to the runtime flash material.")]
    [SerializeField] private Color flashColor = Color.white;

    [Tooltip("How long the player stays white after a hit, in seconds.")]
    [Min(0.01f)]
    [SerializeField] private float flashDuration = 0.12f;

    [Header("Particles")]
    [Tooltip("Particle system played when the player is hit. Leave empty to create a small default burst at runtime.")]
    [SerializeField] private ParticleSystem hitParticles;

    [Tooltip("Create a default hit particle burst if no particle system is assigned.")]
    [SerializeField] private bool createDefaultParticlesIfMissing = true;

    [Tooltip("Local-space offset where the hit particles should play from the player root.")]
    [SerializeField] private Vector3 particleOffset = new Vector3(0f, 0.65f, 0f);

    [Tooltip("Color used by the generated default particle burst.")]
    [SerializeField] private Color particleColor = new Color(1f, 0.95f, 0.55f, 1f);

    [Tooltip("Number of particles emitted by the generated default burst.")]
    [Min(1)]
    [SerializeField] private int defaultParticleBurstCount = 18;

    [Tooltip("Radius of the generated default burst, in meters.")]
    [Min(0.01f)]
    [SerializeField] private float defaultParticleRadius = 0.28f;

    [Tooltip("Start speed of generated default particles, in meters per second.")]
    [Min(0.01f)]
    [SerializeField] private float defaultParticleSpeed = 1.8f;

    private Material[][] originalMaterials;
    private Material runtimeFlashMaterial;
    private Material runtimeParticleMaterial;
    private Coroutine flashRoutine;

    private void Awake()
    {
        CacheRenderers();
        CacheOriginalMaterials();
        EnsureParticles();
    }

    private void OnDisable()
    {
        RestoreMaterials();
    }

    private void OnDestroy()
    {
        if (runtimeFlashMaterial != null)
        {
            Destroy(runtimeFlashMaterial);
        }

        if (runtimeParticleMaterial != null)
        {
            Destroy(runtimeParticleMaterial);
        }
    }

    public void PlayHitFeedback(Vector3 sourcePosition)
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            RestoreMaterials();
        }

        flashRoutine = StartCoroutine(FlashRoutine());
        PlayParticles(sourcePosition);
    }

    private IEnumerator FlashRoutine()
    {
        ApplyFlashMaterial();
        yield return new WaitForSeconds(flashDuration);
        RestoreMaterials();
        flashRoutine = null;
    }

    private void CacheRenderers()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            return;
        }

        Renderer[] foundRenderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> filteredRenderers = new List<Renderer>(foundRenderers.Length);
        for (int i = 0; i < foundRenderers.Length; i++)
        {
            Renderer foundRenderer = foundRenderers[i];
            if (foundRenderer == null || foundRenderer is ParticleSystemRenderer || foundRenderer.name == "Facing_Marker")
            {
                continue;
            }

            filteredRenderers.Add(foundRenderer);
        }

        targetRenderers = filteredRenderers.ToArray();
    }

    private void CacheOriginalMaterials()
    {
        if (targetRenderers == null)
        {
            originalMaterials = new Material[0][];
            return;
        }

        originalMaterials = new Material[targetRenderers.Length][];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            originalMaterials[i] = targetRenderers[i] != null ? targetRenderers[i].sharedMaterials : null;
        }
    }

    private void ApplyFlashMaterial()
    {
        Material material = GetOrCreateFlashMaterial();
        if (material == null || targetRenderers == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            Material[] cachedMaterials = i < originalMaterials.Length ? originalMaterials[i] : null;
            if (targetRenderer == null || cachedMaterials == null || cachedMaterials.Length == 0)
            {
                continue;
            }

            Material[] flashMaterials = new Material[cachedMaterials.Length];
            for (int materialIndex = 0; materialIndex < flashMaterials.Length; materialIndex++)
            {
                flashMaterials[materialIndex] = material;
            }

            targetRenderer.sharedMaterials = flashMaterials;
        }
    }

    private void RestoreMaterials()
    {
        if (targetRenderers == null || originalMaterials == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length && i < originalMaterials.Length; i++)
        {
            if (targetRenderers[i] != null && originalMaterials[i] != null)
            {
                targetRenderers[i].sharedMaterials = originalMaterials[i];
            }
        }
    }

    private Material GetOrCreateFlashMaterial()
    {
        if (runtimeFlashMaterial == null)
        {
            if (flashMaterial != null)
            {
                runtimeFlashMaterial = new Material(flashMaterial);
                runtimeFlashMaterial.name = flashMaterial.name + " (Runtime Hit Flash)";
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                                Shader.Find("Unlit/Color") ??
                                Shader.Find("Universal Render Pipeline/Lit") ??
                                Shader.Find("Standard");

                if (shader == null)
                {
                    return null;
                }

                runtimeFlashMaterial = new Material(shader)
                {
                    name = "Runtime Player Hit Flash"
                };
            }
        }

        SetMaterialColor(runtimeFlashMaterial, flashColor);
        return runtimeFlashMaterial;
    }

    private void EnsureParticles()
    {
        if (hitParticles != null || !createDefaultParticlesIfMissing)
        {
            return;
        }

        Transform existingParticles = transform.Find("HitFeedback_Particles");
        GameObject particleObject = existingParticles != null ? existingParticles.gameObject : new GameObject("HitFeedback_Particles");
        particleObject.transform.SetParent(transform, false);

        hitParticles = particleObject.GetComponent<ParticleSystem>();
        if (hitParticles == null)
        {
            hitParticles = particleObject.AddComponent<ParticleSystem>();
        }

        ConfigureDefaultParticles(hitParticles);
    }

    private void ConfigureDefaultParticles(ParticleSystem particles)
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Clear(true);

        particles.transform.localPosition = particleOffset;
        particles.transform.localRotation = Quaternion.identity;

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(defaultParticleSpeed * 0.6f, defaultParticleSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = particleColor;
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(32, defaultParticleBurstCount * 2);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Clamp(defaultParticleBurstCount, 1, 128))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = defaultParticleRadius;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material particleMaterial = GetOrCreateParticleMaterial();
            if (particleMaterial != null)
            {
                particleRenderer.material = particleMaterial;
            }
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void PlayParticles(Vector3 sourcePosition)
    {
        if (hitParticles == null)
        {
            return;
        }

        Vector3 awayFromSource = transform.position - sourcePosition;
        awayFromSource.y = 0f;
        if (awayFromSource.sqrMagnitude < 0.001f)
        {
            awayFromSource = transform.forward;
        }

        Transform particleTransform = hitParticles.transform;
        particleTransform.position = transform.TransformPoint(particleOffset);
        particleTransform.rotation = Quaternion.LookRotation(awayFromSource.normalized, Vector3.up);

        hitParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        hitParticles.Play(true);
    }

    private Material GetOrCreateParticleMaterial()
    {
        if (runtimeParticleMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Particles/Standard Unlit") ??
                            Shader.Find("Sprites/Default");

            if (shader == null)
            {
                return null;
            }

            runtimeParticleMaterial = new Material(shader)
            {
                name = "Runtime Player Hit Particles"
            };
        }

        SetMaterialColor(runtimeParticleMaterial, particleColor);
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
            material.SetColor(EmissionColorId, color);
        }
    }
}
