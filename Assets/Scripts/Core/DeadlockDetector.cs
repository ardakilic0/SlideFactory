using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum DeadlockCheckResult
{
    Solvable,
    Deadlocked,
    Unknown
}

public static class DeadlockDetector
{
    private const int DefaultMaxStates = 25000;
    private const int DefaultMaxMilliseconds = 18;
    private const int HeavyPushLimit = 2;

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private sealed class BoardData
    {
        public int width;
        public int height;

        public bool[] walls;
        public bool[] bufferPads;

        // BufferManager supports one active UI pad at a time.
        public int bufferPadIndex = -1;
        public int bufferSlotCapacity;

        public int[] goalIDs;
        public int[] doorIDs;
        public int[] switchIDs;
        public int[] pressurePlateIDs;
        public int[] portalPairIndex;

        public Vector2Int[] conveyorDirections;

        public Dictionary<int, int> doorBitByID;

        public PieceInfo[] pieces;

        public bool HasBuffer =>
            bufferPads != null &&
            System.Array.Exists(
                bufferPads,
                value => value
            );
    }

    private struct PieceInfo
    {
        public BoxType type;
        public int id;
    }

    private sealed class SearchState
    {
        public int[] positions;
        public bool[] completed;
        public ulong permanentlyOpenDoorMask;

        public SearchState Clone()
        {
            return new SearchState
            {
                positions =
                    (int[])positions.Clone(),

                completed =
                    (bool[])completed.Clone(),

                permanentlyOpenDoorMask =
                    permanentlyOpenDoorMask
            };
        }
    }

    public static DeadlockCheckResult Check(
        LevelData levelData,
        GridManager gridManager,
        int maxStates = DefaultMaxStates,
        int maxMilliseconds = DefaultMaxMilliseconds)
    {
        if (levelData == null ||
            gridManager == null)
        {
            return DeadlockCheckResult.Unknown;
        }

        if (!TryBuildBoard(
                levelData,
                gridManager,
                out BoardData board,
                out SearchState initialState))
        {
            return DeadlockCheckResult.Unknown;
        }

        if (board.HasBuffer && board.bufferPadIndex < 0)
        {
            return DeadlockCheckResult.Unknown;
        }

        if (IsSolved(
                board,
                initialState))
        {
            return DeadlockCheckResult.Solvable;
        }

        Queue<SearchState> queue =
            new Queue<SearchState>();

        HashSet<string> visited =
            new HashSet<string>();

        string initialKey =
            BuildStateKey(initialState);

        visited.Add(initialKey);
        queue.Enqueue(initialState);

        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        int exploredStates = 0;

        while (queue.Count > 0)
        {
            if (exploredStates >= maxStates ||
                stopwatch.ElapsedMilliseconds >=
                maxMilliseconds)
            {
                // Arama bütçesi doldu.
                // False-positive fail vermiyoruz.
                return DeadlockCheckResult.Unknown;
            }

            SearchState state =
                queue.Dequeue();

            exploredStates++;

            for (int pieceIndex = 0;
                 pieceIndex < board.pieces.Length;
                 pieceIndex++)
            {
                PieceInfo piece =
                    board.pieces[pieceIndex];

                if (piece.type ==
                    BoxType.Crate)
                {
                    continue;
                }

                if (state.completed[pieceIndex])
                {
                    continue;
                }

                foreach (
                    Vector2Int direction
                    in CardinalDirections)
                {
                    if (TryCreatePlayerMove(
                            board,
                            state,
                            pieceIndex,
                            direction,
                            out SearchState nextState))
                    {
                        if (IsSolved(
                                board,
                                nextState))
                        {
                            return
                                DeadlockCheckResult.Solvable;
                        }

                        EnqueueIfNew(
                            nextState,
                            queue,
                            visited
                        );
                    }
                }

                if (TryCreatePortalReactivate(
                        board,
                        state,
                        pieceIndex,
                        out SearchState portalState))
                {
                    if (IsSolved(board, portalState))
                    {
                        return DeadlockCheckResult.Solvable;
                    }

                    EnqueueIfNew(portalState, queue, visited);
                }

                // A Box on the active BufferPad can be stored in a free UI slot.
                if (TryCreateBufferStore(
                        board,
                        state,
                        pieceIndex,
                        out SearchState storedState))
                {
                    if (IsSolved(board, storedState))
                    {
                        return DeadlockCheckResult.Solvable;
                    }

                    EnqueueIfNew(storedState, queue, visited);
                }

                // A UI-stored Box can return only when the active pad cell is empty.
                if (TryCreateBufferReturn(
                        board,
                        state,
                        pieceIndex,
                        out SearchState returnedState))
                {
                    if (IsSolved(board, returnedState))
                    {
                        return DeadlockCheckResult.Solvable;
                    }

                    EnqueueIfNew(returnedState, queue, visited);
                }
            }
        }

        return DeadlockCheckResult.Deadlocked;
    }

    private static bool TryBuildBoard(
        LevelData levelData,
        GridManager gridManager,
        out BoardData board,
        out SearchState initialState)
    {
        board = null;
        initialState = null;

        int width =
            levelData.Width;

        int height =
            levelData.Height;

        if (width <= 0 ||
            height <= 0)
        {
            return false;
        }

        int cellCount =
            width * height;

        BoardData builtBoard =
            new BoardData
            {
                width = width,
                height = height,

                walls =
                    new bool[cellCount],

                bufferPads =
                    new bool[cellCount],

                goalIDs =
                    CreateFilledArray(
                        cellCount,
                        -1
                    ),

                doorIDs =
                    CreateFilledArray(
                        cellCount,
                        -1
                    ),

                switchIDs =
                    CreateFilledArray(
                        cellCount,
                        -1
                    ),

                pressurePlateIDs =
                    CreateFilledArray(
                        cellCount,
                        -1
                    ),

                portalPairIndex =
                    CreateFilledArray(
                        cellCount,
                        -1
                    ),

                conveyorDirections =
                    new Vector2Int[cellCount],

                doorBitByID =
                    new Dictionary<int, int>()
            };

        Dictionary<int, List<int>>
            portalCellsByID =
                new Dictionary<int, List<int>>();

        HashSet<int> allDoorIDs =
            new HashSet<int>();

        for (int rowIndex = 0;
             rowIndex < height;
             rowIndex++)
        {
            string[] row =
                levelData.GetRowTokens(
                    rowIndex
                );

            if (row == null ||
                row.Length != width)
            {
                return false;
            }

            int y =
                height -
                1 -
                rowIndex;

            for (int x = 0;
                 x < width;
                 x++)
            {
                string token =
                    row[x];

                int index =
                    ToIndex(
                        x,
                        y,
                        width
                    );

                if (token == "#")
                {
                    builtBoard
                        .walls[index] =
                        true;

                    continue;
                }

                if (token == "BUF")
                {
                    builtBoard.bufferPads[index] = true;
                    builtBoard.bufferPadIndex = index;
                    continue;
                }

                if (token == "^")
                {
                    builtBoard
                        .conveyorDirections[index] =
                        Vector2Int.up;

                    continue;
                }

                if (token == "v")
                {
                    builtBoard
                        .conveyorDirections[index] =
                        Vector2Int.down;

                    continue;
                }

                if (token == "<")
                {
                    builtBoard
                        .conveyorDirections[index] =
                        Vector2Int.left;

                    continue;
                }

                if (token == ">")
                {
                    builtBoard
                        .conveyorDirections[index] =
                        Vector2Int.right;

                    continue;
                }

                if (TryReadID(
                        token,
                        'G',
                        out int goalID))
                {
                    builtBoard
                        .goalIDs[index] =
                        goalID;

                    continue;
                }

                if (TryReadID(
                        token,
                        'D',
                        out int doorID))
                {
                    builtBoard
                        .doorIDs[index] =
                        doorID;

                    allDoorIDs.Add(
                        doorID
                    );

                    continue;
                }

                if (TryReadID(
                        token,
                        'S',
                        out int switchID))
                {
                    builtBoard
                        .switchIDs[index] =
                        switchID;

                    allDoorIDs.Add(
                        switchID
                    );

                    continue;
                }

                if (TryReadID(
                        token,
                        'T',
                        out int pressurePlateID))
                {
                    builtBoard
                        .pressurePlateIDs[index] =
                        pressurePlateID;

                    allDoorIDs.Add(
                        pressurePlateID
                    );

                    continue;
                }

                if (TryReadID(
                        token,
                        'P',
                        out int portalID))
                {
                    if (!portalCellsByID
                        .ContainsKey(portalID))
                    {
                        portalCellsByID.Add(
                            portalID,
                            new List<int>()
                        );
                    }

                    portalCellsByID[
                        portalID
                    ].Add(index);
                }
            }
        }

        foreach (
            KeyValuePair<int, List<int>>
            pair in portalCellsByID)
        {
            List<int> portalCells =
                pair.Value;

            if (portalCells.Count != 2)
            {
                continue;
            }

            builtBoard
                .portalPairIndex[
                    portalCells[0]
                ] =
                portalCells[1];

            builtBoard
                .portalPairIndex[
                    portalCells[1]
                ] =
                portalCells[0];
        }

        List<int> sortedDoorIDs =
            new List<int>(
                allDoorIDs
            );

        sortedDoorIDs.Sort();

        if (sortedDoorIDs.Count > 63)
        {
            return false;
        }

        for (int i = 0;
             i < sortedDoorIDs.Count;
             i++)
        {
            builtBoard
                .doorBitByID.Add(
                    sortedDoorIDs[i],
                    i
                );
        }

        if (builtBoard.HasBuffer)
        {
            BufferManager bufferManager = BufferManager.Instance;

            if (bufferManager == null ||
                bufferManager.IsTransitioning ||
                bufferManager.CurrentPadCell == null)
            {
                return false;
            }

            Vector2Int padCoordinates =
                bufferManager.CurrentPadCell.Coordinates;

            if (padCoordinates.x < 0 ||
                padCoordinates.x >= width ||
                padCoordinates.y < 0 ||
                padCoordinates.y >= height)
            {
                return false;
            }

            int activePadIndex = ToIndex(
                padCoordinates.x,
                padCoordinates.y,
                width
            );

            if (!builtBoard.bufferPads[activePadIndex])
            {
                return false;
            }

            builtBoard.bufferPadIndex = activePadIndex;
            builtBoard.bufferSlotCapacity = bufferManager.SlotCapacity;

            if (bufferManager.StoredBoxCount > builtBoard.bufferSlotCapacity)
            {
                return false;
            }
        }

        Box[] liveBoxes =
            gridManager.GetComponentsInChildren<Box>(true);

        if (liveBoxes == null ||
            liveBoxes.Length == 0)
        {
            return false;
        }

        PieceInfo[] pieces =
            new PieceInfo[
                liveBoxes.Length
            ];

        int[] positions =
            new int[
                liveBoxes.Length
            ];

        bool[] completed =
            new bool[
                liveBoxes.Length
            ];

        for (int i = 0;
             i < liveBoxes.Length;
             i++)
        {
            Box box = liveBoxes[i];

            if (box == null)
            {
                return false;
            }

            pieces[i] = new PieceInfo
            {
                type = box.Type,
                id = box.BoxID
            };

            // A UI-buffered Box is inactive and has no CurrentCell.
            // It is represented by position -1 in the search state.
            if (box.CurrentCell == null)
            {
                if (!builtBoard.HasBuffer ||
                    BufferManager.Instance == null ||
                    !BufferManager.Instance.IsBoxStored(box))
                {
                    return false;
                }

                positions[i] = -1;
                completed[i] = false;
                continue;
            }

            Vector2Int coordinates = box.CurrentCell.Coordinates;

            if (coordinates.x < 0 ||
                coordinates.x >= width ||
                coordinates.y < 0 ||
                coordinates.y >= height)
            {
                return false;
            }

            int position = ToIndex(
                coordinates.x,
                coordinates.y,
                width
            );

            positions[i] = position;

            completed[i] =
                box.Type != BoxType.Crate &&
                !box.CanBeSelected &&
                builtBoard.goalIDs[position] == box.BoxID;
        }

        builtBoard.pieces =
            pieces;

        SearchState builtState =
            new SearchState
            {
                positions =
                    positions,

                completed =
                    completed,

                permanentlyOpenDoorMask =
                    0UL
            };

        foreach (
            int doorID
            in sortedDoorIDs)
        {
            if (!gridManager
                .IsDoorPermanentlyOpen(
                    doorID
                ))
            {
                continue;
            }

            SetDoorPermanentOpen(
                builtBoard,
                builtState,
                doorID
            );
        }

        board =
            builtBoard;

        initialState =
            builtState;

        return true;
    }

    private static bool TryCreateBufferStore(
        BoardData board,
        SearchState source,
        int pieceIndex,
        out SearchState result)
    {
        result = null;

        if (board.bufferPadIndex < 0 ||
            board.bufferSlotCapacity <= 0)
        {
            return false;
        }

        PieceInfo piece = board.pieces[pieceIndex];

        if (piece.type == BoxType.Crate ||
            source.completed[pieceIndex] ||
            source.positions[pieceIndex] != board.bufferPadIndex)
        {
            return false;
        }

        if (CountStoredBoxes(board, source) >= board.bufferSlotCapacity)
        {
            return false;
        }

        SearchState storedState = source.Clone();
        storedState.positions[pieceIndex] = -1;

        result = storedState;
        return true;
    }

    private static bool TryCreateBufferReturn(
        BoardData board,
        SearchState source,
        int pieceIndex,
        out SearchState result)
    {
        result = null;

        if (board.bufferPadIndex < 0 ||
            source.positions[pieceIndex] >= 0 ||
            source.completed[pieceIndex] ||
            board.pieces[pieceIndex].type == BoxType.Crate)
        {
            return false;
        }

        int returnPosition = board.bufferPadIndex;

        if (IsBlocked(board, source, returnPosition))
        {
            return false;
        }

        if (FindPieceAt(source, returnPosition, -1) >= 0)
        {
            return false;
        }

        // Runtime ReturnToGridBuffer places the Box on the pad without
        // invoking movement effects, so no ResolvePieceEffects call here.
        SearchState returnedState = source.Clone();
        returnedState.positions[pieceIndex] = returnPosition;

        result = returnedState;
        return true;
    }

    private static int CountStoredBoxes(
        BoardData board,
        SearchState state)
    {
        int count = 0;

        for (int i = 0; i < state.positions.Length; i++)
        {
            if (state.positions[i] < 0 &&
                board.pieces[i].type != BoxType.Crate)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryCreatePlayerMove(
        BoardData board,
        SearchState source,
        int pieceIndex,
        Vector2Int direction,
        out SearchState result)
    {
        result = null;

        PieceInfo piece =
            board.pieces[
                pieceIndex
            ];

        if (source.completed[pieceIndex] ||
            source.positions[pieceIndex] < 0 ||
            piece.type == BoxType.Crate)
        {
            return false;
        }

        if (piece.type ==
            BoxType.Ice)
        {
            return TryCreateIceMove(
                board,
                source,
                pieceIndex,
                direction,
                out result
            );
        }

        if (piece.type ==
            BoxType.Heavy)
        {
            return TryCreateHeavyMove(
                board,
                source,
                pieceIndex,
                direction,
                out result
            );
        }

        return TryCreateNormalMove(
            board,
            source,
            pieceIndex,
            direction,
            out result
        );
    }

    private static bool TryCreateNormalMove(
        BoardData board,
        SearchState source,
        int pieceIndex,
        Vector2Int direction,
        out SearchState result)
    {
        result = null;

        int currentPosition =
            source.positions[
                pieceIndex
            ];

        int targetPosition =
            GetNeighborIndex(
                board,
                currentPosition,
                direction
            );

        if (targetPosition < 0 ||
            IsBlocked(
                board,
                source,
                targetPosition))
        {
            return false;
        }

        int blockingPieceIndex =
            FindPieceAt(
                source,
                targetPosition,
                pieceIndex
            );

        // Normal move.
        if (blockingPieceIndex < 0)
        {
            SearchState movedState =
                source.Clone();

            movedState
                .positions[pieceIndex] =
                targetPosition;

            ResolvePieceEffects(
                board,
                movedState,
                pieceIndex,
                true
            );

            result =
                movedState;

            return true;
        }

        // Normal Box sadece crate itebilir.
        if (board
            .pieces[
                blockingPieceIndex
            ].type !=
            BoxType.Crate)
        {
            return false;
        }

        int pushedTargetPosition =
            GetNeighborIndex(
                board,
                targetPosition,
                direction
            );

        if (pushedTargetPosition < 0 ||
            IsBlocked(
                board,
                source,
                pushedTargetPosition))
        {
            return false;
        }

        if (FindPieceAt(
                source,
                pushedTargetPosition,
                -1) >= 0)
        {
            return false;
        }

        SearchState pushedState =
            source.Clone();

        pushedState
            .positions[pieceIndex] =
            targetPosition;

        pushedState
            .positions[
                blockingPieceIndex
            ] =
            pushedTargetPosition;

        // Runtime sırası:
        // önce player Box, sonra pushed crate.
        ResolvePieceEffects(
            board,
            pushedState,
            pieceIndex,
            true
        );

        ResolvePieceEffects(
            board,
            pushedState,
            blockingPieceIndex,
            true
        );

        result =
            pushedState;

        return true;
    }

    private static bool TryCreateHeavyMove(
        BoardData board,
        SearchState source,
        int pieceIndex,
        Vector2Int direction,
        out SearchState result)
    {
        result = null;

        int currentPosition =
            source.positions[
                pieceIndex
            ];

        int targetPosition =
            GetNeighborIndex(
                board,
                currentPosition,
                direction
            );

        if (targetPosition < 0 ||
            IsBlocked(
                board,
                source,
                targetPosition))
        {
            return false;
        }

        int firstOccupant =
            FindPieceAt(
                source,
                targetPosition,
                pieceIndex
            );

        // Önü boşsa normal Heavy move.
        if (firstOccupant < 0)
        {
            SearchState movedState =
                source.Clone();

            movedState
                .positions[pieceIndex] =
                targetPosition;

            ResolvePieceEffects(
                board,
                movedState,
                pieceIndex,
                true
            );

            result =
                movedState;

            return true;
        }

        List<int> crateIndices =
            new List<int>();

        int checkPosition =
            targetPosition;

        while (checkPosition >= 0)
        {
            int occupantIndex =
                FindPieceAt(
                    source,
                    checkPosition,
                    pieceIndex
                );

            if (occupantIndex < 0)
            {
                break;
            }

            if (board
                .pieces[
                    occupantIndex
                ].type !=
                BoxType.Crate)
            {
                return false;
            }

            crateIndices.Add(
                occupantIndex
            );

            if (crateIndices.Count >
                HeavyPushLimit)
            {
                return false;
            }

            checkPosition =
                GetNeighborIndex(
                    board,
                    checkPosition,
                    direction
                );
        }

        if (checkPosition < 0 ||
            IsBlocked(
                board,
                source,
                checkPosition))
        {
            return false;
        }

        if (FindPieceAt(
                source,
                checkPosition,
                -1) >= 0)
        {
            return false;
        }

        SearchState pushedState =
            source.Clone();

        int[] crateTargets =
            new int[
                crateIndices.Count
            ];

        for (int i = 0;
             i < crateIndices.Count;
             i++)
        {
            int crateIndex =
                crateIndices[i];

            crateTargets[i] =
                GetNeighborIndex(
                    board,
                    source.positions[
                        crateIndex
                    ],
                    direction
                );
        }

        pushedState
            .positions[pieceIndex] =
            targetPosition;

        for (int i = 0;
             i < crateIndices.Count;
             i++)
        {
            pushedState
                .positions[
                    crateIndices[i]
                ] =
                crateTargets[i];
        }

        ResolvePieceEffects(
            board,
            pushedState,
            pieceIndex,
            true
        );

        // Runtime da crate effectlerini
        // yakından uzağa işler.
        foreach (
            int crateIndex
            in crateIndices)
        {
            ResolvePieceEffects(
                board,
                pushedState,
                crateIndex,
                true
            );
        }

        result =
            pushedState;

        return true;
    }

    private static bool TryCreateIceMove(
        BoardData board,
        SearchState source,
        int pieceIndex,
        Vector2Int direction,
        out SearchState result)
    {
        result = null;

        int currentPosition =
            source.positions[
                pieceIndex
            ];

        int checkPosition =
            GetNeighborIndex(
                board,
                currentPosition,
                direction
            );

        if (checkPosition < 0 ||
            IsBlocked(
                board,
                source,
                checkPosition))
        {
            return false;
        }

        if (FindPieceAt(
                source,
                checkPosition,
                pieceIndex) >= 0)
        {
            return false;
        }

        int finalPosition =
            checkPosition;

        while (true)
        {
            int nextPosition =
                GetNeighborIndex(
                    board,
                    finalPosition,
                    direction
                );

            if (nextPosition < 0)
            {
                break;
            }

            if (IsBlocked(
                    board,
                    source,
                    nextPosition))
            {
                break;
            }

            if (FindPieceAt(
                    source,
                    nextPosition,
                    pieceIndex) >= 0)
            {
                break;
            }

            finalPosition =
                nextPosition;
        }

        SearchState iceState =
            source.Clone();

        iceState
            .positions[pieceIndex] =
            finalPosition;

        ResolvePieceEffects(
            board,
            iceState,
            pieceIndex,
            true
        );

        result =
            iceState;

        return true;
    }

    private static bool TryCreatePortalReactivate(
        BoardData board,
        SearchState source,
        int pieceIndex,
        out SearchState result)
    {
        result = null;

        if (source.completed[pieceIndex])
        {
            return false;
        }

        if (board
            .pieces[
                pieceIndex
            ].type ==
            BoxType.Crate)
        {
            return false;
        }

        int currentPosition =
            source.positions[
                pieceIndex
            ];

        if (currentPosition < 0 ||
            currentPosition >=
            board
                .portalPairIndex
                .Length)
        {
            return false;
        }

        int destinationPosition =
            board
                .portalPairIndex[
                    currentPosition
                ];

        if (destinationPosition < 0)
        {
            return false;
        }

        if (IsBlocked(
                board,
                source,
                destinationPosition))
        {
            return false;
        }

        if (FindPieceAt(
                source,
                destinationPosition,
                pieceIndex) >= 0)
        {
            return false;
        }

        SearchState portalState =
            source.Clone();

        portalState
            .positions[pieceIndex] =
            destinationPosition;

        // Manual portal retry sonrasında
        // exit portal tekrar otomatik tetiklenmez.
        ResolvePieceEffects(
            board,
            portalState,
            pieceIndex,
            false
        );

        result =
            portalState;

        return true;
    }

    private static void ResolvePieceEffects(
        BoardData board,
        SearchState state,
        int pieceIndex,
        bool allowPortal)
    {
        if (pieceIndex < 0 ||
            pieceIndex >=
            board.pieces.Length)
        {
            return;
        }

        if (state.completed[pieceIndex])
        {
            return;
        }

        int position =
            state.positions[
                pieceIndex
            ];

        if (position < 0)
        {
            return;
        }

        // Floor Switch.
        int switchID =
            board.switchIDs[
                position
            ];

        if (switchID >= 0)
        {
            SetDoorPermanentOpen(
                board,
                state,
                switchID
            );
        }

        // Portal.
        if (allowPortal)
        {
            int portalDestination =
                board
                    .portalPairIndex[
                        position
                    ];

            if (portalDestination >= 0 &&
                !IsBlocked(
                    board,
                    state,
                    portalDestination) &&
                FindPieceAt(
                    state,
                    portalDestination,
                    pieceIndex) < 0)
            {
                state
                    .positions[
                        pieceIndex
                    ] =
                    portalDestination;

                ResolvePieceEffects(
                    board,
                    state,
                    pieceIndex,
                    false
                );

                return;
            }
        }

        // Goal.
        CheckGoalCompletion(
            board,
            state,
            pieceIndex
        );

        if (state.completed[pieceIndex])
        {
            return;
        }

        position =
            state.positions[
                pieceIndex
            ];

        // Conveyor.
        if (board
            .conveyorDirections[
                position
            ] !=
            Vector2Int.zero)
        {
            ResolveConveyor(
                board,
                state,
                pieceIndex
            );
        }
    }

    private static void ResolveConveyor(
        BoardData board,
        SearchState state,
        int pieceIndex)
    {
        HashSet<int>
            visitedConveyorCells =
                new HashSet<int>();

        while (!state.completed[
                   pieceIndex
               ])
        {
            int currentPosition =
                state.positions[
                    pieceIndex
                ];

            Vector2Int direction =
                board
                    .conveyorDirections[
                        currentPosition
                    ];

            if (direction ==
                Vector2Int.zero)
            {
                break;
            }

            if (!visitedConveyorCells
                .Add(currentPosition))
            {
                break;
            }

            int targetPosition =
                GetNeighborIndex(
                    board,
                    currentPosition,
                    direction
                );

            if (targetPosition < 0)
            {
                break;
            }

            if (IsBlocked(
                    board,
                    state,
                    targetPosition))
            {
                break;
            }

            if (FindPieceAt(
                    state,
                    targetPosition,
                    pieceIndex) >= 0)
            {
                break;
            }

            state
                .positions[
                    pieceIndex
                ] =
                targetPosition;

            int switchID =
                board
                    .switchIDs[
                        targetPosition
                    ];

            if (switchID >= 0)
            {
                SetDoorPermanentOpen(
                    board,
                    state,
                    switchID
                );
            }

            int portalDestination =
                board
                    .portalPairIndex[
                        targetPosition
                    ];

            if (portalDestination >= 0 &&
                !IsBlocked(
                    board,
                    state,
                    portalDestination) &&
                FindPieceAt(
                    state,
                    portalDestination,
                    pieceIndex) < 0)
            {
                state
                    .positions[
                        pieceIndex
                    ] =
                    portalDestination;

                // Conveyor -> Portal runtime'da
                // teleport sonrası conveyor zinciri biter.
                CheckGoalCompletion(
                    board,
                    state,
                    pieceIndex
                );

                break;
            }

            CheckGoalCompletion(
                board,
                state,
                pieceIndex
            );

            if (state.completed[
                    pieceIndex
                ])
            {
                break;
            }
        }
    }

    private static void CheckGoalCompletion(
        BoardData board,
        SearchState state,
        int pieceIndex)
    {
        PieceInfo piece =
            board.pieces[
                pieceIndex
            ];

        if (piece.type ==
            BoxType.Crate)
        {
            return;
        }

        int position =
            state.positions[
                pieceIndex
            ];

        if (position < 0)
        {
            return;
        }

        if (board
            .goalIDs[position] ==
            piece.id)
        {
            state
                .completed[
                    pieceIndex
                ] =
                true;
        }
    }

    private static bool IsSolved(
        BoardData board,
        SearchState state)
    {
        bool foundGoalPiece =
            false;

        for (int i = 0;
             i < board.pieces.Length;
             i++)
        {
            if (board
                .pieces[i]
                .type ==
                BoxType.Crate)
            {
                continue;
            }

            foundGoalPiece =
                true;

            if (!state.completed[i])
            {
                return false;
            }
        }

        return foundGoalPiece;
    }

    private static bool IsBlocked(
        BoardData board,
        SearchState state,
        int position)
    {
        if (position < 0 ||
            position >=
            board.walls.Length)
        {
            return true;
        }

        if (board.walls[position])
        {
            return true;
        }

        int doorID =
            board.doorIDs[
                position
            ];

        if (doorID >= 0 &&
            !IsDoorOpen(
                board,
                state,
                doorID))
        {
            return true;
        }

        return false;
    }

    private static bool IsDoorOpen(
        BoardData board,
        SearchState state,
        int doorID)
    {
        if (board
            .doorBitByID
            .TryGetValue(
                doorID,
                out int bitIndex))
        {
            ulong bit =
                1UL << bitIndex;

            if ((state
                 .permanentlyOpenDoorMask &
                 bit) != 0UL)
            {
                return true;
            }
        }

        // Pressure Plate geçici olarak kapıyı açabilir.
        for (int i = 0;
             i < state.positions.Length;
             i++)
        {
            int position =
                state.positions[i];

            if (position < 0)
            {
                continue;
            }

            if (board
                .pressurePlateIDs[
                    position
                ] ==
                doorID)
            {
                return true;
            }
        }

        return false;
    }

    private static void SetDoorPermanentOpen(
        BoardData board,
        SearchState state,
        int doorID)
    {
        if (!board
            .doorBitByID
            .TryGetValue(
                doorID,
                out int bitIndex))
        {
            return;
        }

        state
            .permanentlyOpenDoorMask |=
            1UL << bitIndex;
    }

    private static int FindPieceAt(
        SearchState state,
        int position,
        int ignoredPieceIndex)
    {
        for (int i = 0;
             i < state.positions.Length;
             i++)
        {
            if (i ==
                ignoredPieceIndex)
            {
                continue;
            }

            if (state.positions[i] ==
                position)
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetNeighborIndex(
        BoardData board,
        int currentPosition,
        Vector2Int direction)
    {
        if (currentPosition < 0)
        {
            return -1;
        }

        int x =
            currentPosition %
            board.width;

        int y =
            currentPosition /
            board.width;

        int nextX =
            x +
            direction.x;

        int nextY =
            y +
            direction.y;

        if (nextX < 0 ||
            nextX >= board.width ||
            nextY < 0 ||
            nextY >= board.height)
        {
            return -1;
        }

        return ToIndex(
            nextX,
            nextY,
            board.width
        );
    }

    private static void EnqueueIfNew(
        SearchState state,
        Queue<SearchState> queue,
        HashSet<string> visited)
    {
        string key =
            BuildStateKey(
                state
            );

        if (!visited.Add(key))
        {
            return;
        }

        queue.Enqueue(
            state
        );
    }

    private static string BuildStateKey(
        SearchState state)
    {
        StringBuilder builder =
            new StringBuilder(
                state
                    .positions
                    .Length *
                5 +
                24
            );

        builder.Append(
            state
                .permanentlyOpenDoorMask
        );

        builder.Append('|');

        for (int i = 0;
             i < state.positions.Length;
             i++)
        {
            builder.Append(
                state.positions[i]
            );

            builder.Append(
                state.completed[i]
                    ? '1'
                    : '0'
            );

            builder.Append(',');
        }

        return builder.ToString();
    }

    private static bool TryReadID(
        string token,
        char prefix,
        out int id)
    {
        id = -1;

        if (string.IsNullOrEmpty(
                token) ||
            token.Length < 2 ||
            token[0] != prefix)
        {
            return false;
        }

        return int.TryParse(
            token.Substring(1),
            out id
        );
    }

    private static int[] CreateFilledArray(
        int length,
        int value)
    {
        int[] array =
            new int[length];

        for (int i = 0;
             i < array.Length;
             i++)
        {
            array[i] =
                value;
        }

        return array;
    }

    private static int ToIndex(
        int x,
        int y,
        int width)
    {
        return
            y * width +
            x;
    }
}