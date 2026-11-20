using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// POPUP
// =============================================================
//
// One modal card at a time (tutorial cards, power-up intros,
// shop offers, out-of-lives). Board input is blocked while a
// popup is open.
// =============================================================

public enum PopupButtonStyle
{
    Primary,    // orange
    Secondary,  // teal
    Quiet       // light
}

public sealed class PopupButton
{
    public string label;
    public PopupButtonStyle style;
    public Action onClick;

    // Optional coin price shown on the button.
    public int price = -1;

    public bool interactable = true;

    public PopupButton(string label, PopupButtonStyle style, Action onClick)
    {
        this.label = label;
        this.style = style;
        this.onClick = onClick;
    }
}

public sealed class PopupSpec
{
    public string title;
    public string body;
    public Sprite icon;
    public Color iconColor = Color.white;
    public bool iconIsPowerUpButton;
    public float iconSize = 130f;
    public bool iconGlow;

    // Bottom = small card above the bottom edge so the board stays
    // visible (tutorial cards); otherwise centered.
    public bool bottom;
    public bool dim = true;

    public List<PopupButton> buttons = new List<PopupButton>();

    // Extra content builder (e.g. coin balance line).
    public Action<RectTransform> extra;
    public float extraHeight = 80f;

    public Action onClosed;
}

public class Popup : MonoBehaviour
{
    private static Popup current;

    private PopupSpec spec;
    private CanvasGroup group;
    private RectTransform card;
    private bool closing;

    public static bool IsOpen => current != null;

    public static Popup Show(PopupSpec spec)
    {
        CloseImmediate();

        Canvas canvas = UIKit.FindMainCanvas();

        if (canvas == null)
            return null;

        RectTransform root = UIKit.Stretch("Popup", canvas.transform);
        root.SetAsLastSibling();

        Popup popup = root.gameObject.AddComponent<Popup>();
        popup.Build(spec);
        current = popup;
        InputManager.BlockBoardInput = true;
        return popup;
    }

    public static void CloseImmediate()
    {
        if (current == null)
            return;

        Popup popup = current;
        current = null;
        InputManager.BlockBoardInput = false;
        Destroy(popup.gameObject);
    }

    public void Close()
    {
        if (closing)
            return;

        closing = true;
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        yield return UIKit.Fade(group, group.alpha, 0f, 0.12f);

        if (current == this)
        {
            current = null;
            InputManager.BlockBoardInput = false;
        }

        Action closed = spec.onClosed;
        Destroy(gameObject);
        closed?.Invoke();
    }

    private void Build(PopupSpec popupSpec)
    {
        spec = popupSpec;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // Dim + click blocker.
        Image dim = UIKit.Image("Dim", transform, UIKit.Sprite("ui_white"),
            Vector2.zero, Vector2.zero, Vector2.zero, true);
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
        dim.color = spec.dim ? UIKit.Dim : new Color(0, 0, 0, 0.01f);

        // ----- build content, then stack it symmetrically -----
        const float Width = 880f;
        const float Pad = 72f;          // same visual margin top and bottom
        const float SectionGap = 26f;
        const float ButtonHeight = 125f;
        const float ButtonGap = 16f;
        // the pill art has a transparent shadow lip at the bottom
        const float PillLip = ButtonHeight * (1f - 131f / 159f);

        Vector2 anchor = spec.bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);

        Image cardImage = UIKit.Image("Card", transform, UIKit.Panel,
            anchor, Vector2.zero, new Vector2(Width, 400f), true);
        card = cardImage.rectTransform;

        List<(RectTransform rect, float height)> stack = new List<(RectTransform, float)>();

        if (!string.IsNullOrEmpty(spec.title))
        {
            TMP_Text title = UIKit.Text("Title", card, spec.title, true, 76f, UIKit.Slate,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width - 120f, 80f));
            title.textWrappingMode = TextWrappingModes.NoWrap;
            stack.Add((title.rectTransform, 80f));
        }

        if (spec.icon != null)
        {
            float slot = Mathf.Max(150f, spec.iconSize + 20f);
            RectTransform iconRoot = UIKit.Rect("IconRoot", card,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(slot, slot));

            if (spec.iconGlow)
            {
                Image glow = UIKit.Image("Glow", iconRoot, UIKit.Sprite("ui_soft_dot"),
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * spec.iconSize * 1.9f);
                glow.color = new Color(1f, 0.76f, 0.25f, 0.55f);
                glow.raycastTarget = false;
            }

            if (spec.iconIsPowerUpButton)
            {
                UIKit.Image("IconBG", iconRoot, UIKit.Sprite("ui_round_button"),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(142f, 150f));
            }

            Image icon = UIKit.Image("Icon", iconRoot, spec.icon,
                new Vector2(0.5f, 0.5f), new Vector2(0f, spec.iconIsPowerUpButton ? 8f : 0f),
                spec.iconIsPowerUpButton ? new Vector2(84f, 84f) : Vector2.one * spec.iconSize);
            icon.color = spec.iconColor;
            icon.preserveAspect = true;
            stack.Add((iconRoot, slot));
        }

        if (!string.IsNullOrEmpty(spec.body))
        {
            TMP_Text body = UIKit.Text("Body", card, spec.body, false, 48f, UIKit.SlateSoft,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width - 150f, 100f));
            float h = body.GetPreferredValues(spec.body, Width - 150f, 0f).y;
            body.rectTransform.sizeDelta = new Vector2(Width - 150f, h);
            stack.Add((body.rectTransform, h));
        }

        if (spec.extra != null)
        {
            RectTransform extraRoot = UIKit.Rect("Extra", card,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width - 80f, spec.extraHeight));
            spec.extra(extraRoot);
            stack.Add((extraRoot, spec.extraHeight));
        }

        List<RectTransform> buttons = new List<RectTransform>();

        foreach (PopupButton b in spec.buttons)
            buttons.Add(AddButton(b, ButtonHeight));

        float contentHeight = 0f;

        foreach (var item in stack)
            contentHeight += item.height + SectionGap;

        float buttonsHeight = buttons.Count > 0
            ? buttons.Count * ButtonHeight + (buttons.Count - 1) * ButtonGap - PillLip
            : -SectionGap;

        float height = Pad + contentHeight + buttonsHeight + Pad;
        card.sizeDelta = new Vector2(Width, height);
        card.anchoredPosition = spec.bottom
            ? new Vector2(0f, 60f + height * 0.5f)
            : new Vector2(0f, 40f);

        float y = height * 0.5f - Pad;

        foreach (var item in stack)
        {
            item.rect.anchoredPosition = new Vector2(0f, y - item.height * 0.5f);
            y -= item.height + SectionGap;
        }

        foreach (RectTransform button in buttons)
        {
            button.anchoredPosition = new Vector2(0f, y - ButtonHeight * 0.5f);
            y -= ButtonHeight + ButtonGap;
        }

        StartCoroutine(UIKit.Fade(group, 0f, 1f, 0.15f));
        StartCoroutine(UIKit.Pop(card, 0.25f, 1.06f));
    }

    private RectTransform AddButton(PopupButton b, float height)
    {
        Sprite sprite =
            b.style == PopupButtonStyle.Primary ? UIKit.PillOrange :
            b.style == PopupButtonStyle.Secondary ? UIKit.PillTeal :
            UIKit.PillLight;

        Color labelColor = b.style == PopupButtonStyle.Quiet ? UIKit.Slate : Color.white;
        Vector2 size = new Vector2(600f, height);

        Button button = UIKit.TextButton(
            "Button_" + b.label, card, sprite, b.label, labelColor, 58f,
            new Vector2(0.5f, 0.5f), Vector2.zero, size,
            () =>
            {
                if (closing)
                    return;

                Close();
                b.onClick?.Invoke();
            });

        if (b.price >= 0)
        {
            // [LABEL]  [coin][price] as one centred group
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            float faceY = height * UIKit.PillLabelOffset;
            float labelW = label.GetPreferredValues(b.label).x;

            TMP_Text price = UIKit.Text("Price", button.transform, b.price.ToString(), true, 46f,
                labelColor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 60f));
            float priceW = price.GetPreferredValues(b.price.ToString()).x;

            const float Coin = 54f, Gap = 10f, GroupGap = 34f;
            float total = labelW + GroupGap + Coin + Gap + priceW;
            float x = -total * 0.5f;

            label.rectTransform.sizeDelta = new Vector2(labelW + 4f, height);
            label.rectTransform.anchoredPosition = new Vector2(x + labelW * 0.5f, faceY);
            x += labelW + GroupGap;

            UIKit.Image("Coin", button.transform, UIKit.Sprite("ui_coin"),
                new Vector2(0.5f, 0.5f), new Vector2(x + Coin * 0.5f, faceY), new Vector2(Coin, Coin));
            x += Coin + Gap;

            price.rectTransform.sizeDelta = new Vector2(priceW + 4f, 60f);
            price.rectTransform.anchoredPosition = new Vector2(x + priceW * 0.5f, faceY);
        }

        if (!b.interactable)
        {
            button.interactable = false;
            CanvasGroup g = button.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0.5f;
        }

        return (RectTransform)button.transform;
    }

    private static float EstimateBodyHeight(string body, float width)
    {
        // ~ characters per line at 40pt in an 700px wide box.
        int charsPerLine = Mathf.Max(10, Mathf.FloorToInt(width / 25f));
        int lines = 0;

        foreach (string paragraph in body.Split('\n'))
            lines += Mathf.Max(1, Mathf.CeilToInt(paragraph.Length / (float)charsPerLine));

        return lines * 62f;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
            InputManager.BlockBoardInput = false;
        }
    }

    // =========================================================
    // TOAST (non-blocking message)
    // =========================================================

    private const float ToastY = 540f;

    public static void Toast(string message, float seconds = 1.8f)
    {
        Canvas canvas = UIKit.FindMainCanvas();

        if (canvas == null)
            return;

        // Sits well above the bottom power-up buttons (Undo / Hint).
        Image bg = UIKit.Image("Toast", canvas.transform, UIKit.PillLight,
            new Vector2(0.5f, 0f), new Vector2(0f, ToastY), new Vector2(760f, 110f));

        TMP_Text text = UIKit.Text("Text", bg.transform, message, false, 38f, UIKit.Slate,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 110f * UIKit.PillLabelOffset), new Vector2(700f, 100f));
        text.textWrappingMode = TextWrappingModes.Normal;
        text.verticalAlignment = VerticalAlignmentOptions.Geometry;

        float preferred = Mathf.Clamp(text.GetPreferredValues(message, 1000f, 100f).x + 90f, 320f, 900f);
        bg.rectTransform.sizeDelta = new Vector2(preferred, 110f);
        text.rectTransform.sizeDelta = new Vector2(preferred - 60f, 100f);

        CanvasGroup group = bg.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        UIKit.Run(ToastRoutine(bg.rectTransform, group, seconds));
    }

    private static IEnumerator ToastRoutine(RectTransform rect, CanvasGroup group, float seconds)
    {
        rect.SetAsLastSibling();
        UIKit.Run(UIKit.Pop(rect, 0.2f, 1.06f));
        yield return UIKit.Fade(group, 0f, 1f, 0.12f);
        yield return new WaitForSecondsRealtime(seconds);
        yield return UIKit.Fade(group, 1f, 0f, 0.25f);

        if (rect != null)
            Destroy(rect.gameObject);
    }
}
