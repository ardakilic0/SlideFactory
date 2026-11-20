using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// BOARD OVERLAY
// =============================================================
//
// UI layer drawn over the board (under popups and result panels):
// - a gloved hand that demonstrates a swipe or a tap (hints and
//   tutorials), with a fading trail / tap ripple,
// - pulsing cell highlights (mechanic cards),
// - a spotlight that darkens everything except chosen cells, with
//   a caption bubble (level 1 introduction),
// - the "rewind" flash played by Undo.
// =============================================================

public class BoardOverlay : MonoBehaviour
{
    public static BoardOverlay Instance { get; private set; }

    private const float CellWorldSize = 1.1f;

    private Canvas canvas;
    private RectTransform root;

    // Hand pointer.
    private RectTransform hand;
    private Image handImage;
    private readonly List<Image> trail = new List<Image>();
    private Image ripple;
    private RectTransform arrow;
    private Transform pointerTarget;
    private RectTransform pointerUITarget;
    private Vector2Int pointerDirection;
    private bool pointerIsTap;
    private float pointerTime;

    // Highlights.
    private readonly List<RectTransform> highlights = new List<RectTransform>();
    private readonly List<Vector3> highlightWorld = new List<Vector3>();
    private float highlightTime;

    // Spotlight.
    private RawImage spotlight;
    private Texture2D spotlightTexture;
    private readonly List<Vector3> focusWorld = new List<Vector3>();
    private RectTransform caption;
    private TMP_Text captionText;
    private Button captionButton;
    private System.Action captionAction;
    private Vector2 lastFocusKey = new Vector2(-9999, -9999);
    private float spotlightAlpha;
    private float spotlightTarget;

    public bool IsPointerVisible => hand != null && hand.gameObject.activeSelf;

    public static BoardOverlay Create(Canvas canvas, Transform afterSibling)
    {
        RectTransform rect = UIKit.Stretch("BoardOverlay", canvas.transform);

        if (afterSibling != null)
            rect.SetSiblingIndex(afterSibling.GetSiblingIndex() + 1);

        BoardOverlay overlay = rect.gameObject.AddComponent<BoardOverlay>();
        overlay.canvas = canvas;
        overlay.root = rect;
        overlay.Build();
        return overlay;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (spotlightTexture != null)
            Destroy(spotlightTexture);
    }

    private void Build()
    {
        // Spotlight (under everything else in the overlay).
        GameObject spot = new GameObject("Spotlight", typeof(RectTransform));
        spot.transform.SetParent(root, false);
        RectTransform spotRect = (RectTransform)spot.transform;
        spotRect.anchorMin = Vector2.zero;
        spotRect.anchorMax = Vector2.one;
        spotRect.offsetMin = spotRect.offsetMax = Vector2.zero;
        spotlight = spot.AddComponent<RawImage>();
        spotlight.raycastTarget = false;
        spotlight.color = new Color(1f, 1f, 1f, 0f);
        spot.SetActive(false);

        // Caption bubble.
        Image bubble = UIKit.Image("Caption", root, UIKit.Panel,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 160f));
        caption = bubble.rectTransform;
        captionText = UIKit.Text("Text", caption, "", true, 60f, UIKit.Slate,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(700f, 130f));
        captionButton = UIKit.TextButton("CaptionGotIt", caption, UIKit.PillOrange, "GOT IT",
            Color.white, 44f, new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(280f, 96f),
            () =>
            {
                System.Action a = captionAction;
                captionAction = null;
                a?.Invoke();
            });
        captionButton.gameObject.SetActive(false);
        caption.gameObject.SetActive(false);

        // Swipe trail dots.
        for (int i = 0; i < 6; i++)
        {
            Image dot = UIKit.Image("Trail" + i, root, UIKit.Sprite("ui_soft_dot"),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            dot.color = new Color(1f, 1f, 1f, 0f);
            trail.Add(dot);
        }

        // Direction arrow (shown faintly in front of the box).
        Image arrowImage = UIKit.Image("HintArrow", root, UIKit.Sprite("ui_arrow"),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
        arrow = arrowImage.rectTransform;

        ripple = UIKit.Image("TapRipple", root, UIKit.Sprite("ui_ring"),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));

        handImage = UIKit.Image("Hand", root, UIKit.Sprite("ui_hand"),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 200f));
        hand = handImage.rectTransform;
        // Fingertip is the hot spot.
        hand.pivot = new Vector2(0.46f, 0.93f);

        HidePointer();
    }

    // =========================================================
    // POINTER
    // =========================================================

    public void ShowSwipe(Box box, Vector2Int direction)
    {
        if (box == null)
            return;

        pointerTarget = box.transform;
        pointerUITarget = null;
        pointerDirection = direction;
        pointerIsTap = false;
        pointerTime = 0f;

        hand.gameObject.SetActive(true);
        arrow.gameObject.SetActive(true);
        ripple.gameObject.SetActive(false);
        arrow.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        LateUpdate();
    }

    public void ShowTap(Transform worldTarget)
    {
        pointerTarget = worldTarget;
        pointerUITarget = null;
        pointerIsTap = true;
        pointerTime = 0f;

        hand.gameObject.SetActive(worldTarget != null);
        arrow.gameObject.SetActive(false);
        ripple.gameObject.SetActive(worldTarget != null);
        LateUpdate();
    }

    public void ShowTapUI(RectTransform uiTarget)
    {
        pointerTarget = null;
        pointerUITarget = uiTarget;
        pointerIsTap = true;
        pointerTime = 0f;

        hand.gameObject.SetActive(uiTarget != null);
        arrow.gameObject.SetActive(false);
        ripple.gameObject.SetActive(uiTarget != null);
        LateUpdate();
    }

    public void HidePointer()
    {
        pointerTarget = null;
        pointerUITarget = null;

        if (hand != null) hand.gameObject.SetActive(false);
        if (arrow != null) arrow.gameObject.SetActive(false);
        if (ripple != null) ripple.gameObject.SetActive(false);

        foreach (Image dot in trail)
            dot.color = new Color(1f, 1f, 1f, 0f);
    }

    // =========================================================
    // HIGHLIGHTS
    // =========================================================

    public void Highlight(IEnumerable<Vector3> worldPositions)
    {
        ClearHighlights();

        foreach (Vector3 world in worldPositions)
        {
            Image ring = UIKit.Image("Highlight", root, UIKit.Sprite("ui_cell_highlight"),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            ring.color = UIKit.Highlight;
            ring.rectTransform.SetSiblingIndex(1);
            highlights.Add(ring.rectTransform);
            highlightWorld.Add(world);
        }

        highlightTime = 0f;
        LateUpdate();
    }

    public void ClearHighlights()
    {
        foreach (RectTransform h in highlights)
        {
            if (h != null)
                Destroy(h.gameObject);
        }

        highlights.Clear();
        highlightWorld.Clear();
    }

    // =========================================================
    // SPOTLIGHT + CAPTION
    // =========================================================

    // Darkens the screen except around the given cells.
    public void Focus(IEnumerable<Vector3> worldCells, string text, System.Action onGotIt = null)
    {
        focusWorld.Clear();
        focusWorld.AddRange(worldCells);
        lastFocusKey = new Vector2(-9999, -9999);

        spotlight.gameObject.SetActive(true);
        spotlightTarget = 1f;

        SetCaption(text, onGotIt);
        LateUpdate();
    }

    // Caption bubble; with onGotIt it gets a small GOT IT button and
    // stays until that button is pressed.
    public void SetCaption(string text, System.Action onGotIt = null)
    {
        bool show = !string.IsNullOrEmpty(text);
        caption.gameObject.SetActive(show);
        captionAction = onGotIt;
        captionButton.gameObject.SetActive(show && onGotIt != null);

        if (show)
        {
            bool withButton = onGotIt != null;
            captionText.text = text;
            float w = Mathf.Clamp(captionText.GetPreferredValues(text, 1400f, 130f).x + 140f, 420f, 920f);
            float h = withButton ? 310f : 170f;
            caption.sizeDelta = new Vector2(w, h);
            captionText.rectTransform.sizeDelta = new Vector2(w - 70f, 120f);
            captionText.rectTransform.anchoredPosition = new Vector2(0f, withButton ? 66f : 4f);
            UIKit.Run(UIKit.Pop(caption, 0.22f, 1.08f));
        }
    }

    public bool CaptionVisible => caption != null && caption.gameObject.activeSelf;

    public void ClearFocus()
    {
        focusWorld.Clear();
        spotlightTarget = 0f;
        caption.gameObject.SetActive(false);
    }

    private void RebuildSpotlight(float cell)
    {
        Rect r = root.rect;

        const int Down = 6; // texture pixel = 6 canvas units
        int w = Mathf.Max(8, Mathf.CeilToInt(r.width / Down));
        int h = Mathf.Max(8, Mathf.CeilToInt(r.height / Down));

        if (spotlightTexture == null || spotlightTexture.width != w || spotlightTexture.height != h)
        {
            if (spotlightTexture != null)
                Destroy(spotlightTexture);

            spotlightTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            spotlightTexture.wrapMode = TextureWrapMode.Clamp;
            spotlight.texture = spotlightTexture;
        }

        List<Vector2> centers = new List<Vector2>();

        foreach (Vector3 world in focusWorld)
        {
            if (UIKit.WorldToLocal(canvas, root, world, out Vector2 local))
                centers.Add(new Vector2((local.x - r.xMin) / Down, (local.y - r.yMin) / Down));
        }

        float half = cell * 0.62f / Down;
        float soft = cell * 0.18f / Down;
        Color32[] pixels = new Color32[w * h];
        byte dim = (byte)(255 * 0.62f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float a = 1f;

                foreach (Vector2 c in centers)
                {
                    // rounded-square hole
                    float dx = Mathf.Max(0f, Mathf.Abs(x - c.x) - half * 0.55f);
                    float dy = Mathf.Max(0f, Mathf.Abs(y - c.y) - half * 0.55f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - half * 0.45f;
                    a = Mathf.Min(a, Mathf.Clamp01(d / soft));
                }

                pixels[y * w + x] = new Color32(18, 28, 38, (byte)(dim * a));
            }
        }

        spotlightTexture.SetPixels32(pixels);
        spotlightTexture.Apply(false);

        // caption above the highest focused cell (or below if no room)
        if (centers.Count > 0 && caption.gameObject.activeSelf)
        {
            float top = float.MinValue, bottom = float.MaxValue, cx = 0f;

            foreach (Vector2 c in centers)
            {
                top = Mathf.Max(top, c.y);
                bottom = Mathf.Min(bottom, c.y);
                cx += c.x;
            }

            cx /= centers.Count;
            float x = cx * Down + r.xMin;
            float halfH = caption.sizeDelta.y * 0.5f;
            float above = top * Down + r.yMin + cell * 0.6f + halfH + 30f;
            float below = bottom * Down + r.yMin - cell * 0.6f - halfH - 30f;
            float y = above + halfH < r.yMax - 330f ? above : below;
            float halfW = caption.sizeDelta.x * 0.5f;
            x = Mathf.Clamp(x, r.xMin + halfW + 30f, r.xMax - halfW - 30f);
            caption.anchoredPosition = new Vector2(x, y);
        }
    }

    // =========================================================
    // REWIND (Undo)
    // =========================================================

    public void PlayRewind()
    {
        UIKit.Run(RewindRoutine());
    }

    private IEnumerator RewindRoutine()
    {
        Image flash = UIKit.Image("RewindFlash", root, UIKit.Sprite("ui_white"),
            Vector2.zero, Vector2.zero, Vector2.zero);
        RectTransform fr = flash.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        Color tint = new Color(0.45f, 0.85f, 0.95f, 0.30f);

        Image icon = UIKit.Image("RewindIcon", root, UIKit.Sprite("ui_icon_rewind"),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(230f, 230f));
        RectTransform ir = icon.rectTransform;

        float t = 0f;
        const float D = 0.5f;

        while (t < D && flash != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / D);

            flash.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - k) * (k < 0.1f ? k / 0.1f : 1f));

            float pop = k < 0.25f ? Mathf.Lerp(0.6f, 1.12f, k / 0.25f) : Mathf.Lerp(1.12f, 1f, (k - 0.25f) / 0.75f);
            ir.localScale = Vector3.one * pop;
            ir.anchoredPosition = new Vector2(-40f * k, 120f);
            icon.color = new Color(1f, 1f, 1f, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
            yield return null;
        }

        if (flash != null) Destroy(flash.gameObject);
        if (icon != null) Destroy(icon.gameObject);
    }

    public void ClearAll()
    {
        HidePointer();
        ClearHighlights();
        ClearFocus();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private float CellSizeOnCanvas()
    {
        if (!UIKit.WorldToLocal(canvas, root, Vector3.zero, out Vector2 a) ||
            !UIKit.WorldToLocal(canvas, root, Vector3.right * CellWorldSize, out Vector2 b))
        {
            return 150f;
        }

        return Mathf.Abs(b.x - a.x);
    }

    private Vector2 CenterOf(Transform world, RectTransform ui, out bool ok)
    {
        ok = true;

        if (ui != null)
        {
            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, ui.TransformPoint(ui.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCam, out Vector2 local);
            return local;
        }

        ok = UIKit.WorldToLocal(canvas, root, world.position, out Vector2 l);
        return l;
    }

    private void LateUpdate()
    {
        if (root == null)
            return;

        float cell = CellSizeOnCanvas();

        // ----- spotlight -----
        spotlightAlpha = Mathf.MoveTowards(spotlightAlpha, spotlightTarget, Time.unscaledDeltaTime * 4f);

        if (spotlight.gameObject.activeSelf)
        {
            Vector2 key = new Vector2(cell, focusWorld.Count);

            if (focusWorld.Count > 0 && key != lastFocusKey)
            {
                RebuildSpotlight(cell);
                lastFocusKey = key;
            }

            spotlight.color = new Color(1f, 1f, 1f, spotlightAlpha);

            if (spotlightAlpha <= 0f && spotlightTarget <= 0f)
                spotlight.gameObject.SetActive(false);
        }

        // ----- highlights -----
        highlightTime += Time.unscaledDeltaTime;
        float pulse = 1f + Mathf.Sin(highlightTime * 6f) * 0.06f;

        for (int i = 0; i < highlights.Count; i++)
        {
            if (highlights[i] == null)
                continue;

            if (UIKit.WorldToLocal(canvas, root, highlightWorld[i], out Vector2 local))
            {
                highlights[i].anchoredPosition = local;
                highlights[i].sizeDelta = Vector2.one * cell * 1.08f;
                highlights[i].localScale = Vector3.one * pulse;
            }
        }

        // ----- hand -----
        if (hand == null || !hand.gameObject.activeSelf)
            return;

        if (pointerTarget == null && pointerUITarget == null)
        {
            HidePointer();
            return;
        }

        Vector2 center = CenterOf(pointerTarget, pointerUITarget, out bool ok);

        if (!ok)
            return;

        pointerTime += Time.unscaledDeltaTime;
        float handSize = Mathf.Clamp(cell * 1.05f, 150f, 230f);
        hand.sizeDelta = Vector2.one * handSize;

        if (pointerIsTap)
        {
            // press - release loop with a ripple on each press
            float cycle = 1.2f;
            float k = Mathf.Repeat(pointerTime, cycle) / cycle;
            float press = k < 0.18f ? Mathf.SmoothStep(0f, 1f, k / 0.18f)
                        : k < 0.36f ? Mathf.SmoothStep(1f, 0f, (k - 0.18f) / 0.18f)
                        : 0f;

            hand.anchoredPosition = center + new Vector2(0f, -cell * 0.05f - press * 10f);
            hand.localScale = Vector3.one * (1f - press * 0.1f);
            handImage.color = Color.white;

            float rk = Mathf.Clamp01((k - 0.15f) / 0.6f);
            ripple.rectTransform.anchoredPosition = center;
            ripple.rectTransform.sizeDelta = Vector2.one * cell * Mathf.Lerp(0.3f, 1.2f, rk);
            ripple.color = new Color(1f, 1f, 1f, k < 0.15f ? 0f : 0.9f * (1f - rk));
            return;
        }

        // Swipe: hand presses on the box, drags one cell, lifts, fades.
        float swipeCycle = 1.5f;
        float t = Mathf.Repeat(pointerTime, swipeCycle) / swipeCycle;
        Vector2 dir = new Vector2(pointerDirection.x, pointerDirection.y);

        float appear = Mathf.Clamp01(t / 0.12f);
        float pressT = Mathf.Clamp01((t - 0.12f) / 0.1f);
        float travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.22f) / 0.42f));
        float fadeOut = Mathf.Clamp01((t - 0.72f) / 0.18f);

        Vector2 pos = center + dir * (travel * cell * 1.05f);
        hand.anchoredPosition = pos + new Vector2(cell * 0.05f, -cell * 0.08f);
        hand.localScale = Vector3.one * Mathf.Lerp(1.08f, 0.94f, pressT) * (1f + fadeOut * 0.06f);
        handImage.color = new Color(1f, 1f, 1f, appear * (1f - fadeOut));

        for (int i = 0; i < trail.Count; i++)
        {
            float lag = Mathf.Clamp01(travel - i * 0.09f);
            trail[i].rectTransform.anchoredPosition = center + dir * (lag * cell * 1.05f);
            trail[i].rectTransform.sizeDelta = Vector2.one * cell * (0.42f - i * 0.04f);
            float a = travel > 0.01f ? (0.55f - i * 0.08f) * (1f - fadeOut) : 0f;
            trail[i].color = new Color(1f, 1f, 1f, Mathf.Max(0f, a));
        }

        arrow.sizeDelta = Vector2.one * Mathf.Max(80f, cell * 0.6f);
        arrow.anchoredPosition = center + dir * (cell * 1.05f + Mathf.Sin(pointerTime * 6f) * cell * 0.04f);
        arrow.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
    }
}
