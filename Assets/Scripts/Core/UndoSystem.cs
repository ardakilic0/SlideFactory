using System.Collections.Generic;
using UnityEngine;

// =============================================================
// UNDO SYSTEM
// =============================================================
//
// Before every player action the full board state is captured
// (piece cells, buffer slots, completed pieces, opened doors,
// moves left). Undo rebuilds the level from LevelData and puts
// that state back.
//
// Actions started while the board is still animating (fast swipes
// on several boxes) do not get their own snapshot: one Undo then
// reverts the whole burst, because the in-between state was never
// a resting state.
// =============================================================

public sealed class LevelSnapshot
{
    public int levelIndex;
    public int movesRemaining;

    // Per piece, in GridManager hierarchy order.
    public Vector2Int[] cells;
    public bool[] onGrid;
    public bool[] completed;
    public int[] bufferSlots;

    public List<int> openDoors;
}

public static class UndoSystem
{
    private const int MaxSteps = 200;

    private static readonly List<LevelSnapshot> history =
        new List<LevelSnapshot>();

    public static int Count => history.Count;

    public static bool CanUndo =>
        history.Count > 0 &&
        LevelManager.Instance != null &&
        LevelManager.Instance.CanRestoreSnapshot;

    public static void Clear()
    {
        history.Clear();
    }

    // Returns true when a new step was recorded.
    public static bool Capture()
    {
        LevelManager levelManager = LevelManager.Instance;

        if (levelManager == null)
            return false;

        // Mid-animation: this action joins the previous step.
        if (levelManager.IsBoardBusy)
            return false;

        LevelSnapshot snapshot = levelManager.CaptureSnapshot();

        if (snapshot == null)
            return false;

        history.Add(snapshot);

        if (history.Count > MaxSteps)
            history.RemoveAt(0);

        return true;
    }

    public static void DiscardLast()
    {
        if (history.Count > 0)
            history.RemoveAt(history.Count - 1);
    }

    public static bool Undo()
    {
        if (!CanUndo)
            return false;

        LevelSnapshot snapshot = history[history.Count - 1];
        history.RemoveAt(history.Count - 1);

        return LevelManager.Instance.RestoreSnapshot(snapshot);
    }
}
