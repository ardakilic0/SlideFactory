using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// LEVEL COMPLETE: stars + rewards
// =============================================================
//
// Lives inside CompleteContent in place of the "LEVEL n" row.
// LevelCompleteAnimator calls PlayReveal() at the step where it
// used to show that row: the stars pop in one by one, then the
// coin reward line appears.
// =============================================================

public class CompleteRewardsView : MonoBehaviour
{
    private readonly Image[] stars = new Image[3];
    private RectTransform starsRow;
    private RectTransform rewardRow;
    private TMP_Text rewardText;
    private TMP_Text totalText;
    private CanvasGroup rewardGroup;
    private RectTransform bestBadge;

    private LevelReward reward;

    public bool HasContent => reward != null;

    // CompleteContent starts inactive, so Unity callbacks (Awake /
    // OnEnable) do not run yet: events are bound through the host.
    public static CompleteRewardsView Create(Transform completeContent, ExtrasHost host)
    {
        CompleteRewardsView view = completeContent.gameObject.AddComponent<CompleteRewardsView>();
        view.Build(completeContent);

        LevelManager.LevelCompleted += view.OnLevelCompleted;
        LevelManager.LevelLoaded += view.OnLevelLoaded;

        host.OnCleanup(() =>
        {
            LevelManager.LevelCompleted -= view.OnLevelCompleted;
            LevelManager.LevelLoaded -= view.OnLevelLoaded;
        });

        return view;
    }

    private void Build(Transform content)
    {
        starsRow = UIKit.Rect("StarsRow", content,
            new Vector2(0.5f, 1f), new Vector2(0f, -668f), new Vector2(420f, 140f));

        for (int i = 0; i < 3; i++)
        {
            float size = i == 1 ? 132f : 108f;
            stars[i] = UIKit.Image("Star" + i, starsRow, UIKit.Sprite("ui_star_empty"),
                new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 124f, i == 1 ? 12f : -4f),
                new Vector2(size, size));
        }

        // "NEW BEST!" tag: small tilted teal pill at the top right of
        // the stars, like a sticker.
        Image badge = UIKit.Image("NewBest", starsRow, UIKit.PillTeal,
            new Vector2(0.5f, 0.5f), new Vector2(236f, 56f), new Vector2(230f, 76f));
        bestBadge = badge.rectTransform;
        bestBadge.localEulerAngles = new Vector3(0f, 0f, -12f);
        TMP_Text bestLabel = UIKit.Text("Text", bestBadge, "NEW BEST!", true, 34f, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 76f * UIKit.PillLabelOffset), new Vector2(210f, 60f));
        bestLabel.textWrappingMode = TextWrappingModes.NoWrap;
        bestBadge.gameObject.SetActive(false);

        // Two steel chips centred under the card: coins earned, total stars.
        rewardRow = UIKit.Rect("RewardRow", content,
            new Vector2(0.5f, 0f), new Vector2(0f, -78f), new Vector2(640f, 110f));
        rewardGroup = rewardRow.gameObject.AddComponent<CanvasGroup>();

        rewardText = UIKit.Chip("CoinsChip", rewardRow, "ui_coin",
            new Vector2(0.5f, 0.5f), new Vector2(-160f, 0f));

        totalText = UIKit.Chip("StarsChip", rewardRow, "ui_star_full",
            new Vector2(0.5f, 0.5f), new Vector2(160f, 0f));

        HideAll();
    }

    private void OnLevelLoaded(int levelIndex)
    {
        reward = null;
    }

    private void OnLevelCompleted(int levelIndex, LevelReward levelReward)
    {
        reward = levelReward;
        HideAll();
    }

    private void HideAll()
    {
        foreach (Image star in stars)
        {
            star.sprite = UIKit.Sprite("ui_star_empty");
            star.rectTransform.localScale = Vector3.zero;
        }

        if (bestBadge != null)
            bestBadge.gameObject.SetActive(false);

        if (rewardGroup != null)
            rewardGroup.alpha = 0f;
    }

    // Called by LevelCompleteAnimator.
    public IEnumerator PlayReveal()
    {
        if (reward == null)
            yield break;

        // Empty slots first.
        for (int i = 0; i < 3; i++)
        {
            stars[i].sprite = UIKit.Sprite("ui_star_empty");
            UIKit.Run(UIKit.Pop(stars[i].rectTransform, 0.14f, 1.05f));
        }

        yield return new WaitForSecondsRealtime(0.12f);

        float[] notes = { 1f, 1.12f, 1.26f };

        for (int i = 0; i < reward.stars; i++)
        {
            stars[i].sprite = UIKit.Sprite("ui_star_full");
            HapticsManager.UI();
            AudioManager.Play(GameSfx.StarEarned, notes[i]);
            yield return UIKit.Pop(stars[i].rectTransform, 0.24f, 1.35f);
        }

        if (reward.IsNewBest && reward.previousBestStars > 0)
        {
            bestBadge.gameObject.SetActive(true);
            UIKit.Run(UIKit.Pop(bestBadge, 0.25f, 1.25f));
        }

        rewardText.text = "+" + reward.coinsEarned;
        totalText.text = reward.totalStars.ToString();
        UIKit.Run(UIKit.Fade(rewardGroup, 0f, 1f, 0.2f));
        UIKit.Run(UIKit.Pop(rewardRow, 0.25f, 1.08f));

        if (reward.coinsEarned > 0)
            AudioManager.Play(GameSfx.Coin);

        if (reward.chestItems.Count > 0)
            UIKit.Run(ShowChestReward(reward));
    }

    private static IEnumerator ShowChestReward(LevelReward reward)
    {
        // Let the stars / coins reveal settle first.
        yield return new WaitForSecondsRealtime(0.9f);
        StarChestPopup.Show(reward.chestItems);
    }
}

// =============================================================
// STAR CHEST reward popup
// =============================================================
//
// Every EconomyConfig.StarsPerChest collected stars a chest opens
// and its power-ups are added to the inventory. This card shows
// what was inside: chest art, one tile per power-up type with a
// "+n" badge, and a COLLECT button.
// =============================================================

public static class StarChestPopup
{
    private const float TileW = 150f;
    private const float TileGap = 56f;
    private const float RowHeight = 236f;

    public static void Show(List<PowerUpType> items)
    {
        if (items == null || items.Count == 0)
            return;

        // Group by type, in a fixed order.
        PowerUpType[] order = { PowerUpType.Hint, PowerUpType.Undo, PowerUpType.ExtraMoves };
        List<(PowerUpType type, int count)> groups = new List<(PowerUpType, int)>();

        foreach (PowerUpType type in order)
        {
            int n = 0;
            foreach (PowerUpType item in items)
                if (item == type) n++;

            if (n > 0)
                groups.Add((type, n));
        }

        AudioManager.Play(GameSfx.Popup);

        Popup.Show(new PopupSpec
        {
            title = "STAR CHEST",
            icon = UIKit.Sprite("ui_chest"),
            iconSize = 210f,
            iconGlow = true,
            body = "You collected " + EconomyConfig.StarsPerChest + " more stars!",
            extraHeight = RowHeight,
            extra = root => BuildRow(root, groups),
            buttons =
            {
                new PopupButton("COLLECT", PopupButtonStyle.Primary,
                    () => AudioManager.Play(GameSfx.Coin))
            }
        });
    }

    private static void BuildRow(RectTransform root, List<(PowerUpType type, int count)> groups)
    {
        float total = groups.Count * TileW + (groups.Count - 1) * TileGap;
        float x = -total * 0.5f + TileW * 0.5f;

        for (int i = 0; i < groups.Count; i++)
        {
            RectTransform tile = BuildTile(root, groups[i].type, groups[i].count);
            tile.anchoredPosition = new Vector2(x, 0f);
            x += TileW + TileGap;

            UIKit.Run(PopIn(tile, 0.25f + i * 0.12f));
        }
    }

    private static RectTransform BuildTile(RectTransform root, PowerUpType type, int count)
    {
        RectTransform tile = UIKit.Rect("Tile_" + type, root,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TileW, RowHeight));

        // Same steel round button the HUD uses, so the reward reads
        // as "this button now has more charges".
        const float BtnY = 34f;
        UIKit.Image("Button", tile, UIKit.Sprite("ui_round_button"),
            new Vector2(0.5f, 0.5f), new Vector2(0f, BtnY), new Vector2(142f, 150f));

        if (type == PowerUpType.ExtraMoves)
        {
            TMP_Text moves = UIKit.Text("Moves", tile, "+" + EconomyConfig.ExtraMovesAmount, true, 58f,
                UIKit.Slate, new Vector2(0.5f, 0.5f), new Vector2(0f, BtnY + 8f), new Vector2(120f, 80f));
            moves.verticalAlignment = VerticalAlignmentOptions.Geometry;
        }
        else
        {
            Image icon = UIKit.Image("Icon", tile,
                UIKit.Sprite(type == PowerUpType.Undo ? "ui_icon_undo" : "ui_icon_hint"),
                new Vector2(0.5f, 0.5f), new Vector2(0f, BtnY + 8f), new Vector2(84f, 84f));
            icon.preserveAspect = true;
        }

        // "+n" badge on the top-right corner.
        Image badge = UIKit.Image("Badge", tile, UIKit.Sprite("ui_badge_orange"),
            new Vector2(0.5f, 0.5f), new Vector2(56f, BtnY + 60f), new Vector2(76f, 76f));
        TMP_Text badgeText = UIKit.Text("Count", badge.transform, "+" + count, true, 38f, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
        badgeText.verticalAlignment = VerticalAlignmentOptions.Geometry;

        string name = type == PowerUpType.ExtraMoves ? "MOVES" : PowerUps.DisplayName(type).ToUpperInvariant();
        TMP_Text label = UIKit.Text("Label", tile, name, true, 38f, UIKit.SlateSoft,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -RowHeight * 0.5f + 40f), new Vector2(TileW + 40f, 50f));
        label.textWrappingMode = TextWrappingModes.NoWrap;

        tile.localScale = Vector3.zero;
        return tile;
    }

    private static IEnumerator PopIn(RectTransform tile, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (tile == null)
            yield break;

        AudioManager.Play(GameSfx.StarEarned, 1.3f);
        yield return UIKit.Pop(tile, 0.24f, 1.18f);
    }
}

// =============================================================
// LEVEL FAILED: continue offers
// =============================================================
//
// A button under the fail card:
// - out of moves  -> "+5 MOVES" (uses one, or buys one for coins)
// - no valid moves -> "UNDO" (steps back out of the dead end)
// =============================================================

public class FailExtrasView : MonoBehaviour
{
    private RectTransform card;
    private RectTransform offer;
    private CanvasGroup offerGroup;
    private Coroutine showRoutine;

    // The card's own buttons move down to make room for the offer.
    private RectTransform tryAgain;
    private RectTransform mainMenu;
    private Vector2 tryAgainPos, mainMenuPos;
    private bool layoutShifted;

    // The fail panel starts inactive: events are bound through the host.
    public static FailExtrasView Create(RectTransform failCard, ExtrasHost host)
    {
        FailExtrasView view = failCard.gameObject.AddComponent<FailExtrasView>();
        view.card = failCard;
        view.tryAgain = failCard.Find("TryAgainButton") as RectTransform;
        view.mainMenu = failCard.Find("MainMenuButton") as RectTransform;
        if (view.tryAgain != null)
        {
            view.tryAgainSize = view.tryAgain.sizeDelta;
            view.tryAgainLabel = view.tryAgain.GetComponentInChildren<TMP_Text>(true);
            if (view.tryAgainLabel != null)
                view.tryAgainFontSize = view.tryAgainLabel.fontSize;
        }

        System.Action<int> onLoaded = i => view.Clear();
        System.Action onRestored = view.Clear;

        LevelManager.LevelFailed += view.OnLevelFailed;
        LevelManager.LevelLoaded += onLoaded;
        LevelManager.BoardRestored += onRestored;
        LevelManager.MovesChanged += view.OnMovesChanged;

        host.OnCleanup(() =>
        {
            LevelManager.LevelFailed -= view.OnLevelFailed;
            LevelManager.LevelLoaded -= onLoaded;
            LevelManager.BoardRestored -= onRestored;
            LevelManager.MovesChanged -= view.OnMovesChanged;
        });

        return view;
    }

    private void OnMovesChanged()
    {
        if (LevelManager.Instance != null && !LevelManager.Instance.IsLevelEnded)
            Clear();
    }

    private void Clear()
    {
        if (offer != null)
            Destroy(offer.gameObject);

        offer = null;
        RestoreLayout();
    }

    // Offer and TRY AGAIN become a matched pair of pills: same rect,
    // same label font/size, stacked with a clear gap under the reason
    // line. TRY AGAIN is resized (not scaled): the fail animator owns
    // its localScale and resets it to 1 at the end of its entrance.
    private const float PairSize = 0.68f;      // of TRY AGAIN's art size
    private const float OfferTop = -36f;       // top edge of the offer pill
    private const float PairGap = 20f;         // space between the two pills
    private const float MenuGap = 6f;          // TRY AGAIN -> MAIN MENU

    private const float LabelScale = 0.84f;    // label size on the smaller pills

    private Vector2 tryAgainSize;
    private TMP_Text tryAgainLabel;
    private float tryAgainFontSize;

    private Vector2 PillSize =>
        (tryAgainSize != Vector2.zero ? tryAgainSize : new Vector2(530f, 159f)) * PairSize;

    private float PairFontSize => (tryAgainFontSize > 0f ? tryAgainFontSize : 49f) * LabelScale;

    private float OfferY => OfferTop - PillSize.y * 0.5f;

    private void ShiftLayout()
    {
        if (layoutShifted || tryAgain == null || mainMenu == null)
            return;

        tryAgainPos = tryAgain.anchoredPosition;
        mainMenuPos = mainMenu.anchoredPosition;

        float h = PillSize.y;
        float tryY = OfferY - h - PairGap;

        tryAgain.sizeDelta = PillSize;
        tryAgain.anchoredPosition = new Vector2(tryAgainPos.x, tryY);

        if (tryAgainLabel != null)
            tryAgainLabel.fontSize = PairFontSize;

        float menuH = mainMenu.rect.height;
        mainMenu.anchoredPosition = new Vector2(mainMenuPos.x,
            tryY - h * 0.5f - MenuGap - menuH * 0.5f);
        layoutShifted = true;
    }

    private void RestoreLayout()
    {
        if (!layoutShifted)
            return;

        tryAgain.anchoredPosition = tryAgainPos;
        tryAgain.sizeDelta = tryAgainSize;
        mainMenu.anchoredPosition = mainMenuPos;

        if (tryAgainLabel != null)
            tryAgainLabel.fontSize = tryAgainFontSize;
        layoutShifted = false;
    }

    private void OnLevelFailed(int levelIndex, LevelFailReason reason)
    {
        Clear();

        if (reason == LevelFailReason.OutOfMoves)
            BuildExtraMovesOffer();
        else if (UndoSystem.Count > 0 && PowerUps.IsUnlocked(PowerUpType.Undo, levelIndex))
            BuildUndoOffer();

        if (offer != null)
        {
            ShiftLayout();

            if (showRoutine != null)
                UIRunner.Instance.StopCoroutine(showRoutine);

            showRoutine = UIKit.Run(Reveal());
        }
    }

    private IEnumerator Reveal()
    {
        offerGroup.alpha = 0f;
        offer.localScale = Vector3.zero;

        // After the card's own entrance animation.
        yield return new WaitForSecondsRealtime(0.75f);

        if (offer == null)
            yield break;

        ApplyOfferLayout();
        offerGroup.alpha = 1f;
        // Pop to the pill's own (paired) scale, not to 1.
        float t = 0f;
        const float Duration = 0.25f;

        while (t < Duration && offer != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Duration);
            float m = k < 0.6f ? Mathf.Lerp(0f, 1.1f, k / 0.6f) : Mathf.Lerp(1.1f, 1f, (k - 0.6f) / 0.4f);
            offer.localScale = Vector3.one * offerScale * m;
            yield return null;
        }

        if (offer != null)
            offer.localScale = Vector3.one * offerScale;
    }

    // Offer pill: [icon] LABEL [count badge | coin price], laid out as
    // one group centred on the pill face.
    private Button BuildOfferButton(string label, string iconName, PowerUpType type)
    {
        // Same rect as TRY AGAIN, so after the shared scale both pills
        // and their labels are the same size.
        Vector2 size = PillSize;
        const float scale = 1f;

        TMP_Text tryLabel = tryAgainLabel;
        float fontSize = PairFontSize;

        Button button = UIKit.TextButton(
            "ContinueOffer", card, UIKit.PillTeal, label, Color.white, fontSize,
            new Vector2(0.5f, 0.5f), Vector2.zero, size, null);

        offer = (RectTransform)button.transform;
        offer.anchoredPosition = new Vector2(0f, OfferY);
        offer.localScale = Vector3.one * scale;
        offerScale = scale;
        offerGroup = button.gameObject.AddComponent<CanvasGroup>();

        offerFaceY = size.y * UIKit.PillLabelOffset;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();

        // Copy TRY AGAIN's label look (font, material/outline, spacing).
        if (tryLabel != null)
        {
            text.font = tryLabel.font;
            text.fontSharedMaterial = tryLabel.fontSharedMaterial;
            text.fontStyle = tryLabel.fontStyle;
            text.characterSpacing = tryLabel.characterSpacing;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.color = tryLabel.color;
        }

        // Widths are measured when the panel is visible (TMP cannot
        // measure text on the inactive fail panel), see ApplyOfferLayout.
        List<(RectTransform rect, float width)> parts = offerParts;
        parts.Clear();

        float iconSize = size.y * 0.46f;
        float badgeSize = size.y * 0.50f;

        if (!string.IsNullOrEmpty(iconName))
        {
            Image icon = UIKit.Image("Icon", button.transform, UIKit.Sprite(iconName),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
            icon.color = Color.white;
            icon.preserveAspect = true;
            parts.Add((icon.rectTransform, iconSize));
        }

        parts.Add((text.rectTransform, -1f));   // measured later

        int count = PowerUps.Count(type);

        if (count > 0)
        {
            Image badge = UIKit.Image("Badge", button.transform, UIKit.Sprite("ui_badge_orange"),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(badgeSize, badgeSize));
            TMP_Text c = UIKit.Text("Count", badge.transform, count.ToString(), true, badgeSize * 0.56f,
                Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(badgeSize, badgeSize));
            c.verticalAlignment = VerticalAlignmentOptions.Geometry;
            parts.Add((badge.rectTransform, badgeSize));
        }
        else
        {
            RectTransform priceGroup = UIKit.Rect("Price", button.transform,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 60f));
            string priceText = PowerUps.PackPrice(type).ToString();
            TMP_Text v = UIKit.Text("Value", priceGroup, priceText, true, fontSize * 0.85f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, size.y * 0.5f));
            if (tryLabel != null)
            {
                v.font = tryLabel.font;
                v.fontSharedMaterial = tryLabel.fontSharedMaterial;
            }
            UIKit.Image("Coin", priceGroup, UIKit.Sprite("ui_coin"),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(badgeSize, badgeSize));
            coinSize = badgeSize;
            parts.Add((priceGroup, -2f));       // coin + price, measured later
        }

        return button;
    }

    private float offerScale = 1f;
    private float coinSize = 52f;

    private readonly List<(RectTransform rect, float width)> offerParts =
        new List<(RectTransform, float)>();
    private float offerFaceY;

    // Lays the offer out as one centred group (panel must be active).
    private void ApplyOfferLayout()
    {
        const float Gap = 22f;
        List<float> widths = new List<float>();

        foreach (var part in offerParts)
        {
            float w = part.width;

            if (w == -1f)
            {
                TMP_Text t = part.rect.GetComponent<TMP_Text>();
                t.ForceMeshUpdate();
                w = t.GetPreferredValues(t.text).x;
                part.rect.sizeDelta = new Vector2(w + 4f, part.rect.sizeDelta.y);
            }
            else if (w == -2f)
            {
                TMP_Text t = part.rect.GetComponentInChildren<TMP_Text>();
                t.ForceMeshUpdate();
                float priceW = t.GetPreferredValues(t.text).x;
                w = coinSize + 10f + priceW;
                part.rect.sizeDelta = new Vector2(w, coinSize);
                t.rectTransform.sizeDelta = new Vector2(priceW + 4f, coinSize);
                t.rectTransform.anchoredPosition = new Vector2(w * 0.5f - priceW * 0.5f, 0f);
                part.rect.Find("Coin").GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(-w * 0.5f + coinSize * 0.5f, 0f);
            }

            widths.Add(w);
        }

        float total = -Gap;
        foreach (float w in widths)
            total += w + Gap;

        float x = -total * 0.5f;

        for (int i = 0; i < offerParts.Count; i++)
        {
            offerParts[i].rect.anchoredPosition = new Vector2(x + widths[i] * 0.5f, offerFaceY);
            x += widths[i] + Gap;
        }
    }

    private void BuildExtraMovesOffer()
    {
        Button button = BuildOfferButton("+" + EconomyConfig.ExtraMovesAmount + " MOVES", null,
            PowerUpType.ExtraMoves);

        button.onClick.AddListener(() =>
        {
            Debug.Log("FailExtras: +" + EconomyConfig.ExtraMovesAmount + " moves pressed (have " +
                      PowerUps.Count(PowerUpType.ExtraMoves) + ", coins " + ProgressManager.Coins + ")");

            if (PowerUps.Count(PowerUpType.ExtraMoves) <= 0 &&
                !PowerUps.TryBuyPack(PowerUpType.ExtraMoves))
            {
                UIKit.Run(UIKit.Shake(offer));
                Popup.Toast("Not enough coins");
                return;
            }

            if (PowerUps.TryConsume(PowerUpType.ExtraMoves))
            {
                bool ok = LevelManager.Instance != null &&
                          LevelManager.Instance.AddMoves(EconomyConfig.ExtraMovesAmount);

                if (!ok)
                {
                    // Give it back if the level could not continue.
                    PowerUps.Add(PowerUpType.ExtraMoves, 1);
                    Debug.LogWarning("FailExtras: AddMoves refused");
                }
                else
                {
                    AudioManager.Play(GameSfx.Coin);
                }
            }
        });
    }

    private void BuildUndoOffer()
    {
        Button button = BuildOfferButton("UNDO", "ui_icon_undo_white", PowerUpType.Undo);

        button.onClick.AddListener(() =>
        {
            if (PowerUps.Count(PowerUpType.Undo) <= 0 &&
                !PowerUps.TryBuyPack(PowerUpType.Undo))
            {
                UIKit.Run(UIKit.Shake(offer));
                Popup.Toast("Not enough coins");
                return;
            }

            if (UndoSystem.Undo())
                PowerUps.TryConsume(PowerUpType.Undo);
        });
    }
}
