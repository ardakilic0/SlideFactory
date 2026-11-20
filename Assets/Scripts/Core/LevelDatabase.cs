
// Slide Factory - LevelDatabase (60 levels)
// Revision: 29 October 2026 - based on the playtest notes of the 25 October build.
// Pars (fewest moves) come from LevelSolver run on the real game
// rules inside Unity (ClaudeBridge "pars"), and were verified by
// autoplaying every level in Play mode.
// Stars: 3 = par, 2 = par + max(2, 20%), 1 = solved.
// Move limit = 2-star threshold + 3 (+2 more on levels 1-5).

// Changed levels: 8, 20, 22, 24, 34, 41, 44, 47, 50, 53, 54, 58, 60

public static class LevelDatabase
{
    private static readonly LevelData[] levels =
    {
        // LEVEL 01 - FIRST DELIVERY [EASY]
        new LevelData(
            ". . . G0",
            ". # # .",
            ". . . .",
            "B0 . . ."
        ),

        // LEVEL 02 - TWO BOXES [EASY]
        new LevelData(
            "G1 . . G0",
            ". # # .",
            ". B0 B1 .",
            ". . . ."
        ),

        // LEVEL 03 - CROSSING [EASY+]
        new LevelData(
            "# G1 . G0 #",
            "# . . . #",
            "# B0 # B1 #",
            "# . . . #",
            "# # . # #"
        ),

        // LEVEL 04 - PUSH INTRODUCTION [EASY]
        new LevelData(
            ". . . # B0",
            "# . . . C",
            ". . . . .",
            "G0 . . . .",
            ". # . . ."
        ),

        // LEVEL 05 - PUSH AROUND [EASY+]
        new LevelData(
            "# # . # #",
            "B0 . C . G0",
            "# . . # #"
        ),

        // LEVEL 06 - SHARED LANE [MEDIUM]
        new LevelData(
            ". . G0 . .",
            "G1 # . # .",
            ". # C # .",
            ". # B0 # B1",
            ". . . . ."
        ),

        // LEVEL 07 - SWITCH DOOR [EASY]
        new LevelData(
            "# # G0 # #",
            "# # D0 # #",
            "# . . . #",
            "# B0 S0 . #",
            "# # # # #"
        ),

        // LEVEL 08 - CARGO KEY [EASY+]
        new LevelData(
            ". # # # .",
            ". . # . .",
            ". B0 D0 . .",
            "T0 B1 # . G0",
            "G1 # # # ."
        ),

        // LEVEL 09 - ONE DOOR TWO BOXES [MEDIUM]
        new LevelData(
            ". . . . S0",
            "# . B0 B1 .",
            "# # D0 # #",
            "# . . . .",
            "G1 G0 . . ."
        ),

        // LEVEL 10 - TWO KEY LOOP [HARD]
        new LevelData(
            ". . . . . .",
            ". # G1 # # .",
            "D1 S1 . . B1 C",
            ". # . # # S0",
            ". B0 D0 . . G0"
        ),

        // LEVEL 11 - SWITCH DETOUR [EASY+]
        new LevelData(
            "# . # G1 #",
            ". . D0 G0 .",
            ". B0 # . .",
            ". # . B1 .",
            "# . S0 . #"
        ),

        // LEVEL 12 - RELAY RING [MEDIUM]
        new LevelData(
            "# B0 . B1 #",
            ". D0 S1 D1 .",
            "G0 . # . S0",
            "G1 # . . .",
            "# . . . #"
        ),

        // LEVEL 13 - PORTAL INTRO [EASY]
        new LevelData(
            ". . . # . .",
            ". . . # . .",
            "B0 # . # . #",
            ". . . # . P0",
            ". . P0 # G0 ."
        ),

        // LEVEL 14 - PORTAL APPROACH [EASY+]
        new LevelData(
            ". . . # . P0",
            "P0 B0 . # G0 .",
            ". # . # G1 #",
            ". . C # . .",
            ". # B1 # . #"
        ),

        // LEVEL 15 - PASSING PORTAL [MEDIUM]
        new LevelData(
            "B1 C . . #",
            "# P0 # . B0",
            "# # # # #",
            ". . G1 . .",
            ". . . . .",
            "# P0 # G0 #"
        ),

        // LEVEL 16 - RELAY CROSSING [MEDIUM]
        new LevelData(
            ". G0 . # . B0",
            ". . . # . .",
            ". # . # B1 #",
            "G1 . . # P0 .",
            "P0 # . # . #"
        ),

        // LEVEL 17 - TWIN GATEWAYS [EASY+]
        new LevelData(
            ". . # . # # .",
            ". P1 # P0 # . .",
            ". B0 # G1 # P1 .",
            "G0 . # . # . .",
            "P0 # # . # . B1"
        ),

        // LEVEL 18 - PORTAL RELAY [MEDIUM+]
        new LevelData(
            "G1 # . # . S0",
            "D0 . . # . .",
            ". . B0 # . .",
            "G0 . B1 # . P0",
            "P0 # . # . #"
        ),

        // LEVEL 19 - ICE DETOUR [EASY]
        new LevelData(
            "# # . . G0",
            ". # . . #",
            "I0 . # . #",
            "# . . . #",
            ". . . # ."
        ),

        // LEVEL 20 - BRAKE EXCHANGE [HARD]
        new LevelData(
            ". . . . .",
            "B1 # . # .",
            ". I0 C # .",
            ". # G0 # .",
            "G1 . . . ."
        ),

        // LEVEL 21 - ICE ALIGNMENT [EASY+]
        new LevelData(
            "# . # . G0",
            "# . . . #",
            ". # B1 # .",
            ". . . . G1",
            "# . # I0 #"
        ),

        // LEVEL 22 - DOOR BRAKE [EASY+]
        new LevelData(
            "# I0 B1 . #",
            ". D0 . G1 .",
            ". . # . .",
            "G0 # S0 . .",
            "# . . . #"
        ),

        // LEVEL 23 - REMOTE BRAKE [MEDIUM]
        new LevelData(
            "# . I0 . #",
            ". # D0 C .",
            ". . # . G1",
            ". # . . S0",
            "# B1 G0 . #"
        ),

        // LEVEL 24 - ICE CROSSING [MEDIUM+]
        new LevelData(
            ". . . . . .",
            ". . . # . I0",
            ". G1 # # . .",
            ". G0 # # # I1",
            ". . . . . .",
            ". . . . . ."
        ),

        // LEVEL 25 - HEAVY INTRODUCTION [EASY]
        new LevelData(
            ". # . # .",
            ". # . G0 .",
            "B0 C C . .",
            "H1 # G1 . .",
            ". # . . ."
        ),

        // LEVEL 26 - DOUBLE FREIGHT [EASY+]
        new LevelData(
            "# # G0 # #",
            "# # D0 # #",
            "H0 C C S0 .",
            "# . . . #"
        ),

        // LEVEL 27 - MUSCLE COURIER [MEDIUM]
        new LevelData(
            ". # . # .",
            "S0 # . . G1",
            "H0 C C D0 .",
            ". # . . G0",
            "B1 # . # ."
        ),

        // LEVEL 28 - BUILD THE BRAKE [EASY+]
        new LevelData(
            "I1 . G1 . #",
            "I2 . G2 . #",
            "# # # C G0",
            "# # # C #",
            "# # # H0 #"
        ),

        // LEVEL 29 - SPLIT LOAD [MEDIUM+]
        new LevelData(
            ". D1 . H0 . .",
            "C # G0 . # .",
            "C . . . . B1",
            "S0 # . . # .",
            "G1 D0 S1 . . ."
        ),

        // LEVEL 30 - REVERSE FREIGHT [HARD]
        new LevelData(
            ". # . # .",
            "S0 # . . C",
            ". C C D0 G1",
            "H0 # . G0 .",
            "B1 # . . ."
        ),

        // LEVEL 31 - CONVEYOR INTRODUCTION [EASY]
        new LevelData(
            ". B1 B0 . .",
            "# . . . #",
            "> > > . .",
            "G0 . C G1 #",
            ". # . . ."
        ),

        // LEVEL 32 - TWO TRACKS [EASY+]
        new LevelData(
            "B1 . . # .",
            "> > > . #",
            ". . . C G0",
            ". . . . v",
            "B0 . . G1 v"
        ),

        // LEVEL 33 - RETURN BELT [MEDIUM]
        new LevelData(
            ". # . # B0",
            "^ . . C C",
            "^ G0 # . G1",
            ". . < < <",
            ". . . . B1"
        ),

        // LEVEL 34 - SWITCHBACK BELT [EASY+]
        new LevelData(
            "> > > > .",
            "B0 # B1 # G0",
            ". # . # .",
            ". # . # .",
            ". G1 D0 . S0"
        ),

        // LEVEL 35 - BELT DISPATCH [MEDIUM]
        new LevelData(
            ". . B0 G1 v",
            ". # C # v",
            "> > > # .",
            ". # G0 # .",
            ". . C . B1"
        ),

        // LEVEL 36 - CONVEYOR GATE [MEDIUM+]
        new LevelData(
            "# v . . #",
            "B0 v G1 # G0",
            "S0 D0 # . .",
            "> > C . .",
            "# . B1 . #"
        ),

        // LEVEL 37 - HOLD THE GATE [EASY]
        new LevelData(
            ". . # # .",
            ". . # . .",
            "B1 . D0 . .",
            ". T0 # G0 .",
            "G1 B0 # # ."
        ),

        // LEVEL 38 - PLATE & CRATE [EASY+]
        new LevelData(
            ". . C T0 B1",
            ". B0 . . #",
            "# # D0 # #",
            ". . G0 G1 #",
            ". . . . ."
        ),

        // LEVEL 39 - HOLD THE PATH [MEDIUM]
        new LevelData(
            ". . . . . .",
            ". # . # # B0",
            ". . . . G1 T0",
            ". # . . # C",
            ". B1 . . D0 G0"
        ),

        // LEVEL 40 - DUAL AIRLOCK [HARD]
        new LevelData(
            ". . # . .",
            ". C # G1 .",
            "B0 . D0 T1 .",
            "T0 B1 # . D1",
            ". . # # G0"
        ),

        // LEVEL 41 - PLATE DELIVERY [EASY+]
        new LevelData(
            "T0 B0 D0 . .",
            ". # . # B1",
            ". # . # .",
            ". # G0 # .",
            "> > > . G1"
        ),

        // LEVEL 42 - REPLACE THE GUARD [MEDIUM]
        new LevelData(
            "# . . B0 #",
            ". . S1 C G0",
            "B1 . # D1 T0",
            ". # . D0 .",
            "# . . G1 #"
        ),

        // LEVEL 43 - MAKE ROOM [EASY]
        new LevelData(
            "G0 BUF B1",
            "# # B0",
            ". . G1"
        ),

        // LEVEL 44 - ORDER SWAP [EASY+]
        new LevelData(
            "G0 . . B1",
            "# # # BUF",
            ". . . B0",
            "G1 # # #"
        ),

        // LEVEL 45 - PASSING BAY [MEDIUM]
        new LevelData(
            ". . . . .",
            "B0 # # # #",
            ". . B1 BUF G1",
            "# # # # .",
            ". G0 . . ."
        ),

        // LEVEL 46 - REMOVE AND RETURN [MEDIUM]
        new LevelData(
            ". . S0 B0 B1",
            "# # # # .",
            ". G0 G1 BUF D0",
            ". # # # #",
            ". . . . ."
        ),

        // LEVEL 47 - HIDE THE CARGO [EASY+]
        new LevelData(
            "B0 G1 BUF G0 B2",
            "# # # # B1",
            ". . G2 . .",
            ". # # # #"
        ),

        // LEVEL 48 - COVER SHIFT [MEDIUM+]
        new LevelData(
            ". . . . .",
            "# # # # .",
            "G1 BUF B1 T0 B0",
            ". # # # #",
            "D0 . G0 . ."
        ),

        // LEVEL 49 - REMOTE STOPPER [MEDIUM]
        new LevelData(
            ". B1 . # P0 .",
            "C . P0 # . .",
            "G1 # . # . .",
            "D0 . . # . .",
            "G0 # . # B0 S0"
        ),

        // LEVEL 50 - SHIFT REGISTER [HARD]
        new LevelData(
            ". B1 B2 D0 .",
            "# # # # .",
            "G2 BUF S0 G0 G1",
            ". # # # #",
            "B0 . . . ."
        ),

        // LEVEL 51 - THREE ROOM HANDOFF [MEDIUM]
        new LevelData(
            "B0 # # . # . G1",
            ". . # . # . .",
            "P1 P0 # B1 # G0 .",
            ". . # P1 # . C",
            ". . # . # # P0"
        ),

        // LEVEL 52 - RETURN GATE [MEDIUM+]
        new LevelData(
            "B0 # G1 # P0 #",
            "T0 C . # B1 .",
            ". . . # . .",
            ". . D0 # G0 .",
            ". # P0 # . #"
        ),

        // LEVEL 53 - DOUBLE BRAKE [MEDIUM]
        new LevelData(
            ". # . . . .",
            "H0 # I1 # . .",
            ". C C . G1 .",
            ". # . # G0 .",
            ". # . . . ."
        ),

        // LEVEL 54 - COOL STORAGE [MEDIUM+]
        new LevelData(
            ". . . . .",
            "# # # # .",
            "BUF . G1 . .",
            "B1 # # # #",
            "I0 C G0 . ."
        ),

        // LEVEL 55 - LAST HANDOFF [MEDIUM]
        new LevelData(
            ". . . C T1",
            ". # G0 # .",
            ". . . # .",
            "B0 # . # G1",
            ". S0 B1 D0 D1"
        ),

        // LEVEL 56 - DOOR GEOMETRY [MEDIUM]
        new LevelData(
            ". G1 # # . S0",
            ". # . D0 . I1",
            ". C C . G0 .",
            ". H0 # # . .",
            "# . . # # ."
        ),

        // LEVEL 57 - THREE STATE RAIL [MEDIUM+]
        new LevelData(
            ". . . . .",
            "S0 # # # #",
            "B1 . B0 D0 BUF",
            "# # # # G0",
            ". G1 . . ."
        ),

        // LEVEL 58 - BELT AND PLATE [MEDIUM+]
        new LevelData(
            ". . . B1 .",
            "# B0 . T0 .",
            ". . < < <",
            "# C . G1 #",
            "G0 D0 . . ."
        ),

        // LEVEL 59 - FACTORY REHEARSAL [HARD]
        new LevelData(
            "G0 C T0 . . .",
            "D1 # . . # .",
            "D0 S1 . B0 . .",
            ". # G1 # # .",
            "B1 . . . . ."
        ),

        // LEVEL 60 - FACTORY CORE [HARD]
        new LevelData(
            "# # # # . G0",
            "# # T0 . D0 #",
            "B1 C C . . .",
            "BUF # . P0 # #",
            "H0 # # D1 G1 #",
            "# # P0 S1 # #"
        )
    };

    private static readonly int[] moveLimits =
    {
        13, 17, 15, 14, 13, 19, 9, 11, 20, 29,
        15, 21, 11, 17, 20, 19, 15, 23, 12, 20,
        15, 17, 20, 21, 15, 9, 20, 10, 21, 29,
        13, 15, 13, 15, 20, 23, 14, 17, 20, 27,
        20, 21, 12, 17, 18, 18, 21, 20, 20, 27,
        20, 23, 21, 20, 19, 21, 20, 23, 25, 27
    };

    // Optimal (fewest) moves for each level, from the solver
    // (Tools > Slide Factory > Compute Level Pars). 3 stars = par.
    private static readonly int[] pars =
    {
        6, 10, 8, 7, 6, 13, 4, 6, 14, 21,
        10, 15, 6, 11, 14, 13, 10, 16, 7, 14,
        10, 11, 14, 15, 10, 4, 14, 5, 15, 21,
        8, 10, 8, 10, 14, 16, 9, 11, 14, 20,
        14, 15, 7, 11, 12, 12, 15, 14, 14, 20,
        14, 16, 15, 14, 13, 15, 14, 16, 18, 20
    };

    public static int GetPar(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= pars.Length)
            return GetMoveLimit(levelIndex);

        return pars[levelIndex];
    }

    // Most moves that still earn 2 stars.
    public static int GetTwoStarMoves(int levelIndex)
    {
        int par = GetPar(levelIndex);
        return par + System.Math.Max(2, (int)System.Math.Ceiling(par * 0.2));
    }

    // 3 = solved in par, 2 = close to par, 1 = solved.
    // Bought extra moves always give 1 star.
    public static int GetStars(int levelIndex, int movesUsed, bool usedExtraMoves)
    {
        if (usedExtraMoves)
            return 1;

        if (movesUsed <= GetPar(levelIndex))
            return 3;

        if (movesUsed <= GetTwoStarMoves(levelIndex))
            return 2;

        return 1;
    }

    public static LevelData GetLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Length)
            return null;

        return levels[levelIndex];
    }

    public static int GetMoveLimit(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= moveLimits.Length)
            return 30;

        return moveLimits[levelIndex];
    }

    public static int GetLevelCount()
    {
        return levels.Length;
    }
}
