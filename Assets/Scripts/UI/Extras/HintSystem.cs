using System.Collections.Generic;
using UnityEngine;

// =============================================================
// HINT SYSTEM
// =============================================================
//
// Asks the solver for the best continuation from the current
// board and shows its first action on the BoardOverlay. The hint
// disappears as soon as the player acts (or undoes / restarts).
// =============================================================

public enum HintOutcome
{
    Shown,
    NotEnoughMoves,
    DeadEnd,
    Unknown,
    Busy
}

public static class HintSystem
{
    private const int MaxStates = 400000;
    private const int MaxMilliseconds = 2500;

    public static bool IsShowing { get; private set; }

    // Moves the solver needs from the current state (last query).
    public static int LastCost { get; private set; } = -1;

    private static SolverAction shownAction;

    public static HintOutcome TryShow()
    {
        LevelManager levelManager = LevelManager.Instance;

        if (levelManager == null ||
            levelManager.IsLevelEnded ||
            levelManager.IsBoardBusy ||
            BoardOverlay.Instance == null)
        {
            return HintOutcome.Busy;
        }

        SolveResult result = LevelSolver.Solve(
            LevelDatabase.GetLevel(levelManager.CurrentLevelIndex),
            GridManager.Instance,
            MaxStates,
            MaxMilliseconds,
            out List<SolverAction> path,
            out int cost);

        LastCost = cost;

        if (result == SolveResult.Unsolvable)
            return HintOutcome.DeadEnd;

        if (result != SolveResult.Solved || path.Count == 0)
            return HintOutcome.Unknown;

        if (cost > levelManager.MovesRemaining)
            return HintOutcome.NotEnoughMoves;

        Show(path[0]);
        return HintOutcome.Shown;
    }

    // Shows an action without any checks (tutorial guidance).
    public static void Show(SolverAction action)
    {
        BoardOverlay overlay = BoardOverlay.Instance;
        Box box = GameActions.GetPiece(action.pieceIndex);

        if (overlay == null || box == null)
            return;

        shownAction = action;
        IsShowing = true;

        switch (action.type)
        {
            case SolverActionType.Move:
                overlay.ShowSwipe(box, action.direction);
                break;

            case SolverActionType.PortalTap:
            case SolverActionType.BufferStore:
                overlay.ShowTap(box.transform);
                break;

            case SolverActionType.BufferReturn:
                BufferManager buffer = BufferManager.Instance;
                overlay.ShowTapUI(buffer != null
                    ? buffer.GetSlotRect(buffer.GetSlotOfBox(box))
                    : null);
                break;
        }
    }

    // Re-displays the current hint (no new search).
    public static void Reshow()
    {
        if (IsShowing)
            Show(shownAction);
    }

    public static void Hide()
    {
        IsShowing = false;

        if (BoardOverlay.Instance != null)
            BoardOverlay.Instance.HidePointer();
    }

    // Next action of the best solution, for tutorial guidance.
    public static bool TryGetNextAction(out SolverAction action)
    {
        action = default;
        LevelManager levelManager = LevelManager.Instance;

        if (levelManager == null || levelManager.IsLevelEnded)
            return false;

        SolveResult result = LevelSolver.Solve(
            LevelDatabase.GetLevel(levelManager.CurrentLevelIndex),
            GridManager.Instance,
            MaxStates,
            MaxMilliseconds,
            out List<SolverAction> path,
            out int _);

        if (result != SolveResult.Solved || path.Count == 0)
            return false;

        action = path[0];
        return true;
    }
}
