using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =============================================================
// EXTRAS BOOTSTRAP
// =============================================================
//
// Builds the runtime UI of the new systems into the existing
// scenes, so the scene files stay untouched:
// - Game: board overlay, power-up buttons, star meter, level
//   complete stars, fail-screen continue offer, tutorials.
// - MainMenu: stars / coins (/ lives) chips.
// =============================================================

public static class GameExtrasBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Setup();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Setup();
    }

    private static void Setup()
    {
        if (GameObject.Find("GameExtras") != null)
            return;

        Canvas canvas = UIKit.FindMainCanvas();

        if (canvas == null)
            return;

        if (Object.FindFirstObjectByType<LevelManager>() != null)
            SetupGame(canvas);
        else if (Object.FindFirstObjectByType<MainMenuManager>() != null)
            SetupMainMenu(canvas);
    }

    private static Transform FindHudSafeArea(Canvas canvas)
    {
        foreach (Transform child in canvas.transform)
        {
            if (child.name == "SafeArea")
                return child;
        }

        return canvas.transform;
    }

    private static void SetupGame(Canvas canvas)
    {
        GameObject hostObject = new GameObject("GameExtras");
        ExtrasHost host = hostObject.AddComponent<ExtrasHost>();

        Transform safeArea = FindHudSafeArea(canvas);

        BoardOverlay.Create(canvas, safeArea);
        PowerUpHUD.Create(safeArea);

        Transform topHud = UIKit.FindDeep(safeArea, "TopHUD");

        if (topHud != null)
            StarMeter.Create(topHud);

        Transform completeContent = UIKit.FindDeep(canvas.transform, "CompleteContent");

        if (completeContent != null)
            CompleteRewardsView.Create(completeContent, host);

        Transform failCard = UIKit.FindDeep(canvas.transform, "FailCard");

        if (failCard != null)
            FailExtrasView.Create((RectTransform)failCard, host);

        hostObject.AddComponent<TutorialDirector>();
        hostObject.AddComponent<OutOfLivesWatcher>();
    }

    private static void SetupMainMenu(Canvas canvas)
    {
        GameObject host = new GameObject("GameExtras");
        // Main menu SafeArea is a 100x100 center anchor: use the canvas.
        host.AddComponent<MainMenuExtras>().Build(canvas, canvas.transform);
    }
}

// Owns cleanup for views that live on inactive panels.
public class ExtrasHost : MonoBehaviour
{
    private readonly System.Collections.Generic.List<System.Action> cleanups =
        new System.Collections.Generic.List<System.Action>();

    public void OnCleanup(System.Action action) => cleanups.Add(action);

    private void OnDestroy()
    {
        foreach (System.Action action in cleanups)
            action();

        cleanups.Clear();
    }
}

// =============================================================
// MAIN MENU CHIPS
// =============================================================

public class MainMenuExtras : MonoBehaviour
{
    private TMP_Text starsText;
    private TMP_Text coinsText;
    private TMP_Text livesText;
    private RectTransform livesChip;

    public void Build(Canvas canvas, Transform parent)
    {
        // Same height as the settings button (top right).
        starsText = Chip(parent, "StarsChip", "ui_star_full", new Vector2(175f, -262f));
        coinsText = Chip(parent, "CoinsChip", "ui_coin", new Vector2(465f, -262f));

        if (LivesManager.Enabled)
        {
            livesText = Chip(parent, "LivesChip", "ui_heart", new Vector2(175f, -370f));
            livesChip = (RectTransform)livesText.transform.parent;
        }

        Refresh();
    }

    private TMP_Text Chip(Transform parent, string name, string icon, Vector2 position)
    {
        return UIKit.Chip(name, parent, icon, new Vector2(0f, 1f), position, 270f);
    }

    private void OnEnable()
    {
        ProgressManager.CoinsChanged += Refresh;
        LivesManager.Changed += Refresh;
    }

    private void OnDisable()
    {
        ProgressManager.CoinsChanged -= Refresh;
        LivesManager.Changed -= Refresh;
    }

    private float nextRefresh;

    private void Update()
    {
        // Lives timer.
        if (livesText != null && Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (starsText != null)
            starsText.text = ProgressManager.TotalStars.ToString();

        if (coinsText != null)
            coinsText.text = ProgressManager.Coins.ToString();

        if (livesText != null)
        {
            int lives = LivesManager.Lives;
            livesText.text = LivesManager.IsFull
                ? lives.ToString()
                : lives + "  " + LivesManager.FormatTime(LivesManager.SecondsToNextLife);

        }
    }

    // Shown when PLAY is pressed without lives.
    public static void ShowOutOfLives(System.Action onRefilled)
    {
        PopupSpec spec = new PopupSpec
        {
            title = "OUT OF LIVES",
            body = "Next life in " + LivesManager.FormatTime(LivesManager.SecondsToNextLife) +
                   ".\nRefill all lives now?",
            icon = UIKit.Sprite("ui_heart"),
            extra = root => ShopOffer.CoinBalanceLine(root)
        };

        spec.buttons.Add(new PopupButton("REFILL", PopupButtonStyle.Primary, () =>
        {
            if (LivesManager.TryBuyRefill())
                onRefilled?.Invoke();
        })
        {
            price = EconomyConfig.RefillLivesPrice,
            interactable = ProgressManager.Coins >= EconomyConfig.RefillLivesPrice
        });

        spec.buttons.Add(new PopupButton("WAIT", PopupButtonStyle.Quiet, null));
        Popup.Show(spec);
    }
}

// =============================================================
// OUT OF LIVES (Game scene)
// =============================================================

public class OutOfLivesWatcher : MonoBehaviour
{
    private void OnEnable()
    {
        LevelManager.OutOfLives += OnOutOfLives;
    }

    private void OnDisable()
    {
        LevelManager.OutOfLives -= OnOutOfLives;
    }

    private void OnOutOfLives(int levelIndex)
    {
        PopupSpec spec = new PopupSpec
        {
            title = "OUT OF LIVES",
            body = "Next life in " + LivesManager.FormatTime(LivesManager.SecondsToNextLife) + ".",
            icon = UIKit.Sprite("ui_heart"),
            extra = root => ShopOffer.CoinBalanceLine(root)
        };

        spec.buttons.Add(new PopupButton("REFILL", PopupButtonStyle.Primary, () =>
        {
            if (LivesManager.TryBuyRefill())
                LevelManager.Instance?.LoadLevel(levelIndex);
        })
        {
            price = EconomyConfig.RefillLivesPrice,
            interactable = ProgressManager.Coins >= EconomyConfig.RefillLivesPrice
        });

        spec.buttons.Add(new PopupButton("MAIN MENU", PopupButtonStyle.Quiet,
            () => SceneManager.LoadScene("MainMenu")));

        Popup.Show(spec);
    }
}
