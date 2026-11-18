public static class LevelRulesDatabase
{
    // =========================================================
    // TEMPORARY MOVE LIMITS
    // =========================================================
    //
    // Bunlar mevcut eski 26 level icin
    // gecici test degerleri.
    //
    // Yeni 30 level tasarlanirken her biri
    // optimal cozum + toleransa gore ayarlanacak.
    // =========================================================

    private static readonly int[] moveLimits =
    {
        8,   // Level 1
        12,  // Level 2
        16,  // Level 3
        14,  // Level 4
        20,  // Level 5
        22,  // Level 6
        20,  // Level 7
        22,  // Level 8
        30,  // Level 9
        16,  // Level 10
        24,  // Level 11
        26,  // Level 12
        20,  // Level 13
        26,  // Level 14
        18,  // Level 15
        30,  // Level 16
        16,  // Level 17
        24,  // Level 18
        30,  // Level 19
        22,  // Level 20
        26,  // Level 21
        12,  // Level 22
        16,  // Level 23
        22,  // Level 24
        18,  // Level 25
        20   // Level 26
    };


    public static int GetMoveLimit(
        int levelIndex)
    {
        if (levelIndex < 0 ||
            levelIndex >= moveLimits.Length)
        {
            // Yeni level eklenmis ama burada
            // henuz limit tanimlanmamissa
            // guvenli fallback.
            return 20;
        }

        return moveLimits[levelIndex];
    }
}