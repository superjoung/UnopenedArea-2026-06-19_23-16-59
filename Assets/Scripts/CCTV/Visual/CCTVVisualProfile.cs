using UnityEngine;

[CreateAssetMenu(fileName = "CCTVVisualProfile", menuName = "Unrecorded Area/CCTV Visual Profile")]
public class CCTVVisualProfile : ScriptableObject
{
    [Header("Render Texture")]
    [SerializeField] private Vector2Int renderTextureSize = new Vector2Int(1280, 720);
    [SerializeField] private int depthBufferBits = 16;

    [Header("CRT Shape")]
    [SerializeField, Range(0f, 0.3f)] private float distortionStrength = 0.08f;
    [SerializeField, Range(0f, 0.03f)] private float chromaticAberration = 0.004f;

    [Header("CCTV Tone")]
    [SerializeField] private Color tintColor = new Color(0.48f, 0.68f, 0.58f, 1f);
    [SerializeField, Range(0f, 1f)] private float desaturation = 0.25f;
    [SerializeField, Range(0.25f, 2f)] private float brightness = 0.9f;
    [SerializeField, Range(0.25f, 2f)] private float contrast = 1.1f;

    [Header("Edges")]
    [SerializeField, Range(0f, 1f)] private float vignetteStrength = 0.35f;
    [SerializeField, Range(0.01f, 1f)] private float vignetteSoftness = 0.55f;

    [Header("Signal")]
    [SerializeField, Range(0f, 0.3f)] private float scanlineStrength = 0.08f;
    [SerializeField, Range(50f, 1200f)] private float scanlineCount = 420f;
    [SerializeField, Range(0f, 0.3f)] private float noiseStrength = 0.035f;
    [SerializeField, Range(0f, 10f)] private float noiseSpeed = 1.5f;

    public Vector2Int RenderTextureSize => renderTextureSize;
    public int DepthBufferBits => depthBufferBits;
    public float DistortionStrength => distortionStrength;
    public float ChromaticAberration => chromaticAberration;
    public Color TintColor => tintColor;
    public float Desaturation => desaturation;
    public float Brightness => brightness;
    public float Contrast => contrast;
    public float VignetteStrength => vignetteStrength;
    public float VignetteSoftness => vignetteSoftness;
    public float ScanlineStrength => scanlineStrength;
    public float ScanlineCount => scanlineCount;
    public float NoiseStrength => noiseStrength;
    public float NoiseSpeed => noiseSpeed;
}
