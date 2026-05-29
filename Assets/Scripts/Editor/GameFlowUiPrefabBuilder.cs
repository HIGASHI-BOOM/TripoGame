using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GameFlowUiPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/PF_GameFlowUI.prefab";
    private const string ResourcesFolder = "Assets/Resources";
    private const string UiFolder = "Assets/Resources/UI";
    private const string BuiltSessionKey = "TripoGame.GameFlowUiPrefabBuilder.BuiltThisSession";

    [InitializeOnLoadMethod]
    private static void CreateDefaultPrefabIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(BuiltSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(BuiltSessionKey, true);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            CreateOrReplacePrefab();
        };
    }

    [MenuItem("Tools/Game/Create Game Flow UI Prefab")]
    public static void CreateOrReplacePrefab()
    {
        EnsureFolder(ResourcesFolder, "Resources");
        EnsureFolder(UiFolder, "UI");

        GameObject canvasObject = new GameObject("PF_GameFlowUI", typeof(RectTransform));
        try
        {
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            GameFlowUI gameFlowUi = canvasObject.AddComponent<GameFlowUI>();

            Text hitCounter = CreateText("HitCounter", canvasObject.transform, 26, TextAnchor.UpperLeft, FontStyle.Bold);
            RectTransform hitCounterRect = hitCounter.rectTransform;
            hitCounterRect.anchorMin = new Vector2(0f, 1f);
            hitCounterRect.anchorMax = new Vector2(0f, 1f);
            hitCounterRect.pivot = new Vector2(0f, 1f);
            hitCounterRect.anchoredPosition = new Vector2(28f, -24f);
            hitCounterRect.sizeDelta = new Vector2(320f, 48f);
            hitCounter.color = new Color(1f, 0.95f, 0.82f, 1f);

            GameObject failurePanel = CreateUiObject("FailurePanel", canvasObject.transform);
            RectTransform panelRect = failurePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = failurePanel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.015f, 0.012f, 0.78f);

            GameObject boxObject = CreateUiObject("FailureBox", failurePanel.transform);
            RectTransform boxRect = boxObject.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(560f, 320f);

            Image boxImage = boxObject.AddComponent<Image>();
            boxImage.color = new Color(0.08f, 0.075f, 0.07f, 0.96f);

            Text title = CreateText("FailureTitle", boxObject.transform, 48, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.text = "FAILED";
            title.color = new Color(1f, 0.28f, 0.22f, 1f);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -34f);
            titleRect.sizeDelta = new Vector2(-72f, 72f);

            Text body = CreateText("FailureBody", boxObject.transform, 25, TextAnchor.MiddleCenter, FontStyle.Normal);
            body.text = "The player was hit 5 / 5 times.";
            body.color = new Color(0.95f, 0.92f, 0.86f, 1f);
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0.5f);
            bodyRect.anchorMax = new Vector2(1f, 0.5f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = new Vector2(0f, 12f);
            bodyRect.sizeDelta = new Vector2(-80f, 72f);

            Button retryButton = CreateButton("RetryButton", boxObject.transform, "Try Again");
            RectTransform buttonRect = retryButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 34f);
            buttonRect.sizeDelta = new Vector2(220f, 62f);

            Text retryLabel = retryButton.transform.Find("Label").GetComponent<Text>();
            failurePanel.SetActive(false);

            SerializedObject serializedUi = new SerializedObject(gameFlowUi);
            serializedUi.FindProperty("canvas").objectReferenceValue = canvas;
            serializedUi.FindProperty("hitCounterLabel").objectReferenceValue = hitCounter;
            serializedUi.FindProperty("failurePanel").objectReferenceValue = failurePanel;
            serializedUi.FindProperty("failureTitleLabel").objectReferenceValue = title;
            serializedUi.FindProperty("failureBodyLabel").objectReferenceValue = body;
            serializedUi.FindProperty("retryButton").objectReferenceValue = retryButton;
            serializedUi.FindProperty("retryButtonLabel").objectReferenceValue = retryLabel;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created Game Flow UI prefab at " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    private static void EnsureFolder(string path, string folderName)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = path.Substring(0, path.LastIndexOf('/'));
        if (!AssetDatabase.IsValidFolder(parent))
        {
            string parentName = parent.Substring(parent.LastIndexOf('/') + 1);
            EnsureFolder(parent, parentName);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        GameObject buttonObject = CreateUiObject(name, parent);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.9f, 0.24f, 0.18f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.9f, 0.24f, 0.18f, 1f);
        colors.highlightedColor = new Color(1f, 0.36f, 0.28f, 1f);
        colors.pressedColor = new Color(0.7f, 0.12f, 0.1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text labelText = CreateText("Label", buttonObject.transform, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
        labelText.text = label;
        labelText.color = Color.white;
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, FontStyle style)
    {
        GameObject textObject = CreateUiObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                    Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;

        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }
}
