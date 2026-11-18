using TMPro;
using UnityEngine;

public class MatchMarker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer badgeBaseRenderer;
    [SerializeField] private SpriteRenderer badgeGlossRenderer;
    [SerializeField] private TMP_Text markerText;

    [Header("Rendering")]
    [SerializeField] private int badgeBaseSortingOrder = 11;
    [SerializeField] private int badgeGlossSortingOrder = 12;
    [SerializeField] private int textSortingOrder = 13;

    private int currentID;

    // =========================================================
    // PUBLIC
    // =========================================================

    public int CurrentID => currentID;

    public SpriteRenderer BadgeBaseRenderer =>
        badgeBaseRenderer;

    public SpriteRenderer BadgeGlossRenderer =>
        badgeGlossRenderer;

    public TMP_Text MarkerText =>
        markerText;

    public Sprite BadgeBaseSprite
    {
        get
        {
            return badgeBaseRenderer != null
                ? badgeBaseRenderer.sprite
                : null;
        }
    }

    public Sprite BadgeGlossSprite
    {
        get
        {
            return badgeGlossRenderer != null
                ? badgeGlossRenderer.sprite
                : null;
        }
    }

    public Color BadgeColor =>
        GetColorForID(currentID);

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        ApplySortingOrders();

        if (markerText != null)
        {
            markerText.color = Color.white;
        }

        if (badgeGlossRenderer != null)
        {
            badgeGlossRenderer.color = Color.white;
        }
    }

    // =========================================================
    // SET ID
    // =========================================================

    public void SetID(int id)
    {
        currentID = id;

        if (markerText != null)
        {
            markerText.text =
                GetIDLabel(id);

            markerText.color =
                Color.white;
        }

        if (badgeBaseRenderer != null)
        {
            badgeBaseRenderer.color =
                GetColorForID(id);
        }

        if (badgeGlossRenderer != null)
        {
            badgeGlossRenderer.color =
                Color.white;
        }

        ApplySortingOrders();
    }

    // =========================================================
    // VISIBILITY
    // =========================================================

    public void SetVisible(bool visible)
    {
        if (badgeBaseRenderer != null)
        {
            badgeBaseRenderer.gameObject
                .SetActive(visible);
        }

        if (badgeGlossRenderer != null)
        {
            badgeGlossRenderer.gameObject
                .SetActive(visible);
        }

        if (markerText != null)
        {
            markerText.gameObject
                .SetActive(visible);
        }
    }

    // =========================================================
    // SORTING
    // =========================================================

    private void ApplySortingOrders()
    {
        if (badgeBaseRenderer != null)
        {
            badgeBaseRenderer.sortingOrder =
                badgeBaseSortingOrder;
        }

        if (badgeGlossRenderer != null)
        {
            badgeGlossRenderer.sortingOrder =
                badgeGlossSortingOrder;
        }

        if (markerText != null)
        {
            markerText.GetComponent<Renderer>().sortingOrder =
                textSortingOrder;
        }
    }

    // =========================================================
    // LABEL
    // =========================================================

    public static string GetIDLabel(int id)
    {
        if (id >= 0 && id < 26)
        {
            return
                ((char)('A' + id))
                .ToString();
        }

        return id.ToString();
    }

    // =========================================================
    // COLORS
    // =========================================================

    public static Color GetColorForID(int id)
    {
        switch (id % 6)
        {
            // A - Red
            case 0:
                return HexToColor("#FF4F5E");

            // B - Blue
            case 1:
                return HexToColor("#3D82FF");

            // C - Green
            case 2:
                return HexToColor("#44CC68");

            // D - Orange
            case 3:
                return HexToColor("#FFB52E");

            // E - Purple
            case 4:
                return HexToColor("#A85CFF");

            // F - Cyan
            default:
                return HexToColor("#32C8D8");
        }
    }

    private static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(
                hex,
                out Color color))
        {
            return color;
        }

        return Color.white;
    }
}