// =============================================================
// DEADLOCK SELF-TEST (Editor only, not included in builds)
// =============================================================
//
// Tools > Slide Factory > Run Deadlock Self-Test
//
// Enters Play mode in the Game scene and checks DeadlockDetector:
//
// 1. Every level in LevelDatabase is loaded; its starting state
//    must not be reported as Deadlocked.
// 2. Small hand-made boards check the Pressure Plate release rule
//    (a piece leaving a plate cannot enter the door that plate
//    holds open). For each board the runtime rule (Box.CanMove)
//    and the detector result are compared with the expected ones.
//
// The report is written to Temp/deadlock_selftest_result.txt and
// summarized in the Console. Creating the file
// Temp/run_deadlock_selftest also starts the test.
// =============================================================

using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DeadlockSelfTest
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string PendingKey = "SlideFactory.DeadlockSelfTest.Pending";

    private const int TestMaxStates = 400000;
    private const int TestMaxMilliseconds = 15000;

    private struct Scenario
    {
        public string Name;
        public string[] Rows;

        // Piece moved before the check (row/column as written in Rows).
        // FromRow < 0 = no relocation.
        public int FromRow, FromCol, ToRow, ToCol;

        // Runtime check on the relocated piece (zero = skip).
        public Vector2Int Direction;
        public bool ExpectCanMove;

        public DeadlockCheckResult Expected;
    }

    private static readonly Scenario[] Scenarios =
    {
        new Scenario
        {
            Name = "Normal Box on a plate cannot walk into its own door",
            Rows = new[] { "# G0 #", "# D0 #", "# T0 #", "# B0 #" },
            FromRow = 3, FromCol = 1, ToRow = 2, ToCol = 1,
            Direction = Vector2Int.up, ExpectCanMove = false,
            Expected = DeadlockCheckResult.Deadlocked
        },
        new Scenario
        {
            Name = "Control: a second Box holds the plate, first Box passes",
            Rows = new[] { ". G0 . .", "# D0 # #", ". . T0 G1", "B0 . . B1" },
            FromRow = -1,
            Direction = Vector2Int.zero,
            Expected = DeadlockCheckResult.Solvable
        },
        new Scenario
        {
            Name = "Crate pushed off a plate cannot enter the door it held",
            Rows = new[] { "# # # # #", "B0 T0 D0 C .", "# # # G0 #" },
            FromRow = 1, FromCol = 3, ToRow = 1, ToCol = 1,
            Direction = Vector2Int.zero,
            Expected = DeadlockCheckResult.Deadlocked
        },
        new Scenario
        {
            Name = "Heavy Box on a plate cannot walk into its own door",
            Rows = new[] { "# G0 #", "# D0 #", "# T0 #", "# H0 #" },
            FromRow = 3, FromCol = 1, ToRow = 2, ToCol = 1,
            Direction = Vector2Int.up, ExpectCanMove = false,
            Expected = DeadlockCheckResult.Deadlocked
        },
        new Scenario
        {
            Name = "Heavy Box leaving a plate cannot push a Crate into its door",
            Rows = new[] { "# # # # # #", "T0 C D0 . . H0", "# # # # G0 #" },
            FromRow = 1, FromCol = 5, ToRow = 1, ToCol = 0,
            Direction = Vector2Int.right, ExpectCanMove = false,
            Expected = DeadlockCheckResult.Deadlocked
        },
        new Scenario
        {
            Name = "Ice on a plate cannot start into its own door",
            Rows = new[] { "# # # # #", "T0 D0 . I0 G0", "# # # # #" },
            FromRow = 1, FromCol = 3, ToRow = 1, ToCol = 0,
            Direction = Vector2Int.right, ExpectCanMove = false,
            Expected = DeadlockCheckResult.Deadlocked
        },
        new Scenario
        {
            Name = "Ice leaving a plate stops in front of the door further on",
            Rows = new[] { "# # # # # #", "T0 . D0 I0 . G0", "# # # # # #" },
            FromRow = 1, FromCol = 3, ToRow = 1, ToCol = 0,
            Direction = Vector2Int.right, ExpectCanMove = true,
            Expected = DeadlockCheckResult.Deadlocked
        }
    };

    private enum Phase { Idle, WaitForGame, Levels, Scenarios, Done }

    private static Phase phase = Phase.Idle;
    private static int index;
    private static bool loadIssued;
    private static int waitUntilFrame;
    private static int failures;
    private static StringBuilder report;
    private static double nextTriggerCheck;

    private static string ProjectRoot =>
        Path.GetDirectoryName(Application.dataPath);

    private static string TriggerPath =>
        Path.Combine(ProjectRoot, "Temp", "run_deadlock_selftest");

    private static string ResultPath =>
        Path.Combine(ProjectRoot, "Temp", "deadlock_selftest_result.txt");

    static DeadlockSelfTest()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    // =========================================================
    // START / STOP
    // =========================================================

    [MenuItem("Tools/Slide Factory/Run Deadlock Self-Test")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("DeadlockSelfTest: exit Play mode first.");
            return;
        }

        SceneAsset gameScene =
            AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath);

        if (gameScene == null)
        {
            Debug.LogError("DeadlockSelfTest: scene not found: " + GameScenePath);
            return;
        }

        SessionState.SetBool(PendingKey, true);
        EditorSceneManager.playModeStartScene = gameScene;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode &&
            SessionState.GetBool(PendingKey, false))
        {
            SessionState.SetBool(PendingKey, false);

            Application.runInBackground = true;

            report = new StringBuilder();
            failures = 0;
            index = 0;
            loadIssued = false;
            phase = Phase.WaitForGame;
            waitUntilFrame = Time.frameCount + 10;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = null;
            phase = Phase.Idle;
        }
    }

    private static void OnEditorUpdate()
    {
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.timeSinceStartup < nextTriggerCheck)
                return;

            nextTriggerCheck = EditorApplication.timeSinceStartup + 1.0;

            if (!EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                File.Exists(TriggerPath))
            {
                File.Delete(TriggerPath);
                Run();
            }

            return;
        }

        if (phase == Phase.Idle || phase == Phase.Done)
            return;

        try
        {
            Step();
        }
        catch (Exception exception)
        {
            failures++;

            if (report != null)
                report.AppendLine("EXCEPTION: " + exception);

            Finish();
        }
    }

    private static void Finish()
    {
        phase = Phase.Done;

        string summary =
            failures == 0
                ? "RESULT: ALL PASSED"
                : "RESULT: " + failures + " FAILED";

        if (report != null)
        {
            report.AppendLine(summary);
            File.WriteAllText(ResultPath, report.ToString());
        }

        if (failures == 0)
            Debug.Log("DeadlockSelfTest: all passed. Report: " + ResultPath);
        else
            Debug.LogError("DeadlockSelfTest: " + failures + " failed. Report: " + ResultPath);

        EditorApplication.isPlaying = false;
    }

    // =========================================================
    // STEPS
    // =========================================================

    private static void Step()
    {
        if (Time.frameCount < waitUntilFrame)
            return;

        GridManager grid = GridManager.Instance;
        LevelManager levels = LevelManager.Instance;

        if (phase == Phase.WaitForGame)
        {
            if (grid == null || levels == null)
            {
                waitUntilFrame = Time.frameCount + 5;
                return;
            }

            report.AppendLine(
                "Deadlock self-test  " +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            report.AppendLine(
                "--- Levels: the starting state must not be Deadlocked " +
                "(test budget / in-game budget)");

            phase = Phase.Levels;
            return;
        }

        if (phase == Phase.Levels)
        {
            StepLevel(grid, levels);
            return;
        }

        if (phase == Phase.Scenarios)
            StepScenario(grid, levels);
    }

    private static void StepLevel(GridManager grid, LevelManager levels)
    {
        if (index >= LevelDatabase.GetLevelCount())
        {
            report.AppendLine("--- Pressure Plate release scenarios");
            phase = Phase.Scenarios;
            index = 0;
            loadIssued = false;
            return;
        }

        if (!loadIssued)
        {
            levels.LoadLevel(index);
            loadIssued = true;
            waitUntilFrame = Time.frameCount + 4;
            return;
        }

        LevelData data = LevelDatabase.GetLevel(index);

        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        DeadlockCheckResult result =
            DeadlockDetector.Check(
                data,
                grid,
                TestMaxStates,
                TestMaxMilliseconds);

        stopwatch.Stop();

        // The budget the game itself uses after every move.
        DeadlockCheckResult inGameResult =
            DeadlockDetector.Check(data, grid);

        bool ok =
            result != DeadlockCheckResult.Deadlocked &&
            inGameResult != DeadlockCheckResult.Deadlocked;

        if (!ok)
            failures++;

        report.AppendLine(string.Format(
            "{0}  Level {1,2}: {2} / {3}  ({4} ms)",
            ok ? "PASS" : "FAIL",
            index + 1,
            result,
            inGameResult,
            stopwatch.ElapsedMilliseconds));

        index++;
        loadIssued = false;
    }

    private static void StepScenario(GridManager grid, LevelManager levels)
    {
        if (index >= Scenarios.Length)
        {
            Finish();
            return;
        }

        Scenario scenario = Scenarios[index];

        if (!loadIssued)
        {
            // Stop LevelManager's pending fail/win check and clear the board.
            InvokePrivate(levels, "StopGameStateCheck");

            if (BufferManager.Instance != null)
                BufferManager.Instance.ResetForLevel();

            grid.ClearEverything();

            loadIssued = true;
            waitUntilFrame = Time.frameCount + 2;
            return;
        }

        LevelData data = new LevelData(scenario.Rows);

        grid.GenerateGrid(data.Width, data.Height);

        if (GridCameraController.Instance != null)
            GridCameraController.Instance.FitToGrid(data.Width, data.Height);

        InvokePrivate(levels, "CreateLevel", data);

        StringBuilder line = new StringBuilder();
        bool ok = true;

        if (scenario.FromRow >= 0)
        {
            Cell from = GetCell(grid, data, scenario.FromRow, scenario.FromCol);
            Cell to = GetCell(grid, data, scenario.ToRow, scenario.ToCol);

            Box piece = from != null ? from.Box : null;

            if (piece == null || to == null)
                throw new InvalidOperationException(
                    "Scenario setup failed: " + scenario.Name);

            piece.Place(to);

            if (scenario.Direction != Vector2Int.zero)
            {
                bool canMove = piece.CanMove(scenario.Direction);

                if (canMove != scenario.ExpectCanMove)
                    ok = false;

                line.Append(string.Format(
                    "runtime CanMove={0} (expected {1}), ",
                    canMove,
                    scenario.ExpectCanMove));
            }
        }

        DeadlockCheckResult result =
            DeadlockDetector.Check(
                data,
                grid,
                TestMaxStates,
                TestMaxMilliseconds);

        if (result != scenario.Expected)
            ok = false;

        line.Append(string.Format(
            "detector={0} (expected {1})",
            result,
            scenario.Expected));

        if (!ok)
            failures++;

        report.AppendLine(
            (ok ? "PASS  " : "FAIL  ") +
            scenario.Name + ": " + line);

        index++;
        loadIssued = false;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    // Rows are written top to bottom; grid y = 0 is the bottom row.
    private static Cell GetCell(GridManager grid, LevelData data, int row, int column)
    {
        return grid.GetCell(
            new Vector2Int(column, data.Height - 1 - row));
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method =
            target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);

        method.Invoke(target, arguments);
    }
}
