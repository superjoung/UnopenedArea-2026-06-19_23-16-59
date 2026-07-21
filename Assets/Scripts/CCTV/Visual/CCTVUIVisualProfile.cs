using UnityEngine;

[CreateAssetMenu(fileName = "CCTVUIVisualProfile", menuName = "Unrecorded Area/CCTV UI Visual Profile")]
public class CCTVUIVisualProfile : ScriptableObject
{
    [Header("Tone")]
    [SerializeField] private Color tintColor = new Color(0.48f, 0.68f, 0.58f, 1f);
    [SerializeField, Range(0f, 1f)] private float desaturation = 0.2f;
    [SerializeField, Range(0.25f, 2f)] private float brightness = 1f;

    [Header("Signal")]
    [SerializeField, Range(0f, 0.4f)] private float scanlineStrength = 0.08f;
    [SerializeField, Range(50f, 1200f)] private float scanlineCount = 420f;
    [SerializeField, Range(0f, 0.3f)] private float noiseStrength = 0.04f;
    [SerializeField, Range(0f, 10f)] private float noiseSpeed = 1.5f;

    [Header("Instability")]
    [SerializeField, Range(0f, 0.5f)] private float flickerStrength = 0.04f;
    [SerializeField, Range(0f, 0.08f)] private float glitchStrength = 0.01f;
    [SerializeField, Range(0f, 0.02f)] private float chromaticAberration = 0.001f;

    public Color TintColor => tintColor;
    public float Desaturation => desaturation;
    public float Brightness => brightness;
    public float ScanlineStrength => scanlineStrength;
    public float ScanlineCount => scanlineCount;
    public float NoiseStrength => noiseStrength;
    public float NoiseSpeed => noiseSpeed;
    public float FlickerStrength => flickerStrength;
    public float GlitchStrength => glitchStrength;
    public float ChromaticAberration => chromaticAberration;
}
