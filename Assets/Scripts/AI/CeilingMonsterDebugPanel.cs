using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CeilingMonsterBrain))]
public class CeilingMonsterDebugPanel : MonoBehaviour
{
    [Tooltip("场景开始时是否显示怪物调试面板。")]
    [SerializeField] private bool showPanel = true;

    [Tooltip("显示或隐藏该怪物调试面板的按键。")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F8;

    [Tooltip("面板左上角的屏幕坐标。")]
    [SerializeField] private Vector2 panelPosition = new Vector2(12f, 12f);

    [Tooltip("面板宽度，单位为像素。")]
    [SerializeField] private float panelWidth = 430f;

    [Tooltip("面板中显示的最近怪物日志行数。")]
    [SerializeField] private int visibleLogLines = 10;

    private CeilingMonsterBrain brain;
    private string[] logBuffer;
    private GUIStyle labelStyle;
    private GUIStyle titleStyle;

    private void Awake()
    {
        brain = GetComponent<CeilingMonsterBrain>();
        visibleLogLines = Mathf.Max(1, visibleLogLines);
        logBuffer = new string[visibleLogLines];
    }

    private void Update()
    {
        if (IsTogglePressed())
            showPanel = !showPanel;
    }

    private void OnGUI()
    {
        if (!showPanel || brain == null)
            return;

        EnsureStyles();

        float lineHeight = 19f;
        float height = 350f + visibleLogLines * lineHeight;
        Rect rect = new Rect(panelPosition.x, panelPosition.y, panelWidth, height);

        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label($"吸顶怪调试面板 - {brain.name}", titleStyle);
        DrawLine("开关按键", toggleKey.ToString());
        DrawLine("逻辑状态", TranslateAction(brain.DebugAction));
        DrawLine("当前目标", brain.CurrentTarget != null ? brain.CurrentTarget.name : "无");
        DrawLine("目标距离", $"{brain.DebugHorizontalDistanceToTarget:F2}m");
        DrawLine("当前速度", FormatVector(brain.DebugLinearVelocity));
        DrawLine("移动目标", FormatVector(brain.DebugMoveDestination));
        DrawLine("当前天花板Y", brain.DebugCeilingSurfaceY.ToString("F2"));
        DrawLine("是否跳跃", YesNo(brain.IsJumping));
        DrawLine("跳跃进度", $"{brain.DebugJumpProgress * 100f:F0}% / {brain.DebugJumpDuration:F2}s");
        DrawLine("跳跃目标", FormatVector(brain.DebugJumpTargetPosition));
        DrawLine("跳跃天花板Y", brain.DebugJumpTargetCeilingY.ToString("F2"));
        DrawLine("最大跳距", $"{brain.DebugMaxJumpDistance:F2}m");
        DrawLine("跳跃冷却", $"{brain.DebugJumpCooldownRemaining:F2}s");
        DrawLine("攻击锁移动", brain.IsAttackMoveLocked ? $"{brain.DebugAttackMoveLockRemaining:F2}s" : "否");
        DrawLine("最近决策", brain.DebugLastDecision);
        DrawLine("最近跳跃", brain.DebugLastJumpReason);
        DrawLine("最近天花板命中", brain.DebugLastCeilingHit);
        DrawLine("落点检查", brain.DebugLastLandingValidation);

        GUILayout.Space(6f);
        GUILayout.Label("最近怪物日志", titleStyle);
        int count = brain.CopyDebugLogLines(logBuffer);
        for (int i = 0; i < count; i++)
            GUILayout.Label(logBuffer[i], labelStyle);

        GUILayout.EndArea();
    }

    private void DrawLine(string name, string value)
    {
        GUILayout.Label($"{name}: {value}", labelStyle);
    }

    private void EnsureStyles()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }

    private static string YesNo(bool value)
    {
        return value ? "是" : "否";
    }

    private static string TranslateAction(string action)
    {
        switch (action)
        {
            case "Idle":
                return "待机";
            case "Chase":
                return "追逐";
            case "Jumping":
                return "跳跃";
            case "Attacking":
                return "攻击";
            default:
                return string.IsNullOrEmpty(action) ? "无" : action;
        }
    }

    private bool IsTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.f8Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(toggleKey);
#endif
    }
}
