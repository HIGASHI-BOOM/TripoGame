#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class BreadMachineCrabMonsterSetup
{
    private const string ModelPath = "Assets/Models/BreadMachineCrab/BreadMachineCrab.fbx";
    private const string MonsterPrefabPath = "Assets/Prefabs/AI/PF_Monster_Capsule.prefab";
    private const string ControllerPath = "Assets/Animations/AI/BreadMachineCrab_Monster.controller";
    private const float VisualNavMeshSurfaceOffset = 0.08333337f;
    private const float VisualGroundInset = 0.02f;

    [MenuItem("Tools/AI/Setup Bread Machine Crab Monster")]
    public static void Setup()
    {
        AssetDatabase.Refresh();
        EnsureFolders();
        ConfigureModelImporter(ModelPath);

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null)
        {
            Debug.LogWarning("BreadMachineCrab model was not found at " + ModelPath);
            return;
        }

        AnimatorController controller = CreateOrUpdateController(ModelPath, ControllerPath);
        ConfigureMonsterPrefab(modelPrefab, controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("BreadMachineCrab monster prefab setup complete.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Animations");
        EnsureFolder("Assets/Animations/AI");
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/AI");
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

    private static AnimatorController CreateOrUpdateController(string modelPath, string controllerPath)
    {
        AnimationClip[] clips = FindUsableClips(modelPath);
        AnimationClip idleClip = PickClip(clips, "idle", "stand", "wait") ?? FirstClip(clips);
        AnimationClip moveClip = PickClip(clips, "run", "walk", "move", "crawl") ?? idleClip;
        AnimationClip jumpClip = PickClip(clips, "jump", "leap", "fall", "drop") ?? moveClip ?? idleClip;
        AnimationClip attackClip = PickClip(clips, "attack", "hit", "bite", "claw") ?? null;

        LogClipSelection(clips, idleClip, moveClip, jumpClip, attackClip);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        ResetController(controller);
        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "MoveSpeedMultiplier", AnimatorControllerParameterType.Float, 1f);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idle = AddState(stateMachine, "Idle", idleClip, new Vector3(220f, 80f, 0f));
        AnimatorState move = AddState(stateMachine, "Move", moveClip, new Vector3(500f, 80f, 0f));
        AnimatorState jump = AddState(stateMachine, "Jump", jumpClip, new Vector3(360f, -80f, 0f));
        AnimatorState attack = attackClip != null ? AddState(stateMachine, "Attack", attackClip, new Vector3(640f, -80f, 0f)) : null;
        move.speedParameterActive = true;
        move.speedParameter = "MoveSpeedMultiplier";

        stateMachine.defaultState = idle;

        AnimatorStateTransition idleToMove = idle.AddTransition(move);
        ConfigureTransition(idleToMove, false, 0.12f);
        idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        AnimatorStateTransition moveToIdle = move.AddTransition(idle);
        ConfigureTransition(moveToIdle, false, 0.12f);
        moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AnimatorStateTransition anyToJump = stateMachine.AddAnyStateTransition(jump);
        ConfigureTransition(anyToJump, false, 0.05f);
        anyToJump.canTransitionToSelf = false;
        anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        AnimatorStateTransition jumpToIdle = jump.AddTransition(idle);
        ConfigureTransition(jumpToIdle, false, 0.08f);
        jumpToIdle.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        jumpToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AnimatorStateTransition jumpToMove = jump.AddTransition(move);
        ConfigureTransition(jumpToMove, false, 0.08f);
        jumpToMove.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        jumpToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        if (attack != null)
        {
            AnimatorStateTransition anyToAttack = stateMachine.AddAnyStateTransition(attack);
            ConfigureTransition(anyToAttack, false, 0.04f);
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            AnimatorStateTransition attackToIdle = attack.AddTransition(idle);
            ConfigureTransition(attackToIdle, true, 0.08f);
            attackToIdle.exitTime = 0.85f;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void LogClipSelection(
        AnimationClip[] clips,
        AnimationClip idleClip,
        AnimationClip moveClip,
        AnimationClip jumpClip,
        AnimationClip attackClip)
    {
        List<string> clipNames = new List<string>();
        for (int i = 0; i < clips.Length; i++)
        {
            clipNames.Add(clips[i].name);
        }

        Debug.Log("BreadMachineCrab imported animation clips: " + string.Join(", ", clipNames));
        Debug.Log(
            "BreadMachineCrab controller clip selection: Idle=" + ClipNameOrNone(idleClip)
            + ", Move=" + ClipNameOrNone(moveClip)
            + ", Jump=" + ClipNameOrNone(jumpClip)
            + ", Attack=" + ClipNameOrNone(attackClip));

        if (attackClip == null)
        {
            Debug.LogWarning("BreadMachineCrab attack clip was not found. Rename the clip to include attack, hit, bite, or claw if this is unexpected.");
        }
    }

    private static string ClipNameOrNone(AnimationClip clip)
    {
        return clip != null ? clip.name : "<none>";
    }

    private static void ConfigureModelImporter(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = CleanClipName(clips[i].name);
                bool shouldLoop = IsLoopingClip(clips[i].name);
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        importer.SaveAndReimport();
    }

    private static string CleanClipName(string clipName)
    {
        int separatorIndex = clipName.LastIndexOf('|');
        if (separatorIndex >= 0 && separatorIndex + 1 < clipName.Length)
        {
            return clipName.Substring(separatorIndex + 1);
        }

        return clipName;
    }

    private static bool IsLoopingClip(string clipName)
    {
        return clipName.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0
            || clipName.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0
            || clipName.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0
            || clipName.IndexOf("move", StringComparison.OrdinalIgnoreCase) >= 0
            || clipName.IndexOf("crawl", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ResetController(AnimatorController controller)
    {
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
        {
            controller.RemoveParameter(controller.parameters[i]);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            stateMachine.RemoveState(states[i].state);
        }

        AnimatorStateTransition[] anyTransitions = stateMachine.anyStateTransitions;
        for (int i = 0; i < anyTransitions.Length; i++)
        {
            stateMachine.RemoveAnyStateTransition(anyTransitions[i]);
        }
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float defaultFloat = 0f)
    {
        for (int i = 0; i < controller.parameters.Length; i++)
        {
            if (controller.parameters[i].name == name)
            {
                return;
            }
        }

        AnimatorControllerParameter parameter = new AnimatorControllerParameter
        {
            name = name,
            type = type,
            defaultFloat = defaultFloat
        };
        controller.AddParameter(parameter);
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    private static void ConfigureTransition(AnimatorStateTransition transition, bool hasExitTime, float duration)
    {
        transition.hasExitTime = hasExitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.interruptionSource = TransitionInterruptionSource.Source;
        transition.orderedInterruption = true;
    }

    private static AnimationClip[] FindUsableClips(string modelPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        List<AnimationClip> clips = new List<AnimationClip>();
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            clips.Add(clip);
        }

        return clips.ToArray();
    }

    private static AnimationClip PickClip(AnimationClip[] clips, params string[] keywords)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            for (int j = 0; j < clips.Length; j++)
            {
                if (clips[j].name.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return clips[j];
                }
            }
        }

        return null;
    }

    private static AnimationClip FirstClip(AnimationClip[] clips)
    {
        return clips.Length > 0 ? clips[0] : null;
    }

    private static void ConfigureMonsterPrefab(GameObject modelPrefab, AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
        try
        {
            RemoveChildIfExists(root.transform, "Visual_Capsule");
            RemoveChildIfExists(root.transform, "Facing_Marker");

            Transform existingModel = root.transform.Find("BreadMachineCrab_Model");
            if (existingModel != null)
            {
                UnityEngine.Object.DestroyImmediate(existingModel.gameObject);
            }

            GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelPrefab, root.transform) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = UnityEngine.Object.Instantiate(modelPrefab, root.transform);
            }

            modelInstance.name = "BreadMachineCrab_Model";
            modelInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            modelInstance.transform.localScale = Vector3.one;
            modelInstance.layer = root.layer;
            SetLayerRecursively(modelInstance, root.layer);

            FitModelToAgent(root, modelInstance);
            GroundModelToIdlePose(root, modelInstance);

            Animator animator = modelInstance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = modelInstance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            MonsterAnimationDriver driver = root.GetComponent<MonsterAnimationDriver>();
            if (driver == null)
            {
                driver = root.AddComponent<MonsterAnimationDriver>();
            }

            SerializedObject driverObject = new SerializedObject(driver);
            driverObject.FindProperty("animator").objectReferenceValue = animator;
            driverObject.FindProperty("agent").objectReferenceValue = root.GetComponent<NavMeshAgent>();
            driverObject.FindProperty("brain").objectReferenceValue = root.GetComponent<MonsterBrain>();
            driverObject.FindProperty("speedParameter").stringValue = "Speed";
            driverObject.FindProperty("groundedParameter").stringValue = "Grounded";
            driverObject.FindProperty("jumpTriggerParameter").stringValue = "Jump";
            driverObject.FindProperty("moveSpeedMultiplierParameter").stringValue = "MoveSpeedMultiplier";
            driverObject.FindProperty("fullSpeedMetersPerSecond").floatValue = 4.2f;
            driverObject.FindProperty("maxMoveAnimationSpeedMultiplier").floatValue = 2f;
            driverObject.ApplyModifiedPropertiesWithoutUndo();

            MonsterSpeedBoostSkill speedBoostSkill = root.GetComponent<MonsterSpeedBoostSkill>();
            if (speedBoostSkill == null)
            {
                speedBoostSkill = root.AddComponent<MonsterSpeedBoostSkill>();
            }

            ParticleSystem accelerationVfx = BreadMachineCrabAccelerationVfxSetup.EnsureAccelerationVfx(root);
            SerializedObject speedBoostObject = new SerializedObject(speedBoostSkill);
            speedBoostObject.FindProperty("brain").objectReferenceValue = root.GetComponent<MonsterBrain>();
            speedBoostObject.FindProperty("agent").objectReferenceValue = root.GetComponent<NavMeshAgent>();
            speedBoostObject.FindProperty("autoActivateWhileChasing").boolValue = true;
            speedBoostObject.FindProperty("firstUseDelay").floatValue = 0.75f;
            speedBoostObject.FindProperty("cooldown").floatValue = 6f;
            speedBoostObject.FindProperty("speedMultiplier").floatValue = 2f;
            speedBoostObject.FindProperty("duration").floatValue = 2.5f;
            speedBoostObject.FindProperty("cancelWhenNotChasing").boolValue = true;
            BreadMachineCrabAccelerationVfxSetup.AssignSpeedBoostVfx(speedBoostObject, root, accelerationVfx);
            speedBoostObject.ApplyModifiedPropertiesWithoutUndo();

            Transform attackTrigger = root.transform.Find("Attack_Trigger");
            if (attackTrigger != null)
            {
                MonsterContactAttack attack = attackTrigger.GetComponent<MonsterContactAttack>();
                if (attack != null)
                {
                    SerializedObject attackObject = new SerializedObject(attack);
                    attackObject.FindProperty("animator").objectReferenceValue = animator;
                    attackObject.FindProperty("attackTriggerParameter").stringValue = "Attack";
                    attackObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/AI/Ground Bread Machine Crab Feet")]
    public static void GroundBreadMachineCrabFeet()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
        try
        {
            Transform model = root.transform.Find("BreadMachineCrab_Model");
            if (model == null)
            {
                Debug.LogWarning("BreadMachineCrab_Model was not found in " + MonsterPrefabPath);
                return;
            }

            GroundModelToIdlePose(root, model.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("BreadMachineCrab feet grounded.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveChildIfExists(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        if (child != null)
        {
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void FitModelToAgent(GameObject root, GameObject modelInstance)
    {
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        float targetHeight = agent != null ? agent.height : 2f;
        Bounds bounds = CalculateRendererBounds(modelInstance);
        if (bounds.size.y > 0.001f)
        {
            float scale = targetHeight / bounds.size.y;
            modelInstance.transform.localScale = Vector3.one * scale;
        }

        bounds = CalculateRendererBounds(modelInstance);
        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
        float localBottom = root.transform.InverseTransformPoint(bounds.min).y;
        modelInstance.transform.localPosition -= new Vector3(localCenter.x, localBottom, localCenter.z);
    }

    private static void GroundModelToIdlePose(GameObject root, GameObject modelInstance)
    {
        AnimationClip idleClip = PickClip(FindUsableClips(ModelPath), "idle") ?? FirstClip(FindUsableClips(ModelPath));
        Vector3 originalPosition = modelInstance.transform.localPosition;

        if (idleClip != null)
        {
            AnimationMode.StartAnimationMode();
            AnimationMode.SampleAnimationClip(modelInstance, idleClip, 0f);
        }

        Bounds bounds = CalculateRendererBounds(modelInstance);
        float localBottom = root.transform.InverseTransformPoint(bounds.min).y;
        float targetLocalBottom = -VisualNavMeshSurfaceOffset - VisualGroundInset;
        float yCorrection = targetLocalBottom - localBottom;
        modelInstance.transform.localPosition = originalPosition + Vector3.up * yCorrection;

        if (idleClip != null)
        {
            AnimationMode.StopAnimationMode();
        }
    }

    private static Bounds CalculateRendererBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position + Vector3.up, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }
}
#endif
