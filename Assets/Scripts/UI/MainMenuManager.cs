using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    private const string CurrentLevelKey = "CurrentLevelIndex";

    [Header("Level Numbers")]
    [SerializeField] private TMP_Text currentLevelText;
    [SerializeField] private TMP_Text nextLevelText;
    [SerializeField] private TMP_Text nextNextLevelText;

    [Header("Level Objects")]
    [SerializeField] private GameObject nextLevelObject;
    [SerializeField] private GameObject nextNextLevelObject;

    [Header("Connectors")]
    [SerializeField] private GameObject connector1;
    [SerializeField] private GameObject connector2;
    [SerializeField] private GameObject connector3;

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "Game";

    private int currentLevelIndex;
    private int totalLevels;

    private void Start()
    {
        RefreshMenu();
    }

    private void RefreshMenu()
    {
        // Toplam level sayısını LevelDatabase üzerinden al.
        totalLevels = LevelDatabase.GetLevelCount();

        if (totalLevels <= 0)
        {
            Debug.LogError(
                "MainMenuManager: LevelDatabase boş!"
            );

            return;
        }

        // Kayıtlı ilerlemeyi oku.
        currentLevelIndex = PlayerPrefs.GetInt(
            CurrentLevelKey,
            0
        );

        currentLevelIndex = Mathf.Clamp(
            currentLevelIndex,
            0,
            totalLevels - 1
        );

        // Sıfır tabanlı index'i gerçek level numarasına çevir.
        int currentLevelNumber = currentLevelIndex + 1;

        int nextLevelNumber = currentLevelNumber + 1;

        int nextNextLevelNumber = currentLevelNumber + 2;

        bool hasNextLevel =
            nextLevelNumber <= totalLevels;

        bool hasNextNextLevel =
            nextNextLevelNumber <= totalLevels;

        // CURRENT LEVEL
        if (currentLevelText != null)
        {
            currentLevelText.text =
                currentLevelNumber.ToString();
        }

        // NEXT LEVEL
        if (nextLevelObject != null)
        {
            nextLevelObject.SetActive(hasNextLevel);
        }

        if (nextLevelText != null && hasNextLevel)
        {
            nextLevelText.text =
                nextLevelNumber.ToString();
        }

        // NEXT NEXT LEVEL
        if (nextNextLevelObject != null)
        {
            nextNextLevelObject.SetActive(
                hasNextNextLevel
            );
        }

        if (nextNextLevelText != null && hasNextNextLevel)
        {
            nextNextLevelText.text =
                nextNextLevelNumber.ToString();
        }

        // CONNECTORS
        if (connector1 != null)
        {
            connector1.SetActive(hasNextLevel);
        }

        if (connector2 != null)
        {
            connector2.SetActive(hasNextNextLevel);
        }

        if (connector3 != null)
        {
            connector3.SetActive(hasNextNextLevel);
        }

        Debug.Log(
            "Main Menu Updated | Current: " +
            currentLevelNumber +
            " | Next: " +
            (hasNextLevel ? nextLevelNumber.ToString() : "None") +
            " | Next Next: " +
            (hasNextNextLevel ? nextNextLevelNumber.ToString() : "None") +
            " | Total Levels: " +
            totalLevels
        );
    }

    public void PlayGame()
    {
        if (!LivesManager.CanPlay(currentLevelIndex))
        {
            MainMenuExtras.ShowOutOfLives(PlayGame);
            return;
        }

        SceneManager.LoadScene(
            gameplaySceneName
        );
    }

    // =========================================================
    // DEVELOPMENT / TEST
    // =========================================================

    [ContextMenu("Reset Progression to Level 1")]
    private void ResetProgressionToLevel1()
    {
        // Everything: level, stars, coins, power-ups, tutorials, lives.
        ProgressManager.ResetAll();

        Debug.Log(
            "Progression reset. Next game starts from Level 1."
        );

        if (Application.isPlaying)
        {
            RefreshMenu();
        }
    }
}