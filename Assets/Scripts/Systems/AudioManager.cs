using UnityEngine;

public enum GameSfx
{
    UIClick,
    Move,
    Slide,
    Push,
    HeavyPush,
    Switch,
    Plate,
    DoorOpen,
    DoorClose,
    Portal,
    Buffer,
    Goal,
    Invalid,
    LevelComplete,
    Fail,
    Rewind,
    StarLost,
    StarEarned,
    Coin,
    Popup
}

// =============================================================
// AUDIO MANAGER
// =============================================================
//
// Sound effects + music. Creates itself on first use and lives
// across scenes, so no scene setup is needed.
//
// Clips are loaded from Assets/Resources/Audio by file name
// (see ClipNames). A missing clip is skipped silently, so a
// sound can be replaced by dropping a new file with the same
// name into that folder. Music plays "music_loop" if it exists.
//
// Sound / Music toggles use the same PlayerPrefs keys as
// SettingsController ("SoundEnabled", "MusicEnabled").
// =============================================================

public class AudioManager : MonoBehaviour
{
    private const string SoundKey = "SoundEnabled";
    private const string MusicKey = "MusicEnabled";

    private const string ResourceFolder = "Audio/";
    private const string MusicClipName = "music_loop";

    private const int SfxVoiceCount = 10;
    private const float SfxVolume = 1f;
    private const float MusicVolume = 0.35f;

    // The same clip triggered twice within this window
    // (e.g. two doors with the same ID) is played once.
    private const float SameClipCooldown = 0.05f;

    // Index = (int)GameSfx.
    private static readonly string[] ClipNames =
    {
        "sfx_ui_click",
        "sfx_move",
        "sfx_slide",
        "sfx_push",
        "sfx_push_heavy",
        "sfx_switch",
        "sfx_plate",
        "sfx_door_open",
        "sfx_door_close",
        "sfx_portal",
        "sfx_buffer",
        "sfx_goal",
        "sfx_invalid",
        "sfx_level_complete",
        "sfx_fail",
        "sfx_rewind",
        "sfx_star_lost",
        "sfx_star",
        "sfx_coin",
        "sfx_popup"
    };

    // Per-sound mix level (index = (int)GameSfx). Frequent sounds sit
    // lower so long sessions stay comfortable.
    private static readonly float[] Volumes =
    {
        // The clips are loudness-balanced when generated (frequent
        // gameplay sounds only ~3 dB under the celebration sounds), so
        // these stay near 1. Tweak here to rebalance in the editor.
        1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f
    };

    // Extra variations: "<name>_2", "<name>_3" ... are picked at random
    // (never the same one twice in a row).
    private const int MaxVariants = 4;

    private static AudioManager instance;
    private static bool isQuitting;

    private AudioSource[] sfxVoices;
    private AudioSource musicSource;
    private AudioClip[][] clips;
    private int[] lastVariant;
    private float[] lastPlayTime;

    private int nextVoice;
    private float suppressUntil;

    private bool soundEnabled = true;
    private bool musicEnabled = true;

    // =========================================================
    // BOOTSTRAP
    // =========================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Keeps things correct when Domain Reload is disabled.
        instance = null;
        isQuitting = false;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetInstance();
    }

    private static AudioManager GetInstance()
    {
        if (isQuitting)
            return null;

        if (instance != null)
            return instance;

        GameObject root =
            new GameObject("AudioManager");

        DontDestroyOnLoad(root);

        instance =
            root.AddComponent<AudioManager>();

        instance.Initialize();

        return instance;
    }

    private void Initialize()
    {
        sfxVoices =
            new AudioSource[SfxVoiceCount];

        for (int i = 0; i < SfxVoiceCount; i++)
        {
            AudioSource voice =
                gameObject.AddComponent<AudioSource>();

            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;

            sfxVoices[i] = voice;
        }

        musicSource =
            gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = MusicVolume;

        clips =
            new AudioClip[ClipNames.Length][];

        lastVariant =
            new int[ClipNames.Length];

        lastPlayTime =
            new float[ClipNames.Length];

        for (int i = 0; i < ClipNames.Length; i++)
        {
            System.Collections.Generic.List<AudioClip> found =
                new System.Collections.Generic.List<AudioClip>();

            AudioClip first =
                Resources.Load<AudioClip>(ResourceFolder + ClipNames[i]);

            if (first != null)
                found.Add(first);

            for (int v = 2; v <= MaxVariants; v++)
            {
                AudioClip variant =
                    Resources.Load<AudioClip>(ResourceFolder + ClipNames[i] + "_" + v);

                if (variant != null)
                    found.Add(variant);
            }

            clips[i] = found.ToArray();
            lastVariant[i] = -1;
            lastPlayTime[i] = -1f;
        }

        musicSource.clip =
            Resources.Load<AudioClip>(
                ResourceFolder + MusicClipName
            );

        soundEnabled =
            PlayerPrefs.GetInt(SoundKey, 1) == 1;

        musicEnabled =
            PlayerPrefs.GetInt(MusicKey, 1) == 1;

        RefreshMusic();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public static void Play(GameSfx sfx)
    {
        AudioManager manager = GetInstance();

        if (manager != null)
            manager.PlayInternal(sfx, -1f);
    }

    // Fixed pitch (e.g. rising notes for the 1st/2nd/3rd star).
    public static void Play(GameSfx sfx, float pitch)
    {
        AudioManager manager = GetInstance();

        if (manager != null)
            manager.PlayInternal(sfx, pitch);
    }

    public static void SetSoundEnabled(bool value)
    {
        AudioManager manager = GetInstance();

        if (manager != null)
            manager.soundEnabled = value;
    }

    public static void SetMusicEnabled(bool value)
    {
        AudioManager manager = GetInstance();

        if (manager == null)
            return;

        manager.musicEnabled = value;
        manager.RefreshMusic();
    }

    // Ignores sound effects for a short time, e.g. while a level is
    // being built (a Box spawned on a Pressure Plate opens its door).
    public static void SuppressSfx(float seconds)
    {
        AudioManager manager = GetInstance();

        if (manager == null)
            return;

        manager.suppressUntil =
            Time.unscaledTime + seconds;
    }

    // =========================================================
    // INTERNAL
    // =========================================================

    private void PlayInternal(GameSfx sfx, float pitch)
    {
        if (!soundEnabled)
            return;

        float now = Time.unscaledTime;

        if (now < suppressUntil)
            return;

        int index = (int)sfx;

        if (index < 0 || index >= clips.Length)
            return;

        AudioClip[] variants = clips[index];

        if (variants == null || variants.Length == 0)
            return;

        int pick = 0;

        if (variants.Length > 1)
        {
            pick = Random.Range(0, variants.Length - 1);

            if (pick >= lastVariant[index])
                pick++;
        }

        lastVariant[index] = pick;
        AudioClip clip = variants[pick];

        if (now - lastPlayTime[index] < SameClipCooldown)
            return;

        lastPlayTime[index] = now;

        AudioSource voice = sfxVoices[nextVoice];
        nextVoice = (nextVoice + 1) % sfxVoices.Length;

        voice.Stop();
        voice.clip = clip;
        voice.volume = SfxVolume * (index < Volumes.Length ? Volumes[index] : 0.7f);
        voice.pitch = pitch > 0f ? pitch : GetPitch(sfx);
        voice.Play();
    }

    // Small random pitch variation keeps frequent sounds
    // (moves, pushes) from feeling repetitive.
    private static float GetPitch(GameSfx sfx)
    {
        switch (sfx)
        {
            case GameSfx.Move:
            case GameSfx.Slide:
            case GameSfx.Push:
            case GameSfx.HeavyPush:
            case GameSfx.UIClick:
                return Random.Range(0.97f, 1.03f);

            default:
                return 1f;
        }
    }

    private void RefreshMusic()
    {
        if (musicSource == null)
            return;

        if (musicEnabled && musicSource.clip != null)
        {
            if (!musicSource.isPlaying)
                musicSource.Play();
        }
        else if (musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }
}
