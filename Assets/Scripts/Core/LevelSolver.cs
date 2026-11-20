using System.Collections.Generic;
using UnityEngine;

// =============================================================
// LEVEL SOLVER
// =============================================================
//
// Finds the cheapest solution from the CURRENT board state, using
// the same rule model as DeadlockDetector (doors, switches, plates,
// portals, conveyors, ice, heavy pushes, buffer).
//
// Cost = moves spent. Portal taps and buffer store / return are
// free, exactly like in the game, so the search is a bucketed
// 0-1 BFS: the first solved state taken from the queue is optimal.
//
// Used by:
// - the Hint power-up and tutorial guidance (next action),
// - the editor par computation (optimal move count per level).
// =============================================================

public enum SolveResult
{
    Solved,
    Unsolvable,
    Unknown
}

public enum SolverActionType
{
    Move,
    PortalTap,
    BufferStore,
    BufferReturn
}

public struct SolverAction
{
    public SolverActionType type;

    // Index into GridManager.GetComponentsInChildren<Box>(true).
    public int pieceIndex;

    public Vector2Int direction;

    public override string ToString()
    {
        return type == SolverActionType.Move
            ? "Move piece " + pieceIndex + " " + DirName(direction)
            : type + " piece " + pieceIndex;
    }

    public string ToShortString()
    {
        switch (type)
        {
            case SolverActionType.Move: return pieceIndex + DirName(direction);
            case SolverActionType.PortalTap: return pieceIndex + "P";
            case SolverActionType.BufferStore: return pieceIndex + "S";
            default: return pieceIndex + "R";
        }
    }

    private static string DirName(Vector2Int d)
    {
        if (d == Vector2Int.up) return "U";
        if (d == Vector2Int.down) return "D";
        if (d == Vector2Int.left) return "L";
        return "R";
    }
}

public static class LevelSolver
{
    public static SolveResult Solve(
        LevelData levelData,
        GridManager gridManager,
        int maxStates,
        int maxMilliseconds,
        out List<SolverAction> path,
        out int cost)
    {
        return DeadlockDetector.Solve(
            levelData,
            gridManager,
            maxStates,
            maxMilliseconds,
            out path,
            out cost
        );
    }
}

public static partial class DeadlockDetector
{
    private static readonly Vector2Int[] SolverDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public static SolveResult Solve(
        LevelData levelData,
        GridManager gridManager,
        int maxStates,
        int maxMilliseconds,
        out List<SolverAction> path,
        out int cost)
    {
        path = new List<SolverAction>();
        cost = -1;

        if (levelData == null || gridManager == null)
            return SolveResult.Unknown;

        if (!TryBuildBoard(
                levelData,
                gridManager,
                out BoardData board,
                out SearchState initialState))
        {
            return SolveResult.Unknown;
        }

        if (board.HasBuffer && board.bufferPadIndex < 0)
            return SolveResult.Unknown;

        List<SearchState> states = new List<SearchState>();
        List<int> parents = new List<int>();
        List<SolverAction> actions = new List<SolverAction>();
        List<int> costs = new List<int>();
        List<string> keys = new List<string>();

        Dictionary<string, int> bestCost = new Dictionary<string, int>();
        List<Queue<int>> buckets = new List<Queue<int>>();

        void Push(SearchState state, int parent, SolverAction action, int newCost)
        {
            string key = BuildStateKey(state);

            if (bestCost.TryGetValue(key, out int known) && known <= newCost)
                return;

            bestCost[key] = newCost;

            int node = states.Count;
            states.Add(state);
            parents.Add(parent);
            actions.Add(action);
            costs.Add(newCost);
            keys.Add(key);

            while (buckets.Count <= newCost)
                buckets.Add(new Queue<int>());

            buckets[newCost].Enqueue(node);
        }

        Push(initialState, -1, default, 0);

        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        int explored = 0;

        for (int bucket = 0; bucket < buckets.Count; bucket++)
        {
            Queue<int> queue = buckets[bucket];

            while (queue.Count > 0)
            {
                int node = queue.Dequeue();

                // A cheaper copy of this state was queued later.
                if (bestCost[keys[node]] < costs[node])
                    continue;

                SearchState state = states[node];

                if (IsSolved(board, state))
                {
                    cost = costs[node];

                    for (int n = node; parents[n] >= 0; n = parents[n])
                        path.Add(actions[n]);

                    path.Reverse();
                    return SolveResult.Solved;
                }

                explored++;

                if (explored >= maxStates ||
                    stopwatch.ElapsedMilliseconds >= maxMilliseconds)
                {
                    return SolveResult.Unknown;
                }

                int baseCost = costs[node];

                for (int piece = 0; piece < board.pieces.Length; piece++)
                {
                    if (board.pieces[piece].type == BoxType.Crate ||
                        state.completed[piece])
                    {
                        continue;
                    }

                    foreach (Vector2Int direction in SolverDirections)
                    {
                        if (TryCreatePlayerMove(
                                board, state, piece, direction,
                                out SearchState moved))
                        {
                            Push(moved, node, new SolverAction
                            {
                                type = SolverActionType.Move,
                                pieceIndex = piece,
                                direction = direction
                            }, baseCost + 1);
                        }
                    }

                    if (TryCreatePortalReactivate(
                            board, state, piece, out SearchState teleported))
                    {
                        Push(teleported, node, new SolverAction
                        {
                            type = SolverActionType.PortalTap,
                            pieceIndex = piece
                        }, baseCost);
                    }

                    if (TryCreateBufferStore(
                            board, state, piece, out SearchState stored))
                    {
                        Push(stored, node, new SolverAction
                        {
                            type = SolverActionType.BufferStore,
                            pieceIndex = piece
                        }, baseCost);
                    }

                    if (TryCreateBufferReturn(
                            board, state, piece, out SearchState returned))
                    {
                        Push(returned, node, new SolverAction
                        {
                            type = SolverActionType.BufferReturn,
                            pieceIndex = piece
                        }, baseCost);
                    }
                }
            }
        }

        return SolveResult.Unsolvable;
    }
}
