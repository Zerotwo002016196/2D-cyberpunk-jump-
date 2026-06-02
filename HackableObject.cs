using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Základní třída pro všechny hacknutelné objekty.
/// Dědí: DoorHackable, LaserHackable, CameraHackable
/// </summary>
public class HackableObject : MonoBehaviour
{
    [Header("Hackování")]
    public string objectName = "Terminál";
    public bool isOneTimeHack = true;   // lze hacknout pouze jednou?
    public float resetDelay = 0f;       // 0 = nenastane reset

    [Header("Vizuální indikátor")]
    public SpriteRenderer indicatorSprite;
    public Color idleColor    = new Color(0f, 1f, 1f, 0.8f);   // cyan
    public Color hackingColor = new Color(1f, 1f, 0f, 1f);     // žlutá
    public Color hackedColor  = new Color(0f, 1f, 0f, 0.5f);   // zelená

    [Header("Efekty")]
    public ParticleSystem hackParticles;
    public AudioSource audioSource;
    public AudioClip hackSound;

    [Header("Unity Events")]
    public UnityEvent OnHacked;         // volá se po hacknutí
    public UnityEvent OnReset;          // volá se po resetu

    // Stav
    public bool IsHacked { get; private set; }

    protected virtual void Start()
    {
        SetIndicatorColor(idleColor);
    }

    /// <summary>Vizuální feedback - hackování probíhá</summary>
    public void StartHackVisual()
    {
        SetIndicatorColor(hackingColor);
    }

    /// <summary>Zrušení hackování</summary>
    public void CancelHackVisual()
    {
        SetIndicatorColor(IsHacked ? hackedColor : idleColor);
    }

    /// <summary>Provedení hacknutí - přepis v podtřídách</summary>
    public virtual void ExecuteHack()
    {
        IsHacked = true;
        SetIndicatorColor(hackedColor);

        if (hackParticles != null) hackParticles.Play();
        if (audioSource != null && hackSound != null)
            audioSource.PlayOneShot(hackSound);

        OnHacked?.Invoke();

        if (!isOneTimeHack && resetDelay > 0f)
            Invoke(nameof(ResetHack), resetDelay);
    }

    /// <summary>Reset hacknutelného objektu</summary>
    public virtual void ResetHack()
    {
        IsHacked = false;
        SetIndicatorColor(idleColor);
        OnReset?.Invoke();
    }

    void SetIndicatorColor(Color c)
    {
        if (indicatorSprite != null) indicatorSprite.color = c;
    }

    /// <summary>Zobrazit jméno objektu při najetí</summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            PlayerHUD.Instance?.ShowHackPrompt(objectName);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            PlayerHUD.Instance?.HideHackPrompt();
    }
}

// ─── Specializované hacknutelné objekty ───────────────────────────────────────

/// <summary>Hacknutelné dveře - otevřou se po hacknutí</summary>
public class DoorHackable : HackableObject
{
    public Animator doorAnimator;
    public string openTrigger  = "Open";
    public string closeTrigger = "Close";

    public override void ExecuteHack()
    {
        base.ExecuteHack();
        doorAnimator?.SetTrigger(openTrigger);
    }

    public override void ResetHack()
    {
        base.ResetHack();
        doorAnimator?.SetTrigger(closeTrigger);
    }
}

/// <summary>Hacknutelný laser - vypne se po hacknutí</summary>
public class LaserHackable : HackableObject
{
    public GameObject laserBeam;        // vizuální paprsek
    public Collider2D laserCollider;    // kolizní objem

    public override void ExecuteHack()
    {
        base.ExecuteHack();
        if (laserBeam != null)     laserBeam.SetActive(false);
        if (laserCollider != null) laserCollider.enabled = false;
    }

    public override void ResetHack()
    {
        base.ResetHack();
        if (laserBeam != null)     laserBeam.SetActive(true);
        if (laserCollider != null) laserCollider.enabled = true;
    }
}

/// <summary>Hacknutelná kamera - ztuhne po hacknutí, nedetekuje hráče</summary>
public class CameraHackable : HackableObject
{
    private SecurityCamera linkedCamera;

    protected override void Start()
    {
        base.Start();
        linkedCamera = GetComponentInParent<SecurityCamera>();
    }

    public override void ExecuteHack()
    {
        base.ExecuteHack();
        linkedCamera?.Disable();
    }

    public override void ResetHack()
    {
        base.ResetHack();
        linkedCamera?.Enable();
    }
}
