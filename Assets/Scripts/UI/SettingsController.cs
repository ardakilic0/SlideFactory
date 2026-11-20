using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsController : MonoBehaviour
{
    public static SettingsController Instance { get; private set; }

    private const string SoundKey = "SoundEnabled";
    private const string MusicKey = "MusicEnabled";
    private const string HapticsKey = "HapticsEnabled";

    [Header("Overlay")]
    [SerializeField] private GameObject settingsOverlay;

    [Header("Toggle Images")]
    [SerializeField] private Image soundToggleImage;
    [SerializeField] private Image musicToggleImage;
    [SerializeField] private Image vibrationToggleImage;

    [Header("Toggle Sprites")]
    [SerializeField] private Sprite toggleOnSprite;
    [SerializeField] private Sprite toggleOffSprite;

    [Header("Gameplay Only")]
    [SerializeField] private bool showGameplayActions = true;
    [SerializeField] private GameObject dividerObject;
    [SerializeField] private GameObject restartButtonObject;
    [SerializeField] private GameObject mainMenuButtonObject;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool soundEnabled;
    private bool musicEnabled;
    private bool hapticsEnabled;

    public bool IsOpen =>
        settingsOverlay != null &&
        settingsOverlay.activeSelf;

    public bool SoundEnabled => soundEnabled;
    public bool MusicEnabled => musicEnabled;
    public bool HapticsEnabled => hapticsEnabled;

    private void Awake()
    {
        Instance = this;

        LoadSettings();
        HapticsManager.SetEnabled(hapticsEnabled);
        AudioManager.SetSoundEnabled(soundEnabled);
        AudioManager.SetMusicEnabled(musicEnabled);

        RefreshToggleVisuals();
        RefreshSceneLayout();

        if (settingsOverlay != null)
            settingsOverlay.SetActive(false);
    }

    private void LoadSettings()
    {
        soundEnabled =
            PlayerPrefs.GetInt(SoundKey, 1) == 1;

        musicEnabled =
            PlayerPrefs.GetInt(MusicKey, 1) == 1;

        hapticsEnabled =
            PlayerPrefs.GetInt(HapticsKey, 1) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(
            SoundKey,
            soundEnabled ? 1 : 0
        );

        PlayerPrefs.SetInt(
            MusicKey,
            musicEnabled ? 1 : 0
        );

        PlayerPrefs.SetInt(
            HapticsKey,
            hapticsEnabled ? 1 : 0
        );

        PlayerPrefs.Save();
    }

    private void RefreshToggleVisuals()
    {
        SetToggleVisual(
            soundToggleImage,
            soundEnabled
        );

        SetToggleVisual(
            musicToggleImage,
            musicEnabled
        );

        SetToggleVisual(
            vibrationToggleImage,
            hapticsEnabled
        );
    }

    private void SetToggleVisual(
        Image targetImage,
        bool enabled)
    {
        if (targetImage == null)
            return;

        targetImage.sprite =
            enabled
                ? toggleOnSprite
                : toggleOffSprite;
    }

    private void RefreshSceneLayout()
    {
        if (dividerObject != null)
            dividerObject.SetActive(showGameplayActions);

        if (restartButtonObject != null)
            restartButtonObject.SetActive(showGameplayActions);

        if (mainMenuButtonObject != null)
            mainMenuButtonObject.SetActive(showGameplayActions);
    }

    public void OpenSettings()
    {
        if (settingsOverlay == null)
            return;

        settingsOverlay.SetActive(true);
        settingsOverlay.transform.SetAsLastSibling();

        RefreshToggleVisuals();
    }

    public void CloseSettings()
    {
        if (settingsOverlay != null)
            settingsOverlay.SetActive(false);
    }

    public void ToggleSound()
    {
        soundEnabled = !soundEnabled;

        SetToggleVisual(
            soundToggleImage,
            soundEnabled
        );

        SaveSettings();
        AudioManager.SetSoundEnabled(soundEnabled);
    }

    public void ToggleMusic()
    {
        musicEnabled = !musicEnabled;

        SetToggleVisual(
            musicToggleImage,
            musicEnabled
        );

        SaveSettings();
        AudioManager.SetMusicEnabled(musicEnabled);
    }

    public void ToggleVibration()
    {
        if (hapticsEnabled)
            HapticsManager.UI();

        hapticsEnabled = !hapticsEnabled;

        HapticsManager.SetEnabled(hapticsEnabled);

        SetToggleVisual(
            vibrationToggleImage,
            hapticsEnabled
        );

        SaveSettings();

        if (hapticsEnabled)
            HapticsManager.UI();
    }

    public void RestartLevel()
    {
        if (LevelManager.Instance == null)
            return;

        CloseSettings();
        LevelManager.Instance.RestartLevel();
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}