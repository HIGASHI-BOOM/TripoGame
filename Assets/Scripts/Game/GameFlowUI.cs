using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameFlowUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root canvas used by the game flow UI prefab.")]
    [SerializeField] private Canvas canvas;

    [Tooltip("Text label that displays the current player hit count.")]
    [SerializeField] private Text hitCounterLabel;

    [Tooltip("Root object for the failure overlay.")]
    [SerializeField] private GameObject failurePanel;

    [Tooltip("Title label shown on the failure overlay.")]
    [SerializeField] private Text failureTitleLabel;

    [Tooltip("Body label shown on the failure overlay.")]
    [SerializeField] private Text failureBodyLabel;

    [Tooltip("Button used to restart the current scene after failure.")]
    [SerializeField] private Button retryButton;

    [Tooltip("Text label inside the retry button.")]
    [SerializeField] private Text retryButtonLabel;

    [Header("Text")]
    [Tooltip("Format for the running hit counter. {0} is current hits and {1} is maximum hits.")]
    [SerializeField] private string hitCounterFormat = "Hits {0} / {1}";

    [Tooltip("Format for the failure body text. {0} is current hits and {1} is maximum hits.")]
    [SerializeField] private string failureBodyFormat = "The player was hit {0} / {1} times.";

    public bool IsFailureVisible => failurePanel != null && failurePanel.activeSelf;
    public Button RetryButton => retryButton;

    private void Awake()
    {
        CacheMissingReferences();
        HideFailure();
    }

    public void BindRetry(UnityAction retryAction)
    {
        CacheMissingReferences();
        if (retryButton == null)
        {
            return;
        }

        retryButton.onClick.RemoveAllListeners();
        if (retryAction != null)
        {
            retryButton.onClick.AddListener(retryAction);
        }
    }

    public void SetHitCounterVisible(bool visible)
    {
        CacheMissingReferences();
        if (hitCounterLabel != null)
        {
            hitCounterLabel.gameObject.SetActive(visible);
        }
    }

    public void SetHitCounter(int currentHits, int maxHits)
    {
        CacheMissingReferences();
        if (hitCounterLabel != null)
        {
            hitCounterLabel.text = string.Format(hitCounterFormat, currentHits, maxHits);
        }
    }

    public void SetRetryButtonText(string text)
    {
        CacheMissingReferences();
        if (retryButtonLabel != null)
        {
            retryButtonLabel.text = text;
        }
    }

    public void HideFailure()
    {
        CacheMissingReferences();
        if (failurePanel != null)
        {
            failurePanel.SetActive(false);
        }
    }

    public void ShowFailure(int currentHits, int maxHits, string title, string retryText)
    {
        CacheMissingReferences();
        if (failureTitleLabel != null)
        {
            failureTitleLabel.text = title;
        }

        if (failureBodyLabel != null)
        {
            failureBodyLabel.text = string.Format(failureBodyFormat, currentHits, maxHits);
        }

        SetRetryButtonText(retryText);

        if (failurePanel != null)
        {
            failurePanel.SetActive(true);
        }
    }

    public void SelectRetryButton()
    {
        CacheMissingReferences();
        if (retryButton == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    private void CacheMissingReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }

        if (hitCounterLabel == null)
        {
            Transform hitCounter = transform.Find("HitCounter");
            hitCounterLabel = hitCounter != null ? hitCounter.GetComponent<Text>() : null;
        }

        if (failurePanel == null)
        {
            Transform panel = transform.Find("FailurePanel");
            failurePanel = panel != null ? panel.gameObject : null;
        }

        if (failurePanel == null)
        {
            return;
        }

        if (failureTitleLabel == null)
        {
            Transform title = failurePanel.transform.Find("FailureBox/FailureTitle");
            failureTitleLabel = title != null ? title.GetComponent<Text>() : null;
        }

        if (failureBodyLabel == null)
        {
            Transform body = failurePanel.transform.Find("FailureBox/FailureBody");
            failureBodyLabel = body != null ? body.GetComponent<Text>() : null;
        }

        if (retryButton == null)
        {
            Transform button = failurePanel.transform.Find("FailureBox/RetryButton");
            retryButton = button != null ? button.GetComponent<Button>() : null;
        }

        if (retryButtonLabel == null && retryButton != null)
        {
            Transform label = retryButton.transform.Find("Label");
            retryButtonLabel = label != null ? label.GetComponent<Text>() : null;
        }
    }
}
