// ═══════════════════════════════════════════════════════════════════════════════
// SecurityCamera.cs
// Kamera detekující hráče - spustí alarm po detekci
// ═══════════════════════════════════════════════════════════════════════════════
using UnityEngine;

public class SecurityCamera : MonoBehaviour
{
    [Header("Nastavení")]
    public float rotationSpeed = 30f;
    public float maxAngle = 60f;
    public float detectionTime = 1.5f;   // sekund k detekci
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;
    public Transform visionCone;         // vizuální kužel

    [Header("Stav")]
    public bool isDisabled;

    private float detectionProgress;
    private Transform playerTransform;
    private float rotDir = 1f;
    private float currentAngle;
    private bool alarmTriggered;

    void Awake()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        if (isDisabled) return;
        RotateCamera();
        DetectPlayer();
    }

    void RotateCamera()
    {
        currentAngle += rotationSpeed * rotDir * Time.deltaTime;
        if (Mathf.Abs(currentAngle) >= maxAngle) rotDir *= -1f;
        transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    void DetectPlayer()
    {
        if (playerTransform == null) return;

        Vector2 toPlayer = playerTransform.position - transform.position;
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            toPlayer.normalized,
            toPlayer.magnitude,
            obstacleLayer | playerLayer
        );

        if (hit.collider != null && hit.collider.CompareTag("Player"))
        {
            detectionProgress += Time.deltaTime;
            PlayerHUD.Instance?.ShowDetectionWarning(detectionProgress / detectionTime);

            if (detectionProgress >= detectionTime && !alarmTriggered)
            {
                alarmTriggered = true;
                AlarmManager.Instance?.TriggerAlarm(transform.position);
            }
        }
        else
        {
            detectionProgress = Mathf.Max(0f, detectionProgress - Time.deltaTime * 2f);
            PlayerHUD.Instance?.ShowDetectionWarning(detectionProgress / detectionTime);
        }
    }

    public void Disable()
    {
        isDisabled = true;
        detectionProgress = 0f;
        if (visionCone != null) visionCone.gameObject.SetActive(false);
    }

    public void Enable()
    {
        isDisabled = false;
        alarmTriggered = false;
        if (visionCone != null) visionCone.gameObject.SetActive(true);
    }
}
