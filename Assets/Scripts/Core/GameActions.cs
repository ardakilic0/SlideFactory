using System;
using UnityEngine;

// =============================================================
// GAME ACTIONS
// =============================================================
//
// Single entry point for every player action (swipe, tap, buffer
// return). Input, the hint system, tutorials and test tools all go
// through here, so the Undo history and action events stay
// consistent no matter where an action comes from.
// =============================================================

public static class GameActions
{
    // Fired after an action has started (piece = acting box).
    public static event Action<Box> ActionPerformed;

    // =========================================================
    // SWIPE
    // =========================================================

    public static bool TrySwipe(
        Box box,
        Vector2Int direction)
    {
        if (box == null ||
            !box.gameObject.activeInHierarchy ||
            !box.CanBeSelected)
        {
            return false;
        }

        if (LevelManager.Instance != null &&
            LevelManager.Instance.IsInputLocked)
        {
            return false;
        }

        bool valid = box.CanMove(direction);
        bool captured = valid && UndoSystem.Capture();

        // Invalid moves still call Move(): it plays the shake.
        box.Move(direction);

        bool started = valid && !box.CanBeSelected;

        if (!started)
        {
            if (captured)
                UndoSystem.DiscardLast();

            return false;
        }

        if (LevelManager.Instance != null)
            LevelManager.Instance.RegisterSuccessfulPlayerAction();

        SafeEvents.Raise(ActionPerformed, box);
        return true;
    }

    // =========================================================
    // TAP (portal re-activation, grid buffer -> UI buffer)
    // =========================================================

    public static bool TryTap(Box box)
    {
        if (box == null ||
            !box.gameObject.activeInHierarchy ||
            !box.CanBeSelected ||
            box.CurrentCell == null)
        {
            return false;
        }

        if (LevelManager.Instance != null &&
            LevelManager.Instance.IsInputLocked)
        {
            return false;
        }

        // Successful teleport does NOT spend a move.
        if (box.CurrentCell.HasPortal &&
            box.CanReactivatePortal())
        {
            bool captured = UndoSystem.Capture();

            if (box.TryReactivatePortal())
            {
                LevelManager.Instance?.NotifyBoardStateChanged();
                SafeEvents.Raise(ActionPerformed, box);
                return true;
            }

            if (captured)
                UndoSystem.DiscardLast();
        }

        BufferManager buffer = BufferManager.Instance;

        if (box.CurrentCell.HasBufferPad &&
            buffer != null &&
            buffer.CanStoreBox(box))
        {
            UndoSystem.Capture();
            buffer.TryStoreBox(box);
            SafeEvents.Raise(ActionPerformed, box);
            return true;
        }

        return false;
    }

    // =========================================================
    // UI BUFFER -> GRID
    // =========================================================

    public static bool TryReturnFromBuffer(int slotIndex)
    {
        BufferManager buffer = BufferManager.Instance;

        if (buffer == null ||
            !buffer.CanReturnSlot(slotIndex))
        {
            return false;
        }

        if (LevelManager.Instance != null &&
            LevelManager.Instance.IsInputLocked)
        {
            return false;
        }

        Box box = buffer.GetStoredBox(slotIndex);

        UndoSystem.Capture();
        buffer.TryReturnBox(slotIndex);
        SafeEvents.Raise(ActionPerformed, box);
        return true;
    }

    // =========================================================
    // SOLVER ACTION (hint / autoplay)
    // =========================================================

    public static bool Perform(SolverAction action)
    {
        Box box = GetPiece(action.pieceIndex);

        if (box == null)
            return false;

        switch (action.type)
        {
            case SolverActionType.Move:
                return TrySwipe(box, action.direction);

            case SolverActionType.PortalTap:
            case SolverActionType.BufferStore:
                return TryTap(box);

            case SolverActionType.BufferReturn:
                return BufferManager.Instance != null &&
                       TryReturnFromBuffer(
                           BufferManager.Instance.GetSlotOfBox(box));
        }

        return false;
    }

    public static Box GetPiece(int pieceIndex)
    {
        if (GridManager.Instance == null)
            return null;

        Box[] pieces =
            GridManager.Instance.GetComponentsInChildren<Box>(true);

        if (pieceIndex < 0 || pieceIndex >= pieces.Length)
            return null;

        return pieces[pieceIndex];
    }
}
