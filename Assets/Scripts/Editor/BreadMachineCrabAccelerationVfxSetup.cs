#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BreadMachineCrabAccelerationVfxSetup
{
    private const string EffectName = "Skill_Acceleration_VFX";
    private const string MonsterPrefabPath = "Assets/Prefabs/AI/PF_Monster_Capsule.prefab";
    private const string EffectPrefabPath = "Assets/Prefabs/AI/VFX/PF_BreadMachineCrab_Acceleration_VFX.prefab";
    private const string EffectMaterialPath = "Assets/Materials/AI/VFX/M_BreadMachineCrab_Acceleration_VFX.mat";

    [MenuItem("Tools/AI/Setup Bread Machine Crab Acceleration VFX")]
    public static void Setup()
    {
        AssetDatabase.Refresh();
        GameObject root = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
        try
        {
            ParticleSystem effect = EnsureAccelerationVfx(root);
            MonsterSpeedBoostSkill speedBoostSkill = root.GetComponent<MonsterSpeedBoostSkill>();
            if (speedBoostSkill != null)
            {
                SerializedObject speedBoostObject = new SerializedObject(speedBoostSkill);
                AssignSpeedBoostVfx(speedBoostObject, root, effect);
                speedBoostObject.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BreadMachineCrab acceleration VFX setup complete.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static ParticleSystem EnsureAccelerationVfx(GameObject root)
    {
        EnsureFolders();
        GameObject effectPrefab = CreateOrUpdateEffectPrefab();

        Transform existingEffect = root.transform.Find(EffectName);
        if (existingEffect != null)
        {
            Object.DestroyImmediate(existingEffect.gameObject);
        }

        GameObject effectInstance = PrefabUtility.InstantiatePrefab(effectPrefab, root.transform) as GameObject;
        if (effectInstance == null)
        {
            effectInstance = Object.Instantiate(effectPrefab, root.transform);
        }

        effectInstance.name = EffectName;
        effectInstance.transform.localPosition = new Vector3(0f, 0.42f, -0.48f);
        effectInstance.transform.localRotation = Quaternion.Euler(-8f, 180f, 0f);
        effectInstance.transform.localScale = Vector3.one;
        effectInstance.layer = root.layer;

        ParticleSystem effect = effectInstance.GetComponent<ParticleSystem>();
        ConfigureParticleSystem(effect, GetOrCreateMaterial());
        return effect;
    }

    public static void AssignSpeedBoostVfx(SerializedObject speedBoostObject, GameObject root, ParticleSystem effect)
    {
        speedBoostObject.FindProperty("brain").objectReferenceValue = root.GetComponent<MonsterBrain>();
        speedBoostObject.FindProperty("agent").objectReferenceValue = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
        speedBoostObject.FindProperty("accelerationParticles").objectReferenceValue = effect;
        speedBoostObject.FindProperty("particleRatePerAcceleration").floatValue = 2.75f;
        speedBoostObject.FindProperty("particleEmissionRange").vector2Value = new Vector2(30f, 120f);
        speedBoostObject.FindProperty("particleSimulationSpeedPerAcceleration").floatValue = 0.035f;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/AI");
        EnsureFolder("Assets/Prefabs/AI/VFX");
        EnsureFolder("Assets/Materials");
        EnsureFolder("Assets/Materials/AI");
        EnsureFolder("Assets/Materials/AI/VFX");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folder).Replace("\\", "/");
        string name = System.IO.Path.GetFileName(folder);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static GameObject CreateOrUpdateEffectPrefab()
    {
        GameObject effectRoot = new GameObject(EffectName);
        ParticleSystem particles = effectRoot.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(particles, GetOrCreateMaterial());

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(effectRoot, EffectPrefabPath);
        Object.DestroyImmediate(effectRoot);
        return prefab;
    }

    private static void ConfigureParticleSystem(ParticleSystem particles, Material material)
    {
        if (particles == null)
        {
            return;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Clear(true);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 0.7f;
        main.startDelay = 0f;
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
            particleRenderer.material = material;
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(EffectMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default");

        if (material == null)
        {
            if (shader == null)
            {
                Debug.LogWarning("BreadMachineCrab acceleration VFX material skipped because no particle shader was found.");
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, EffectMaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        SetMaterialColor(material, new Color(0.65f, 0.95f, 1f, 0.8f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.35f);
        }
    }
}

[InitializeOnLoad]
internal static class BreadMachineCrabAccelerationVfxAutoSetup
{
    private const string AutoRunSessionKey = "BreadMachineCrabAccelerationVfxAutoSetup.Ran";

    static BreadMachineCrabAccelerationVfxAutoSetup()
    {
        EditorApplication.delayCall += RunIfNeeded;
    }

    private static void RunIfNeeded()
    {
        if (Application.isBatchMode || SessionState.GetBool(AutoRunSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(AutoRunSessionKey, true);
        GameObject monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AI/PF_Monster_Capsule.prefab");
        if (monsterPrefab == null)
        {
            return;
        }

        bool needsEffectAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AI/VFX/PF_BreadMachineCrab_Acceleration_VFX.prefab") == null;
        bool needsEffectChild = monsterPrefab.transform.Find("Skill_Acceleration_VFX") == null;
        MonsterSpeedBoostSkill speedBoostSkill = monsterPrefab.GetComponent<MonsterSpeedBoostSkill>();
        bool needsSkillReference = speedBoostSkill == null || speedBoostSkill.GetComponentInChildren<ParticleSystem>(true) == null;

        if (needsEffectAsset || needsEffectChild || needsSkillReference)
        {
            BreadMachineCrabAccelerationVfxSetup.Setup();
        }
    }
}
#endif
