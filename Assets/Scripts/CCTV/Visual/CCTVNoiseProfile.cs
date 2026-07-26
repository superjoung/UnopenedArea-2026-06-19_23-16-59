using UnityEngine;

[CreateAssetMenu(fileName = "CCTVNoiseProfile", menuName = "Unrecorded Area/CCTV Noise Profile")]
public class CCTVNoiseProfile : ScriptableObject
{
    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float duration = 0.5f;
    [SerializeField, Min(0f)] private float fadeInTime;
    [SerializeField, Min(0f)] private float holdTime;
    [SerializeField, Min(0f)] private float fadeOutTime = 0.5f;

    [Header("Peak Values")]
    [SerializeField, Range(0f, 1f)] private float peakNoiseStrength = 0.22f;
    [SerializeField, Range(0f, 0.4f)] private float peakScanlineStrength = 0.18f;
    [SerializeField, Range(0f, 0.03f)] private float peakChromaticAberration = 0.012f;
    [SerializeField, Range(0f, 0.08f)] private float peakHorizontalTearStrength;
    [SerializeField, Range(0.25f, 2f)] private float peakBrightness = 0.65f;
    [SerializeField, Range(0.25f, 2f)] private float peakContrast = 1.35f;

    public float Duration => Mathf.Max(0.01f, duration);
    public float FadeInTime => Mathf.Max(0f, fadeInTime);
    public float HoldTime => Mathf.Max(0f, holdTime);
    public float FadeOutTime => Mathf.Max(0f, fadeOutTime);
    public float PeakNoiseStrength => peakNoiseStrength;
    public float PeakScanlineStrength => peakScanlineStrength;
    public float PeakChromaticAberration => peakChromaticAberration;
    public float PeakHorizontalTearStrength => peakHorizontalTearStrength;
    public float PeakBrightness => peakBrightness;
    public float PeakContrast => peakContrast;

    public float TotalTimedDuration
    {
        get
        {
            float timedDuration = FadeInTime + HoldTime + FadeOutTime;
            return timedDuration > 0f ? timedDuration : Duration;
        }
    }
}
