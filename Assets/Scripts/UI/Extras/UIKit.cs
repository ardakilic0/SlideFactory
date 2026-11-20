using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// UI KIT
// =============================================================
//
// Small helpers to build the runtime UI of the new systems
// (power-ups, stars, tutorials, popups) in the existing visual
// style: generated sprites from Resources/UI/Generated and the
// game's own fonts (LilitaOne for headings, Fredoka for text).
// =============================================================

public static class UIKit
{
    public static readonly Color Slate = new Color32(64, 80, 92, 255);
    public static readonly Color SlateSoft = new Color32(108, 126, 138, 255);
    public static readonly Color White = Color.white;
    public static readonly Color Orange = new Color32(255, 160, 32, 255);
    public static readonly Color Teal = new Color32(46, 150, 168, 255);
    public static readonly Color Highlight = new Color32(255, 186, 58, 255);
    public static readonly Color Dim = new Color(0.10f, 0.14f, 0.18f, 0.45f);

    private static readonly Dictionary<string, Sprite> sprites =
        new Dictionary<string, Sprite>();

    private static TMP_FontAsset headFont;
    private static TMP_FontAsset headFontWhite;
    private static TMP_FontAsset bodyFont;

    // =========================================================
    // SPRITES
    // =========================================================

    // border = 9-slice border in pixels (left, bottom, right, top).
    public static Sprite Sprite(string name, Vector4 border = default)
    {
        string key = name + border;

        if (sprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Texture2D texture =
            Resources.Load<Texture2D>("UI/Generated/" + name);

        if (texture == null)
        {
            Debug.LogWarning("UIKit: missing sprite " + name);
            return null;
        }

        Sprite sprite = UnityEngine.Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );

        sprite.name = name;
        sprites[key] = sprite;
        return sprite;
    }

    // Card art of the fail panel (shown at half size, like FailCard).
    // Card designed for 9-slicing: corners (rivets, brackets, rounding)
    // never stretch, so any size stays crisp.
    public static Sprite Panel => Sprite("ui_card", new Vector4(64, 64, 64, 64));
    public const float PanelPixelsPerUnit = 1f;

    // Visual centre of the pill face (its lower part is the 3D lip),
    // as a fraction of the button height above the rect centre.
    public const float PillLabelOffset = 0.085f;

    // Pills are the game's own "TRY AGAIN" button (530x159) and
    // recoloured copies of it.
    public static Sprite PillOrange => Sprite("ui_pill_orange", PillBorder);
    public static Sprite PillTeal => Sprite("ui_pill_teal", PillBorder);
    public static Sprite PillLight => Sprite("ui_pill_light", PillBorder);
    public static Sprite PillGrey => Sprite("ui_pill_grey", PillBorder);
    private static readonly Vector4 PillBorder = new Vector4(82, 72, 82, 72);
    private const float PillTextureHeight = 159f;

    // Keeps a pill's round caps in proportion at any height.
    public static void FitPill(Image image, float height)
    {
        if (image == null || height <= 0f)
            return;

        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = PillTextureHeight / height;
    }

    public static bool IsPill(Sprite sprite) =>
        sprite != null && sprite.name.StartsWith("ui_pill");

    // =========================================================
    // FONTS
    // =========================================================

    public static TMP_FontAsset HeadFont
    {
        get
        {
            // Same font asset the "LEVEL COMPLETE!" title uses.
            if (headFont == null)
                headFont = FindFont("LilitaOne-Regular_LevelComplete", "LilitaOne");

            return headFont;
        }
    }

    // White label variant (as on the NEXT LEVEL button).
    public static TMP_FontAsset HeadFontWhite
    {
        get
        {
            if (headFontWhite == null)
                headFontWhite = FindFont("LilitaOne-Regular_white", "LilitaOne");

            return headFontWhite;
        }
    }

    public static TMP_FontAsset BodyFont
    {
        get
        {
            if (bodyFont == null)
                bodyFont = FindFont("Fredoka-SemiBold SDF", "Fredoka");

            return bodyFont;
        }
    }

    private static TMP_FontAsset FindFont(string exact, string family)
    {
        TMP_FontAsset fallback = null;

        foreach (TMP_FontAsset font in
                 Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font == null)
                continue;

            if (font.name == exact)
                return font;

            if (fallback == null && font.name.StartsWith(family))
                fallback = font;
        }

        return fallback != null ? fallback : TMP_Settings.defaultFontAsset;
    }

    // =========================================================
    // BUILDERS
    // =========================================================

    public static RectTransform Rect(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image Image(
        string name,
        Transform parent,
        Sprite sprite,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        bool raycast = false)
    {
        RectTransform rect = Rect(name, parent, anchor, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = raycast;

        if (sprite != null && sprite.border != Vector4.zero)
            image.type = UnityEngine.UI.Image.Type.Sliced;

        if (IsPill(sprite))
            FitPill(image, size.y);
        else if (sprite != null && sprite.name == "ui_card")
            image.pixelsPerUnitMultiplier = PanelPixelsPerUnit;

        return image;
    }

    public static TMP_Text Text(
        string name,
        Transform parent,
        string text,
        bool heading,
        float size,
        Color color,
        Vector2 anchor,
        Vector2 position,
        Vector2 rectSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = Rect(name, parent, anchor, position, rectSize);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = !heading ? BodyFont
                   : color == Color.white ? HeadFontWhite
                   : HeadFont;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;

        // Headings / labels are centred on the real glyph shapes, so
        // they sit exactly in the middle of buttons, chips and badges.
        if (heading)
            label.verticalAlignment = VerticalAlignmentOptions.Geometry;

        return label;
    }

    public static Button Button(
        string name,
        Transform parent,
        Sprite background,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Action onClick)
    {
        Image image = Image(name, parent, background, anchor, position, size, true);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            HapticsManager.UI();
            SafeEvents.Run(name, onClick);
        });

        image.gameObject.AddComponent<UIButtonJuice>();
        return button;
    }

    // Pill button with a centered label.
    public static Button TextButton(
        string name,
        Transform parent,
        Sprite background,
        string label,
        Color labelColor,
        float fontSize,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Action onClick)
    {
        Button button = Button(name, parent, background, anchor, position, size, onClick);

        TMP_Text text = Text(
            "Label",
            button.transform,
            label,
            true,
            fontSize,
            labelColor,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, size.y * PillLabelOffset),
            new Vector2(size.x - 40f, size.y));

        text.textWrappingMode = TextWrappingModes.NoWrap;
        return button;
    }

    // Steel plate chip (ui_chip, 320x112): icon on the left, value in
    // the inset slot. Returns the value label.
    public static TMP_Text Chip(
        string name,
        Transform parent,
        string icon,
        Vector2 anchor,
        Vector2 position,
        float width = 290f)
    {
        float height = width * 112f / 320f;

        Image plate = Image(name, parent, Sprite("ui_chip"),
            anchor, position, new Vector2(width, height));

        Image iconImage = Image("Icon", plate.transform, Sprite(icon),
            new Vector2(0f, 0.5f), new Vector2(width * 0.15f, height * 0.05f),
            Vector2.one * height * 0.92f);
        iconImage.preserveAspect = true;

        TMP_Text value = Text("Value", plate.transform, "0", true, height * 0.46f, Slate,
            new Vector2(0.5f, 0.5f), new Vector2(width * 0.10f, height * 0.05f),
            new Vector2(width * 0.58f, height * 0.6f));
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.enableAutoSizing = true;
        value.fontSizeMin = 18f;
        value.fontSizeMax = height * 0.46f;
        return value;
    }

    // =========================================================
    // CANVAS HELPERS
    // =========================================================

    public static Canvas FindMainCanvas()
    {
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas && canvas.gameObject.name == "Canvas")
                return canvas;
        }

        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (canvas.isRootCanvas)
                return canvas;
        }

        return null;
    }

    public static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);

            if (found != null)
                return found;
        }

        return null;
    }

    // World position -> anchored position inside a RectTransform.
    public static bool WorldToLocal(
        Canvas canvas,
        RectTransform space,
        Vector3 world,
        out Vector2 local)
    {
        local = Vector2.zero;
        Camera camera = Camera.main;

        if (camera == null || canvas == null)
            return false;

        Vector3 screen = camera.WorldToScreenPoint(world);

        Camera uiCamera =
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            space, screen, uiCamera, out local);
    }

    // =========================================================
    // TWEENS
    // =========================================================

    public static Coroutine Run(IEnumerator routine)
    {
        return UIRunner.Instance.StartCoroutine(routine);
    }

    public static IEnumerator Pop(Transform target, float duration, float overshoot = 1.15f)
    {
        if (target == null)
            yield break;

        Vector3 end = Vector3.one;
        float t = 0f;

        while (t < duration && target != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = k < 0.6f
                ? Mathf.Lerp(0f, overshoot, k / 0.6f)
                : Mathf.Lerp(overshoot, 1f, (k - 0.6f) / 0.4f);

            target.localScale = end * s;
            yield return null;
        }

        if (target != null)
            target.localScale = end;
    }

    public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        float t = 0f;

        while (t < duration && group != null)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }

        if (group != null)
            group.alpha = to;
    }

    public static IEnumerator Shake(RectTransform target, float duration = 0.25f, float strength = 14f)
    {
        if (target == null)
            yield break;

        Vector2 origin = target.anchoredPosition;
        float t = 0f;

        while (t < duration && target != null)
        {
            t += Time.unscaledDeltaTime;
            float damp = 1f - Mathf.Clamp01(t / duration);
            target.anchoredPosition =
                origin + new Vector2(Mathf.Sin(t * 70f) * strength * damp, 0f);
            yield return null;
        }

        if (target != null)
            target.anchoredPosition = origin;
    }
}

// Coroutine host that survives UI objects being destroyed.
public class UIRunner : MonoBehaviour
{
    private static UIRunner instance;

    public static UIRunner Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("UIRunner");
                instance = go.AddComponent<UIRunner>();
            }

            return instance;
        }
    }
}
