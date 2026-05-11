using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════════════════
// MainMenu.cs - Cyberpunk hlavní menu
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ovládání hlavního menu - start, nastavení, quit
/// Přidej na Canvas v MainMenu scéně
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Panely")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [Header("Animace")]
    public Animator titleAnimator;
    public float startDelay = 0.5f;

    [Header("Zvuky")]
    public AudioSource menuMusic;
    public AudioClip buttonClickClip;
    public AudioSource sfxSource;

    [Header("UI Elementy")]
    public TextMeshProUGUI versionText;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    void Start()
    {
        if (versionText != null)
            versionText.text = $"v{Application.version}";

        // Načíst uložená nastavení
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);

        mainPanel?.SetActive(true);
        settingsPanel?.SetActive(false);
        creditsPanel?.SetActive(false);
    }

    // ─── Tlačítka ──────────────────────────────────────────────────────────

    public void OnStartGame()
    {
        PlayClick();
        StartCoroutine(LoadGameRoutine());
    }

    IEnumerator LoadGameRoutine()
    {
        yield return new WaitForSeconds(startDelay);
        SceneManager.LoadScene("Level_01_Rooftops");
    }

    public void OnSettings()
    {
        PlayClick();
        mainPanel?.SetActive(false);
        settingsPanel?.SetActive(true);
    }

    public void OnBackFromSettings()
    {
        PlayClick();
        PlayerPrefs.Save();
        settingsPanel?.SetActive(false);
        mainPanel?.SetActive(true);
    }

    public void OnCredits()
    {
        PlayClick();
        mainPanel?.SetActive(false);
        creditsPanel?.SetActive(true);
    }

    public void OnBackFromCredits()
    {
        PlayClick();
        creditsPanel?.SetActive(false);
        mainPanel?.SetActive(true);
    }

    public void OnQuit()
    {
        PlayClick();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ─── Nastavení ─────────────────────────────────────────────────────────

    public void OnMusicVolumeChanged(float value)
    {
        if (menuMusic != null) menuMusic.volume = value;
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    public void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        AudioListener.volume = value;
    }

    void PlayClick()
    {
        if (sfxSource != null && buttonClickClip != null)
            sfxSource.PlayOneShot(buttonClickClip);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// LaserTrap.cs - automatická laserová past
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Laser, který se zapíná a vypíná v intervalu</summary>
public class LaserTrap : MonoBehaviour
{
    [Header("Nastavení")]
    public int damagePerSecond = 20;
    public float onDuration  = 2f;
    public float offDuration = 1f;
    public bool startsOn = true;

    [Header("Vizuál")]
    public SpriteRenderer laserSprite;
    public ParticleSystem sparkParticles;
    public Color activeColor   = new Color(1f, 0.1f, 0.3f, 0.9f);
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);

    private bool isActive;
    private float timer;
    private Collider2D laserCol;

    void Start()
    {
        laserCol = GetComponent<Collider2D>();
        isActive = startsOn;
        timer = isActive ? onDuration : offDuration;
        UpdateVisual();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            isActive = !isActive;
            timer = isActive ? onDuration : offDuration;
            UpdateVisual();
        }
    }

    void UpdateVisual()
    {
        if (laserSprite != null)
            laserSprite.color = isActive ? activeColor : inactiveColor;
        if (laserCol != null)
            laserCol.enabled = isActive;
        if (sparkParticles != null)
        {
            if (isActive) sparkParticles.Play();
            else sparkParticles.Stop();
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive) return;
        if (other.CompareTag("Player"))
            other.GetComponent<PlayerController>()?.TakeDamage(
                Mathf.RoundToInt(damagePerSecond * Time.deltaTime)
            );
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// BossTrigger.cs - aktivuje boss fight při vstupu do arény
// ═══════════════════════════════════════════════════════════════════════════════

public class BossTrigger : MonoBehaviour
{
    public BossAI boss;
    public GameObject arenaBlocker; // zabrání útěku

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        boss?.ActivateBoss();
        if (arenaBlocker != null) arenaBlocker.SetActive(true);
        gameObject.SetActive(false);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MusicManager.cs - správa hudby mezi scénami
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Singleton hudební manažer - přehrává soundtrack, fade in/out při přechodu
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Hudba")]
    public AudioSource musicSource;
    public AudioClip menuMusic;
    public AudioClip[] levelMusics;  // indexy odpovídají číslům levelů
    public AudioClip bossMusic;
    public float fadeDuration = 1.5f;

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

    public void PlayMenuMusic()  => StartCoroutine(FadeToClip(menuMusic));
    public void PlayLevelMusic(int index)
    {
        if (index < levelMusics.Length)
            StartCoroutine(FadeToClip(levelMusics[index]));
    }
    public void PlayBossMusic()  => StartCoroutine(FadeToClip(bossMusic));

    System.Collections.IEnumerator FadeToClip(AudioClip newClip)
    {
        // Fade out
        float startVol = musicSource.volume;
        float t = 0f;
        while (t < fadeDuration * 0.5f)
        {
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / (fadeDuration * 0.5f));
            t += Time.deltaTime;
            yield return null;
        }

        musicSource.clip = newClip;
        musicSource.Play();

        // Fade in
        t = 0f;
        float targetVol = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        while (t < fadeDuration * 0.5f)
        {
            musicSource.volume = Mathf.Lerp(0f, targetVol, t / (fadeDuration * 0.5f));
            t += Time.deltaTime;
            yield return null;
        }
        musicSource.volume = targetVol;
    }
}
