using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CCTVScreenEffectController : MonoBehaviour
{
    private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
    private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
    private static readonly int HorizontalTearStrengthId = Shader.PropertyToID("_HorizontalTearStrength");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int DesaturationId = Shader.PropertyToID("_Desaturation");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");
    private static readonly int VignetteSoftnessId = Shader.PropertyToID("_VignetteSoftness");
    private static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
    private static readonly int ScanlineCountId = Shader.PropertyToID("_ScanlineCount");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int NoiseTimeId = Shader.PropertyToID("_NoiseTime");

    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RawImage screenImage;
    [SerializeField] private CCTVVisualProfile profile;
    [SerializeField] private Material sourceMaterial;

    [Header("Runtime Render Texture")]
    [SerializeField] private bool createRenderTextureAtRuntime = true;
    [SerializeField] private RenderTexture renderTextureAsset;

    [Header("Input")]
    [SerializeField] private bool disableRawImageRaycastTarget = true;

    [Header("Fallback Noise")]
    [SerializeField, Min(0.01f)] private float fallbackNoiseDuration = 0.5f;
    [SerializeField, Range(0f, 1f)] private float fallbackPeakNoiseStrength = 0.22f;
    [SerializeField, Range(0f, 0.4f)] private float fallbackPeakScanlineStrength = 0.18f;
    [SerializeField, Range(0f, 0.03f)] private float fallbackPeakChromaticAberration = 0.012f;
    [SerializeField, Range(0.25f, 2f)] private float fallbackPeakBrightness = 0.65f;
    [SerializeField, Range(0.25f, 2f)] private float fallbackPeakContrast = 1.35f;

    private Material runtimeMaterial;
    private RenderTexture runtimeRenderTexture;
    private Coroutine noiseCoroutine;
    private CCTVNoiseProfile activeNoiseProfile;
    private float fallbackNoiseDurationOverride;
    private float noiseAmount;

    public RenderTexture CurrentRenderTexture => runtimeRenderTexture != null ? runtimeRenderTexture : renderTextureAsset;
    public Material RuntimeMaterial => runtimeMaterial;
    public bool IsNoisePlaying => noiseCoroutine != null;

    public void StopActiveNoise()
    {
        StopNoise();
        ApplyProfile(Time.unscaledTime);
    }

    public void Configure(Camera camera, RawImage image, CCTVVisualProfile visualProfile)
    {
        worldCamera = camera;
        screenImage = image;
        profile = visualProfile;
        Setup();
    }

    private void Awake()
    {
        EnsureReferences();
        Setup();
    }

    private void OnEnable()
    {
        EnsureReferences();
        Setup();
    }

    private void Update()
    {
        ApplyProfile(Time.unscaledTime);
    }

    private void OnDisable()
    {
        StopNoise();

        if (worldCamera != null && worldCamera.targetTexture == runtimeRenderTexture)
            worldCamera.targetTexture = null;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);

            if (runtimeRenderTexture != null)
                Destroy(runtimeRenderTexture);
        }
        else
        {
            if (runtimeMaterial != null)
                DestroyImmediate(runtimeMaterial);

            if (runtimeRenderTexture != null)
                DestroyImmediate(runtimeRenderTexture);
        }
    }

    public void Setup()
    {
        if (worldCamera == null || screenImage == null)
            return;

        EnsureMaterial();
        EnsureRenderTexture();

        RenderTexture current = CurrentRenderTexture;
        worldCamera.targetTexture = current;
        screenImage.texture = current;
        screenImage.material = runtimeMaterial;

        if (disableRawImageRaycastTarget)
            screenImage.raycastTarget = false;

        ApplyProfile(Time.unscaledTime);
    }

    public Coroutine PlayNoise(CCTVNoiseProfile noiseProfile)
    {
        if (!isActiveAndEnabled || !Application.isPlaying)
        {
            StopNoise();
            ApplyProfile(Time.unscaledTime);
            return null;
        }

        if (noiseCoroutine != null)
            StopCoroutine(noiseCoroutine);

        activeNoiseProfile = noiseProfile;
        fallbackNoiseDurationOverride = 0f;
        noiseCoroutine = StartCoroutine(PlayNoiseRoutine(noiseProfile));
        return noiseCoroutine;
    }

    public IEnumerator PlayNoiseRoutine(CCTVNoiseProfile noiseProfile)
    {
        activeNoiseProfile = noiseProfile;

        if (noiseProfile == null)
            yield return PlayFallbackNoiseRoutine(fallbackNoiseDurationOverride > 0f ? fallbackNoiseDurationOverride : fallbackNoiseDuration);
        else
            yield return PlayProfileNoiseRoutine(noiseProfile);

        noiseAmount = 0f;
        activeNoiseProfile = null;
        fallbackNoiseDurationOverride = 0f;
        ApplyProfile(Time.unscaledTime);
        noiseCoroutine = null;
    }

    public Coroutine PlayTransitionNoise(float duration = 0.5f)
    {
        if (!isActiveAndEnabled || !Application.isPlaying)
        {
            StopNoise();
            ApplyProfile(Time.unscaledTime);
            return null;
        }

        if (noiseCoroutine != null)
            StopCoroutine(noiseCoroutine);

        activeNoiseProfile = null;
        fallbackNoiseDurationOverride = Mathf.Max(0.01f, duration);
        noiseCoroutine = StartCoroutine(PlayNoiseRoutine(null));
        return noiseCoroutine;
    }

    /// <summary>
    /// 현재 렌더 텍스처를 잠시 그대로 유지해 CCTV 피드가 멈춘 것처럼 보이게 합니다.
    /// </summary>
    public IEnumerator FreezeFeedRoutine(float duration)
    {
        EnsureReferences();
        if (worldCamera == null || duration <= 0f)
            yield break;

        bool wasEnabled = worldCamera.enabled;
        worldCamera.enabled = false;
        yield return new WaitForSecondsRealtime(duration);

        if (worldCamera != null)
            worldCamera.enabled = wasEnabled;
    }

    public IEnumerator PlayTransitionNoiseRoutine(float duration)
    {
        yield return PlayFallbackNoiseRoutine(duration);
    }

    private IEnumerator PlayProfileNoiseRoutine(CCTVNoiseProfile noiseProfile)
    {
        float fadeIn = noiseProfile.FadeInTime;
        float hold = noiseProfile.HoldTime;
        float fadeOut = noiseProfile.FadeOutTime;

        if (fadeIn + hold + fadeOut <= 0f)
        {
            fadeOut = noiseProfile.Duration;
        }

        if (fadeIn > 0f)
            yield return AnimateNoiseAmount(0f, 1f, fadeIn);
        else
            SetNoiseAmount(1f);

        if (hold > 0f)
            yield return HoldNoiseAmount(1f, hold);

        if (fadeOut > 0f)
            yield return AnimateNoiseAmount(1f, 0f, fadeOut);
        else
            SetNoiseAmount(0f);
    }

    private IEnumerator PlayFallbackNoiseRoutine(float duration)
    {
        SetNoiseAmount(1f);
        yield return AnimateNoiseAmount(1f, 0f, Mathf.Max(0.01f, duration));
    }

    private IEnumerator AnimateNoiseAmount(float from, float to, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetNoiseAmount(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / safeDuration)));
            yield return null;
        }

        SetNoiseAmount(to);
    }

    private IEnumerator HoldNoiseAmount(float amount, float duration)
    {
        SetNoiseAmount(amount);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyProfile(Time.unscaledTime);
            yield return null;
        }
    }

    private void SetNoiseAmount(float amount)
    {
        noiseAmount = Mathf.Clamp01(amount);
        ApplyProfile(Time.unscaledTime);
    }

    private void StopNoise()
    {
        if (noiseCoroutine != null)
        {
            StopCoroutine(noiseCoroutine);
            noiseCoroutine = null;
        }

        noiseAmount = 0f;
        activeNoiseProfile = null;
        fallbackNoiseDurationOverride = 0f;
    }

    private void EnsureReferences()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (screenImage == null)
            screenImage = GetComponent<RawImage>();
    }

    private void EnsureMaterial()
    {
        if (runtimeMaterial != null)
            return;

        if (sourceMaterial != null)
        {
            runtimeMaterial = new Material(sourceMaterial);
            runtimeMaterial.name = $"{sourceMaterial.name}_Runtime";
            return;
        }

        Shader shader = Shader.Find("Unrecorded Area/CCTV CRT");
        if (shader == null)
        {
            Debug.LogWarning("[CCTVScreenEffectController] Shader not found: Unrecorded Area/CCTV CRT");
            return;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "MAT_CCTV_CRT_Runtime"
        };
    }

    private void EnsureRenderTexture()
    {
        if (!createRenderTextureAtRuntime)
            return;

        Vector2Int size = profile != null
            ? profile.RenderTextureSize
            : new Vector2Int(1280, 720);

        int depth = profile != null ? profile.DepthBufferBits : 16;

        if (runtimeRenderTexture != null &&
            runtimeRenderTexture.width == size.x &&
            runtimeRenderTexture.height == size.y &&
            runtimeRenderTexture.depth == depth)
        {
            return;
        }

        if (runtimeRenderTexture != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeRenderTexture);
            else
                DestroyImmediate(runtimeRenderTexture);
        }

        runtimeRenderTexture = new RenderTexture(size.x, size.y, depth, RenderTextureFormat.ARGB32)
        {
            name = "RT_CCTV_View_Runtime",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        runtimeRenderTexture.Create();
    }

    private void ApplyProfile(float time)
    {
        if (runtimeMaterial == null)
            return;

        CCTVVisualProfile activeProfile = profile;
        float baseChromaticAberration = activeProfile != null ? activeProfile.ChromaticAberration : 0.004f;
        float baseBrightness = activeProfile != null ? activeProfile.Brightness : 0.9f;
        float baseContrast = activeProfile != null ? activeProfile.Contrast : 1.1f;
        float baseScanlineStrength = activeProfile != null ? activeProfile.ScanlineStrength : 0.08f;
        float baseNoiseStrength = activeProfile != null ? activeProfile.NoiseStrength : 0.035f;
        float amount = Mathf.Clamp01(noiseAmount);

        runtimeMaterial.SetFloat(DistortionStrengthId, activeProfile != null ? activeProfile.DistortionStrength : 0.08f);
        runtimeMaterial.SetFloat(ChromaticAberrationId, Mathf.Lerp(baseChromaticAberration, GetPeakChromaticAberration(), amount));
        runtimeMaterial.SetFloat(HorizontalTearStrengthId, Mathf.Lerp(0f, GetPeakHorizontalTearStrength(), amount));
        runtimeMaterial.SetColor(TintColorId, activeProfile != null ? activeProfile.TintColor : new Color(0.48f, 0.68f, 0.58f, 1f));
        runtimeMaterial.SetFloat(DesaturationId, activeProfile != null ? activeProfile.Desaturation : 0.25f);
        runtimeMaterial.SetFloat(BrightnessId, Mathf.Lerp(baseBrightness, GetPeakBrightness(), amount));
        runtimeMaterial.SetFloat(ContrastId, Mathf.Lerp(baseContrast, GetPeakContrast(), amount));
        runtimeMaterial.SetFloat(VignetteStrengthId, activeProfile != null ? activeProfile.VignetteStrength : 0.35f);
        runtimeMaterial.SetFloat(VignetteSoftnessId, activeProfile != null ? activeProfile.VignetteSoftness : 0.55f);
        runtimeMaterial.SetFloat(ScanlineStrengthId, Mathf.Lerp(baseScanlineStrength, GetPeakScanlineStrength(), amount));
        runtimeMaterial.SetFloat(ScanlineCountId, activeProfile != null ? activeProfile.ScanlineCount : 420f);
        runtimeMaterial.SetFloat(NoiseStrengthId, Mathf.Lerp(baseNoiseStrength, GetPeakNoiseStrength(), amount));
        runtimeMaterial.SetFloat(NoiseSpeedId, activeProfile != null ? activeProfile.NoiseSpeed : 1.5f);
        runtimeMaterial.SetFloat(NoiseTimeId, time);
    }

    private float GetPeakNoiseStrength()
    {
        return activeNoiseProfile != null ? activeNoiseProfile.PeakNoiseStrength : fallbackPeakNoiseStrength;
    }

    private float GetPeakScanlineStrength()
    {
        return activeNoiseProfile != null ? activeNoiseProfile.PeakScanlineStrength : fallbackPeakScanlineStrength;
    }

    private float GetPeakChromaticAberration()
    {
        return activeNoiseProfile != null ? activeNoiseProfile.PeakChromaticAberration : fallbackPeakChromaticAberration;
    }

    private float GetPeakHorizontalTearStrength()
    {
        return activeNoiseProfile != null ? activeNoiseProfile.PeakHorizontalTearStrength : 0f;
    }

    private float GetPeakBrightness()
    {
        return activeNoiseProfile != null ? activeNoiseProfile.PeakBrightness : fallbackPeakBrightness;
    }

    private float GetPeakContrast()
    {
        return activeNoiseProfile != null ? activeNoiseProfile.PeakContrast : fallbackPeakContrast;
    }
}
