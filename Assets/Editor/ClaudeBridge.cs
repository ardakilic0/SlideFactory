// =============================================================
// CLAUDE BRIDGE (Editor only, not included in builds)
// =============================================================
//
// A tiny script runner used for automated play-testing.
// Write commands (one per line) to Temp/claude/cmd.txt; the file is
// picked up, deleted and executed step by step. Progress, errors and
// exceptions are written to Temp/claude/bridge_log.txt.
//
// Commands:
//   play [MainMenu|Game]   enter Play mode in that scene (default Game)
//   stop                   exit Play mode
//   wait <seconds>
//   idle                   wait until no box / buffer animation runs
//   level <n>              load level n (1-based)
//   move <piece> <U|D|L|R> swipe a piece (index in grid hierarchy order)
//   tap <piece>            tap a piece (portal re-activation / buffer)
//   ret <slot>             return a buffered box from UI slot
//   click <ObjectName>     invoke an active Button by GameObject name
//   shot <name>            capture Temp/claude/shots/<name>.png
//   prefs clear            PlayerPrefs.DeleteAll
//   prefs int <key> <v>    PlayerPrefs.SetInt
//   call <Type> <Method> [int/string args...]   static method call
//   autoplay [maxSteps]    play the current level with the solver
//   pars                   compute optimal move counts for all levels
//   log <text>
// =============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ClaudeBridge
{
    private const string LinesKey = "ClaudeBridge.Lines";
    private const string PcKey = "ClaudeBridge.Pc";

    private static string Root =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "claude");

    private static string CmdPath => Path.Combine(Root, "cmd.txt");
    private static string LogPath => Path.Combine(Root, "bridge_log.txt");

    private static double nextPoll;
    private static double waitUntil;
    private static bool waitingIdle;
    private static double idleSince = -1;
    private static bool waitingPlay;
    private static bool waitingStop;
    private static int autoplayLeft;
    private static double autoplayNext;

    static ClaudeBridge()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
        UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished += OnCompiled;
    }

    private static void OnCompiled(string assembly, UnityEditor.Compilation.CompilerMessage[] messages)
    {
        int errors = 0;
        foreach (var m in messages)
        {
            if (m.type == UnityEditor.Compilation.CompilerMessageType.Error)
            {
                errors++;
                Log("[compile error] " + m.file + ":" + m.line + " " + m.message);
            }
            else
            {
                Log("[compile warning] " + m.file + ":" + m.line + " " + m.message);
            }
        }
        Log("compiled " + Path.GetFileName(assembly) + " errors=" + errors);
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            Log("[" + type + "] " + condition + "\n" + stackTrace);
        }
        else if (type == LogType.Warning && condition.StartsWith("Bridge"))
        {
            Log("[warn] " + condition);
        }
    }

    private static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.AppendAllText(LogPath,
                DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + "\n");
        }
        catch { }
    }

    private static string[] Lines
    {
        get
        {
            string raw = SessionState.GetString(LinesKey, "");
            return string.IsNullOrEmpty(raw) ? new string[0] : raw.Split('\n');
        }
    }

    private static int Pc
    {
        get => SessionState.GetInt(PcKey, 0);
        set => SessionState.SetInt(PcKey, value);
    }

    private static void Update()
    {
        double now = EditorApplication.timeSinceStartup;

        if (now >= nextPoll)
        {
            nextPoll = now + 0.5;

            if (File.Exists(CmdPath) &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating)
            {
                string text = File.ReadAllText(CmdPath).Replace("\r", "");
                File.Delete(CmdPath);
                SessionState.SetString(LinesKey, text.Trim());
                Pc = 0;
                waitUntil = 0;
                waitingIdle = waitingPlay = waitingStop = false;
                autoplayLeft = 0;
                Log("=== new script (" + Lines.Length + " lines)");
            }
        }

        string[] lines = Lines;

        if (Pc >= lines.Length || lines.Length == 0)
            return;

        if (EditorApplication.isCompiling)
            return;

        if (now < waitUntil)
            return;

        if (waitingPlay)
        {
            if (!EditorApplication.isPlaying || LevelManagerReady() == false)
                return;

            waitingPlay = false;
            waitUntil = now + 1.0;
            return;
        }

        if (waitingStop)
        {
            if (EditorApplication.isPlaying)
                return;

            waitingStop = false;
        }

        if (waitingIdle)
        {
            if (IsBusy())
            {
                idleSince = -1;
                return;
            }

            if (idleSince < 0)
            {
                idleSince = now;
                return;
            }

            if (now - idleSince < 0.6)
                return;

            waitingIdle = false;
            idleSince = -1;
        }

        if (autoplayLeft > 0)
        {
            if (now < autoplayNext)
                return;

            if (IsBusy())
                return;

            AutoplayStep();
            return;
        }

        string line = lines[Pc].Trim();
        Pc = Pc + 1;

        if (line.Length == 0 || line.StartsWith("//"))
            return;

        try
        {
            Execute(line);
        }
        catch (Exception exception)
        {
            Log("EXCEPTION in '" + line + "': " + exception);
        }

        if (Pc >= lines.Length)
            Log("=== script done");
    }

    private static bool LevelManagerReady()
    {
        Type lm = FindType("LevelManager");
        if (lm == null) return true;

        object instance = lm.GetProperty("Instance")?.GetValue(null);
        if (instance == null)
            return EditorSceneManager.GetActiveScene().name != "Game";

        return !IsBusy();
    }

    private static bool IsBusy()
    {
        if (!EditorApplication.isPlaying)
            return false;

        Type lm = FindType("LevelManager");
        object instance = lm?.GetProperty("Instance")?.GetValue(null);
        if (instance == null)
            return false;

        PropertyInfo busy = lm.GetProperty("IsBoardBusy");
        if (busy != null)
            return (bool)busy.GetValue(instance);

        MethodInfo method = lm.GetMethod("AnyBoxIsBusy",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return method != null && (bool)method.Invoke(instance, null);
    }

    private static void Execute(string line)
    {
        string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string cmd = p[0].ToLowerInvariant();
        Log("> " + line);

        switch (cmd)
        {
            case "log":
                Log(line.Substring(4));
                break;

            case "play":
            {
                string scene = p.Length > 1 ? p[1] : "Game";
                SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/Scenes/" + scene + ".unity");
                EditorSceneManager.playModeStartScene = asset;
                Application.runInBackground = true;
                if (!EditorApplication.isPlaying)
                    EditorApplication.isPlaying = true;
                waitingPlay = true;
                break;
            }

            case "stop":
                EditorApplication.isPlaying = false;
                EditorSceneManager.playModeStartScene = null;
                waitingStop = true;
                break;

            case "wait":
                waitUntil = EditorApplication.timeSinceStartup +
                            float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                break;

            case "idle":
                waitingIdle = true;
                idleSince = -1;
                break;

            case "level":
                InvokeInstance("LevelManager", "LoadLevel", int.Parse(p[1]) - 1);
                waitUntil = EditorApplication.timeSinceStartup + 0.3;
                waitingIdle = true;
                break;

            case "move":
                DoMove(int.Parse(p[1]), ParseDir(p[2]));
                waitingIdle = true;
                break;

            case "tap":
                DoTap(int.Parse(p[1]));
                waitingIdle = true;
                break;

            case "ret":
                InvokeInstance("BufferManager", "TryReturnBox", int.Parse(p[1]));
                waitingIdle = true;
                break;

            case "click":
                Click(line.Substring(6).Trim());
                break;

            case "shot":
            {
                string dir = Path.Combine(Root, "shots");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, p[1] + ".png");
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                waitUntil = EditorApplication.timeSinceStartup + 0.4;
                break;
            }

            case "prefs":
                if (p[1] == "clear") PlayerPrefs.DeleteAll();
                else if (p[1] == "int") PlayerPrefs.SetInt(p[2], int.Parse(p[3]));
                PlayerPrefs.Save();
                break;

            case "call":
                CallStatic(p);
                break;

            case "autoplay":
                autoplayLeft = p.Length > 1 ? int.Parse(p[1]) : 200;
                autoplayNext = 0;
                break;

            case "pars":
                ComputePars();
                break;

            case "undotest":
                utLevel = int.Parse(p[1]) - 1;
                utLast = p.Length > 2 ? int.Parse(p[2]) - 1 : utLevel;
                utStage = 0;
                utReport = new StringBuilder();
                EditorApplication.update -= UndoTestStep;
                EditorApplication.update += UndoTestStep;
                break;

            case "prefsbackup":
                PrefsBackup();
                break;

            case "prefsrestore":
                PrefsRestore();
                break;

            case "hit":
                Hit(line.Substring(4).Trim());
                break;

            case "livestest":
                LivesTest();
                break;

            case "verifyall":
                verifyLevel = p.Length > 1 ? int.Parse(p[1]) - 1 : 0;
                verifyLast = p.Length > 2 ? int.Parse(p[2]) - 1 : LevelDatabase.GetLevelCount() - 1;
                verifyReport = new StringBuilder();
                verifyStage = 0;
                LevelManager.LevelCompleted -= OnVerifyCompleted;
                LevelManager.LevelFailed -= OnVerifyFailed;
                LevelManager.LevelCompleted += OnVerifyCompleted;
                LevelManager.LevelFailed += OnVerifyFailed;
                EditorApplication.update -= VerifyStep;
                EditorApplication.update += VerifyStep;
                verifyNext = 0;
                break;

            case "refresh":
                AssetDatabase.Refresh();
                break;

            case "dump":
                Dump();
                break;

            case "tree":
                Tree(p[1]);
                break;

            case "fail":
                InvokeInstance("LevelManager", "FailLevel",
                    Enum.Parse(FindType("LevelFailReason"), p[1]));
                break;

            default:
                Log("unknown command: " + cmd);
                break;
        }
    }

    // =========================================================
    // GAME HELPERS
    // =========================================================

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = assembly.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static object InvokeInstance(string typeName, string method, params object[] args)
    {
        Type t = FindType(typeName);
        object instance = t.GetProperty("Instance")?.GetValue(null)
                          ?? t.GetField("Instance")?.GetValue(null);
        MethodInfo m = t.GetMethod(method,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return m.Invoke(instance, args);
    }

    private static Vector2Int ParseDir(string d)
    {
        switch (d.ToUpperInvariant())
        {
            case "U": return Vector2Int.up;
            case "D": return Vector2Int.down;
            case "L": return Vector2Int.left;
            default: return Vector2Int.right;
        }
    }

    private static Box[] Pieces()
    {
        return GridManager.Instance.GetComponentsInChildren<Box>(true);
    }

    private static void DoMove(int piece, Vector2Int dir)
    {
        Box box = Pieces()[piece];
        GameActions.TrySwipe(box, dir);
    }

    private static void DoTap(int piece)
    {
        Box box = Pieces()[piece];
        GameActions.TryTap(box);
    }

    private static void Click(string name)
    {
        foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (button.gameObject.name == name && button.isActiveAndEnabled)
            {
                button.onClick.Invoke();
                Log("clicked " + name);
                return;
            }
        }
        Log("button not found: " + name);
    }

    private static void CallStatic(string[] p)
    {
        Type t = FindType(p[1]);
        foreach (MethodInfo m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (m.Name != p[2]) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != p.Length - 3) continue;
            object[] args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                Type pt = ps[i].ParameterType;
                if (pt == typeof(int)) args[i] = int.Parse(p[3 + i]);
                else if (pt == typeof(bool)) args[i] = bool.Parse(p[3 + i]);
                else if (pt == typeof(float)) args[i] = float.Parse(p[3 + i], System.Globalization.CultureInfo.InvariantCulture);
                else if (pt.IsEnum) args[i] = Enum.Parse(pt, p[3 + i]);
                else args[i] = p[3 + i];
            }
            object result = m.Invoke(null, args);
            Log("result: " + result);
            return;
        }
        Log("method not found");
    }

    private static void Tree(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name != name || !go.scene.IsValid()) continue;
            StringBuilder sb = new StringBuilder();
            TreeRec(go.transform, 0, sb);
            Log("tree " + name + "\n" + sb);
            return;
        }
        Log("tree: not found " + name);
    }

    private static void TreeRec(Transform t, int depth, StringBuilder sb)
    {
        sb.Append(new string(' ', depth * 2)).Append(t.name);
        if (!t.gameObject.activeSelf) sb.Append(" [off]");
        RectTransform r = t as RectTransform;
        if (r != null)
        {
            sb.Append(" pos=").Append(r.anchoredPosition).Append(" size=").Append(r.rect.size)
              .Append(" amin=").Append(r.anchorMin).Append(" amax=").Append(r.anchorMax)
              .Append(" piv=").Append(r.pivot).Append(" scl=").Append(r.localScale);
        }
        foreach (Component c in t.GetComponents<Component>())
        {
            if (c == null || c is Transform) continue;
            sb.Append(" ").Append(c.GetType().Name);
            if (c is TMPro.TMP_Text tx) sb.Append("('").Append(tx.text).Append("' ").Append(tx.font != null ? tx.font.name : "").Append(" ").Append(tx.fontSize).Append(")");
            if (c is Image im && im.sprite != null) sb.Append("(").Append(im.sprite.name).Append(")");
        }
        sb.Append("\n");
        if (depth < 7)
            foreach (Transform child in t) TreeRec(child, depth + 1, sb);
    }

    private static void Dump()
    {
        StringBuilder sb = new StringBuilder();
        Box[] pieces = Pieces();
        for (int i = 0; i < pieces.Length; i++)
        {
            Box b = pieces[i];
            sb.Append(i).Append(':').Append(b.Type).Append(b.BoxID).Append('@')
              .Append(b.CurrentCell != null ? b.CurrentCell.Coordinates.ToString() : "buffer")
              .Append(b.CanBeSelected ? "" : "(done/busy)").Append("  ");
        }
        sb.Append(" moves=").Append(LevelManager.Instance.MovesRemaining);
        Log(sb.ToString());
    }

    // =========================================================
    // SOLVER-DRIVEN AUTOPLAY
    // =========================================================

    private static void AutoplayStep()
    {
        autoplayLeft--;

        LevelManager lm = LevelManager.Instance;
        if (lm == null || lm.IsLevelEnded)
        {
            Log("autoplay: level ended");
            autoplayLeft = 0;
            return;
        }

        LevelData data = LevelDatabase.GetLevel(lm.CurrentLevelIndex);
        SolveResult result = LevelSolver.Solve(
            data, GridManager.Instance, 600000, 20000,
            out List<SolverAction> path, out int cost);

        if (result != SolveResult.Solved || path.Count == 0)
        {
            Log("autoplay: solver " + result);
            autoplayLeft = 0;
            return;
        }

        SolverAction action = path[0];
        Log("autoplay: " + action + " (remaining cost " + cost + ")");
        GameActions.Perform(action);
        autoplayNext = EditorApplication.timeSinceStartup + 0.25;
        waitingIdle = true;
    }

    // =========================================================
    // VERIFY: autoplay every level with the solver in the real game
    // =========================================================

    private static int verifyLevel, verifyLast, verifyStage, verifyActions;
    private static double verifyNext, verifyStart;
    private static string verifyEnd;
    private static StringBuilder verifyReport;

    private static void OnVerifyCompleted(int level, LevelReward reward)
    {
        verifyEnd = "COMPLETED stars=" + reward.stars;
    }

    private static void OnVerifyFailed(int level, LevelFailReason reason)
    {
        verifyEnd = "FAILED " + reason;
    }

    private static void VerifyStep()
    {
        double now = EditorApplication.timeSinceStartup;

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= VerifyStep;
            return;
        }

        if (now < verifyNext)
            return;

        LevelManager lm = LevelManager.Instance;

        if (verifyLevel > verifyLast)
        {
            EditorApplication.update -= VerifyStep;
            LevelManager.LevelCompleted -= OnVerifyCompleted;
            LevelManager.LevelFailed -= OnVerifyFailed;
            File.WriteAllText(Path.Combine(Root, "verify.txt"), verifyReport.ToString());
            Log("verify: done");
            return;
        }

        if (verifyStage == 0)
        {
            verifyEnd = null;
            verifyActions = 0;
            lm.LoadLevel(verifyLevel);
            verifyStage = 1;
            verifyStart = now;
            verifyNext = now + 0.5;
            return;
        }

        if (lm.IsBoardBusy)
            return;

        if (verifyEnd != null || lm.IsLevelEnded || verifyActions > 80 || now - verifyStart > 90)
        {
            int par = LevelDatabase.GetPar(verifyLevel);
            string line = string.Format("{0}\t{1}\tmovesUsed={2}\tpar={3}\tlimit={4}\t{5}",
                verifyLevel + 1, verifyEnd ?? "STOPPED", lm.MovesUsed, par,
                LevelDatabase.GetMoveLimit(verifyLevel),
                verifyEnd != null && verifyEnd.StartsWith("COMPLETED") && lm.MovesUsed == par ? "OK" : "CHECK");
            verifyReport.AppendLine(line);
            Log("verify " + line);
            verifyLevel++;
            verifyStage = 0;
            verifyNext = now + 0.3;
            return;
        }

        SolveResult result = LevelSolver.Solve(
            LevelDatabase.GetLevel(lm.CurrentLevelIndex), GridManager.Instance,
            600000, 20000, out List<SolverAction> path, out int cost);

        if (result != SolveResult.Solved || path.Count == 0)
        {
            verifyEnd = "SOLVER " + result;
            return;
        }

        GameActions.Perform(path[0]);
        verifyActions++;
        verifyNext = now + 0.15;
    }

    // =========================================================
    // UNDO ROUND TRIP: play the solution, undo every step, compare
    // =========================================================

    private static int utLevel, utLast, utStage, utStep, utFailures;
    private static List<SolverAction> utPath;
    private static List<string> utStates;
    private static StringBuilder utReport;
    private static double utNext;

    // Saves / restores the game's PlayerPrefs so tests do not wipe the
    // developer's own progress. File survives editor restarts.
    private static string BackupPath =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "claude_prefs_backup.txt");

    private static List<string> KnownPrefKeys()
    {
        List<string> keys = new List<string>
        {
            "CurrentLevelIndex", "Coins", "StarChestsOpened", "PowerUps_Initialized",
            "Lives", "LivesLastRegenUtc", "SoundEnabled", "MusicEnabled", "HapticsEnabled"
        };
        for (int i = 0; i < 100; i++) keys.Add("Stars_L" + i);
        foreach (string t in new[] { "Undo", "Hint", "ExtraMoves" }) { keys.Add("PowerUp_" + t); keys.Add("PowerUpIntro_" + t); }
        foreach (string t in new[] { "Basics", "Crate", "Switch", "Plate", "Portal", "Ice", "Heavy", "Conveyor", "Buffer" })
            keys.Add("Tutorial_" + t);
        return keys;
    }

    private static void PrefsBackup()
    {
        StringBuilder sb = new StringBuilder();
        foreach (string k in KnownPrefKeys())
        {
            if (!PlayerPrefs.HasKey(k)) continue;
            string str = PlayerPrefs.GetString(k, "\u0001");
            if (str != "\u0001") { sb.Append("s\t").Append(k).Append('\t').Append(str).Append('\n'); continue; }
            int iv = PlayerPrefs.GetInt(k, int.MinValue);
            if (iv != int.MinValue) { sb.Append("i\t").Append(k).Append('\t').Append(iv).Append('\n'); continue; }
            sb.Append("f\t").Append(k).Append('\t').Append(PlayerPrefs.GetFloat(k).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        }
        File.WriteAllText(BackupPath, sb.ToString());
        Log("prefs backed up:\n" + sb);
    }

    private static void PrefsRestore()
    {
        if (!File.Exists(BackupPath)) { Log("no prefs backup"); return; }
        foreach (string k in KnownPrefKeys()) PlayerPrefs.DeleteKey(k);
        foreach (string line in File.ReadAllLines(BackupPath))
        {
            string[] p = line.Split('\t');
            if (p.Length < 3) continue;
            if (p[0] == "s") PlayerPrefs.SetString(p[1], p[2]);
            else if (p[0] == "i") PlayerPrefs.SetInt(p[1], int.Parse(p[2]));
            else PlayerPrefs.SetFloat(p[1], float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture));
        }
        PlayerPrefs.Save();
        Log("prefs restored");
    }

    // Real UI tap: raycast at the button's screen position and send
    // pointer down / up / click to whatever is on top.
    private static void Hit(string name)
    {
        RectTransform target = null;
        foreach (RectTransform r in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (r.name == name) { target = r; break; }

        if (target == null) { Log("hit: not found " + name); return; }

        Canvas canvas = target.GetComponentInParent<Canvas>().rootCanvas;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, target.TransformPoint(target.rect.center));

        var es = UnityEngine.EventSystems.EventSystem.current;
        var data = new UnityEngine.EventSystems.PointerEventData(es) { position = screen, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        var results = new List<UnityEngine.EventSystems.RaycastResult>();
        es.RaycastAll(data, results);

        StringBuilder sb = new StringBuilder("hit " + name + " at " + screen + ": ");
        foreach (var r in results) sb.Append(r.gameObject.name).Append(" > ");
        Log(sb.ToString());

        if (results.Count == 0) return;

        GameObject top = results[0].gameObject;
        data.pointerCurrentRaycast = results[0];
        data.pointerPressRaycast = results[0];
        GameObject pressed = UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(top, data, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
        GameObject clickTarget = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(top);
        data.pointerPress = pressed ?? clickTarget;
        data.rawPointerPress = top;
        UnityEngine.EventSystems.ExecuteEvents.Execute(data.pointerPress, data, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
        if (clickTarget != null)
            UnityEngine.EventSystems.ExecuteEvents.Execute(clickTarget, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        Log("hit: clicked " + (clickTarget != null ? clickTarget.name : "nothing"));
    }

    private static void LivesTest()
    {
        StringBuilder r = new StringBuilder();
        void Check(string name, bool ok) { r.AppendLine((ok ? "PASS " : "FAIL ") + name); }

        LivesManager.TestMode = true;
        LivesManager.Refill();
        Check("starts full (5)", LivesManager.Lives == 5);
        Check("early level never costs", LivesManager.CostsLife(5) == false);
        LivesManager.OnLevelFailed(5);
        Check("fail on level 6 keeps 5", LivesManager.Lives == 5);

        for (int i = 0; i < 5; i++) LivesManager.OnLevelFailed(20);
        Check("5 fails on level 21 -> 0", LivesManager.Lives == 0);
        Check("cannot play level 21", !LivesManager.CanPlay(20));
        Check("can still play level 3", LivesManager.CanPlay(2));
        int secs = LivesManager.SecondsToNextLife;
        Check("timer ~25 min (" + secs + "s)", secs > 24 * 60 && secs <= 25 * 60);

        long last = long.Parse(PlayerPrefs.GetString("LivesLastRegenUtc"));
        long step = TimeSpan.FromMinutes(EconomyConfig.LifeRegenMinutes).Ticks;
        PlayerPrefs.SetString("LivesLastRegenUtc", (last - step * 2 - TimeSpan.FromMinutes(3).Ticks).ToString());
        Check("53 min later -> 2 lives (" + LivesManager.Lives + ")", LivesManager.Lives == 2);
        int secs2 = LivesManager.SecondsToNextLife;
        Check("next life in ~22 min (" + secs2 + "s)", secs2 > 21 * 60 && secs2 <= 22 * 60 + 1);

        PlayerPrefs.SetString("LivesLastRegenUtc", DateTime.UtcNow.AddHours(5).Ticks.ToString());
        int before = LivesManager.Lives;
        Check("clock moved back: no gain, no crash (" + before + ")", before == 2);

        PlayerPrefs.SetString("LivesLastRegenUtc", DateTime.UtcNow.AddHours(-10).Ticks.ToString());
        Check("10 h later -> full", LivesManager.Lives == 5 && LivesManager.SecondsToNextLife == 0);

        ProgressManager.AddCoins(150);
        LivesManager.OnLevelFailed(30);
        int coins = ProgressManager.Coins;
        Check("refill buy", LivesManager.TryBuyRefill() && LivesManager.Lives == 5 &&
                            ProgressManager.Coins == coins - EconomyConfig.RefillLivesPrice);

        LivesManager.TestMode = false;
        LivesManager.Refill();
        for (int i = 0; i < 3; i++) LivesManager.OnLevelFailed(40);
        Check("disabled system charges nothing", LivesManager.Lives == 5 && LivesManager.CanPlay(40));

        File.WriteAllText(Path.Combine(Root, "livestest.txt"), r.ToString());
        Log("livestest:\n" + r);
    }

    public static string StateString()
    {
        StringBuilder sb = new StringBuilder();
        Box[] pieces = Pieces();
        BufferManager buffer = BufferManager.Instance;

        for (int i = 0; i < pieces.Length; i++)
        {
            Box b = pieces[i];
            sb.Append(i).Append(b.CurrentCell != null ? b.CurrentCell.Coordinates.ToString() : "buf" + (buffer != null ? buffer.GetSlotOfBox(b) : -1));
            sb.Append(b.Type != BoxType.Crate && b.CurrentCell != null && !b.CanBeSelected ? "*" : "").Append(' ');
        }

        sb.Append("| doors:");
        foreach (Door d in GridManager.Instance.GetComponentsInChildren<Door>())
            sb.Append(d.DoorID).Append(d.IsOpen ? "o" : "c");

        sb.Append(" perm:");
        for (int id = 0; id < 10; id++)
            if (GridManager.Instance.IsDoorPermanentlyOpen(id)) sb.Append(id);

        sb.Append(" plates:");
        foreach (PressurePlate pl in GridManager.Instance.GetComponentsInChildren<PressurePlate>())
            sb.Append(pl.IsPressed ? "1" : "0");

        sb.Append(" moves:").Append(LevelManager.Instance.MovesRemaining);
        return sb.ToString();
    }

    private static void UndoTestStep()
    {
        double now = EditorApplication.timeSinceStartup;

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= UndoTestStep;
            return;
        }

        if (now < utNext)
            return;

        LevelManager lm = LevelManager.Instance;

        if (utLevel > utLast)
        {
            EditorApplication.update -= UndoTestStep;
            utReport.AppendLine("UNDO TEST FAILURES: " + utFailures);
            File.WriteAllText(Path.Combine(Root, "undotest.txt"), utReport.ToString());
            Log("undotest: done, failures=" + utFailures);
            return;
        }

        if (utStage == 0)
        {
            lm.LoadLevel(utLevel);
            utStage = 1;
            utNext = now + 0.6;
            return;
        }

        if (lm.IsBoardBusy)
            return;

        if (utStage == 1)
        {
            LevelSolver.Solve(LevelDatabase.GetLevel(utLevel), GridManager.Instance,
                600000, 20000, out utPath, out int _);
            utStates = new List<string> { StateString() };
            utStep = 0;
            utStage = 2;
            return;
        }

        if (utStage == 2)
        {
            // play all but the winning action
            if (utStep < utPath.Count - 1)
            {
                if (!GameActions.Perform(utPath[utStep]))
                {
                    utReport.AppendLine("L" + (utLevel + 1) + " perform failed at " + utStep);
                    utFailures++;
                    utStage = 9;
                    return;
                }

                utStep++;
                utNext = now + 0.12;
                utStage = 3;
                return;
            }

            utStage = 4;
            return;
        }

        if (utStage == 3)
        {
            utStates.Add(StateString());
            utStage = 2;
            return;
        }

        if (utStage == 4)
        {
            // undo back to the start, checking each state
            if (utStep > 0)
            {
                if (!UndoSystem.Undo())
                {
                    utReport.AppendLine("L" + (utLevel + 1) + " undo refused at " + utStep);
                    utFailures++;
                    utStage = 9;
                    return;
                }

                utStep--;
                utStage = 5;
                utNext = now + 0.1;
                return;
            }

            // back at start: solver must still find par
            LevelSolver.Solve(LevelDatabase.GetLevel(utLevel), GridManager.Instance,
                600000, 20000, out List<SolverAction> _, out int cost);
            bool ok = cost == LevelDatabase.GetPar(utLevel);
            if (!ok) utFailures++;
            utReport.AppendLine("L" + (utLevel + 1) + " steps=" + (utPath.Count - 1) +
                                " restart-cost=" + cost + (ok ? " OK" : " FAIL"));
            utStage = 9;
            return;
        }

        if (utStage == 5)
        {
            string state = StateString();
            if (state != utStates[utStep])
            {
                utFailures++;
                utReport.AppendLine("L" + (utLevel + 1) + " MISMATCH after undo to step " + utStep +
                                    "\n  expected " + utStates[utStep] + "\n  got      " + state);
            }

            utStage = 4;
            return;
        }

        // next level
        utLevel++;
        utStage = 0;
    }

    // =========================================================
    // PAR COMPUTATION
    // =========================================================

    private static void ComputePars()
    {
        // Runs synchronously: loads every level and solves it.
        Log("pars: start");
        pendingPar = 0;
        parReport = new StringBuilder();
        EditorApplication.update += ParStep;
    }

    private static int pendingPar = -1;
    private static bool parLoadIssued;
    private static int parWaitFrames;
    private static StringBuilder parReport;

    private static void ParStep()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= ParStep;
            return;
        }

        if (pendingPar >= LevelDatabase.GetLevelCount())
        {
            EditorApplication.update -= ParStep;
            File.WriteAllText(Path.Combine(Root, "pars.txt"), parReport.ToString());
            Log("pars: done");
            return;
        }

        if (!parLoadIssued)
        {
            LevelManager.Instance.LoadLevel(pendingPar);
            parLoadIssued = true;
            parWaitFrames = 6;
            return;
        }

        if (parWaitFrames-- > 0 || LevelManager.Instance.IsBoardBusy)
            return;

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        SolveResult result = LevelSolver.Solve(
            LevelDatabase.GetLevel(pendingPar), GridManager.Instance,
            3000000, 120000, out List<SolverAction> path, out int cost);
        sw.Stop();

        StringBuilder actions = new StringBuilder();
        if (path != null)
            foreach (SolverAction a in path) actions.Append(a.ToShortString()).Append(' ');

        parReport.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}ms\t{4}",
            pendingPar + 1, result, cost, sw.ElapsedMilliseconds, actions));

        pendingPar++;
        parLoadIssued = false;
    }
}
