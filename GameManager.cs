using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Hlavní správce hry - scény, pause, game over, level progression
/// Singleton - přetrvává mezi scénami
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scény")]
    public string mainMenuScene  = "MainMenu";
    public string gameOverScene  = "GameOver";
    public string nextLevelScene = "Level2";

    [Header("Nastavení")]
    public bool IsPaused { get; private set; }

    [Header("Score")]
    public int collectedUpgrades;
    public int enemiesDefeated;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ─── Pause ────────────────────────────────────────────────────────────────

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        PlayerHUD.Instance?.ShowPauseMenu(true);
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        PlayerHUD.Instance?.ShowPauseMenu(false);
    }

    public void TogglePause()
    {
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    // ─── Level ────────────────────────────────────────────────────────────────

    public void GameOver()
    {
        StartCoroutine(GameOverRoutine());
    }

    IEnumerator GameOverRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameOverScene);
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        // Respawn u posledního checkpointu
        CheckpointSystem checkpoint = FindObjectOfType<CheckpointSystem>();
        if (checkpoint != null)
            checkpoint.RespawnPlayer();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    public void LoadNextLevel()
    {
        StartCoroutine(LoadLevelRoutine(nextLevelScene));
    }

    IEnumerator LoadLevelRoutine(string sceneName)
    {
        // Loading screen
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float progress = 0f;
        while (!op.isDone)
        {
            progress = Mathf.Clamp01(op.progress / 0.9f);
            LoadingScreen.Instance?.UpdateProgress(progress);

            if (op.progress >= 0.9f)
                op.allowSceneActivation = true;

            yield return null;
        }
    }

    // ─── Input ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CheckpointSystem.cs - systém checkpointů a respawnu
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Správce checkpointů - ukládá pozici hráče</summary>
public class CheckpointSystem : MonoBehaviour
{
    public static CheckpointSystem Instance { get; private set; }

    private Vector3 lastCheckpointPosition;
    private Transform playerTransform;
    private PlayerController playerController;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform  = player.transform;
            playerController = player.GetComponent<PlayerController>();
            lastCheckpointPosition = player.transform.position;
        }
    }

    public void SaveCheckpoint(Vector3 position)
    {
        lastCheckpointPosition = position;
        Debug.Log($"[Checkpoint] Uloženo: {position}");

        // Efekt
        PlayerHUD.Instance?.ShowCheckpointNotification();
    }

    public void RespawnPlayer()
    {
        if (playerTransform == null) return;

        playerTransform.position = lastCheckpointPosition;
        playerController?.Heal(playerController.maxHealth); // plné HP po respawnu

        // Restartnout alarm a nepřátele
        AlarmManager.Instance?.DeactivateAlarm();

        // Znovu načíst nepřátele (volitelně)
        Debug.Log("[Checkpoint] Respawn na pozici: " + lastCheckpointPosition);
    }
}
