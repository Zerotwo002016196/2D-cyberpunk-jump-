using UnityEngine;
using System.Collections;

/// <summary>
/// Systém hackování - hráč může hacknout terminály v dosahu klávesou E
/// Komunikuje s HackableObject (dveře, lasery, kamery)
/// </summary>
public class HackingSystem : MonoBehaviour
{
    [Header("Nastavení hackování")]
    public float hackRange = 3f;
    public float hackTime = 1.5f;       // sekund na hacknutí
    public LayerMask hackableLayer;

    [Header("Efekty")]
    public GameObject hackBeam;         // vizuální paprsek k cíli
    public AudioSource hackAudioSource;
    public AudioClip hackStartClip;
    public AudioClip hackCompleteClip;
    public AudioClip hackFailClip;

    // Stav
    private bool isHacking;
    private HackableObject currentTarget;
    private Coroutine hackCoroutine;

    // UI odkaz
    private PlayerHUD playerHUD;

    void Awake()
    {
        playerHUD = FindObjectOfType<PlayerHUD>();
    }

    /// <summary>Pokusit se hacknout nejbližší objekt v dosahu</summary>
    public void TryHack()
    {
        if (isHacking)
        {
            CancelHack();
            return;
        }

        HackableObject target = FindNearestHackable();
        if (target != null)
            hackCoroutine = StartCoroutine(HackRoutine(target));
        else
            PlayClip(hackFailClip);
    }

    /// <summary>Najít nejbližší hacknutelný objekt v dosahu</summary>
    HackableObject FindNearestHackable()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hackRange, hackableLayer);
        HackableObject nearest = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            HackableObject ho = hit.GetComponent<HackableObject>();
            if (ho == null || ho.IsHacked) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = ho;
            }
        }
        return nearest;
    }

    /// <summary>Korutina hackování s progressem</summary>
    IEnumerator HackRoutine(HackableObject target)
    {
        isHacking     = true;
        currentTarget = target;

        PlayClip(hackStartClip);
        target.StartHackVisual();

        // Glitch efekt na kameře
        GlitchEffect.Instance?.TriggerGlitch(hackTime);

        // Zobrazit progress bar v HUD
        playerHUD?.ShowHackProgress(true);

        float timer = 0f;
        while (timer < hackTime)
        {
            // Přerušení - hráč se příliš vzdálil
            if (Vector2.Distance(transform.position, target.transform.position) > hackRange + 0.5f)
            {
                CancelHack();
                yield break;
            }

            timer += Time.deltaTime;
            playerHUD?.UpdateHackProgress(timer / hackTime);
            yield return null;
        }

        // Hacknutí úspěšné
        target.ExecuteHack();
        PlayClip(hackCompleteClip);
        playerHUD?.ShowHackProgress(false);

        isHacking     = false;
        currentTarget = null;
    }

    void CancelHack()
    {
        if (hackCoroutine != null) StopCoroutine(hackCoroutine);
        currentTarget?.CancelHackVisual();
        playerHUD?.ShowHackProgress(false);
        isHacking     = false;
        currentTarget = null;
        GlitchEffect.Instance?.StopGlitch();
    }

    void PlayClip(AudioClip clip)
    {
        if (hackAudioSource != null && clip != null)
            hackAudioSource.PlayOneShot(clip);
    }

    // Gizmos - dosah hackování
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hackRange);
    }
}
