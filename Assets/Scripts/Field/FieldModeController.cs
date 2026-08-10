using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Day 1의 CCTV 감시 모드와 현장 횡스크롤 모드를 전환한다.
/// 현재 범위는 정전 발생 시 현장으로 진입하는 것까지이며, 복귀는 이후 구현한다.
/// </summary>
public class FieldModeController : MonoBehaviour
{
    [Header("Field Mode")]
    [SerializeField] private GameObject fieldModeRoot;
    [SerializeField] private Camera fieldCamera;
    [SerializeField] private FieldPlayerMovementController fieldPlayer;
    [SerializeField] private FieldCameraFollowController fieldCameraFollowController;

    [Header("CCTV Mode")]
    [SerializeField] private GameObject cctvSystemRoot;
    [SerializeField] private GameObject cctvUiRoot;
    [SerializeField] private GameObject cctvScreenCanvasRoot;
    [SerializeField] private Camera cctvCamera;
    [SerializeField] private AudioListener cctvAudioListener;
    [SerializeField] private AudioListener fieldAudioListener;

    [Header("Power Restore Light Effect")]
    [Tooltip("비워두면 FieldModeRoot 아래의 Global Light 2D를 자동으로 사용합니다.")]
    [SerializeField] private Light2D fieldGlobalLight;
    [SerializeField, Min(0f)] private float restoredFieldLightIntensity = 1f;
    [SerializeField, Range(1, 4)] private int powerRestoreFlickerCount = 2;
    [SerializeField, Min(0.01f)] private float powerRestoreFlickerDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float powerRestoreFinalBrightenDuration = 0.2f;

    public bool IsFieldModeActive { get; private set; }

    private readonly Dictionary<Light2D, bool> cctvGlobalLightStates = new Dictionary<Light2D, bool>();
    private Coroutine powerRestoreLightRoutine;
    private float blackoutFieldLightIntensity;
    private bool fieldLightIntensityCached;
    private bool fieldPowerRestored;

    private void Awake()
    {
        ResolveReferences();
        CacheFieldLightIntensity();
        SetFieldModeActive(false);
    }

    /// <summary>배전반 복구 후 필드의 어두운 조명을 점멸시키고 정상 밝기로 전환합니다.</summary>
    public void PlayPowerRestoreLightEffect()
    {
        ResolveReferences();
        CacheFieldLightIntensity();
        if (fieldGlobalLight == null)
            return;

        fieldPowerRestored = true;
        if (powerRestoreLightRoutine != null)
            StopCoroutine(powerRestoreLightRoutine);
        powerRestoreLightRoutine = StartCoroutine(PlayPowerRestoreLightRoutine());
    }

    /// <summary>
    /// 정전 발생 후 CCTV 감시 화면을 숨기고 현장 횡스크롤 조작으로 전환한다.
    /// 필드 오브젝트가 아직 Day1 씬에 이식되지 않은 경우 false를 반환해 기존 디버그 흐름을 유지한다.
    /// </summary>
    public bool EnterFieldMode()
    {
        ResolveReferences();

        if (fieldModeRoot == null || fieldCamera == null || fieldPlayer == null)
        {
            Debug.LogWarning("[FieldModeController] Field mode references are incomplete. Assign FieldModeRoot, FieldCamera, and FieldPlayer in Day1.");
            return false;
        }

        if (cctvUiRoot != null)
            cctvUiRoot.SetActive(false);

        if (cctvScreenCanvasRoot != null)
            cctvScreenCanvasRoot.SetActive(false);

        if (cctvSystemRoot != null)
            cctvSystemRoot.SetActive(false);

        if (cctvCamera != null)
            cctvCamera.enabled = false;

        if (cctvAudioListener != null)
            cctvAudioListener.enabled = false;

        DisableCctvGlobalLights();
        SetFieldModeActive(true);
        Debug.Log("[FieldModeController] Entered field mode.");
        return true;
    }

    /// <summary>
    /// Returns from the field to the control-room CCTV presentation.
    /// The caller is responsible for resuming the monitoring loop only after
    /// this transition has completed.
    /// </summary>
    public bool ExitFieldMode()
    {
        ResolveReferences();

        if (!IsFieldModeActive)
            return false;

        SetFieldModeActive(false);

        if (cctvSystemRoot != null)
            cctvSystemRoot.SetActive(true);

        if (cctvUiRoot != null)
            cctvUiRoot.SetActive(true);

        if (cctvScreenCanvasRoot != null)
            cctvScreenCanvasRoot.SetActive(true);

        if (cctvCamera != null)
            cctvCamera.enabled = true;

        if (cctvAudioListener != null)
            cctvAudioListener.enabled = true;

        RestoreCctvGlobalLights();
        Debug.Log("[FieldModeController] Returned to control-room CCTV mode.");
        return true;
    }

    private void SetFieldModeActive(bool active)
    {
        if (fieldModeRoot != null)
            fieldModeRoot.SetActive(active);

        if (fieldPlayer != null)
            fieldPlayer.SetInputEnabled(active);

        if (active && fieldCamera != null)
        {
            ApplyFieldLightIntensity(fieldPowerRestored ? restoredFieldLightIntensity : blackoutFieldLightIntensity);
            fieldCameraFollowController?.SnapToTarget();

            if (fieldAudioListener == null)
                fieldAudioListener = fieldCamera.GetComponent<AudioListener>();

            if (fieldAudioListener == null)
                fieldAudioListener = fieldCamera.gameObject.AddComponent<AudioListener>();

            fieldAudioListener.enabled = true;
        }
        else if (!active && fieldAudioListener != null)
        {
            fieldAudioListener.enabled = false;
        }

        IsFieldModeActive = active;
    }

    private void ResolveReferences()
    {
        if (fieldModeRoot == null)
        {
            Transform root = transform.root.Find("FieldModeRoot");
            if (root != null)
                fieldModeRoot = root.gameObject;

            // CommonRoot is a scene-level prefab while FieldModeRoot is placed
            // alongside it in each Day scene. In that layout transform.root.Find
            // cannot reach the sibling, so also resolve the scene root by name.
            if (fieldModeRoot == null)
            {
                GameObject sceneFieldRoot = GameObject.Find("FieldModeRoot");
                if (sceneFieldRoot != null)
                    fieldModeRoot = sceneFieldRoot;
            }
        }

        if (fieldModeRoot != null)
        {
            if (fieldCamera == null)
                fieldCamera = fieldModeRoot.GetComponentInChildren<Camera>(true);

            if (fieldPlayer == null)
                fieldPlayer = fieldModeRoot.GetComponentInChildren<FieldPlayerMovementController>(true);

            if (fieldCameraFollowController == null)
                fieldCameraFollowController = fieldModeRoot.GetComponentInChildren<FieldCameraFollowController>(true);

            if (fieldGlobalLight == null)
            {
                Light2D[] fieldLights = fieldModeRoot.GetComponentsInChildren<Light2D>(true);
                foreach (Light2D light in fieldLights)
                {
                    if (light != null && light.lightType == Light2D.LightType.Global)
                    {
                        fieldGlobalLight = light;
                        break;
                    }
                }
            }
        }

        if (cctvSystemRoot == null)
        {
            CCTVTestSceneController cctvController = FindFirstObjectByType<CCTVTestSceneController>();
            if (cctvController != null)
                cctvSystemRoot = cctvController.gameObject;
        }

        if (cctvUiRoot == null)
        {
            CCTVSceneUI cctvUi = FindFirstObjectByType<CCTVSceneUI>();
            if (cctvUi != null)
                cctvUiRoot = cctvUi.gameObject;
        }

        if (cctvScreenCanvasRoot == null)
            cctvScreenCanvasRoot = GameObject.Find("Canvas_CCTV");

        if (cctvCamera == null)
        {
            GameObject cameraObject = GameObject.Find("CCTVCamera");
            if (cameraObject != null)
                cctvCamera = cameraObject.GetComponent<Camera>();
        }

        if (cctvAudioListener == null && cctvCamera != null)
            cctvAudioListener = cctvCamera.GetComponent<AudioListener>();
    }

    private void DisableCctvGlobalLights()
    {
        cctvGlobalLightStates.Clear();
        Light2D[] allLights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Light2D light in allLights)
        {
            if (light == null || light.lightType != Light2D.LightType.Global)
                continue;

            if (fieldModeRoot != null && light.transform.IsChildOf(fieldModeRoot.transform))
                continue;

            cctvGlobalLightStates.Add(light, light.enabled);
            light.enabled = false;
        }
    }

    private void RestoreCctvGlobalLights()
    {
        foreach (KeyValuePair<Light2D, bool> pair in cctvGlobalLightStates)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        cctvGlobalLightStates.Clear();
    }

    private IEnumerator PlayPowerRestoreLightRoutine()
    {
        ApplyFieldLightIntensity(blackoutFieldLightIntensity);

        for (int i = 0; i < powerRestoreFlickerCount; i++)
        {
            yield return FadeFieldLightIntensity(restoredFieldLightIntensity, powerRestoreFlickerDuration);
            yield return FadeFieldLightIntensity(blackoutFieldLightIntensity, powerRestoreFlickerDuration);
        }

        yield return FadeFieldLightIntensity(restoredFieldLightIntensity, powerRestoreFinalBrightenDuration);
        powerRestoreLightRoutine = null;
    }

    private IEnumerator FadeFieldLightIntensity(float targetIntensity, float duration)
    {
        if (fieldGlobalLight == null)
            yield break;

        float startIntensity = fieldGlobalLight.intensity;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fieldGlobalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fieldGlobalLight.intensity = targetIntensity;
    }

    private void CacheFieldLightIntensity()
    {
        if (fieldLightIntensityCached || fieldGlobalLight == null)
            return;

        blackoutFieldLightIntensity = fieldGlobalLight.intensity;
        fieldLightIntensityCached = true;
    }

    private void ApplyFieldLightIntensity(float intensity)
    {
        if (fieldGlobalLight != null)
            fieldGlobalLight.intensity = intensity;
    }

}
