using UnityEngine;
using System.Collections;

/// <summary>
/// Boss AI - Velký AI Robot
/// Fáze 1 (>50% HP): základní útoky
/// Fáze 2 (≤50% HP): rychlejší, nové útoky, hacknutelné slabiny
/// </summary>
public class BossAI : MonoBehaviour
{
    [Header("Statistiky")]
    public int maxHealth = 500;
    public int currentHealth;
    public int phase1Damage = 15;
    public int phase2Damage = 25;

    [Header("Pohyb")]
    public float moveSpeed = 4f;
    public float phase2MoveSpeed = 7f;
    public float jumpForce = 12f;

    [Header("Útoky")]
    public float meleeRange = 2f;
    public float rangedRange = 10f;
    public GameObject projectilePrefab;
    public Transform shootPoint;
    public float shootCooldown = 2f;

    [Header("Fáze 2 - speciální útoky")]
    public GameObject laserPrefab;
    public float laserDuration = 3f;
    public int laserDamagePerSecond = 10;

    [Header("Hacknutelné slabiny")]
    public HackableObject[] weakPoints;  // hacknutí způsobí stun

    [Header("Efekty")]
    public ParticleSystem phase2TransitionFX;
    public GameObject shieldEffect;
    public AudioClip roarClip;
    public AudioClip phase2TransitionClip;
    public AudioClip deathClip;

    [Header("UI")]
    public BossHealthBar bossHealthBar;

    // Komponenty
    private Rigidbody2D rb;
    private Animator anim;
    private AudioSource audioSrc;

    // Stav
    public enum BossState { Idle, Approach, MeleeAttack, RangedAttack, LaserSweep, Stunned, Dead }
    private BossState state = BossState.Idle;
    private Transform playerTransform;
    private bool phase2Triggered;
    private float attackTimer;
    private bool isBossActive;

    // Animace
    private static readonly int AnimWalk   = Animator.StringToHash("isWalking");
    private static readonly int AnimMelee  = Animator.StringToHash("meleeAttack");
    private static readonly int AnimRanged = Animator.StringToHash("rangedAttack");
    private static readonly int AnimStun   = Animator.StringToHash("stunned");
    private static readonly int AnimDead   = Animator.StringToHash("dead");
    private static readonly int AnimPhase2 = Animator.StringToHash("phase2");

    void Awake()
    {
        rb             = GetComponent<Rigidbody2D>();
        anim           = GetComponent<Animator>();
        audioSrc       = GetComponent<AudioSource>();
        currentHealth  = maxHealth;
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Start()
    {
        // Zobrazit boss HP bar
        bossHealthBar?.Initialize(maxHealth, "NEXUS-9 CORE");
        bossHealthBar?.gameObject.SetActive(false);

        // Hacknutelné slabiny - po hacknutí stun bossa
        foreach (var wp in weakPoints)
        {
            if (wp != null)
                wp.OnHacked.AddListener(() => Stun(4f));
        }
    }

    void Update()
    {
        if (!isBossActive || state == BossState.Dead) return;
        attackTimer -= Time.deltaTime;
        CheckPhase2();
        ExecuteStateMachine();
    }

    // ─── Aktivace bosse ───────────────────────────────────────────────────────

    /// <summary>Zavolat z trigger zóny - vstup hráče do boss arény</summary>
    public void ActivateBoss()
    {
        isBossActive = true;
        bossHealthBar?.gameObject.SetActive(true);
        audioSrc?.PlayOneShot(roarClip);
        CameraShake.Instance?.Shake(0.3f, 0.5f);
        state = BossState.Approach;
    }

    // ─── State Machine ────────────────────────────────────────────────────────

    void ExecuteStateMachine()
    {
        switch (state)
        {
            case BossState.Approach:     HandleApproach();    break;
            case BossState.MeleeAttack:  HandleMelee();       break;
            case BossState.RangedAttack: HandleRanged();      break;
            case BossState.LaserSweep:   /* korutina */       break;
            case BossState.Stunned:      /* čeká na timer */  break;
        }
    }

    void HandleApproach()
    {
        if (playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist <= meleeRange && attackTimer <= 0f)
        {
            state = BossState.MeleeAttack;
            return;
        }

        if (dist > meleeRange && dist <= rangedRange && attackTimer <= 0f)
        {
            state = phase2Triggered ? BossState.LaserSweep : BossState.RangedAttack;
            return;
        }

        // Pohyb k hráči
        float speed = phase2Triggered ? phase2MoveSpeed : moveSpeed;
        float dir   = playerTransform.position.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        anim.SetBool(AnimWalk, true);
    }

    void HandleMelee()
    {
        rb.linearVelocity = Vector2.zero;
        anim.SetBool(AnimWalk, false);
        anim.SetTrigger(AnimMelee);

        int dmg = phase2Triggered ? phase2Damage : phase1Damage;
        Collider2D hit = Physics2D.OverlapCircle(transform.position, meleeRange, LayerMask.GetMask("Player"));
        if (hit != null)
            hit.GetComponent<PlayerController>()?.TakeDamage(dmg);

        CameraShake.Instance?.Shake(0.2f, 0.3f);
        attackTimer = phase2Triggered ? shootCooldown * 0.7f : shootCooldown;
        state = BossState.Approach;
    }

    void HandleRanged()
    {
        rb.linearVelocity = Vector2.zero;
        anim.SetTrigger(AnimRanged);

        if (projectilePrefab != null && shootPoint != null)
        {
            Vector2 dir = ((Vector2)playerTransform.position - (Vector2)shootPoint.position).normalized;
            GameObject proj = Instantiate(projectilePrefab, shootPoint.position, Quaternion.identity);
            proj.GetComponent<Rigidbody2D>()?.AddForce(dir * 15f, ForceMode2D.Impulse);
        }

        attackTimer = shootCooldown;
        state = BossState.Approach;
    }

    // ─── Fáze 2 ───────────────────────────────────────────────────────────────

    void CheckPhase2()
    {
        if (phase2Triggered) return;
        if (currentHealth <= maxHealth / 2)
            StartCoroutine(TriggerPhase2());
    }

    IEnumerator TriggerPhase2()
    {
        phase2Triggered = true;
        state = BossState.Stunned;

        rb.linearVelocity = Vector2.zero;
        audioSrc?.PlayOneShot(phase2TransitionClip);
        phase2TransitionFX?.Play();
        anim.SetTrigger(AnimPhase2);

        if (shieldEffect != null) shieldEffect.SetActive(true);

        CameraShake.Instance?.Shake(0.5f, 1f);
        GlitchEffect.Instance?.TriggerGlitch(2f);

        yield return new WaitForSeconds(2.5f);

        if (shieldEffect != null) shieldEffect.SetActive(false);
        state = BossState.Approach;
    }

    // ─── Poškození & Smrt ─────────────────────────────────────────────────────

    public void TakeDamage(int damage)
    {
        if (state == BossState.Dead) return;

        currentHealth -= damage;
        currentHealth  = Mathf.Max(0, currentHealth);
        bossHealthBar?.UpdateHealth(currentHealth);

        StartCoroutine(FlashRed());

        if (currentHealth <= 0)
            StartCoroutine(BossDie());
    }

    public void Stun(float duration)
    {
        if (state == BossState.Dead) return;
        StartCoroutine(StunRoutine(duration));
    }

    IEnumerator StunRoutine(float duration)
    {
        state = BossState.Stunned;
        rb.linearVelocity = Vector2.zero;
        anim.SetBool(AnimStun, true);
        yield return new WaitForSeconds(duration);
        anim.SetBool(AnimStun, false);
        state = BossState.Approach;
    }

    IEnumerator BossDie()
    {
        state = BossState.Dead;
        rb.linearVelocity = Vector2.zero;
        anim.SetTrigger(AnimDead);
        audioSrc?.PlayOneShot(deathClip);

        CameraShake.Instance?.Shake(0.8f, 2f);
        GlitchEffect.Instance?.TriggerGlitch(3f);

        yield return new WaitForSeconds(3f);

        bossHealthBar?.gameObject.SetActive(false);
        // Přechod na výhru / další level
        GameManager.Instance?.LoadNextLevel();
    }

    IEnumerator FlashRed()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) { sr.color = Color.red; yield return new WaitForSeconds(0.08f); sr.color = Color.white; }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangedRange);
    }
}
