using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// TUTORIAL DIRECTOR
// =============================================================
//
// Data-driven tutorials:
// 1. Mechanic cards: the first level in LevelDatabase that uses a
//    mechanic shows a short card and highlights those objects on
//    the board. The level list decides where each card appears, so
//    reordering levels keeps tutorials in the right place.
// 2. Power-up intros on their unlock levels.
// 3. Guided first levels: the solver's next move is shown as a
//    ghost swipe when the player waits (instantly on level 1).
//
// Each card is shown once (PlayerPrefs "Tutorial_<key>").
// =============================================================

public class TutorialDirector : MonoBehaviour
{
    private sealed class Mechanic
    {
        public string key;
        public string title;
        public string body;
        public Func<string, bool> uses;        // token -> uses mechanic
        public Func<string, bool> highlight;   // token -> highlight cell
        public BoxType? iconBox;
    }

    private static readonly Mechanic[] Mechanics =
    {
        new Mechanic
        {
            key = "Basics",
            title = "HOW TO PLAY",
            body = "Swipe a box to move it one tile.\nBring every box to the goal with its letter.",
            uses = t => t.StartsWith("B") && t != "BUF",
            highlight = t => (t.StartsWith("B") && t != "BUF") || t.StartsWith("G")
        },
        new Mechanic
        {
            key = "Crate",
            title = "CRATES",
            body = "Crates never move by themselves.\nWalk a box into a crate to push it.",
            uses = t => t == "C",
            highlight = t => t == "C",
            iconBox = BoxType.Crate
        },
        new Mechanic
        {
            key = "Switch",
            title = "SWITCH & DOOR",
            body = "Move a box onto a switch to open the door of the same color.\nIt stays open for good.",
            uses = t => t.StartsWith("S"),
            highlight = t => t.StartsWith("S") || t.StartsWith("D")
        },
        new Mechanic
        {
            key = "Plate",
            title = "PRESSURE PLATE",
            body = "A plate keeps its door open only while something stands on it.\nLeave the plate and the door shuts.",
            uses = t => t.StartsWith("T"),
            highlight = t => t.StartsWith("T") || t.StartsWith("D")
        },
        new Mechanic
        {
            key = "Portal",
            title = "PORTALS",
            body = "A box entering a portal comes out of its twin.\nTap a box standing on a portal to send it through again (free).",
            uses = t => t.StartsWith("P"),
            highlight = t => t.StartsWith("P")
        },
        new Mechanic
        {
            key = "Ice",
            title = "ICE BOX",
            body = "Ice boxes slide until something stops them.\nPlan where they will land!",
            uses = t => t.StartsWith("I"),
            highlight = t => t.StartsWith("I"),
            iconBox = BoxType.Ice
        },
        new Mechanic
        {
            key = "Heavy",
            title = "HEAVY BOX",
            body = "Heavy boxes push up to two crates in a row.\nNormal boxes can push only one.",
            uses = t => t.StartsWith("H"),
            highlight = t => t.StartsWith("H"),
            iconBox = BoxType.Heavy
        },
        new Mechanic
        {
            key = "Conveyor",
            title = "CONVEYOR",
            body = "Conveyor belts carry boxes along the arrows until something blocks them.",
            uses = t => t == "^" || t == "v" || t == "<" || t == ">",
            highlight = t => t == "^" || t == "v" || t == "<" || t == ">"
        },
        new Mechanic
        {
            key = "Buffer",
            title = "BUFFER PAD",
            body = "Tap a box on the pad to store it below the board.\nTap its slot to bring it back. Storing is free!",
            uses = t => t == "BUF",
            highlight = t => t == "BUF"
        }
    };

    // Guided levels: seconds of waiting before the ghost move shows.
    private static float GuideDelay(int levelIndex)
    {
        switch (levelIndex)
        {
            case 0: return 12f;
            case 1: return 12f;
            case 2: return 15f;
            default: return -1f;
        }
    }

    private readonly Queue<Action> queue = new Queue<Action>();
    private bool queueRunning;
    private float idleTime;
    private bool guideShowing;

    // =========================================================
    // SETUP
    // =========================================================

    private void OnEnable()
    {
        LevelManager.LevelLoaded += OnLevelLoaded;
        LevelManager.LevelCompleted += OnLevelEnded;
        LevelManager.LevelFailed += OnLevelFailed;
        LevelManager.BoardRestored += OnBoardChanged;
        GameActions.ActionPerformed += OnAction;
    }

    private void OnDisable()
    {
        LevelManager.LevelLoaded -= OnLevelLoaded;
        LevelManager.LevelCompleted -= OnLevelEnded;
        LevelManager.LevelFailed -= OnLevelFailed;
        LevelManager.BoardRestored -= OnBoardChanged;
        GameActions.ActionPerformed -= OnAction;
    }

    private void OnAction(Box box)
    {
        firstActionDone = true;
        BoardOverlay.Instance?.SetCaption(null);
        idleTime = 0f;
        guideShowing = false;
        HintSystem.Hide();
    }

    private void OnBoardChanged()
    {
        idleTime = 0f;
        guideShowing = false;
        HintSystem.Hide();
    }

    private void OnLevelEnded(int levelIndex, LevelReward reward)
    {
        HintSystem.Hide();
        BoardOverlay.Instance?.ClearAll();
    }

    private void OnLevelFailed(int levelIndex, LevelFailReason reason)
    {
        HintSystem.Hide();
        BoardOverlay.Instance?.ClearAll();
    }

    private void OnLevelLoaded(int levelIndex)
    {
        Popup.CloseImmediate();
        HintSystem.Hide();
        BoardOverlay.Instance?.ClearAll();

        StopAllCoroutines();
        InputManager.BlockBoardInput = Popup.IsOpen;
        queue.Clear();
        queueRunning = false;
        idleTime = 0f;
        guideShowing = false;

        LevelData data = LevelDatabase.GetLevel(levelIndex);

        foreach (Mechanic mechanic in Mechanics)
        {
            if (FirstLevelUsing(mechanic) == levelIndex && !Seen(mechanic.key))
            {
                Mechanic m = mechanic;

                if (m.key == "Basics")
                    queue.Enqueue(() => StartCoroutine(BasicsIntro(m, data)));
                else
                    queue.Enqueue(() => ShowMechanicCard(m, data));
            }
        }

        foreach (PowerUpType type in new[] { PowerUpType.Undo, PowerUpType.Hint })
        {
            if (PowerUps.IsUnlocked(type, levelIndex) && !PowerUps.WasIntroduced(type))
            {
                PowerUpType t = type;
                queue.Enqueue(() => ShowPowerUpIntro(t));
            }
        }

        RunNext();
    }

    // =========================================================
    // QUEUE
    // =========================================================

    private void RunNext()
    {
        BoardOverlay.Instance?.ClearHighlights();

        if (queue.Count == 0)
        {
            queueRunning = false;
            return;
        }

        queueRunning = true;
        queue.Dequeue().Invoke();
    }

    private void ShowMechanicCard(Mechanic mechanic, LevelData data)
    {
        MarkSeen(mechanic.key);

        List<Vector3> cells = new List<Vector3>();

        if (data != null && GridManager.Instance != null)
        {
            for (int row = 0; row < data.Height; row++)
            {
                string[] tokens = data.GetRowTokens(row);

                for (int x = 0; x < tokens.Length; x++)
                {
                    if (mechanic.highlight(tokens[x]))
                    {
                        cells.Add(GridManager.Instance.GetWorldPosition(
                            new Vector2Int(x, data.Height - 1 - row)));
                    }
                }
            }
        }

        BoardOverlay.Instance?.Highlight(cells);

        Sprite icon = null;

        if (mechanic.iconBox.HasValue && GridManager.Instance != null)
        {
            foreach (Box box in GridManager.Instance.GetComponentsInChildren<Box>(true))
            {
                if (box.Type == mechanic.iconBox.Value)
                {
                    icon = box.BufferSprite;
                    break;
                }
            }
        }

        PopupSpec spec = new PopupSpec
        {
            title = mechanic.title,
            body = mechanic.body,
            icon = icon,
            bottom = true,
            dim = false,
            onClosed = RunNext
        };

        spec.buttons.Add(new PopupButton("GOT IT", PopupButtonStyle.Primary, null));
        Popup.Show(spec);
    }

    // =========================================================
    // LEVEL 1 INTRO
    // =========================================================
    //
    // 1. spotlight on the box, 2. on its goal, 3. on both, then a
    // single swipe demonstration that stays until the first move.

    private bool firstActionDone;

    private IEnumerator BasicsIntro(Mechanic mechanic, LevelData data)
    {
        MarkSeen(mechanic.key);

        BoardOverlay overlay = BoardOverlay.Instance;
        List<Vector3> boxes = Cells(data, t => t.StartsWith("B") && t != "BUF");
        List<Vector3> goals = Cells(data, t => t.StartsWith("G"));

        if (overlay == null || boxes.Count == 0 || goals.Count == 0)
        {
            RunNext();
            yield break;
        }

        InputManager.BlockBoardInput = true;
        yield return new WaitForSecondsRealtime(0.35f);

        // Each step stays until the player presses GOT IT.
        yield return FocusStep(overlay, boxes, "This is a box");
        yield return FocusStep(overlay, goals, "This is its goal - same letter!");

        List<Vector3> both = new List<Vector3>(boxes);
        both.AddRange(goals);
        yield return FocusStep(overlay, both, "Deliver every box to its goal");

        overlay.ClearFocus();
        InputManager.BlockBoardInput = false;

        // One swipe demonstration, until the player moves.
        firstActionDone = false;

        if (HintSystem.TryGetNextAction(out SolverAction action))
        {
            HintSystem.Show(action);
            overlay.SetCaption("Swipe the box to move it");
        }

        while (!firstActionDone && LevelManager.Instance != null && !LevelManager.Instance.IsLevelEnded)
            yield return null;

        overlay.SetCaption(null);
        guideShowing = false;
        idleTime = 0f;
        RunNext();
    }

    private static IEnumerator FocusStep(BoardOverlay overlay, List<Vector3> cells, string text)
    {
        bool pressed = false;
        overlay.Focus(cells, text, () => pressed = true);
        AudioManager.Play(GameSfx.Popup);

        while (!pressed && overlay != null)
            yield return null;
    }

    private static IEnumerator WaitOrTap(float seconds)
    {
        // ignore the tap that may still be held from before
        yield return null;
        float t = 0f;

        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;

            if (t > 0.35f && (Input.GetMouseButtonDown(0) ||
                (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
            {
                break;
            }

            yield return null;
        }
    }

    private static List<Vector3> Cells(LevelData data, Func<string, bool> match)
    {
        List<Vector3> cells = new List<Vector3>();

        if (data == null || GridManager.Instance == null)
            return cells;

        for (int row = 0; row < data.Height; row++)
        {
            string[] tokens = data.GetRowTokens(row);

            for (int x = 0; x < tokens.Length; x++)
            {
                if (match(tokens[x]))
                {
                    cells.Add(GridManager.Instance.GetWorldPosition(
                        new Vector2Int(x, data.Height - 1 - row)));
                }
            }
        }

        return cells;
    }

    private void ShowPowerUpIntro(PowerUpType type)
    {
        PowerUps.MarkIntroduced(type);

        string body = type == PowerUpType.Undo
            ? "Made a wrong move? Undo takes it back - and gives the move back too."
            : "Stuck? A hint shows the best next move.";

        int count = PowerUps.Count(type);

        PopupSpec spec = new PopupSpec
        {
            title = "NEW: " + PowerUps.DisplayName(type),
            body = body + "\nYou have " + count + ". Get more from star chests or with coins.",
            icon = UIKit.Sprite(type == PowerUpType.Undo ? "ui_icon_undo" : "ui_icon_hint"),
            iconIsPowerUpButton = true,
            onClosed = RunNext
        };

        spec.buttons.Add(new PopupButton("NICE!", PopupButtonStyle.Primary, null));
        Popup.Show(spec);
    }

    // =========================================================
    // GUIDED PLAY
    // =========================================================

    private void Update()
    {
        LevelManager levelManager = LevelManager.Instance;

        if (levelManager == null || queueRunning || Popup.IsOpen)
            return;

        float delay = GuideDelay(levelManager.CurrentLevelIndex);

        if (delay < 0f || guideShowing ||
            levelManager.IsLevelEnded || levelManager.IsBoardBusy)
        {
            return;
        }

        idleTime += Time.unscaledDeltaTime;

        if (idleTime < delay)
            return;

        if (HintSystem.TryGetNextAction(out SolverAction action))
        {
            HintSystem.Show(action);
            guideShowing = true;
        }
        else
        {
            idleTime = -999f; // nothing to show (dead end): stop trying
        }
    }

    // =========================================================
    // DATA
    // =========================================================

    private static int FirstLevelUsing(Mechanic mechanic)
    {
        for (int i = 0; i < LevelDatabase.GetLevelCount(); i++)
        {
            LevelData data = LevelDatabase.GetLevel(i);

            for (int row = 0; row < data.Height; row++)
            {
                foreach (string token in data.GetRowTokens(row))
                {
                    if (mechanic.uses(token))
                        return i;
                }
            }
        }

        return -1;
    }

    private static bool Seen(string key) =>
        PlayerPrefs.GetInt("Tutorial_" + key, 0) == 1;

    private static void MarkSeen(string key)
    {
        PlayerPrefs.SetInt("Tutorial_" + key, 1);
        PlayerPrefs.Save();
    }
}
