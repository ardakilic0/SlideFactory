using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Level UI")]
    [SerializeField] private TMP_Text levelText;

    [Header("Level Complete")]
    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private TMP_Text completeLevelText;

    private int currentLevelNumber = 1;

    private void Awake()
    {
        Instance = this;

        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
    }

    [ContextMenu("Replay Level Complete")]
    private void ReplayLevelComplete()
    {
        if (levelCompletePanel == null)
            return;

        levelCompletePanel.SetActive(false);
        levelCompletePanel.SetActive(true);
    }

    public void SetLevelNumber(int levelNumber)
    {
        currentLevelNumber = levelNumber;

        if (levelText != null)
            levelText.text = "LEVEL " + levelNumber;

        if (completeLevelText != null)
            completeLevelText.text = "LEVEL " + levelNumber;
    }

    public void ShowLevelComplete()
    {
        HapticsManager.LevelComplete();

        if (completeLevelText != null)
            completeLevelText.text = "LEVEL " + currentLevelNumber;

        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(true);
    }

    public void HideLevelComplete()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
    }

    public void RestartLevel()
    {
        if (LevelManager.Instance != null)
            LevelManager.Instance.RestartLevel();
    }
}