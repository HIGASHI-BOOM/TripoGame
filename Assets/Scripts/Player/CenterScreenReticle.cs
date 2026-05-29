using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class CenterScreenReticle : MonoBehaviour
{
    [Tooltip("Length of each crosshair bar in screen pixels.")]
    [SerializeField] private float barLength = 18f;

    [Tooltip("Thickness of each crosshair bar in screen pixels.")]
    [SerializeField] private float barThickness = 3f;

    [Tooltip("Crosshair color.")]
    [SerializeField] private Color reticleColor = new Color(1f, 1f, 1f, 0.85f);

    private RectTransform horizontal;
    private RectTransform vertical;
    private static Texture2D whitePixel;

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        EnsureBar("Crosshair_Horizontal", ref horizontal, new Vector2(barLength, barThickness));
        EnsureBar("Crosshair_Vertical", ref vertical, new Vector2(barThickness, barLength));
    }

    private void LateUpdate()
    {
        PlaceAtScreenCenter(horizontal);
        PlaceAtScreenCenter(vertical);
    }

    private void OnGUI()
    {
        EnsureWhitePixel();

        Color previousColor = GUI.color;
        GUI.color = reticleColor;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        GUI.DrawTexture(new Rect(centerX - barLength * 0.5f, centerY - barThickness * 0.5f, barLength, barThickness), whitePixel);
        GUI.DrawTexture(new Rect(centerX - barThickness * 0.5f, centerY - barLength * 0.5f, barThickness, barLength), whitePixel);

        GUI.color = previousColor;
    }

    private void EnsureBar(string barName, ref RectTransform rectTransform, Vector2 size)
    {
        Transform child = transform.Find(barName);
        GameObject bar = child != null ? child.gameObject : new GameObject(barName, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(transform, false);

        rectTransform = bar.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;

        Image image = bar.GetComponent<Image>();
        image.color = reticleColor;
        image.raycastTarget = false;
    }

    private static void PlaceAtScreenCenter(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private static void EnsureWhitePixel()
    {
        if (whitePixel != null)
        {
            return;
        }

        whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whitePixel.SetPixel(0, 0, Color.white);
        whitePixel.Apply();
    }
}
