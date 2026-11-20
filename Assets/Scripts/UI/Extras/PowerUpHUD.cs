using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// POWER-UP HUD
// =============================================================
//
// Undo (bottom left) and Hint (bottom right) buttons with a count
// badge. An empty power-up shows a green "+" badge and opens the
// shop offer. Buttons appear from their unlock level on.
// =============================================================

public class PowerUpHUD : MonoBehaviour
{
    private sealed class Slot
    {
        public PowerUpType type;
        public RectTransform root;
        public Image badge;
        public TMP_Text badgeText;
        public CanvasGroup group;
    }

    private Slot undoSlot;
    private Slot hintSlot;
    private bool levelActive;

    public static PowerUpHUD Create(Transform safeArea)
    {
        RectTransform rect = UIKit.Stretch("PowerUpHUD", safeArea);
        PowerUpHUD hud = rect.gameObject.AddComponent<PowerUpHUD>();
        hud.Build(rect);
        return hud;
    }

    private void Build(RectTransform root)
    {
        undoSlot = BuildSlot(root, PowerUpType.Undo, "ui_icon_undo",
            new Vector2(0f, 0f), new Vector2(150f, 165f), OnUndoPressed);

        hintSlot = BuildSlot(root, PowerUpType.Hint, "ui_icon_hint",
            new Vector2(1f, 0f), new Vector2(-150f, 165f), OnHintPressed);

        Refresh();
    }

    private Slot BuildSlot(
        RectTransform root,
        PowerUpType type,
        string icon,
        Vector2 anchor,
        Vector2 position,
        System.Action onPressed)
    {
        // Same round button art as the settings button (284x300 incl. shadow).
        Button button = UIKit.Button(
            type + "Button", root, UIKit.Sprite("ui_round_button"),
            anchor, position, new Vector2(180f, 190f), onPressed);

        Image iconImage = UIKit.Image("Icon", button.transform, UIKit.Sprite(icon),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(104f, 104f));
        iconImage.preserveAspect = true;

        UIKit.Text("Label", button.transform, PowerUps.DisplayName(type), true, 36f,
            UIKit.Slate, new Vector2(0.5f, 0f), new Vector2(0f, -14f), new Vector2(240f, 50f));

        Image badge = UIKit.Image("Badge", button.transform, UIKit.Sprite("ui_badge_orange"),
            new Vector2(1f, 1f), new Vector2(-24f, -26f), new Vector2(76f, 76f));

        TMP_Text badgeText = UIKit.Text("Count", badge.transform, "0", true, 40f, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));

        return new Slot
        {
            type = type,
            root = (RectTransform)button.transform,
            badge = badge,
            badgeText = badgeText,
            group = button.gameObject.AddComponent<CanvasGroup>()
        };
    }

    private void OnEnable()
    {
        PowerUps.Changed += Refresh;
        LevelManager.LevelLoaded += OnLevelLoaded;
        LevelManager.LevelCompleted += OnLevelCompleted;
        LevelManager.LevelFailed += OnLevelFailed;
        LevelManager.BoardRestored += OnBoardRestored;
        LevelManager.MovesChanged += OnMovesChanged;
    }

    private void OnDisable()
    {
        PowerUps.Changed -= Refresh;
        LevelManager.LevelLoaded -= OnLevelLoaded;
        LevelManager.LevelCompleted -= OnLevelCompleted;
        LevelManager.LevelFailed -= OnLevelFailed;
        LevelManager.BoardRestored -= OnBoardRestored;
        LevelManager.MovesChanged -= OnMovesChanged;
    }

    private void OnLevelLoaded(int levelIndex) { levelActive = true; Refresh(); }
    private void OnLevelCompleted(int levelIndex, LevelReward reward) { levelActive = false; Refresh(); }
    private void OnLevelFailed(int levelIndex, LevelFailReason reason) { levelActive = false; Refresh(); }
    private void OnBoardRestored()
    {
        levelActive = true;
        Refresh();

        // Undo feedback: rewind sound + flash, pieces glide back.
        AudioManager.Play(GameSfx.Rewind);

        if (BoardOverlay.Instance != null)
            BoardOverlay.Instance.PlayRewind();
    }

    private void OnMovesChanged()
    {
        // +5 moves revives a failed level.
        if (LevelManager.Instance != null && !LevelManager.Instance.IsLevelEnded)
            levelActive = true;

        Refresh();
    }

    public void Refresh()
    {
        int levelIndex = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelIndex : 0;

        RefreshSlot(undoSlot, levelIndex);
        RefreshSlot(hintSlot, levelIndex);
    }

    private void RefreshSlot(Slot slot, int levelIndex)
    {
        if (slot == null)
            return;

        bool visible = levelActive && PowerUps.IsUnlocked(slot.type, levelIndex);
        slot.root.gameObject.SetActive(visible);

        int count = PowerUps.Count(slot.type);

        slot.badge.sprite = UIKit.Sprite(count > 0 ? "ui_badge_orange" : "ui_badge_green");
        slot.badgeText.text = count > 0 ? count.ToString() : "+";
    }

    // =========================================================
    // UNDO
    // =========================================================

    private void OnUndoPressed()
    {
        LevelManager levelManager = LevelManager.Instance;

        if (levelManager == null || levelManager.IsBoardBusy)
            return;

        if (UndoSystem.Count == 0)
        {
            StartCoroutine(UIKit.Shake(undoSlot.root));
            Popup.Toast("Nothing to undo yet");
            return;
        }

        if (PowerUps.Count(PowerUpType.Undo) <= 0)
        {
            ShopOffer.Show(PowerUpType.Undo, OnUndoPressed);
            return;
        }

        if (UndoSystem.Undo())
            PowerUps.TryConsume(PowerUpType.Undo);
    }

    // =========================================================
    // HINT
    // =========================================================

    private void OnHintPressed()
    {
        LevelManager levelManager = LevelManager.Instance;

        if (levelManager == null || levelManager.IsBoardBusy || levelManager.IsLevelEnded)
            return;

        if (HintSystem.IsShowing)
        {
            HintSystem.Reshow();
            return;
        }

        if (PowerUps.Count(PowerUpType.Hint) <= 0)
        {
            ShopOffer.Show(PowerUpType.Hint, OnHintPressed);
            return;
        }

        HintOutcome outcome = HintSystem.TryShow();

        switch (outcome)
        {
            case HintOutcome.Shown:
                PowerUps.TryConsume(PowerUpType.Hint);
                break;

            case HintOutcome.NotEnoughMoves:
                ShowStuckPopup(
                    "TOO FAR OFF",
                    "From here the level needs " + HintSystem.LastCost +
                    " more moves, but you have " + levelManager.MovesRemaining +
                    ".\nStep back with Undo or start over.");
                break;

            case HintOutcome.DeadEnd:
                ShowStuckPopup(
                    "DEAD END",
                    "The boxes can't all reach their goals from here.\nStep back with Undo or start over.");
                break;

            case HintOutcome.Unknown:
                Popup.Toast("No hint found right now");
                break;
        }
    }

    private void ShowStuckPopup(string title, string body)
    {
        PopupSpec spec = new PopupSpec
        {
            title = title,
            body = body + "\n(The hint was not used.)",
            icon = UIKit.Sprite("ui_icon_hint"),
            iconIsPowerUpButton = true
        };

        if (UndoSystem.Count > 0 &&
            PowerUps.IsUnlocked(PowerUpType.Undo, LevelManager.Instance.CurrentLevelIndex))
        {
            spec.buttons.Add(new PopupButton("UNDO", PopupButtonStyle.Secondary, OnUndoPressed));
        }

        spec.buttons.Add(new PopupButton("RESTART", PopupButtonStyle.Primary,
            () => LevelManager.Instance?.RestartLevel()));
        spec.buttons.Add(new PopupButton("CLOSE", PopupButtonStyle.Quiet, null));

        Popup.Show(spec);
    }
}

// =============================================================
// SHOP OFFER
// =============================================================

public static class ShopOffer
{
    public static void Show(PowerUpType type, System.Action afterPurchase = null)
    {
        int price = PowerUps.PackPrice(type);
        int size = PowerUps.PackSize(type);
        int coins = ProgressManager.Coins;
        bool canBuy = coins >= price;

        string what =
            type == PowerUpType.Undo ? (size > 1 ? size + " Undos" : "1 Undo") :
            type == PowerUpType.Hint ? (size > 1 ? size + " Hints" : "1 Hint") :
            "+" + EconomyConfig.ExtraMovesAmount + " Moves";

        PopupSpec spec = new PopupSpec
        {
            title = "OUT OF " + (type == PowerUpType.ExtraMoves ? "EXTRA MOVES" : PowerUps.DisplayName(type) + "S"),
            body = canBuy
                ? "Get " + what + " for " + price + " coins?"
                : "Get " + what + " for " + price + " coins.\nEarn coins by finishing levels and collecting stars.",
            icon = type == PowerUpType.ExtraMoves ? null
                 : UIKit.Sprite(type == PowerUpType.Undo ? "ui_icon_undo" : "ui_icon_hint"),
            iconIsPowerUpButton = true,
            extra = root => CoinBalanceLine(root)
        };

        spec.buttons.Add(new PopupButton("BUY", PopupButtonStyle.Primary, () =>
        {
            if (PowerUps.TryBuyPack(type))
            {
                AudioManager.Play(GameSfx.Goal);
                afterPurchase?.Invoke();
            }
        })
        {
            price = price,
            interactable = canBuy
        });

        spec.buttons.Add(new PopupButton("NOT NOW", PopupButtonStyle.Quiet, null));

        Popup.Show(spec);
    }

    public static void CoinBalanceLine(RectTransform root)
    {
        UIKit.Image("Coin", root, UIKit.Sprite("ui_coin"),
            new Vector2(0.5f, 0.5f), new Vector2(-70f, 0f), new Vector2(58f, 58f));

        UIKit.Text("Coins", root, ProgressManager.Coins.ToString(), true, 48f, UIKit.Slate,
            new Vector2(0.5f, 0.5f), new Vector2(40f, 2f), new Vector2(160f, 70f),
            TextAlignmentOptions.Left);
    }
}

// =============================================================
// STAR METER (under the move counter)
// =============================================================
//
// Shows how many stars the level would give if it were finished
// now. When a star is lost it pops, flashes red, then drops away,
// so the player clearly notices it.
// =============================================================

public class StarMeter : MonoBehaviour
{
    private static readonly Color LossRed = new Color32(255, 70, 60, 255);

    private readonly Image[] stars = new Image[3];
    private RectTransform root;
    private int shownStars = 3;

    public static StarMeter Create(Transform topHud)
    {
        RectTransform rect = UIKit.Rect("StarMeter", topHud,
            new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(270f, 84f));

        StarMeter meter = rect.gameObject.AddComponent<StarMeter>();
        meter.root = rect;

        for (int i = 0; i < 3; i++)
        {
            meter.stars[i] = UIKit.Image("Star" + i, rect, UIKit.Sprite("ui_star_full"),
                new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 80f, i == 1 ? 8f : 0f),
                new Vector2(i == 1 ? 84f : 74f, i == 1 ? 84f : 74f));
        }

        return meter;
    }

    private void OnEnable()
    {
        LevelManager.MovesChanged += Refresh;
        LevelManager.LevelLoaded += OnLevelLoaded;
    }

    private void OnDisable()
    {
        LevelManager.MovesChanged -= Refresh;
        LevelManager.LevelLoaded -= OnLevelLoaded;
    }

    private void OnLevelLoaded(int levelIndex)
    {
        shownStars = 3;
        Refresh();
    }

    private void Refresh()
    {
        LevelManager levelManager = LevelManager.Instance;

        if (levelManager == null)
            return;

        int levelIndex = levelManager.CurrentLevelIndex;
        int starsNow = LevelDatabase.GetStars(
            levelIndex,
            levelManager.MovesUsed,
            levelManager.MoveBudget > LevelDatabase.GetMoveLimit(levelIndex));

        for (int i = 0; i < 3; i++)
        {
            bool lit = i < starsNow;
            bool losing = !lit && i < shownStars && gameObject.activeInHierarchy;

            stars[i].sprite = UIKit.Sprite(lit ? "ui_star_full" : "ui_star_empty");
            stars[i].color = Color.white;
            stars[i].rectTransform.localScale = Vector3.one;

            if (losing)
                StartCoroutine(LoseStar(stars[i]));
        }

        // Undo can give a star back: pop it in.
        for (int i = shownStars; i < starsNow; i++)
            StartCoroutine(UIKit.Pop(stars[i].rectTransform, 0.25f, 1.25f));

        shownStars = starsNow;
    }

    // Ghost copy of the full star: pop + red flashes, then it falls.
    private IEnumerator LoseStar(Image slot)
    {
        AudioManager.Play(GameSfx.StarLost);
        HapticsManager.Invalid();

        RectTransform slotRect = slot.rectTransform;
        Image ghost = UIKit.Image("LostStar", root, UIKit.Sprite("ui_star_full"),
            new Vector2(0.5f, 0.5f), slotRect.anchoredPosition, slotRect.sizeDelta);
        RectTransform g = ghost.rectTransform;

        // pop
        float t = 0f;
        while (t < 0.14f)
        {
            t += Time.unscaledDeltaTime;
            g.localScale = Vector3.one * Mathf.Lerp(1f, 1.45f, t / 0.14f);
            yield return null;
        }

        // flash red three times, shrinking back a little
        for (int k = 0; k < 3; k++)
        {
            ghost.color = LossRed;
            yield return new WaitForSecondsRealtime(0.08f);
            ghost.color = Color.white;
            yield return new WaitForSecondsRealtime(0.07f);
            g.localScale = Vector3.one * (1.45f - 0.1f * (k + 1));
        }

        // fall and fade
        ghost.color = LossRed;
        Vector2 start = g.anchoredPosition;
        t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.4f;
            g.anchoredPosition = start + new Vector2(18f * k, -90f * k * k);
            g.localEulerAngles = new Vector3(0f, 0f, -35f * k);
            g.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.6f, k);
            ghost.color = new Color(LossRed.r, LossRed.g, LossRed.b, 1f - k);
            yield return null;
        }

        Destroy(ghost.gameObject);
    }
}
