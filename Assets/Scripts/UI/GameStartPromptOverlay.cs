using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CanvasGroup))]
public class GameStartPromptOverlay : MonoBehaviour
{
    [Tooltip("Seconds the start prompt remains fully visible before fading.")]
    [SerializeField] private float visibleDuration = 2.25f;

    [Tooltip("Seconds used to fade the prompt out after the visible duration.")]
    [SerializeField] private float fadeDuration = 0.55f;

    [Tooltip("When enabled, pressing any key, mouse button, or gamepad button hides the prompt immediately.")]
    [SerializeField] private bool hideOnAnyInput = true;

    private CanvasGroup canvasGroup;
    private Coroutine hideRoutine;
    private bool isHiding;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void OnEnable()
    {
        isHiding = false;
        canvasGroup.alpha = 1f;
        hideRoutine = StartCoroutine(AutoHideRoutine());
    }

    private void Update()
    {
        if (!hideOnAnyInput || isHiding)
        {
            return;
        }

        if (WasAnyInputPressed())
        {
            BeginHide();
        }
    }

    private IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSecondsRealtime(visibleDuration);
        BeginHide();
    }

    private void BeginHide()
    {
        if (isHiding)
        {
            return;
        }

        isHiding = true;
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private static bool WasAnyInputPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null &&
            (mouse.leftButton.wasPressedThisFrame ||
             mouse.rightButton.wasPressedThisFrame ||
             mouse.middleButton.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad != null &&
               (gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.buttonWest.wasPressedThisFrame ||
                gamepad.buttonNorth.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame);
    }
}
