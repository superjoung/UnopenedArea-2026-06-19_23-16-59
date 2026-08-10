using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Day 2 정전 직전, 설비실에 있는 작업복 인물을 잠깐 보여주는 복선 연출입니다.
/// CCTV 채널 목록에는 설비실을 추가하지 않고, 연출 동안만 AreaView의 인스턴스를 표시합니다.
/// </summary>
public class Day2BlackoutForeshadowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private CCTVAreaView areaView;
    [SerializeField] private CCTVPanController panController;
    [SerializeField] private CCTVTestSceneController sceneController;
    [Tooltip("기본 CCTV 채널에서 자동 탐색합니다. 설비실이 기본 채널에 없을 때만 수동 할당하세요.")]
    [SerializeField] private CCTVAreaDefinition utilityRoomArea;

    [Header("Staff")]
    [Tooltip("Area_UtilRoom 프리팹 안에 둘 작업복 인물의 이름입니다.")]
    [SerializeField] private string staffObjectName = "Staff";
    [Tooltip("Staff에 CCTVSceneObject를 붙인 경우 이름 대신 이 ID를 우선 사용합니다.")]
    [SerializeField] private string staffObjectId = "OBJ_UTIL_BLACKOUT_STAFF_01";

    [Header("Presentation")]
    [SerializeField, Min(0f)] private float revealHoldDuration = 3f;
    [SerializeField, Min(0.05f)] private float moveAndFadeDuration = 2.5f;
    [SerializeField] private float moveRightDistance = 2.5f;
    [SerializeField] private bool onlyDay2 = true;

    private readonly List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    private readonly List<Graphic> graphics = new List<Graphic>();
    private readonly List<Color> baseSpriteColors = new List<Color>();
    private readonly List<Color> baseGraphicColors = new List<Color>();
    private Coroutine activeRoutine;

    public bool IsPlaying => activeRoutine != null;

    public bool CanPlay()
    {
        ResolveReferences();
        return !onlyDay2 || (dayRuntimeController != null && dayRuntimeController.CurrentDayDefinition != null &&
                             dayRuntimeController.CurrentDayDefinition.Day == 2);
    }

    public IEnumerator Play()
    {
        ResolveReferences();
        if (!CanPlay() || areaView == null || panController == null)
            yield break;

        if (utilityRoomArea == null && sceneController != null)
            sceneController.TryGetChannelArea(AreaId.UtilityRoom, out utilityRoomArea);

        if (utilityRoomArea == null)
        {
            Debug.LogWarning("[Day2BlackoutForeshadow] 기본 CCTV 채널에서 UtilityRoom을 찾지 못했습니다.", this);
            yield break;
        }

        if (activeRoutine != null)
            yield break;

        activeRoutine = StartCoroutine(PlayRoutine());
        yield return activeRoutine;
    }

    private IEnumerator PlayRoutine()
    {
        bool wasCctvInputEnabled = sceneController != null && sceneController.CCTVInputEnabled;
        bool wasPanInputLocked = panController.InputLocked;
        sceneController?.SetCCTVInputEnabled(false);
        panController.SetInputLocked(true);

        // Day 2 기본 채널에 포함된 설비실은 평소 Q/E 전환과 같은 노이즈를 거쳐 선택한다.
        bool selectedExistingChannel = sceneController != null &&
                                       sceneController.TryGetChannelArea(AreaId.UtilityRoom, out _);

        if (selectedExistingChannel)
            yield return sceneController.SwitchToAreaWithNoise(AreaId.UtilityRoom);

        // 일반 채널 전환 루틴은 종료 시 팬 잠금을 해제하므로 Staff 연출용 잠금을 다시 확정한다.
        panController.SetInputLocked(true);

        // 테스트 세팅 등으로 설비실 채널이 빠진 경우에만 임시 인스턴스를 준비한다.
        if (!selectedExistingChannel && !areaView.TryPrepareAdditionalArea(utilityRoomArea))
        {
            Debug.LogWarning("[Day2BlackoutForeshadow] Area_UtilRoom 인스턴스를 준비하지 못했습니다.", this);
            RestoreInputState(wasCctvInputEnabled, wasPanInputLocked);
            activeRoutine = null;
            yield break;
        }

        if (!areaView.TryGetAreaInstance(AreaId.UtilityRoom, out CCTVAreaInstance utilityInstance) ||
            utilityInstance == null)
        {
            Debug.LogWarning("[Day2BlackoutForeshadow] Area_UtilRoom 인스턴스를 준비하지 못했습니다.", this);
            RestoreInputState(wasCctvInputEnabled, wasPanInputLocked);
            activeRoutine = null;
            yield break;
        }

        Transform staff = FindStaff(utilityInstance.transform);
        if (staff == null)
        {
            Debug.LogWarning("[Day2BlackoutForeshadow] Area_UtilRoom 안에서 Staff를 찾지 못했습니다. 이름 또는 Object Id를 확인하세요.", this);
            RestoreInputState(wasCctvInputEnabled, wasPanInputLocked);
            activeRoutine = null;
            yield break;
        }

        if (!selectedExistingChannel)
        {
            areaView.ShowArea(utilityRoomArea);
            panController.SetAreaInstance(utilityInstance);
        }
        panController.SnapToWorldX(staff.position.x);

        CacheVisuals(staff);
        Vector3 startLocalPosition = staff.localPosition;
        staff.gameObject.SetActive(true);
        ApplyAlpha(1f);

        if (revealHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(revealHoldDuration);

        float elapsed = 0f;
        while (elapsed < moveAndFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / moveAndFadeDuration);
            staff.localPosition = startLocalPosition + Vector3.right * (moveRightDistance * progress);
            ApplyAlpha(1f - progress);
            yield return null;
        }

        staff.localPosition = startLocalPosition;
        ApplyAlpha(1f);
        staff.gameObject.SetActive(false);
        RestoreInputState(wasCctvInputEnabled, wasPanInputLocked);
        activeRoutine = null;
    }

    private void RestoreInputState(bool cctvInputEnabled, bool panInputLocked)
    {
        sceneController?.SetCCTVInputEnabled(cctvInputEnabled);
        panController?.SetInputLocked(panInputLocked);
    }

    private Transform FindStaff(Transform utilityRoot)
    {
        CCTVSceneObject[] sceneObjects = utilityRoot.GetComponentsInChildren<CCTVSceneObject>(true);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(staffObjectId) && sceneObjects[i].ObjectId == staffObjectId)
                return sceneObjects[i].transform;
        }

        Transform[] transforms = utilityRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == staffObjectName)
                return transforms[i];
        }

        return null;
    }

    private void CacheVisuals(Transform staff)
    {
        spriteRenderers.Clear();
        graphics.Clear();
        baseSpriteColors.Clear();
        baseGraphicColors.Clear();

        SpriteRenderer[] foundSpriteRenderers = staff.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < foundSpriteRenderers.Length; i++)
        {
            spriteRenderers.Add(foundSpriteRenderers[i]);
            baseSpriteColors.Add(foundSpriteRenderers[i].color);
        }

        Graphic[] foundGraphics = staff.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < foundGraphics.Length; i++)
        {
            graphics.Add(foundGraphics[i]);
            baseGraphicColors.Add(foundGraphics[i].color);
        }
    }

    private void ApplyAlpha(float alphaMultiplier)
    {
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            Color color = baseSpriteColors[i];
            color.a *= alphaMultiplier;
            spriteRenderers[i].color = color;
        }

        for (int i = 0; i < graphics.Count; i++)
        {
            if (graphics[i] == null)
                continue;

            Color color = baseGraphicColors[i];
            color.a *= alphaMultiplier;
            graphics[i].color = color;
        }
    }

    private void ResolveReferences()
    {
        if (dayRuntimeController == null)
            dayRuntimeController = FindFirstObjectByType<DayRuntimeController>();
        if (areaView == null)
            areaView = FindFirstObjectByType<CCTVAreaView>();
        if (panController == null)
            panController = FindFirstObjectByType<CCTVPanController>();
        if (sceneController == null)
            sceneController = FindFirstObjectByType<CCTVTestSceneController>();
    }
}
