using UnityEngine;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════════════════
// CameraShake.cs - třes kamery pro impakt
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Singleton pro třes kamery. Volej: CameraShake.Instance.Shake(intenzita, trvání)</summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        originalPosition = transform.localPosition;
    }

    public void Shake(float intensity, float duration)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
    }

    IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            float fade = 1f - (timer / duration);
            transform.localPosition = originalPosition + (Vector3)Random.insideUnitCircle * intensity * fade;
            timer += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originalPosition;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// GlitchEffect.cs - glitch / scan-line efekt při hackování
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Vizuální glitch efekt - přidej Material s Glitch Shaderem na full-screen Image
/// nebo použij cinemachine noise profil
/// </summary>
public class GlitchEffect : MonoBehaviour
{
    public static GlitchEffect Instance { get; private set; }

    [Header("Glitch Material")]
    public Material glitchMaterial;    // PostProcess nebo UI Overlay material
    public float glitchIntensity = 0.05f;

    private Coroutine glitchCoroutine;
    private bool isGlitching;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TriggerGlitch(float duration)
    {
        if (glitchCoroutine != null) StopCoroutine(glitchCoroutine);
        glitchCoroutine = StartCoroutine(GlitchRoutine(duration));
    }

    public void StopGlitch()
    {
        if (glitchCoroutine != null) StopCoroutine(glitchCoroutine);
        SetGlitch(0f);
        isGlitching = false;
    }

    IEnumerator GlitchRoutine(float duration)
    {
        isGlitching = true;
        float timer = 0f;

        while (timer < duration)
        {
            float t = timer / duration;
            // Náhodná intenzita pro organický pocit
            float intensity = isGlitching ? glitchIntensity * Random.Range(0.3f, 1f) : 0f;
            SetGlitch(intensity);

            timer += Time.deltaTime;
            yield return null;
        }

        SetGlitch(0f);
        isGlitching = false;
    }

    void SetGlitch(float value)
    {
        if (glitchMaterial != null)
            glitchMaterial.SetFloat("_GlitchIntensity", value);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// RainParticles.cs - atmosferický déšť
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Řídí efekt deště - sleduje kameru, přizpůsobuje intenzitu</summary>
public class RainParticles : MonoBehaviour
{
    [Header("Déšť")]
    public ParticleSystem rainParticleSystem;
    public float rainIntensityNormal = 200f;
    public float rainIntensityHeavy  = 800f;
    public bool heavyRain;

    private Camera mainCamera;
    private ParticleSystem.EmissionModule emission;

    void Start()
    {
        mainCamera = Camera.main;
        emission = rainParticleSystem.emission;
        SetRainIntensity(heavyRain);
    }

    void LateUpdate()
    {
        // Déšť sleduje kameru
        if (mainCamera != null)
        {
            Vector3 pos = mainCamera.transform.position;
            transform.position = new Vector3(pos.x, pos.y + 10f, 0f);
        }
    }

    public void SetRainIntensity(bool heavy)
    {
        heavyRain = heavy;
        emission.rateOverTime = heavy ? rainIntensityHeavy : rainIntensityNormal;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ParallaxBackground.cs - vícevrstvé paralaxní pozadí
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Paralaxní vrstva pozadí - každá vrstva se pohybuje různou rychlostí
/// Přidej na každou vrstvu s různým parallaxMultiplier (0.1 = vzdálené, 0.9 = blízké)
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("Paralax")]
    [Range(0f, 1f)]
    public float parallaxMultiplier = 0.5f;
    public bool infiniteHorizontal = true;

    private Transform camTransform;
    private Vector3 lastCameraPos;
    private float textureUnitSizeX;

    void Start()
    {
        camTransform  = Camera.main.transform;
        lastCameraPos = camTransform.position;

        Sprite sprite = GetComponent<SpriteRenderer>().sprite;
        Texture2D texture = sprite.texture;
        textureUnitSizeX = texture.width / sprite.pixelsPerUnit;
    }

    void LateUpdate()
    {
        Vector3 delta = camTransform.position - lastCameraPos;

        // Pohyb o zlomek pohybu kamery
        transform.position += new Vector3(delta.x * parallaxMultiplier, delta.y * parallaxMultiplier, 0f);
        lastCameraPos = camTransform.position;

        // Nekonečné opakování
        if (infiniteHorizontal)
        {
            float distX = camTransform.position.x * (1f - parallaxMultiplier);
            float relX  = distX % textureUnitSizeX;
            transform.position = new Vector3(camTransform.position.x + relX,
                                             transform.position.y,
                                             transform.position.z);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MovingPlatform.cs - pohyblivá platforma
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Platforma pohybující se mezi dvěma body</summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("Body pohybu")]
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;
    public bool waitAtEnds = true;
    public float waitTime = 1f;

    private Vector3 targetPos;
    private bool waiting;

    void Start() => targetPos = pointB.position;

    void Update()
    {
        if (waiting) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.05f)
        {
            targetPos = targetPos == pointA.position ? pointB.position : pointA.position;
            if (waitAtEnds) StartCoroutine(WaitRoutine());
        }
    }

    System.Collections.IEnumerator WaitRoutine()
    {
        waiting = true;
        yield return new WaitForSeconds(waitTime);
        waiting = false;
    }

    // Hráč se pohybuje s platformou
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            col.transform.SetParent(transform);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
            col.transform.SetParent(null);
    }
}
