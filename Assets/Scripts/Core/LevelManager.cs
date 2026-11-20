using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance
    {
        get;
        private set;
    }

    // =========================================================
    // SAVE
    // =========================================================

    private const string CurrentLevelSaveKey =
        "CurrentLevelIndex";

    // =========================================================
    // DEVELOPMENT
    // =========================================================

    [Header("Development")]

    [Tooltip(
        "ON: Starting Level kullanilir ve save etkilenmez.\n" +
        "OFF: Oyuncunun kayitli progression'i yuklenir."
    )]
    [SerializeField]
    private bool useStartingLevelForTesting = false;

    [Tooltip(
        "0 = Level 1, 1 = Level 2, 2 = Level 3..."
    )]
    [SerializeField]
    private int startingLevel = 0;

    // =========================================================
    // FAIL SYSTEM
    // =========================================================

    [Header("Fail System")]
    [SerializeField]
    private float gameStateSettleDelay = 0.45f;

    // =========================================================
    // REFERENCES / STATE
    // =========================================================

    private GridManager gridManager;

    private int currentLevelIndex;

    private int totalBoxes;
    private int completedBoxes;

    private int movesRemaining;

    private bool isLevelEnded;

    // Move limit of the current attempt (level limit + bought moves).
    private int moveBudget;

    // Undo: pieces glide back to their old cells for a moment.
    private Coroutine restoreTween;
    private bool restoreAnimating;

    // =========================================================
    // EVENTS (tutorial, HUD, power-ups)
    // =========================================================

    public static event Action<int> LevelLoaded;
    public static event Action<int, LevelReward> LevelCompleted;
    public static event Action<int, LevelFailReason> LevelFailed;
    public static event Action BoardRestored;
    public static event Action MovesChanged;
    public static event Action<int> OutOfLives;

    private Coroutine loadRoutine;
    private Coroutine gameStateCheckRoutine;

    public int MovesRemaining =>
        movesRemaining;

    public bool IsInputLocked =>
        isLevelEnded ||
        restoreAnimating ||
        movesRemaining <= 0 ||
        (BufferManager.Instance != null &&
         BufferManager.Instance.IsTransitioning);

    public int CurrentLevelIndex =>
        currentLevelIndex;

    public bool IsLevelEnded =>
        isLevelEnded;

    public bool IsLoading =>
        loadRoutine != null;

    public bool IsBoardBusy =>
        loadRoutine != null || AnyBoxIsBusy();

    public int MoveBudget =>
        moveBudget;

    public int MovesUsed =>
        moveBudget - movesRemaining;

    // Undo may run when the board rests: during play, or on the
    // fail screen (to step back out of a dead end).
    public bool CanRestoreSnapshot =>
        loadRoutine == null &&
        !AnyBoxIsBusy() &&
        (!isLevelEnded || (LevelFailUI.Instance != null &&
                           LevelFailUI.Instance.IsShowing));

    public bool IsDevelopmentMode =>
        useStartingLevelForTesting;

    private static readonly Vector2Int[]
        CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Instance =
            this;
    }

    private void Start()
    {
        gridManager =
            GridManager.Instance;

        if (gridManager == null)
        {
            Debug.LogError(
                "LevelManager: GridManager bulunamadı."
            );

            return;
        }

        // =====================================================
        // DEVELOPMENT MODE
        // =====================================================

        if (useStartingLevelForTesting)
        {
            currentLevelIndex =
                Mathf.Clamp(
                    startingLevel,
                    0,
                    LevelDatabase.GetLevelCount() - 1
                );

            Debug.Log(
                "DEVELOPMENT MODE - Starting Level: " +
                (currentLevelIndex + 1)
            );
        }

        // =====================================================
        // NORMAL GAME
        // =====================================================

        else
        {
            currentLevelIndex =
                LoadSavedLevelIndex();

            Debug.Log(
                "Saved Progress Loaded - Level: " +
                (currentLevelIndex + 1)
            );
        }

        LoadLevel(
            currentLevelIndex
        );
    }

    // =========================================================
    // SAVE SYSTEM
    // =========================================================

    private int LoadSavedLevelIndex()
    {
        int savedLevelIndex =
            PlayerPrefs.GetInt(
                CurrentLevelSaveKey,
                0
            );

        savedLevelIndex =
            Mathf.Clamp(
                savedLevelIndex,
                0,
                LevelDatabase.GetLevelCount() - 1
            );

        return savedLevelIndex;
    }

    private void SaveProgress(
        int levelIndex)
    {
        // Development mode gercek save'i bozmaz.
        if (useStartingLevelForTesting)
        {
            return;
        }

        int clampedLevelIndex =
            Mathf.Clamp(
                levelIndex,
                0,
                LevelDatabase.GetLevelCount() - 1
            );

        int currentlySaved =
            PlayerPrefs.GetInt(
                CurrentLevelSaveKey,
                0
            );

        // Progression geriye gitmesin.
        int levelToSave =
            Mathf.Max(
                currentlySaved,
                clampedLevelIndex
            );

        PlayerPrefs.SetInt(
            CurrentLevelSaveKey,
            levelToSave
        );

        PlayerPrefs.Save();

        Debug.Log(
            "Progress Saved - Level: " +
            (levelToSave + 1)
        );
    }

    [ContextMenu("Reset Saved Progress")]
    public void ResetSavedProgress()
    {
        // Everything: level, stars, coins, power-ups, tutorials, lives.
        ProgressManager.ResetAll();

        Debug.Log(
            "Saved progression reset. Next normal start = Level 1."
        );
    }

    // =========================================================
    // LOAD LEVEL
    // =========================================================

    public void LoadLevel(
        int levelIndex)
    {
        if (levelIndex < 0 ||
            levelIndex >=
            LevelDatabase.GetLevelCount())
        {
            Debug.LogError(
                "Invalid level index: " +
                levelIndex
            );

            return;
        }

        // Lives (energy): only when EconomyConfig.LivesEnabled.
        if (!useStartingLevelForTesting &&
            !LivesManager.CanPlay(levelIndex))
        {
            SafeEvents.Raise(OutOfLives, levelIndex);
            return;
        }

        if (loadRoutine != null)
        {
            StopCoroutine(
                loadRoutine
            );
        }

        loadRoutine =
            StartCoroutine(
                LoadLevelRoutine(
                    levelIndex
                )
            );
    }

    private IEnumerator LoadLevelRoutine(
        int levelIndex)
    {
        currentLevelIndex =
            levelIndex;

        totalBoxes =
            0;

        completedBoxes =
            0;

        isLevelEnded =
            false;

        StopGameStateCheck();

        if (LevelFailUI.Instance != null)
        {
            LevelFailUI.Instance
                .HideFail();
        }

        // =====================================================
        // MOVE LIMIT
        // =====================================================

        movesRemaining =
            LevelDatabase.GetMoveLimit(
                levelIndex
            );

        moveBudget =
            movesRemaining;

        if (restoreTween != null)
            StopCoroutine(restoreTween);

        restoreTween = null;
        restoreAnimating = false;

        UndoSystem.Clear();

        UpdateMoveUI();

        // =====================================================
        // UI
        // =====================================================

        if (UIManager.Instance != null)
        {
            UIManager.Instance
                .HideLevelComplete();

            UIManager.Instance
                .SetLevelNumber(
                    currentLevelIndex + 1
                );
        }

        // =====================================================
        // BUFFER
        // =====================================================

        if (BufferManager.Instance != null)
        {
            BufferManager.Instance
                .ResetForLevel();
        }

        // =====================================================
        // CLEAR
        // =====================================================

        gridManager.ClearEverything();

        yield return null;

        // =====================================================
        // LEVEL DATA
        // =====================================================

        LevelData levelData =
            LevelDatabase.GetLevel(
                levelIndex
            );

        if (levelData == null)
        {
            Debug.LogError(
                "Level bulunamadı: " +
                levelIndex
            );

            loadRoutine =
                null;

            yield break;
        }

        // =====================================================
        // GRID
        // =====================================================

        gridManager.GenerateGrid(
            levelData.Width,
            levelData.Height
        );

        if (GridCameraController.Instance != null)
        {
            GridCameraController.Instance
                .FitToGrid(
                    levelData.Width,
                    levelData.Height
                );
        }

        // =====================================================
        // CONTENT
        // =====================================================

        AudioManager.SuppressSfx(0.25f);
        CreateLevel(
            levelData
        );

        Debug.Log(
            "Level " +
            (currentLevelIndex + 1) +
            " loaded - Grid: " +
            levelData.Width +
            "x" +
            levelData.Height +
            " - Boxes: " +
            totalBoxes +
            " - Moves: " +
            movesRemaining
        );

        loadRoutine =
            null;

        ScheduleGameStateCheck();

        SafeEvents.Raise(LevelLoaded, 
            currentLevelIndex
        );
    }

    // =========================================================
    // CREATE LEVEL
    // =========================================================

    private void CreateLevel(
        LevelData levelData)
    {
        for (
            int rowIndex = 0;
            rowIndex < levelData.Height;
            rowIndex++)
        {
            string[] row =
                levelData.GetRowTokens(
                    rowIndex
                );

            for (
                int x = 0;
                x < row.Length;
                x++)
            {
                string token =
                    row[x];

                int y =
                    levelData.Height -
                    1 -
                    rowIndex;

                Vector2Int coordinates =
                    new Vector2Int(
                        x,
                        y
                    );

                // =================================================
                // EMPTY
                // =================================================

                if (token == ".")
                {
                    continue;
                }

                // =================================================
                // WALL
                // =================================================

                if (token == "#")
                {
                    gridManager.CreateWall(
                        coordinates
                    );

                    continue;
                }

                // =================================================
                // DOOR
                // =================================================

                if (token.StartsWith("D"))
                {
                    if (!TryGetID(
                            token,
                            out int doorID))
                    {
                        Debug.LogError(
                            "Geçersiz Door token: " +
                            token
                        );

                        continue;
                    }

                    gridManager.CreateDoor(
                        coordinates,
                        doorID
                    );

                    continue;
                }

                // =================================================
                // FLOOR SWITCH
                // =================================================

                if (token.StartsWith("S"))
                {
                    if (!TryGetID(
                            token,
                            out int switchID))
                    {
                        Debug.LogError(
                            "Geçersiz Switch token: " +
                            token
                        );

                        continue;
                    }

                    gridManager
                        .CreateFloorSwitch(
                            coordinates,
                            switchID
                        );

                    continue;
                }

                // =================================================
                // PORTAL
                // =================================================

                if (token.StartsWith("P"))
                {
                    if (!TryGetID(
                            token,
                            out int portalID))
                    {
                        Debug.LogError(
                            "Geçersiz Portal token: " +
                            token
                        );

                        continue;
                    }

                    gridManager.CreatePortal(
                        coordinates,
                        portalID
                    );

                    continue;
                }

                // =================================================
                // CRATE
                // =================================================

                if (token == "C")
                {
                    Box crate =
                        gridManager.SpawnBox(
                            coordinates
                        );

                    if (crate != null)
                    {
                        crate.SetBoxType(
                            BoxType.Crate
                        );
                    }

                    continue;
                }

                // =================================================
                // ICE
                // =================================================

                if (token.StartsWith("I"))
                {
                    if (!TryGetID(
                            token,
                            out int iceBoxID))
                    {
                        Debug.LogError(
                            "Geçersiz Ice Box token: " +
                            token
                        );

                        continue;
                    }

                    Box iceBox =
                        gridManager.SpawnBox(
                            coordinates
                        );

                    if (iceBox == null)
                    {
                        continue;
                    }

                    iceBox.SetBoxID(
                        iceBoxID
                    );

                    iceBox.SetBoxType(
                        BoxType.Ice
                    );

                    totalBoxes++;

                    continue;
                }

                // =================================================
                // HEAVY
                // =================================================

                if (token.StartsWith("H"))
                {
                    if (!TryGetID(
                            token,
                            out int heavyBoxID))
                    {
                        Debug.LogError(
                            "Geçersiz Heavy Box token: " +
                            token
                        );

                        continue;
                    }

                    Box heavyBox =
                        gridManager.SpawnBox(
                            coordinates
                        );

                    if (heavyBox == null)
                    {
                        continue;
                    }

                    heavyBox.SetBoxID(
                        heavyBoxID
                    );

                    heavyBox.SetBoxType(
                        BoxType.Heavy
                    );

                    totalBoxes++;

                    continue;
                }

                // =================================================
                // BUFFER
                // =================================================

                if (token == "BUF")
                {
                    gridManager
                        .CreateBufferPad(
                            coordinates
                        );

                    continue;
                }

                // =================================================
                // NORMAL BOX
                // =================================================

                if (token.StartsWith("B"))
                {
                    if (!TryGetID(
                            token,
                            out int boxID))
                    {
                        Debug.LogError(
                            "Geçersiz Box token: " +
                            token
                        );

                        continue;
                    }

                    Box box =
                        gridManager.SpawnBox(
                            coordinates
                        );

                    if (box == null)
                    {
                        continue;
                    }

                    box.SetBoxType(
                        BoxType.Normal
                    );

                    box.SetBoxID(
                        boxID
                    );

                    totalBoxes++;

                    continue;
                }

                // =================================================
                // GOAL
                // =================================================

                if (token.StartsWith("G"))
                {
                    if (!TryGetID(
                            token,
                            out int goalID))
                    {
                        Debug.LogError(
                            "Geçersiz Goal token: " +
                            token
                        );

                        continue;
                    }

                    gridManager.CreateGoal(
                        coordinates,
                        goalID
                    );

                    continue;
                }

                // =================================================
                // CONVEYOR
                // =================================================

                if (token == "^")
                {
                    gridManager.CreateConveyor(
                        coordinates,
                        ConveyorDirection.Up
                    );

                    continue;
                }

                if (token == "v")
                {
                    gridManager.CreateConveyor(
                        coordinates,
                        ConveyorDirection.Down
                    );

                    continue;
                }

                if (token == "<")
                {
                    gridManager.CreateConveyor(
                        coordinates,
                        ConveyorDirection.Left
                    );

                    continue;
                }

                if (token == ">")
                {
                    gridManager.CreateConveyor(
                        coordinates,
                        ConveyorDirection.Right
                    );

                    continue;
                }

                // =================================================
                // PRESSURE PLATE
                // =================================================

                if (token.StartsWith("T"))
                {
                    if (!TryGetID(
                            token,
                            out int pressurePlateID))
                    {
                        Debug.LogError(
                            "Geçersiz Pressure Plate token: " +
                            token
                        );

                        continue;
                    }

                    gridManager
                        .CreatePressurePlate(
                            coordinates,
                            pressurePlateID
                        );

                    continue;
                }

                // =================================================
                // UNKNOWN
                // =================================================

                Debug.LogWarning(
                    "Bilinmeyen level token: " +
                    token
                );
            }
        }
    }

    private bool TryGetID(
        string token,
        out int id)
    {
        id =
            -1;

        if (token.Length < 2)
        {
            return false;
        }

        return int.TryParse(
            token.Substring(1),
            out id
        );
    }

    // =========================================================
    // PLAYER ACTION
    // =========================================================

    public void RegisterSuccessfulPlayerAction()
    {
        if (isLevelEnded)
        {
            return;
        }

        if (movesRemaining <= 0)
        {
            return;
        }

        movesRemaining--;

        UpdateMoveUI();

        Debug.Log(
            "Moves Remaining: " +
            movesRemaining
        );

        ScheduleGameStateCheck();
    }

    // Buffer store/return actions do not consume a move, but they change the board state.
    public void NotifyBoardStateChanged()
    {
        if (isLevelEnded)
            return;

        ScheduleGameStateCheck();
    }

    private void UpdateMoveUI()
    {
        SafeEvents.Raise(MovesChanged);

        if (MoveCounterUI.Instance == null)
        {
            return;
        }

        MoveCounterUI.Instance.SetMoves(
            movesRemaining
        );
    }

    // =========================================================
    // GAME STATE
    // =========================================================

    private void ScheduleGameStateCheck()
    {
        if (isLevelEnded)
        {
            return;
        }

        StopGameStateCheck();

        gameStateCheckRoutine =
            StartCoroutine(
                WaitAndEvaluateGameState()
            );
    }

    private void StopGameStateCheck()
    {
        if (gameStateCheckRoutine == null)
        {
            return;
        }

        StopCoroutine(
            gameStateCheckRoutine
        );

        gameStateCheckRoutine =
            null;
    }

    private IEnumerator WaitAndEvaluateGameState()
    {
        yield return null;

        while (true)
        {
            while (AnyBoxIsBusy())
            {
                yield return null;
            }

            if (gameStateSettleDelay > 0f)
            {
                yield return
                    new WaitForSecondsRealtime(
                        gameStateSettleDelay
                    );
            }

            if (AnyBoxIsBusy())
            {
                continue;
            }

            break;
        }

        yield return null;

        EvaluateGameState();

        gameStateCheckRoutine =
            null;
    }

    private void EvaluateGameState()
    {
        if (isLevelEnded)
        {
            return;
        }

        // =====================================================
        // WIN FIRST
        // =====================================================

        if (completedBoxes >=
            totalBoxes &&
            totalBoxes > 0)
        {
            CompleteLevel();
            return;
        }

        // =====================================================
        // OUT OF MOVES
        // =====================================================

        if (movesRemaining <= 0)
        {
            FailLevel(
                LevelFailReason.OutOfMoves
            );

            return;
        }

        // =====================================================
        // HARD DEADLOCK
        // =====================================================

        if (!HasAnyValidAction())
        {
            FailLevel(
                LevelFailReason.Deadlock
            );

            return;
        }

        // =====================================================
        // SOFT DEADLOCK
        // =====================================================

        LevelData currentLevelData =
            LevelDatabase.GetLevel(
                currentLevelIndex
            );

        DeadlockCheckResult
            deadlockResult =
                DeadlockDetector.Check(
                    currentLevelData,
                    gridManager
                );

        if (deadlockResult ==
            DeadlockCheckResult.Deadlocked)
        {
            FailLevel(
                LevelFailReason.Deadlock
            );
        }
    }

    private bool HasAnyValidAction()
    {
        if (gridManager == null)
            return true;

        BufferManager bufferManager = BufferManager.Instance;

        // A UI-stored Box can return only if the active BufferPad cell is free.
        if (bufferManager != null &&
            bufferManager.CanReturnAnyStoredBox)
        {
            return true;
        }

        Box[] boxes =
            gridManager.GetComponentsInChildren<Box>(true);

        foreach (Box box in boxes)
        {
            if (box == null || !box.gameObject.activeInHierarchy)
                continue;

            if (box.CanReactivatePortal())
                return true;

            // A Box on the active pad can be stored only if a UI slot is free.
            if (bufferManager != null &&
                bufferManager.CanStoreBox(box))
            {
                return true;
            }

            foreach (Vector2Int direction in CardinalDirections)
            {
                if (box.CanMove(direction))
                    return true;
            }
        }

        return false;
    }

    private bool AnyBoxIsBusy()
    {
        if (restoreAnimating)
            return true;

        // Wait until Buffer store/return animations have fully completed.
        if (BufferManager.Instance != null &&
            BufferManager.Instance.IsTransitioning)
        {
            return true;
        }

        if (gridManager == null)
            return false;

        Box[] boxes =
            gridManager.GetComponentsInChildren<Box>(true);

        foreach (Box box in boxes)
        {
            if (box != null && box.IsBusy)
                return true;
        }

        return false;
    }

    // =========================================================
    // FAIL
    // =========================================================

    private void FailLevel(
        LevelFailReason reason)
    {
        if (isLevelEnded)
        {
            return;
        }

        isLevelEnded =
            true;

        StopGameStateCheck();

        LivesManager.OnLevelFailed(
            currentLevelIndex
        );

        SafeEvents.Raise(LevelFailed, 
            currentLevelIndex,
            reason
        );

        if (LevelFailUI.Instance != null)
        {
            LevelFailUI.Instance
                .ShowFail(
                    reason
                );
        }
        else
        {
            Debug.LogWarning(
                "LevelManager: LevelFailUI bulunamadı. Fail reason: " +
                reason
            );
        }
    }

    // =========================================================
    // COMPLETE BOX
    // =========================================================

    public void CompleteBox(
        Box box)
    {
        if (isLevelEnded)
        {
            return;
        }

        completedBoxes++;

        Debug.Log(
            "Box completed: " +
            completedBoxes +
            "/" +
            totalBoxes
        );

        if (completedBoxes >=
            totalBoxes)
        {
            CompleteLevel();
        }
    }

    // =========================================================
    // COMPLETE LEVEL
    // =========================================================

    private void CompleteLevel()
    {
        if (isLevelEnded)
        {
            return;
        }

        isLevelEnded =
            true;

        StopGameStateCheck();

        // =====================================================
        // STARS / REWARDS
        // =====================================================

        int stars =
            LevelDatabase.GetStars(
                currentLevelIndex,
                MovesUsed,
                moveBudget > LevelDatabase.GetMoveLimit(currentLevelIndex)
            );

        LevelReward reward =
            useStartingLevelForTesting
                ? LevelReward.Preview(currentLevelIndex, stars)
                : ProgressManager.RecordLevelResult(
                    currentLevelIndex,
                    stars
                );

        SafeEvents.Raise(LevelCompleted, 
            currentLevelIndex,
            reward
        );

        // =====================================================
        // SAVE NEXT LEVEL
        // =====================================================

        if (!useStartingLevelForTesting)
        {
            int nextLevelIndex =
                currentLevelIndex + 1;

            // Final level degilse sonraki level unlock edilir.
            if (nextLevelIndex <
                LevelDatabase.GetLevelCount())
            {
                SaveProgress(
                    nextLevelIndex
                );
            }
            else
            {
                // Son level tamamlandiysa
                // son level save'de tutulur.
                SaveProgress(
                    currentLevelIndex
                );
            }
        }

        // =====================================================
        // UI
        // =====================================================

        if (UIManager.Instance != null)
        {
            bool isFinalLevel =
                currentLevelIndex >=
                LevelDatabase.GetLevelCount() - 1;

            UIManager.Instance
                .ShowLevelComplete(
                    isFinalLevel
                );
        }
    }

    // =========================================================
    // EXTRA MOVES (power-up)
    // =========================================================

    // Adds moves and, if the level failed for lack of moves,
    // continues the attempt.
    public bool AddMoves(int amount)
    {
        if (amount <= 0 || loadRoutine != null)
            return false;

        bool revive =
            isLevelEnded &&
            LevelFailUI.Instance != null &&
            LevelFailUI.Instance.IsShowing;

        if (isLevelEnded && !revive)
            return false;

        movesRemaining += amount;
        moveBudget += amount;

        if (revive)
        {
            isLevelEnded = false;
            LevelFailUI.Instance.HideFail();
        }

        UpdateMoveUI();
        ScheduleGameStateCheck();
        return true;
    }

    // =========================================================
    // UNDO SNAPSHOTS
    // =========================================================

    public LevelSnapshot CaptureSnapshot()
    {
        if (gridManager == null || loadRoutine != null)
            return null;

        Box[] pieces =
            gridManager.GetComponentsInChildren<Box>(true);

        LevelSnapshot snapshot = new LevelSnapshot
        {
            levelIndex = currentLevelIndex,
            movesRemaining = movesRemaining,
            cells = new Vector2Int[pieces.Length],
            onGrid = new bool[pieces.Length],
            completed = new bool[pieces.Length],
            bufferSlots = new int[pieces.Length],
            openDoors = gridManager.GetPermanentlyOpenDoors()
        };

        BufferManager buffer = BufferManager.Instance;

        for (int i = 0; i < pieces.Length; i++)
        {
            Box box = pieces[i];

            snapshot.onGrid[i] = box.CurrentCell != null;
            snapshot.cells[i] =
                box.CurrentCell != null
                    ? box.CurrentCell.Coordinates
                    : Vector2Int.zero;

            snapshot.completed[i] =
                box.CurrentCell != null &&
                box.Type != BoxType.Crate &&
                !box.CanBeSelected &&
                !box.IsBusy;

            snapshot.bufferSlots[i] =
                buffer != null
                    ? buffer.GetSlotOfBox(box)
                    : -1;
        }

        return snapshot;
    }

    public bool RestoreSnapshot(LevelSnapshot snapshot)
    {
        if (snapshot == null ||
            snapshot.levelIndex != currentLevelIndex ||
            !CanRestoreSnapshot)
        {
            return false;
        }

        LevelData levelData =
            LevelDatabase.GetLevel(currentLevelIndex);

        if (levelData == null)
            return false;

        StopGameStateCheck();

        if (InputManager.Instance != null)
            InputManager.Instance.CancelAll();

        // Where every piece is now (for the rewind glide).
        Box[] oldPieces = gridManager.GetComponentsInChildren<Box>(true);
        Vector3[] oldPositions = new Vector3[oldPieces.Length];
        bool[] oldOnGrid = new bool[oldPieces.Length];

        for (int i = 0; i < oldPieces.Length; i++)
        {
            oldPositions[i] = oldPieces[i].transform.position;
            oldOnGrid[i] = oldPieces[i].CurrentCell != null;
        }

        if (LevelFailUI.Instance != null &&
            LevelFailUI.Instance.IsShowing)
        {
            LevelFailUI.Instance.HideFail();
        }

        AudioManager.SuppressSfx(0.35f);

        // ----- rebuild the board from scratch -----

        if (BufferManager.Instance != null)
            BufferManager.Instance.ResetForLevel();

        gridManager.ClearEverything();

        gridManager.GenerateGrid(
            levelData.Width,
            levelData.Height
        );

        totalBoxes = 0;
        completedBoxes = 0;

        CreateLevel(levelData);

        Box[] pieces =
            gridManager.GetComponentsInChildren<Box>(true);

        if (pieces.Length != snapshot.cells.Length)
        {
            Debug.LogError("Undo: piece count mismatch, reloading level.");
            LoadLevel(currentLevelIndex);
            return false;
        }

        foreach (Box box in pieces)
            box.DetachForRestore();

        // ----- buffered pieces, in slot order -----

        BufferManager buffer = BufferManager.Instance;
        Cell padCell = buffer != null ? buffer.CurrentPadCell : null;

        for (int slot = 0; slot < 8; slot++)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (snapshot.onGrid[i] || snapshot.bufferSlots[i] != slot)
                    continue;

                if (padCell == null)
                    continue;

                pieces[i].Place(padCell);
                buffer.RestoreStoredBox(slot, pieces[i]);
            }
        }

        // ----- pieces on the grid -----

        for (int i = 0; i < pieces.Length; i++)
        {
            if (!snapshot.onGrid[i])
                continue;

            Cell cell = gridManager.GetCell(snapshot.cells[i]);

            if (cell == null)
                continue;

            Box box = pieces[i];
            box.Place(cell);

            if (snapshot.completed[i])
            {
                box.RestoreCompleted();
                completedBoxes++;
            }
            else if (cell.HasBufferPad && box.IsBufferCompatible)
            {
                cell.BufferPad.TrackBox(box);
            }

            box.ResetMotionJuice();
        }

        // ----- doors and switches -----

        foreach (int doorID in snapshot.openDoors)
            gridManager.OpenDoor(doorID);

        for (int i = 0; i < pieces.Length; i++)
        {
            Cell cell = pieces[i].CurrentCell;

            if (cell != null && cell.HasSwitch)
                cell.FloorSwitch.Activate();
        }

        gridManager.SnapAllDoors();

        // ----- rewind glide -----

        List<Transform> movers = new List<Transform>();
        List<Vector3> from = new List<Vector3>();
        List<Vector3> to = new List<Vector3>();

        for (int i = 0; i < pieces.Length && i < oldPieces.Length; i++)
        {
            if (!oldOnGrid[i] || pieces[i].CurrentCell == null)
                continue;

            Vector3 target = pieces[i].transform.position;

            if ((target - oldPositions[i]).sqrMagnitude < 0.0001f)
                continue;

            movers.Add(pieces[i].transform);
            from.Add(oldPositions[i]);
            to.Add(target);
            pieces[i].transform.position = oldPositions[i];
        }

        if (restoreTween != null)
            StopCoroutine(restoreTween);

        restoreTween = movers.Count > 0
            ? StartCoroutine(RestoreGlide(movers, from, to))
            : null;

        // ----- moves / state -----

        movesRemaining = snapshot.movesRemaining;
        isLevelEnded = false;

        UpdateMoveUI();

        SafeEvents.Raise(BoardRestored);

        ScheduleGameStateCheck();
        return true;
    }

    private IEnumerator RestoreGlide(List<Transform> movers, List<Vector3> from, List<Vector3> to)
    {
        restoreAnimating = true;

        const float Duration = 0.3f;
        float t = 0f;

        while (t < Duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / Duration), 3f);

            for (int i = 0; i < movers.Count; i++)
            {
                if (movers[i] != null)
                    movers[i].position = Vector3.LerpUnclamped(from[i], to[i], k);
            }

            yield return null;
        }

        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] != null)
                movers[i].position = to[i];
        }

        restoreAnimating = false;
        restoreTween = null;
    }

    // =========================================================
    // RESTART
    // =========================================================

    public void RestartLevel()
    {
        // Giving up an attempt that already used moves costs a life
        // (a failed attempt was charged when it failed).
        if (!isLevelEnded && MovesUsed > 0)
        {
            LivesManager.OnLevelAbandoned(
                currentLevelIndex
            );
        }

        LoadLevel(
            currentLevelIndex
        );
    }

    // =========================================================
    // NEXT LEVEL
    // =========================================================

    public void LoadNextLevel()
    {
        int nextLevel =
            currentLevelIndex + 1;

        if (nextLevel >=
            LevelDatabase.GetLevelCount())
        {
            // Final level: the complete panel's primary button
            // reads "MAIN MENU" and returns to the menu.
            Debug.Log(
                "All levels completed!"
            );

            if (SettingsController.Instance != null)
            {
                SettingsController.Instance.GoToMainMenu();
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }

            return;
        }

        LoadLevel(
            nextLevel
        );
    }
}