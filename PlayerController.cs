using UnityEngine;
using System.Collections;

/// <summary>
/// Hlavní ovládání hráče - pohyb, skok, dash, double jump
/// Vyžaduje: Rigidbody2D, BoxCollider2D, Animator
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Pohyb")]
    public float moveSpeed = 8f;
    public float jumpForce = 14f;
    public float dashForce = 18f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 1.2f;

    [Header("Double Jump")]
    public int maxJumps = 2;
    private int jumpsRemaining;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    [Header("Dash Energie")]
    public float maxDashEnergy = 100f;
    public float dashEnergyCost = 30f;
    public float dashEnergyRegen = 15f; // za sekundu
    private float currentDashEnergy;

    [Header("Health")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Efekty")]
    public ParticleSystem dashParticles;
    public ParticleSystem landParticles;
    public TrailRenderer dashTrail;
    public GameObject hackGlitchEffect;

    // Reference
    private Rigidbody2D rb;
    private Animator anim;
    private HackingSystem hackingSystem;
    private PlayerHUD playerHUD;

    // Stav
    private bool isGrounded;
    private bool isDashing;
    private bool canDash = true;
    private bool facingRight = true;
    private float dashTimer;
    private float dashCooldownTimer;
    private bool isInvincible;

    // Inputy
    private float horizontalInput;
    private bool jumpPressed;
    private bool dashPressed;
    private bool hackPressed;

    // Animační hashe (výkon - přímý přístup)
    private static readonly int AnimRunning   = Animator.StringToHash("isRunning");
    private static readonly int AnimGrounded  = Animator.StringToHash("isGrounded");
    private static readonly int AnimJump      = Animator.StringToHash("jump");
    private static readonly int AnimDash      = Animator.StringToHash("dash");
    private static readonly int AnimHurt      = Animator.StringToHash("hurt");
    private static readonly int AnimDead      = Animator.StringToHash("dead");

    void Awake()
    {
        rb             = GetComponent<Rigidbody2D>();
        anim           = GetComponent<Animator>();
        hackingSystem  = GetComponent<HackingSystem>();
        playerHUD      = FindObjectOfType<PlayerHUD>();

        currentHealth     = maxHealth;
        currentDashEnergy = maxDashEnergy;
        jumpsRemaining    = maxJumps;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        GatherInput();
        CheckGround();
        HandleDashCooldown();
        RegenerateDashEnergy();
        HandleJump();
        HandleDash();
        HandleHack();
        UpdateAnimations();
        UpdateHUD();
    }

    void FixedUpdate()
    {
        if (!isDashing)
            Move();
    }

    // ─── Input ───────────────────────────────────────────────────────────────

    void GatherInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        jumpPressed     = Input.GetButtonDown("Jump");
        dashPressed     = Input.GetKeyDown(KeyCode.LeftShift);
        hackPressed     = Input.GetKeyDown(KeyCode.E);
    }

    // ─── Pohyb ────────────────────────────────────────────────────────────────

    void Move()
    {
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);

        // Otočení postavy
        if (horizontalInput > 0 && !facingRight) Flip();
        else if (horizontalInput < 0 && facingRight) Flip();
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // ─── Zem / Skok ───────────────────────────────────────────────────────────

    void CheckGround()
    {
        bool wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Přistání → reset skoků + efekt
        if (!wasGrounded && isGrounded)
        {
            jumpsRemaining = maxJumps;
            if (landParticles != null) landParticles.Play();
        }
    }

    void HandleJump()
    {
        if (jumpPressed && jumpsRemaining > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpsRemaining--;
            anim.SetTrigger(AnimJump);
        }
    }

    // ─── Dash ─────────────────────────────────────────────────────────────────

    void HandleDash()
    {
        if (dashPressed && canDash && currentDashEnergy >= dashEnergyCost && !isDashing)
            StartCoroutine(DoDash());
    }

    IEnumerator DoDash()
    {
        isDashing   = true;
        canDash     = false;
        currentDashEnergy -= dashEnergyCost;

        float direction = facingRight ? 1f : -1f;
        rb.linearVelocity  = new Vector2(direction * dashForce, 0f);
        rb.gravityScale = 0f;

        anim.SetTrigger(AnimDash);
        if (dashParticles != null) dashParticles.Play();
        if (dashTrail != null) dashTrail.emitting = true;

        // Třes kamery
        CameraShake.Instance?.Shake(0.15f, 0.2f);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = 1f;
        isDashing = false;
        if (dashTrail != null) dashTrail.emitting = false;

        // Cooldown
        dashCooldownTimer = dashCooldown;
    }

    void HandleDashCooldown()
    {
        if (!canDash)
        {
            dashCooldownTimer -= Time.deltaTime;
            if (dashCooldownTimer <= 0f)
                canDash = true;
        }
    }

    void RegenerateDashEnergy()
    {
        if (currentDashEnergy < maxDashEnergy)
            currentDashEnergy = Mathf.Min(maxDashEnergy, currentDashEnergy + dashEnergyRegen * Time.deltaTime);
    }

    // ─── Hackování ────────────────────────────────────────────────────────────

    void HandleHack()
    {
        if (hackPressed && hackingSystem != null)
            hackingSystem.TryHack();
    }

    // ─── Poškození / Smrt ─────────────────────────────────────────────────────

    public void TakeDamage(int damage)
    {
        if (isInvincible) return;

        currentHealth -= damage;
        currentHealth  = Mathf.Max(0, currentHealth);

        anim.SetTrigger(AnimHurt);
        CameraShake.Instance?.Shake(0.1f, 0.3f);
        StartCoroutine(InvincibilityFrames(1.2f));

        if (currentHealth <= 0)
            Die();
    }

    IEnumerator InvincibilityFrames(float duration)
    {
        isInvincible = true;
        // Blikání sprita
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float timer = 0f;
        while (timer < duration)
        {
            if (sr != null) sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.1f);
            timer += 0.1f;
        }
        if (sr != null) sr.enabled = true;
        isInvincible = false;
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    void Die()
    {
        anim.SetTrigger(AnimDead);
        rb.linearVelocity = Vector2.zero;
        enabled = false; // zastavit Update
        GameManager.Instance?.GameOver();
    }

    // ─── Animace & HUD ────────────────────────────────────────────────────────

    void UpdateAnimations()
    {
        anim.SetBool(AnimRunning,  Mathf.Abs(horizontalInput) > 0.1f);
        anim.SetBool(AnimGrounded, isGrounded);
    }

    void UpdateHUD()
    {
        if (playerHUD == null) return;
        playerHUD.UpdateHealth(currentHealth, maxHealth);
        playerHUD.UpdateDashEnergy(currentDashEnergy, maxDashEnergy);
    }

    // ─── Getters (pro HUD / jiné systémy) ────────────────────────────────────

    public int   GetHealth()         => currentHealth;
    public float GetDashEnergy()     => currentDashEnergy;
    public float GetMaxDashEnergy()  => maxDashEnergy;
    public bool  IsGrounded()        => isGrounded;

    // ─── Gizmos (editor vizualizace) ─────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
