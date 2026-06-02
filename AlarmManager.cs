using UnityEngine;

/// <summary>
/// Správce alarmu - aktivuje nepřátele po detekci hráče kamerou
/// </summary>
public class AlarmManager : MonoBehaviour
{
    public static AlarmManager Instance { get; private set; }

    [Header("Alarm")]
    public AudioSource alarmAudioSource;
    public AudioClip alarmClip;
    public float alarmDuration = 15f;

    [Header("Efekty")]
    public Light2D[] alarmLights;      // červená světla
    public float lightFlashRate = 0.5f;

    private bool alarmActive;
    private float alarmTimer;
    private float flashTimer;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TriggerAlarm(Vector3 sourcePosition)
    {
        if (alarmActive) return;

        alarmActive = true;
        alarmTimer = alarmDuration;

        alarmAudioSource?.PlayOneShot(alarmClip);
        PlayerHUD.Instance?.ShowAlarmWarning(true);

        // Aktivovat všechny nepřátele v levelu
        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();
        foreach (var e in enemies)
        {
            // Vzbudit nepřátele
            e.SendMessage("OnAlarmTriggered", SendMessageOptions.DontRequireReceiver);
        }

        Debug.Log($"[AlarmManager] Alarm triggered at {sourcePosition}");
    }

    void Update()
    {
        if (!alarmActive) return;

        alarmTimer -= Time.deltaTime;
        flashTimer += Time.deltaTime;

        // Blikání světel
        if (flashTimer >= lightFlashRate)
        {
            flashTimer = 0f;
            foreach (var light in alarmLights)
                if (light != null) light.enabled = !light.enabled;
        }

        if (alarmTimer <= 0f)
            DeactivateAlarm();
    }

    public void DeactivateAlarm()
    {
        alarmActive = false;
        alarmAudioSource?.Stop();
        PlayerHUD.Instance?.ShowAlarmWarning(false);

        foreach (var light in alarmLights)
            if (light != null) light.enabled = false;
    }
}
