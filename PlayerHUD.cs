using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════════════════
// PlayerHUD.cs - Cyberpunk HUD
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Kompletní HUD - HP, energie, hack progress, alarm, pause menu, game over
/// Všechny UI elementy v Canvas s CanvasScaler (Scale With Screen Size 1920x1080)
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    public static PlayerHUD Instance { get; private set; }

    [Header("HP Bar")]
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public Image hpFill;
    public Color hpHighColor = new Color(0f, 1f, 0.8f);   // cyan
    public Color hpLowColor  = new Color(1f, 0.2f, 0.4f); // červená

    [Header("Dash Energie")]
    public Slider dashEnergySlider;
    public TextMeshProUGUI dashEnergyText;
    public Image dashEnergyFill;
    public Color energyFullColor  = new Color(0.6f, 0.2f, 1f);    // fialová
    public Color energyEmptyColor = new Color(0.2f, 0.2f, 0.3f);

    [Header("Hack Progress")]
    public GameObject hackProgressPanel;
    public Slider hackProgressSlider;
    public TextMeshProUGUI hackProgressText;
    public TextMeshProUGUI hackPromptText;     // "Stiskni E pro hack"

    [Header("Detekce kamery")]
    public Slider detectionSlider;
    public GameObject detectionPanel;

    [Header("Alarm")]
    public GameObject alarmPanel;
    public TextMeshProUGUI alarmText;

    [Header("Checkpoint")]
    public GameObject checkpointNotification;

    [Header("Menus")]
    public GameObject pauseMenuPanel;
    public GameObject gameOverPanel;

    [Header("Boss HP")]
    public BossHealthBar bossHealthBar;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        hackProgressPanel?.SetActive(false);
        alarmPanel?.SetActive(false);
        checkpointNotification?.SetActive(false);
        pauseMenuPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        detectionPanel?.SetActive(false);
        hackPromptText?.gameObject.SetActive(false);
    }

    // ─── HP & Energie ─────────────────────────────────────────────────────────

    public void UpdateHealth(int current, int max)
    {
        float ratio = (float)current / max;
        if (hpSlider != null) hpSlider.value = ratio;
        if (hpText != null) hpText.text = $"{current}/{max}";
        if (hpFill != null) hpFill.color = Color.Lerp(hpLowColor, hpHighColor, ratio);
    }

    public void UpdateDashEnergy(float current, float max)
    {
        float ratio = current / max;
        if (dashEnergySlider != null) dashEnergySlider.value = ratio;
        if (dashEnergyText != null) dashEnergyText.text = $"{Mathf.RoundToInt(current)}%";
        if (dashEnergyFill != null) dashEnergyFill.color = Color.Lerp(energyEmptyColor, energyFullColor, ratio);
    }

    // ─── Hackování ────────────────────────────────────────────────────────────

    public void ShowHackProgress(bool show)
    {
        hackProgressPanel?.SetActive(show);
        if (!show && hackProgressSlider != null) hackProgressSlider.value = 0f;
    }

    public void UpdateHackProgress(float t)
    {
        if (hackProgressSlider != null) hackProgressSlider.value = t;
        if (hackProgressText != null) hackProgressText.text = $"HACKUJI... {Mathf.RoundToInt(t * 100f)}%";
    }

    public void ShowHackPrompt(string objectName)
    {
        if (hackPromptText != null)
        {
            hackPromptText.text = $"[E] HACK {objectName.ToUpper()}";
            hackPromptText.gameObject.SetActive(true);
        }
    }

    public void HideHackPrompt()
    {
        hackPromptText?.gameObject.SetActive(false);
    }

    // ─── Detekce & Alarm ──────────────────────────────────────────────────────

    public void ShowDetectionWarning(float t)
    {
        if (t > 0.05f)
        {
            detectionPanel?.SetActive(true);
            if (detectionSlider != null) detectionSlider.value = t;
        }
        else
        {
            detectionPanel?.SetActive(false);
        }
    }

    public void ShowAlarmWarning(bool show)
    {
        alarmPanel?.SetActive(show);
        if (show) StartCoroutine(FlashAlarmText());
    }

    IEnumerator FlashAlarmText()
    {
        while (alarmPanel != null && alarmPanel.activeSelf)
        {
            if (alarmText != null) alarmText.enabled = !alarmText.enabled;
            yield return new WaitForSeconds(0.4f);
        }
        if (alarmText != null) alarmText.enabled = true;
    }

    // ─── Checkpoint ───────────────────────────────────────────────────────────

    public void ShowCheckpointNotification()
    {
        StartCoroutine(CheckpointNotificationRoutine());
    }

    IEnumerator CheckpointNotificationRoutine()
    {
        if (checkpointNotification != null)
        {
            checkpointNotification.SetActive(true);
            yield return new WaitForSeconds(2.5f);
            checkpointNotification.SetActive(false);
        }
    }

    // ─── Menus ────────────────────────────────────────────────────────────────

    public void ShowPauseMenu(bool show) => pauseMenuPanel?.SetActive(show);
    public void ShowGameOver(bool show)  => gameOverPanel?.SetActive(show);

    // ─── Tlačítka (propojit v Inspectoru) ────────────────────────────────────

    public void OnResumeButton()    => GameManager.Instance?.ResumeGame();
    public void OnRestartButton()   => GameManager.Instance?.RestartLevel();
    public void OnMainMenuButton()  => GameManager.Instance?.LoadMainMenu();
    public void OnQuitButton()      => Application.Quit();
}

// ═══════════════════════════════════════════════════════════════════════════════
// BossHealthBar.cs
// ═══════════════════════════════════════════════════════════════════════════════

public class BossHealthBar : MonoBehaviour
{
    public Slider healthSlider;
    public TextMeshProUGUI bossNameText;
    public TextMeshProUGUI healthValueText;
    private int maxHealth;

    public void Initialize(int max, string name)
    {
        maxHealth = max;
        if (healthSlider != null) { healthSlider.maxValue = max; healthSlider.value = max; }
        if (bossNameText != null) bossNameText.text = name;
    }

    public void UpdateHealth(int current)
    {
        if (healthSlider != null) healthSlider.value = current;
        if (healthValueText != null) healthValueText.text = $"{current} / {maxHealth}";
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// LoadingScreen.cs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Loading screen s cyberpunk animací</summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    public Slider loadingBar;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI percentText;
    private string[] loadingMessages =
    {
        "INICIALIZACE SYSTÉMU...",
        "PŘIPOJOVÁNÍ K NEXUS CORE...",
        "NAČÍTÁNÍ SÍŤOVÝCH PROTOKOLŮ...",
        "KALIBROVÁNÍ HOLOGRAFIKY...",
        "PŘIPRAVEN K DEPLOYMENTU"
    };
    private int messageIndex;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void UpdateProgress(float t)
    {
        if (loadingBar != null) loadingBar.value = t;
        if (percentText != null) percentText.text = $"{Mathf.RoundToInt(t * 100f)}%";

        int idx = Mathf.FloorToInt(t * loadingMessages.Length);
        idx = Mathf.Clamp(idx, 0, loadingMessages.Length - 1);
        if (idx != messageIndex)
        {
            messageIndex = idx;
            if (loadingText != null) loadingText.text = loadingMessages[idx];
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CyberUpgrade.cs - sběratelný upgrade
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Sběratelný cyber upgrade - zvyšuje statistiky hráče</summary>
public class CyberUpgrade : MonoBehaviour
{
    public enum UpgradeType { HealthBoost, DashRange, AttackPower, JumpHeight }

    [Header("Nastavení")]
    public UpgradeType upgradeType = UpgradeType.HealthBoost;
    public int upgradeValue = 10;
    public string upgradeName = "Nano Implantát";

    [Header("Efekty")]
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;
    public ParticleSystem collectParticles;
    public AudioClip collectSound;

    private Vector3 startPos;
    private AudioSource audioSrc;

    void Start()
    {
        startPos = transform.position;
        audioSrc = GetComponent<AudioSource>();
    }

    void Update()
    {
        // Floating animace
        float y = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        ApplyUpgrade(other.GetComponent<PlayerController>());
        audioSrc?.PlayOneShot(collectSound);
        collectParticles?.Play();

        if (GameManager.Instance != null) GameManager.Instance.collectedUpgrades++;

        PlayerHUD.Instance?.ShowCheckpointNotification(); // recycle jako "upgrade získán" notifikaci

        GetComponent<SpriteRenderer>().enabled = false;
        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 1.5f);
    }

    void ApplyUpgrade(PlayerController player)
    {
        if (player == null) return;

        switch (upgradeType)
        {
            case UpgradeType.HealthBoost:
                player.maxHealth += upgradeValue;
                player.Heal(upgradeValue);
                break;
            case UpgradeType.DashRange:
                player.dashForce += upgradeValue * 0.5f;
                break;
            case UpgradeType.JumpHeight:
                player.jumpForce += upgradeValue * 0.2f;
                break;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CheckpointTrigger.cs - fyzický checkpoint v levelu
// ═══════════════════════════════════════════════════════════════════════════════

public class CheckpointTrigger : MonoBehaviour
{
    private bool activated;
    public Animator checkpointAnim;
    public ParticleSystem activateParticles;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated || !other.CompareTag("Player")) return;

        activated = true;
        CheckpointSystem.Instance?.SaveCheckpoint(transform.position);
        checkpointAnim?.SetTrigger("activate");
        activateParticles?.Play();
    }
}
