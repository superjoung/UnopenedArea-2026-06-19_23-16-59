using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CCTVUIEffectController : MonoBehaviour
{
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int DesaturationId = Shader.PropertyToID("_Desaturation");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
    private static readonly int ScanlineCountId = Shader.PropertyToID("_ScanlineCount");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int FlickerStrengthId = Shader.PropertyToID("_FlickerStrength");
    private static readonly int GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
    private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
    private static readonly int EffectIntensityId = Shader.PropertyToID("_EffectIntensity");
    private static readonly int NoiseTimeId = Shader.PropertyToID("_NoiseTime");

    [Header("Profiles")]
    [SerializeField] private CCTVUIVisualProfile textProfile;
    [SerializeField] private CCTVUIVisualProfile panelProfile;

    [Header("Materials")]
    [SerializeField] private Material panelSourceMaterial;

    [Header("Targets")]
    [SerializeField] private bool autoFindCCTVSceneUITargets = true;
    [SerializeField] private TMP_Text[] textTargets;
    [SerializeField] private Graphic[] panelTargets;

    [Header("Runtime")]
    [SerializeField, Range(0f, 2f)] private float intensity = 1f;
    [SerializeField, Range(0f, 2f)] private float textIntensityMultiplier = 0.65f;
    [SerializeField, Range(0f, 2f)] private float panelIntensityMultiplier = 1f;

    private readonly List<Material> runtimeMaterials = new List<Material>();
    private readonly List<Material> panelRuntimeMaterials = new List<Material>();
    private readonly Dictionary<TMP_Text, Color> originalTextColors = new Dictionary<TMP_Text, Color>();
    private readonly Dictionary<TMP_Text, Color> lastAppliedTextColors = new Dictionary<TMP_Text, Color>();
    private readonly Dictionary<Graphic, Material> originalGraphicMaterials = new Dictionary<Graphic, Material>();

    private bool targetsApplied;

    public float Intensity => intensity;

    private void Awake()
    {
        Setup();
    }

    private void OnEnable()
    {
        Setup();
    }

    private void Update()
    {
        ApplyProfiles(Time.unscaledTime);
    }

    private void OnDisable()
    {
        RestoreOriginalTargets();
        DestroyRuntimeMaterials();
    }

    private void OnDestroy()
    {
        RestoreOriginalTargets();
        DestroyRuntimeMaterials();
    }

    [ContextMenu("Setup CCTV UI Effect")]
    public void Setup()
    {
        if (autoFindCCTVSceneUITargets)
            FindDefaultTargets();

        ApplyTargets();
        ApplyProfiles(Time.unscaledTime);
    }

    public void SetIntensity(float value)
    {
        intensity = Mathf.Max(0f, value);
        ApplyProfiles(Time.unscaledTime);
    }

    private void FindDefaultTargets()
    {
        if (textTargets == null || textTargets.Length == 0)
        {
            TMP_Text areaText = FindChildComponent<TMP_Text>("AreaText");
            TMP_Text timeText = FindChildComponent<TMP_Text>("TimeText");

            var foundTexts = new List<TMP_Text>();
            if (areaText != null)
                foundTexts.Add(areaText);
            if (timeText != null)
                foundTexts.Add(timeText);

            textTargets = foundTexts.ToArray();
        }

        if (panelTargets == null || panelTargets.Length == 0)
        {
            Graphic reportBackground = FindChildComponent<Graphic>("ReportBackGround");
            panelTargets = reportBackground != null
                ? new[] { reportBackground }
                : new Graphic[0];
        }
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name != childName)
                continue;

            return child.GetComponent<T>();
        }

        return null;
    }

    private void ApplyTargets()
    {
        if (targetsApplied)
            return;

        if (textTargets != null)
        {
            foreach (TMP_Text target in textTargets)
            {
                if (target == null)
                    continue;

                if (!originalTextColors.ContainsKey(target))
                    originalTextColors.Add(target, target.color);

                lastAppliedTextColors[target] = target.color;
            }
        }

        if (panelTargets != null)
        {
            foreach (Graphic target in panelTargets)
            {
                if (target == null)
                    continue;

                if (!originalGraphicMaterials.ContainsKey(target))
                    originalGraphicMaterials.Add(target, target.material);

                Material material = CreatePanelRuntimeMaterial(target.material, $"{target.name}_CCTV_UI_Panel_Runtime");
                if (material == null)
                    continue;

                panelRuntimeMaterials.Add(material);
                target.material = material;
            }
        }

        targetsApplied = true;
    }

    private Material CreatePanelRuntimeMaterial(Material fallbackSource, string materialName)
    {
        Shader shader = Shader.Find("Unrecorded Area/CCTV UI");
        if (shader == null)
        {
            Debug.LogWarning("[CCTVUIEffectController] Shader not found: Unrecorded Area/CCTV UI");
            return null;
        }

        Material material;
        if (panelSourceMaterial != null)
        {
            material = new Material(panelSourceMaterial);
        }
        else if (fallbackSource != null)
        {
            material = new Material(fallbackSource);
        }
        else
        {
            material = new Material(shader);
        }

        material.shader = shader;
        material.name = materialName;
        runtimeMaterials.Add(material);
        return material;
    }

    private void ApplyProfiles(float time)
    {
        ApplyTextProfile(time, intensity * textIntensityMultiplier);

        foreach (Material material in panelRuntimeMaterials)
            ApplyMaterialProfile(material, panelProfile, time, intensity * panelIntensityMultiplier);
    }

    private void ApplyTextProfile(float time, float targetIntensity)
    {
        if (textTargets == null)
            return;

        CCTVUIVisualProfile profile = textProfile;
        Color tint = profile != null ? profile.TintColor : new Color(0.48f, 0.68f, 0.58f, 1f);
        float desaturation = profile != null ? profile.Desaturation : 0.2f;
        float brightness = profile != null ? profile.Brightness : 1f;
        float flickerStrength = profile != null ? profile.FlickerStrength : 0.04f;
        float flicker = Mathf.Sin(time * 19f) * 0.5f + 0.5f;

        foreach (TMP_Text target in textTargets)
        {
            if (target == null)
                continue;

            Color baseColor = originalTextColors.TryGetValue(target, out Color original)
                ? original
                : target.color;

            float alphaMultiplier = 1f - flicker * flickerStrength * targetIntensity;
            if (lastAppliedTextColors.TryGetValue(target, out Color lastAppliedColor) &&
                !Mathf.Approximately(target.color.a, lastAppliedColor.a))
            {
                baseColor.a = target.color.a / Mathf.Max(0.0001f, alphaMultiplier);
                originalTextColors[target] = baseColor;
            }

            float luminance = baseColor.r * 0.299f + baseColor.g * 0.587f + baseColor.b * 0.114f;
            Color desaturated = Color.Lerp(baseColor, new Color(luminance, luminance, luminance, baseColor.a), desaturation * targetIntensity);
            Color targetTint = new Color(tint.r, tint.g, tint.b, baseColor.a);
            Color tinted = Color.Lerp(desaturated, targetTint, Mathf.Clamp01(targetIntensity));
            tinted.r *= brightness;
            tinted.g *= brightness;
            tinted.b *= brightness;
            tinted.a = baseColor.a * alphaMultiplier;
            target.color = ClampColor(tinted);
            lastAppliedTextColors[target] = target.color;
        }
    }

    private void ApplyMaterialProfile(Material material, CCTVUIVisualProfile profile, float time, float targetIntensity)
    {
        if (material == null)
            return;

        material.SetColor(TintColorId, profile != null ? profile.TintColor : new Color(0.48f, 0.68f, 0.58f, 1f));
        material.SetFloat(DesaturationId, profile != null ? profile.Desaturation : 0.2f);
        material.SetFloat(BrightnessId, profile != null ? profile.Brightness : 1f);
        material.SetFloat(ScanlineStrengthId, profile != null ? profile.ScanlineStrength : 0.08f);
        material.SetFloat(ScanlineCountId, profile != null ? profile.ScanlineCount : 420f);
        material.SetFloat(NoiseStrengthId, profile != null ? profile.NoiseStrength : 0.04f);
        material.SetFloat(NoiseSpeedId, profile != null ? profile.NoiseSpeed : 1.5f);
        material.SetFloat(FlickerStrengthId, profile != null ? profile.FlickerStrength : 0.04f);
        material.SetFloat(GlitchStrengthId, profile != null ? profile.GlitchStrength : 0.01f);
        material.SetFloat(ChromaticAberrationId, profile != null ? profile.ChromaticAberration : 0.001f);
        material.SetFloat(EffectIntensityId, targetIntensity);
        material.SetFloat(NoiseTimeId, time);
    }


    private Color ClampColor(Color color)
    {
        return new Color(
            Mathf.Clamp01(color.r),
            Mathf.Clamp01(color.g),
            Mathf.Clamp01(color.b),
            Mathf.Clamp01(color.a)
        );
    }

    private void RestoreOriginalTargets()
    {
        foreach (var pair in originalTextColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }

        foreach (var pair in originalGraphicMaterials)
        {
            if (pair.Key != null)
                pair.Key.material = pair.Value;
        }

        originalTextColors.Clear();
        lastAppliedTextColors.Clear();
        originalGraphicMaterials.Clear();
        panelRuntimeMaterials.Clear();
        targetsApplied = false;
    }

    private void DestroyRuntimeMaterials()
    {
        foreach (Material material in runtimeMaterials)
        {
            if (material == null)
                continue;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        runtimeMaterials.Clear();
    }
}

