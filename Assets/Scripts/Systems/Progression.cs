using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// PROGRESSION: stars, coins, power-ups, lives
// =============================================================
//
// Everything is stored in PlayerPrefs. Balance values live in
// EconomyConfig so they can be tuned in one place.
// =============================================================

public static class EconomyConfig
{
    // ---- coins ----
    public const int CoinsFirstClear = 10;
    public const int CoinsPerNewStar = 5;

    // ---- star chests ----
    // A chest opens every N total stars.
    public const int StarsPerChest = 12;

    // ---- power-ups ----
    public const int StartUndo = 3;
    public const int StartHint = 2;
    public const int StartExtraMoves = 1;

    public const int ExtraMovesAmount = 5;

    // Level (1-based) where each power-up button appears
    // (with a free gift the first time).
    public const int UndoUnlockLevel = 4;
    public const int HintUnlockLevel = 7;

    // Shop: pack size and price in coins.
    public const int UndoPackSize = 3;
    public const int UndoPackPrice = 30;
    public const int HintPackSize = 3;
    public const int HintPackPrice = 60;
    public const int ExtraMovesPackSize = 1;
    public const int ExtraMovesPackPrice = 40;

    // ---- lives (energy) ----
    // Off for now: the system is complete but nothing is blocked.
    public const bool LivesEnabled = false;

    public const int MaxLives = 5;
    public const int LifeRegenMinutes = 25;

    // Levels before this one (1-based) never cost a life.
    public const int LivesStartLevel = 11;

    public const int RefillLivesPrice = 100;
}

// =============================================================
// LEVEL REWARD
// =============================================================

public sealed class LevelReward
{
    public int levelIndex;
    public int stars;
    public int previousBestStars;
    public bool firstClear;
    public int coinsEarned;
    public int totalStars;

    // Power-ups found in star chests opened by this result.
    public List<PowerUpType> chestItems = new List<PowerUpType>();

    public bool IsNewBest => stars > previousBestStars;

    public static LevelReward Preview(int levelIndex, int stars)
    {
        return new LevelReward
        {
            levelIndex = levelIndex,
            stars = stars,
            previousBestStars = 0,
            totalStars = ProgressManager.TotalStars
        };
    }
}

// =============================================================
// STARS & COINS
// =============================================================

public static class ProgressManager
{
    private const string StarsKeyPrefix = "Stars_L";
    private const string CoinsKey = "Coins";
    private const string ChestsOpenedKey = "StarChestsOpened";

    public static event Action CoinsChanged;

    public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);

    public static int GetBestStars(int levelIndex)
    {
        return PlayerPrefs.GetInt(StarsKeyPrefix + levelIndex, 0);
    }

    public static int TotalStars
    {
        get
        {
            int total = 0;

            for (int i = 0; i < LevelDatabase.GetLevelCount(); i++)
                total += GetBestStars(i);

            return total;
        }
    }

    public static int ChestsOpened =>
        PlayerPrefs.GetInt(ChestsOpenedKey, 0);

    // Stars collected toward the next chest (0..StarsPerChest-1).
    public static int StarsTowardNextChest =>
        TotalStars - ChestsOpened * EconomyConfig.StarsPerChest;

    public static LevelReward RecordLevelResult(int levelIndex, int stars)
    {
        int previous = GetBestStars(levelIndex);

        LevelReward reward = new LevelReward
        {
            levelIndex = levelIndex,
            stars = stars,
            previousBestStars = previous,
            firstClear = previous == 0
        };

        if (stars > previous)
        {
            PlayerPrefs.SetInt(StarsKeyPrefix + levelIndex, stars);

            reward.coinsEarned += (stars - previous) * EconomyConfig.CoinsPerNewStar;
        }

        if (reward.firstClear)
            reward.coinsEarned += EconomyConfig.CoinsFirstClear;

        reward.totalStars = TotalStars;

        // Star chests.
        int chestsEarned = reward.totalStars / EconomyConfig.StarsPerChest;
        int opened = ChestsOpened;

        while (opened < chestsEarned)
        {
            // Chests alternate their contents so all power-ups come up.
            switch (opened % 3)
            {
                case 0:
                    reward.chestItems.Add(PowerUpType.Hint);
                    reward.chestItems.Add(PowerUpType.Undo);
                    break;
                case 1:
                    reward.chestItems.Add(PowerUpType.ExtraMoves);
                    reward.chestItems.Add(PowerUpType.Undo);
                    break;
                default:
                    reward.chestItems.Add(PowerUpType.Hint);
                    reward.chestItems.Add(PowerUpType.Hint);
                    break;
            }

            opened++;
        }

        PlayerPrefs.SetInt(ChestsOpenedKey, opened);

        foreach (PowerUpType item in reward.chestItems)
            PowerUps.Add(item, 1);

        if (reward.coinsEarned > 0)
            AddCoins(reward.coinsEarned);

        PlayerPrefs.Save();
        return reward;
    }

    // Tutorial card keys (TutorialDirector stores "Tutorial_<key>").
    public static readonly string[] TutorialKeys =
    {
        "Basics", "Crate", "Switch", "Plate", "Portal", "Ice", "Heavy", "Conveyor", "Buffer"
    };

    // Full progress reset: level, stars, coins, chests, power-ups,
    // tutorials and lives. Sound / music / vibration settings stay.
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey("CurrentLevelIndex");
        PlayerPrefs.DeleteKey(CoinsKey);
        PlayerPrefs.DeleteKey(ChestsOpenedKey);

        for (int i = 0; i < Mathf.Max(100, LevelDatabase.GetLevelCount()); i++)
            PlayerPrefs.DeleteKey(StarsKeyPrefix + i);

        PlayerPrefs.DeleteKey("PowerUps_Initialized");

        foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType)))
        {
            PlayerPrefs.DeleteKey("PowerUp_" + type);
            PlayerPrefs.DeleteKey("PowerUpIntro_" + type);
        }

        foreach (string key in TutorialKeys)
            PlayerPrefs.DeleteKey("Tutorial_" + key);

        PlayerPrefs.DeleteKey("Lives");
        PlayerPrefs.DeleteKey("LivesLastRegenUtc");

        PlayerPrefs.Save();
        CoinsChanged?.Invoke();
        Debug.Log("Progress reset: level, stars, coins, power-ups, tutorials and lives.");
    }

    public static void AddCoins(int amount)
    {
        PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + amount));
        PlayerPrefs.Save();
        SafeEvents.Raise(CoinsChanged);
    }

    public static bool TrySpendCoins(int amount)
    {
        if (amount < 0 || Coins < amount)
            return false;

        PlayerPrefs.SetInt(CoinsKey, Coins - amount);
        PlayerPrefs.Save();
        SafeEvents.Raise(CoinsChanged);
        return true;
    }
}

// =============================================================
// POWER-UPS
// =============================================================

public enum PowerUpType
{
    Undo,
    Hint,
    ExtraMoves
}

public static class PowerUps
{
    private const string InitKey = "PowerUps_Initialized";

    public static event Action Changed;

    private static string Key(PowerUpType type) => "PowerUp_" + type;

    private static void EnsureInitialized()
    {
        if (PlayerPrefs.GetInt(InitKey, 0) == 1)
            return;

        PlayerPrefs.SetInt(InitKey, 1);
        PlayerPrefs.SetInt(Key(PowerUpType.Undo), EconomyConfig.StartUndo);
        PlayerPrefs.SetInt(Key(PowerUpType.Hint), EconomyConfig.StartHint);
        PlayerPrefs.SetInt(Key(PowerUpType.ExtraMoves), EconomyConfig.StartExtraMoves);
        PlayerPrefs.Save();
    }

    public static int Count(PowerUpType type)
    {
        EnsureInitialized();
        return PlayerPrefs.GetInt(Key(type), 0);
    }

    public static void Add(PowerUpType type, int amount)
    {
        EnsureInitialized();
        PlayerPrefs.SetInt(Key(type), Mathf.Max(0, Count(type) + amount));
        PlayerPrefs.Save();
        SafeEvents.Raise(Changed);
    }

    public static bool TryConsume(PowerUpType type)
    {
        if (Count(type) <= 0)
            return false;

        Add(type, -1);
        return true;
    }

    public static bool IsUnlocked(PowerUpType type, int levelIndex)
    {
        switch (type)
        {
            case PowerUpType.Undo:
                return levelIndex + 1 >= EconomyConfig.UndoUnlockLevel;
            case PowerUpType.Hint:
                return levelIndex + 1 >= EconomyConfig.HintUnlockLevel;
            default:
                return true;
        }
    }

    public static int PackSize(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Undo: return EconomyConfig.UndoPackSize;
            case PowerUpType.Hint: return EconomyConfig.HintPackSize;
            default: return EconomyConfig.ExtraMovesPackSize;
        }
    }

    public static int PackPrice(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Undo: return EconomyConfig.UndoPackPrice;
            case PowerUpType.Hint: return EconomyConfig.HintPackPrice;
            default: return EconomyConfig.ExtraMovesPackPrice;
        }
    }

    public static bool TryBuyPack(PowerUpType type)
    {
        if (!ProgressManager.TrySpendCoins(PackPrice(type)))
            return false;

        Add(type, PackSize(type));
        return true;
    }

    public static string DisplayName(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Undo: return "UNDO";
            case PowerUpType.Hint: return "HINT";
            default: return "+" + EconomyConfig.ExtraMovesAmount + " MOVES";
        }
    }

    // One-time "you unlocked X" gift tracking.
    public static bool WasIntroduced(PowerUpType type) =>
        PlayerPrefs.GetInt("PowerUpIntro_" + type, 0) == 1;

    public static void MarkIntroduced(PowerUpType type)
    {
        PlayerPrefs.SetInt("PowerUpIntro_" + type, 1);
        PlayerPrefs.Save();
    }
}

// =============================================================
// LIVES (energy)
// =============================================================
//
// A life is lost when a level is failed or restarted after moves
// were made. Winning never costs a life. Lives refill one by one
// in real time (UTC, so changing the phone clock backwards does
// not break the timer).
//
// EconomyConfig.LivesEnabled = false keeps the whole system
// silent: nothing is charged and play is never blocked.
// =============================================================

public static class LivesManager
{
    private const string LivesKey = "Lives";
    private const string LastRegenKey = "LivesLastRegenUtc";

    public static event Action Changed;

    // TestMode turns the system on for automated tests only.
    public static bool TestMode;

    public static bool Enabled => EconomyConfig.LivesEnabled || TestMode;

    private static long NowTicks => DateTime.UtcNow.Ticks;

    private static long RegenTicks =>
        TimeSpan.FromMinutes(EconomyConfig.LifeRegenMinutes).Ticks;

    public static int Lives
    {
        get
        {
            ApplyRegeneration();
            return PlayerPrefs.GetInt(LivesKey, EconomyConfig.MaxLives);
        }
    }

    public static bool IsFull => Lives >= EconomyConfig.MaxLives;

    public static bool CanPlay(int levelIndex)
    {
        if (!Enabled || !CostsLife(levelIndex))
            return true;

        return Lives > 0;
    }

    public static bool CostsLife(int levelIndex) =>
        levelIndex + 1 >= EconomyConfig.LivesStartLevel;

    // Seconds until the next life (0 when full).
    public static int SecondsToNextLife
    {
        get
        {
            if (IsFull)
                return 0;

            long last = ReadLastRegen();
            long elapsed = Math.Max(0, NowTicks - last);
            long remaining = RegenTicks - elapsed % RegenTicks;

            return (int)Math.Ceiling(TimeSpan.FromTicks(remaining).TotalSeconds);
        }
    }

    public static void OnLevelFailed(int levelIndex) => LoseLife(levelIndex);

    public static void OnLevelAbandoned(int levelIndex) => LoseLife(levelIndex);

    private static void LoseLife(int levelIndex)
    {
        if (!Enabled || !CostsLife(levelIndex))
            return;

        int lives = Lives;

        if (lives <= 0)
            return;

        // The timer starts when the first life is missing.
        if (lives >= EconomyConfig.MaxLives)
            WriteLastRegen(NowTicks);

        PlayerPrefs.SetInt(LivesKey, lives - 1);
        PlayerPrefs.Save();
        SafeEvents.Raise(Changed);
    }

    public static void Refill()
    {
        PlayerPrefs.SetInt(LivesKey, EconomyConfig.MaxLives);
        WriteLastRegen(NowTicks);
        PlayerPrefs.Save();
        SafeEvents.Raise(Changed);
    }

    public static bool TryBuyRefill()
    {
        if (IsFull || !ProgressManager.TrySpendCoins(EconomyConfig.RefillLivesPrice))
            return false;

        Refill();
        return true;
    }

    private static void ApplyRegeneration()
    {
        int lives = PlayerPrefs.GetInt(LivesKey, EconomyConfig.MaxLives);

        if (lives >= EconomyConfig.MaxLives)
            return;

        long now = NowTicks;
        long last = ReadLastRegen();

        // Clock moved backwards: restart the timer from now.
        if (last > now)
        {
            WriteLastRegen(now);
            return;
        }

        long gained = (now - last) / RegenTicks;

        if (gained <= 0)
            return;

        int newLives = (int)Math.Min(EconomyConfig.MaxLives, lives + gained);

        PlayerPrefs.SetInt(LivesKey, newLives);
        WriteLastRegen(newLives >= EconomyConfig.MaxLives
            ? now
            : last + gained * RegenTicks);

        PlayerPrefs.Save();
        SafeEvents.Raise(Changed);
    }

    private static long ReadLastRegen()
    {
        string raw = PlayerPrefs.GetString(LastRegenKey, "");
        return long.TryParse(raw, out long ticks) ? ticks : NowTicks;
    }

    private static void WriteLastRegen(long ticks)
    {
        PlayerPrefs.SetString(LastRegenKey, ticks.ToString());
    }

    public static string FormatTime(int seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, seconds));
        return t.TotalHours >= 1
            ? string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
            : string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);
    }
}
