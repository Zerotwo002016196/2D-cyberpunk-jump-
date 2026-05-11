using UnityEngine;
using System.Collections;

/// <summary>
/// AI nepřátel - dron i bezpečnostní robot
/// Stavy: Patrol → Alert → Chase → Attack → Return
/// </summary>
public class EnemyAI : MonoBehaviour
{
    // ─── Typy nepřátel ────────────────────────────────────────────────────────
    public enum EnemyType { SecurityDrone, SecurityRobot }
    public enum EnemyState { Patrol, Alert, Chase, Attack, Stunned, Dead }

    [Header("Typ nepřítele")]
    public EnemyType enemyType = EnemyType.SecurityRobot;

    [Header("Základní statistiky")]
    public int maxHealth = 50;
    public int attackDamage = 10;
    public float attackCooldown = 1.5f;
    public float attackRange = 1.5f;

    [Header("Pohyb")]
    public float patrolSpeed = 2f;
    public float chaseSpeed  = 5f;
    public float droneHoverAmplitude = 0.5f;  // jen pro dron
    public float droneHoverSpeed     = 2f;

    [Header("Detekce hráče")]
    public float sightRange = 8f;
    public float hearingRange = 4f;
    public float fieldOfView = 60f;   // stupně
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Patrol body")]
    public Transform[] patrolPoints;
    private int currentPatrolIndex;
    private Vector3 startPosition;

    [Header("Efekty")]
    public GameObject alertIcon;       // ! ikonka
    public GameObject stunEffect;
    public ParticleSystem deathParticles;
    public AudioClip alertSound;
    public AudioClip attackSound;
    public AudioClip deathSound;

    [Header("Loot")]
    public GameObject cyberUpgradePrefab;
    [Range(0f, 1f)] public float dropChance = 0.3f;

    // Komponenty
    private Rigidbody2D rb;
    private Animator    anim;
    private AudioSource audioSrc;
    private SpriteRenderer sr;

    // Stav
    private EnemyState currentState = EnemyState.Patrol;
    private Transform  playerTransform;
    private int        currentHealth;
    private float      attackTimer;
    private bool       isAlerted;
    private float      hoverOffset;
    private float      stunTimer;

    // Animační hashe
    private static readonly int AnimMove   = Animator.StringToHash("isMoving");
    private static readonly int AnimAttack = Animator.StringToHash("attack");
    private static readonly int AnimDead   = Animator.StringToHash("dead");
    private static readonly int AnimStun   = Animator.StringToHash("stunned");

    void Awake()
    {
        rb        = GetComponent<Rigidbody2D>();
        anim      = GetComponent<Animator>();
        audioSrc  = GetComponent<AudioSource>();
        sr        = GetComponent<SpriteRenderer>();

        currentHealth   = maxHealth;
        startPosition   = transform.position;
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;

        attackTimer -= Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Patrol:  HandlePatrol();  break;
            case EnemyState.Alert:   HandleAlert();   break;
            case EnemyState.Chase:   HandleChase();   break;
            case EnemyState.Attack:  HandleAttack();  break;
            case EnemyState.Stunned: HandleStunned(); break;
        }

        if (enemyType == EnemyType.SecurityDrone)
            HoverEffect();
    }

    // ─── Stavové handlery ─────────────────────────────────────────────────────

    void HandlePatrol()
    {
        if (CanSeePlayer())
        {
            EnterAlertState();
            return;
        }

        MoveTowardsPatrolPoint();
        CheckPatrolPointReached();
    }

    void HandleAlert()
    {
        // Krátká pauza - "všimnutí" hráče
        rb.linearVelocity = Vector2.zero;
        anim.SetBool(AnimMove, false);

        if (CanSeePlayer())
        {
            currentState = EnemyState.Chase;
            AlarmManager.Instance?.TriggerAlarm(transform.position);
        }
        else
        {
            // Vrátit se na patrol po chvíli
            Invoke(nameof(ReturnToPatrol), 2f);
        }
    }

    void HandleChase()
    {
        if (playerTransform == null) return;

        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distToPlayer <= attackRange)
        {
            currentState = EnemyState.Attack;
            return;
        }

        if (distToPlayer > sightRange * 1.5f)
        {
            ReturnToPatrol();
            return;
        }

        // Pohyb k hráči
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * chaseSpeed, rb.linearVelocity.y);

        // Flip
        if (direction.x < 0) FaceLeft();
        else FaceRight();

        anim.SetBool(AnimMove, true);
    }

    void HandleAttack()
    {
        if (playerTransform == null) return;
        rb.linearVelocity = Vector2.zero;

        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distToPlayer > attackRange)
        {
            currentState = EnemyState.Chase;
            return;
        }

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }
    }

    void HandleStunned()
    {
        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f)
        {
            if (stunEffect != null) stunEffect.SetActive(false);
            currentState = EnemyState.Patrol;
        }
    }

    // ─── Akce ─────────────────────────────────────────────────────────────────

    void PerformAttack()
    {
        anim.SetTrigger(AnimAttack);
        audioSrc?.PlayOneShot(attackSound);

        // Zkontrolovat, zda je hráč stále v dosahu
        Collider2D hit = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
        if (hit != null)
            hit.GetComponent<PlayerController>()?.TakeDamage(attackDamage);
    }

    void EnterAlertState()
    {
        if (alertIcon != null) alertIcon.SetActive(true);
        audioSrc?.PlayOneShot(alertSound);
        currentState = EnemyState.Alert;
        Invoke(nameof(HideAlertIcon), 1f);
    }

    void HideAlertIcon() { if (alertIcon != null) alertIcon.SetActive(false); }

    void ReturnToPatrol()
    {
        CancelInvoke(nameof(ReturnToPatrol));
        currentState = EnemyState.Patrol;
        isAlerted = false;
    }

    public void Stun(float duration)
    {
        stunTimer = duration;
        currentState = EnemyState.Stunned;
        rb.linearVelocity = Vector2.zero;
        anim.SetBool(AnimStun, true);
        if (stunEffect != null) stunEffect.SetActive(true);
    }

    public void TakeDamage(int dmg)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= dmg;
        StartCoroutine(FlashRed());

        if (currentHealth <= 0)
            Die();
        else if (currentState == EnemyState.Patrol)
            EnterAlertState();
    }

    void Die()
    {
        currentState = EnemyState.Dead;
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;

        anim.SetTrigger(AnimDead);
        audioSrc?.PlayOneShot(deathSound);
        if (deathParticles != null) deathParticles.Play();

        // Šance na drop
        if (cyberUpgradePrefab != null && Random.value <= dropChance)
            Instantiate(cyberUpgradePrefab, transform.position, Quaternion.identity);

        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 2f);
    }

    // ─── Detekce ──────────────────────────────────────────────────────────────

    bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        Vector2 toPlayer = playerTransform.position - transform.position;
        float distance   = toPlayer.magnitude;

        if (distance > sightRange) return false;

        // FOV check
        float angle = Vector2.Angle(GetFacingDirection(), toPlayer);
        if (angle > fieldOfView * 0.5f) return false;

        // Raycast - není za překážkou?
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            toPlayer.normalized,
            distance,
            obstacleLayer | playerLayer
        );

        return hit.collider != null && hit.collider.CompareTag("Player");
    }

    Vector2 GetFacingDirection() =>
        sr.flipX ? Vector2.left : Vector2.right;

    // ─── Patrol ───────────────────────────────────────────────────────────────

    void MoveTowardsPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 target    = patrolPoints[currentPatrolIndex].position;
        Vector2 direction = (target - (Vector2)transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * patrolSpeed, rb.linearVelocity.y);

        if (direction.x < 0) FaceLeft();
        else FaceRight();

        anim.SetBool(AnimMove, true);
    }

    void CheckPatrolPointReached()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (Vector2.Distance(transform.position, patrolPoints[currentPatrolIndex].position) < 0.3f)
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
    }

    // ─── Utility ──────────────────────────────────────────────────────────────

    void HoverEffect()
    {
        hoverOffset = Mathf.Sin(Time.time * droneHoverSpeed) * droneHoverAmplitude;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, hoverOffset);
    }

    void FaceLeft()  { if (sr != null) sr.flipX = true;  }
    void FaceRight() { if (sr != null) sr.flipX = false; }

    IEnumerator FlashRed()
    {
        if (sr != null) sr.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (sr != null) sr.color = Color.white;
    }

    void OnDrawGizmosSelected()
    {
        // Dosah vidění
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        // Dosah útoku
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
