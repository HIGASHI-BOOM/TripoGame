using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor script to create and set up the ceiling-walking monster prototype.
///
/// Usage: Tools > Ceiling Monster Setup
/// This creates the ceiling monster prefab, places an instance in the current
/// scene at the ceiling, and optionally creates patrol waypoints.
/// </summary>
public class CeilingMonsterSetup : EditorWindow
{
    private float ceilingHeight = 11.97f;
    private string prefabName = "PF_CeilingMonster_Capsule";
    private int waypointCount = 4;
    private float waypointRadius = 5f;
    private string targetTag = "Player";
    private bool createWaypoints = true;

    [MenuItem("Tools/Ceiling Monster Setup")]
    private static void ShowWindow()
    {
        CeilingMonsterSetup window = GetWindow<CeilingMonsterSetup>();
        window.titleContent = new GUIContent("Ceiling Monster");
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Ceiling Monster Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        ceilingHeight = EditorGUILayout.FloatField("Ceiling Height (Y)", ceilingHeight);
        prefabName = EditorGUILayout.TextField("Prefab Name", prefabName);
        targetTag = EditorGUILayout.TextField("Target Tag", targetTag);
        createWaypoints = EditorGUILayout.Toggle("Create Patrol Waypoints", createWaypoints);

        if (createWaypoints)
        {
            EditorGUI.indentLevel++;
            waypointCount = EditorGUILayout.IntField("Waypoint Count", Mathf.Max(2, waypointCount));
            waypointRadius = EditorGUILayout.FloatField("Waypoint Radius", waypointRadius);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Create Ceiling Monster", GUILayout.Height(30)))
        {
            CreateCeilingMonster();
        }
    }

    private void CreateCeilingMonster()
    {
        // 1. Create the root ceiling monster game object
        GameObject monster = new GameObject(prefabName, typeof(CapsuleCollider), typeof(Rigidbody));
        monster.layer = LayerMask.NameToLayer("Default"); // Adjust layer as needed

        // Store reference for undo
        Undo.RegisterCreatedObjectUndo(monster, "Create Ceiling Monster");

        // 2. Configure CapsuleCollider
        CapsuleCollider capsule = monster.GetComponent<CapsuleCollider>();
        capsule.direction = 1; // Y-axis
        capsule.radius = 0.45f;
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 1.1f, 0f);

        // 3. Configure Rigidbody
        Rigidbody rb = monster.GetComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 2f;
        rb.angularDamping = 5f;
        rb.useGravity = false; // CeilingMonsterBrain handles gravity
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // 4. Add visual representation (capsule mesh)
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Capsule_Visual";
        visual.transform.SetParent(monster.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        // Remove the collider from the visual (we use the root collider)
        DestroyImmediate(visual.GetComponent<CapsuleCollider>());

        // Set a visible material
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat != null)
            {
                mat.color = new Color(0.9f, 0.2f, 0.1f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.3f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
                renderer.sharedMaterial = mat;
            }
        }

        // 5. Add CeilingMonsterBrain
        CeilingMonsterBrain brain = monster.AddComponent<CeilingMonsterBrain>();
        brain.hideFlags = HideFlags.None;

        // Set modelRoot to the visual child so it gets flipped
        // The script auto-flips modelRoot in Awake

        // Ranged attack (fires bullets at the player)
        monster.AddComponent<CeilingMonsterAttack>();

        // 6. Create Attack Trigger child
        GameObject attackTrigger = new GameObject("Attack_Trigger");
        attackTrigger.transform.SetParent(monster.transform);
        attackTrigger.transform.localPosition = new Vector3(0f, 1f, 0f);

        SphereCollider attackCollider = attackTrigger.AddComponent<SphereCollider>();
        attackCollider.isTrigger = true;
        attackCollider.radius = 1.3f;

        Rigidbody attackRb = attackTrigger.AddComponent<Rigidbody>();
        attackRb.isKinematic = true;
        attackRb.useGravity = false;

        MonsterContactAttack contactAttack = attackTrigger.AddComponent<MonsterContactAttack>();
        // Set default values
        SerializedObject so = new SerializedObject(contactAttack);
        so.Update();
        so.FindProperty("targetTag").stringValue = targetTag;
        so.ApplyModifiedProperties();

        // 7. Position at ceiling height
        Vector3 pos = monster.transform.position;
        pos.y = ceilingHeight - (capsule.height * 0.5f);
        monster.transform.position = pos;

        // 8. Create patrol waypoints if requested
        Transform waypointParent = null;
        if (createWaypoints)
        {
            GameObject wpParent = new GameObject("CeilingMonster_Waypoints");
            Undo.RegisterCreatedObjectUndo(wpParent, "Create Waypoints");
            waypointParent = wpParent.transform;
            waypointParent.position = monster.transform.position;

            for (int i = 0; i < waypointCount; i++)
            {
                float angle = (float)i / waypointCount * 360f * Mathf.Deg2Rad;
                Vector3 wpPos = new Vector3(
                    Mathf.Cos(angle) * waypointRadius,
                    0f,
                    Mathf.Sin(angle) * waypointRadius
                );

                GameObject wp = new GameObject($"Waypoint_{i + 1}");
                Undo.RegisterCreatedObjectUndo(wp, "Create Waypoint");
                wp.transform.SetParent(waypointParent);
                wp.transform.position = monster.transform.position + wpPos;

                // Visual indicator for waypoints
                var wpRenderer = wp.AddComponent<SphereCollider>();
                wpRenderer.isTrigger = true;
                wpRenderer.radius = 0.3f;
            }

            // Assign waypoints to brain via SerializedObject (fields are [SerializeField] private)
            SerializedObject brainSO = new SerializedObject(brain);
            brainSO.Update();
            SerializedProperty wpArray = brainSO.FindProperty("patrolWaypoints");
            wpArray.ClearArray();
            wpArray.arraySize = waypointCount;
            for (int i = 0; i < waypointCount; i++)
            {
                wpArray.GetArrayElementAtIndex(i).objectReferenceValue = waypointParent.GetChild(i);
            }
            brainSO.ApplyModifiedProperties();
        }

        // 9. Save as prefab
        string prefabPath = $"Assets/Prefabs/AI/{prefabName}.prefab";
        EnsureDirectoryExists("Assets/Prefabs/AI");

        // Check if prefab already exists
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            if (EditorUtility.DisplayDialog("Overwrite Prefab?",
                $"Prefab '{prefabName}.prefab' already exists. Overwrite?",
                "Overwrite", "Cancel"))
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(monster, prefabPath, InteractionMode.UserAction);
                Debug.Log($"Ceiling monster prefab updated at: {prefabPath}");
            }
        }
        else
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(monster, prefabPath, InteractionMode.UserAction);
            Debug.Log($"Ceiling monster prefab created at: {prefabPath}");
        }

        // 10. Select the monster in the hierarchy
        Selection.activeGameObject = monster;

        Debug.Log("Ceiling monster created successfully at ceiling height!");
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            EnsureDirectoryExists(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    /// <summary>
    /// Quick-create a ceiling monster at the default ceiling height (menu item).
    /// </summary>
    [MenuItem("GameObject/Ceiling Monster", false, 10)]
    private static void QuickCreateCeilingMonster()
    {
        // Find ceiling height from scene
        float height = 11.97f;
        GameObject slab = GameObject.Find("Ceiling slab");
        if (slab != null)
        {
            height = slab.transform.position.y - 0.06f;
        }

        // Create monster
        GameObject monster = new GameObject("CeilingMonster", typeof(CapsuleCollider), typeof(Rigidbody));

        // Configure components
        CapsuleCollider capsule = monster.GetComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = 0.45f;
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 1.1f, 0f);

        Rigidbody rb = monster.GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.linearDamping = 2f;
        rb.angularDamping = 5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Visual
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Capsule_Visual";
        visual.transform.SetParent(monster.transform);
        visual.transform.localPosition = Vector3.zero;
        DestroyImmediate(visual.GetComponent<CapsuleCollider>());
        Renderer visRenderer = visual.GetComponent<Renderer>();
        if (visRenderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat != null)
            {
                mat.color = new Color(0.9f, 0.2f, 0.1f);
                visRenderer.sharedMaterial = mat;
            }
        }

        // Brain
        CeilingMonsterBrain brain = monster.AddComponent<CeilingMonsterBrain>();
        brain.hideFlags = HideFlags.None;

        // Ranged attack (fires bullets at the player)
        CeilingMonsterAttack rangedAttack = monster.AddComponent<CeilingMonsterAttack>();
        rangedAttack.hideFlags = HideFlags.None;

        // Attack trigger
        GameObject atk = new GameObject("Attack_Trigger");
        atk.transform.SetParent(monster.transform);
        atk.transform.localPosition = new Vector3(0f, 1f, 0f);
        SphereCollider atkCol = atk.AddComponent<SphereCollider>();
        atkCol.isTrigger = true;
        atkCol.radius = 1.3f;
        Rigidbody atkRb = atk.AddComponent<Rigidbody>();
        atkRb.isKinematic = true;
        atkRb.useGravity = false;
        atk.AddComponent<MonsterContactAttack>();

        // Position (below ceiling by capsule top offset)
        // Capsule center=(0,1.1,0), height=2 => top offset = 1.1 + 1.0 = 2.1
        Vector3 pos = monster.transform.position;
        pos.y = height - 2.1f;
        pos.z = 3f; // Slightly in front of camera start
        monster.transform.position = pos;

        // Register undo
        Undo.RegisterCreatedObjectUndo(monster, "Create Ceiling Monster");
        Selection.activeGameObject = monster;

        Debug.Log($"Ceiling monster placed at Y={pos.y:F2} near ceiling");
    }
}
