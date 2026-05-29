using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[DefaultExecutionOrder(-1000)]
public class GameFlowController : MonoBehaviour
{
    private const string DefaultUiResourcePath = "UI/PF_GameFlowUI";

    public static GameFlowController Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    [Header("Rules")]
    [Tooltip("How many monster hits the player can take before the game fails.")]
    [Min(1)]
    [SerializeField] private int maxPlayerHits = 5;

    [Tooltip("Pause gameplay when the fail screen is shown.")]
    [SerializeField] private bool pauseOnFailure = true;

    [Header("UI")]
    [Tooltip("Optional direct UI prefab reference. If empty, the controller loads the prefab from Resources.")]
    [SerializeField] private GameFlowUI uiPrefab;

    [Tooltip("Resources path used when no direct UI prefab reference is assigned.")]
    [SerializeField] private string uiResourcePath = DefaultUiResourcePath;

    [Tooltip("Show a small hit counter while the game is running.")]
    [SerializeField] private bool showHitCounter = true;

    [Tooltip("Fail screen title text.")]
    [SerializeField] private string failureTitle = "FAILED";

    [Tooltip("Retry button label.")]
    [SerializeField] private string retryButtonText = "Try Again";

    private int currentPlayerHits;
    private bool isGameRunning;
    private GameFlowUI runtimeUi;

    public bool IsGameRunning => isGameRunning;
    public int CurrentPlayerHits => currentPlayerHits;
    public int MaxPlayerHits => maxPlayerHits;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("GameFlowController");
        controllerObject.AddComponent<GameFlowController>();
    }

    public static void ReportPlayerHit(ThirdPersonPlayerController player)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.RegisterPlayerHit(player);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureRuntimeUi();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Start()
    {
        StartGame();
    }

    private void Update()
    {
        if (isGameRunning || runtimeUi == null || !runtimeUi.IsFailureVisible)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.rKey.wasPressedThisFrame))
        {
            RetryCurrentScene();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.R))
        {
            RetryCurrentScene();
        }
#endif
    }

    public void StartGame()
    {
        currentPlayerHits = 0;
        isGameRunning = true;
        Time.timeScale = 1f;

        EnsureRuntimeUi();
        if (runtimeUi != null)
        {
            runtimeUi.HideFailure();
            runtimeUi.SetHitCounterVisible(showHitCounter);
            runtimeUi.SetRetryButtonText(retryButtonText);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        UpdateHitCounter();
    }

    public void RegisterPlayerHit(ThirdPersonPlayerController player)
    {
        if (!isGameRunning || player == null)
        {
            return;
        }

        currentPlayerHits = Mathf.Min(currentPlayerHits + 1, maxPlayerHits);
        UpdateHitCounter();

        if (currentPlayerHits >= maxPlayerHits)
        {
            FailGame();
        }
    }

    public void RetryCurrentScene()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.LoadSceneInPlayMode(activeScene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif

        SceneManager.LoadScene(activeScene.name);
    }

    private void FailGame()
    {
        isGameRunning = false;
        EnsureRuntimeUi();

        if (runtimeUi != null)
        {
            runtimeUi.ShowFailure(currentPlayerHits, maxPlayerHits, failureTitle, retryButtonText);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseOnFailure)
        {
            Time.timeScale = 0f;
        }

        if (runtimeUi != null)
        {
            runtimeUi.SelectRetryButton();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartGame();
    }

    private void EnsureRuntimeUi()
    {
        EnsureEventSystem();
        if (runtimeUi != null)
        {
            runtimeUi.BindRetry(RetryCurrentScene);
            return;
        }

        GameFlowUI prefab = ResolveUiPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("GameFlowController could not find the Game Flow UI prefab. Expected Resources path: " + uiResourcePath);
            return;
        }

        runtimeUi = Instantiate(prefab);
        runtimeUi.name = prefab.name;
        DontDestroyOnLoad(runtimeUi.gameObject);
        runtimeUi.BindRetry(RetryCurrentScene);
    }

    private GameFlowUI ResolveUiPrefab()
    {
        if (uiPrefab != null)
        {
            return uiPrefab;
        }

        string path = string.IsNullOrWhiteSpace(uiResourcePath) ? DefaultUiResourcePath : uiResourcePath;
        GameObject prefabObject = Resources.Load<GameObject>(path);
        if (prefabObject == null)
        {
            return null;
        }

        return prefabObject.GetComponent<GameFlowUI>();
    }

    private void UpdateHitCounter()
    {
        if (runtimeUi == null)
        {
            return;
        }

        runtimeUi.SetHitCounterVisible(showHitCounter);
        runtimeUi.SetHitCounter(currentPlayerHits, maxPlayerHits);
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystemObject);
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null)
        {
            standaloneModule.enabled = false;
        }

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        if (inputModule.actionsAsset == null)
        {
            inputModule.AssignDefaultActions();
        }

        inputModule.enabled = true;
#else
        StandaloneInputModule standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneModule == null)
        {
            standaloneModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        standaloneModule.enabled = true;
#endif
    }
}
