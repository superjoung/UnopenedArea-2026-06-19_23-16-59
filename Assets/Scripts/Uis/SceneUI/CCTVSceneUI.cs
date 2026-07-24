using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CCTVSceneUI : BaseUI
{
    // BaseUI.Bind에서 enum 이름과 같은 자식 UI를 찾아 캐싱한다.
    enum Texts
    {
        AreaText,           // 구역 이름 텍스트
        TimeText,           // 현재 시간 텍스트
        ErrorReportText,    // 오류 보고 텍스트
        AreaReportText,    // 구역 선택 버튼 텍스트
        ObjectReportText,   // 오브젝트 선택 버튼 텍스트
        TypeReportText,     // 이상현상 선택 버튼 텍스트
    }

    enum Objects
    {
        ReportBackGround,   // 플레이어가 보고할 때 올라와야하는 오브젝트
        ReportBoundary,     // 보고서 올라올 위치 게산하기 위한 오브젝트
        AreaContentBox,     // 구역 선택 가능한 리스트 등록 오브젝트
        ObjectContentBox,   // 오브젝트 선택 가능한 리스트 등록 오브젝트
        TypeContentBox,     // 이상현상 선택 가능한 리스트 등록 오브젝트
    }

    enum Buttons
    {
        ReportSendButton,       // 보고서 전송 버튼
        AreaReportButton,       // 구역 선택 버튼
        ObjectReportButton,     // 오브젝트 선택 버튼
        TypeReportButton,       // 이상현상 타입 선택 버튼
    }

    public override UIName ID => UIName.CCTVSceneUI;
    protected override bool IsSorting => false;

    private int _errorReportCount = 0; // 플레이어가 오보고를 몇 번 했는지 카운트

    [Header("References")]
    [SerializeField] private CCTVTestSceneController sceneController;
    [SerializeField] private CCTVAreaView areaView;
    [SerializeField] private AnomalyService anomalyService;
    [SerializeField] private DayRuntimeController dayRuntimeController;
    [SerializeField] private CCTVScreenEffectController screenEffectController;
    [SerializeField] private CCTVPanController panController;

    [Header("Success Report Noise")]
    [SerializeField] private CCTVNoiseProfile successReportNoiseProfile;
    [SerializeField, Min(0.01f)] private float fallbackSuccessReportNoiseDuration = 1.1f;

    [Header("False Report Noise")]
    [SerializeField] private CCTVNoiseProfile falseReportNoiseProfile;
    [SerializeField, Min(0.01f)] private float fallbackFalseReportNoiseDuration = 2f;

    public Sprite[] ReportSprites;  // 오보고에 따른 보고서 이미지 변경 리스트

    // 보고 패널은 기본 높이까지만 열리는 상태와 선택 리스트를 보여주는 전체 높이 상태를 나눠 사용한다.
    private float _reportFullHeight = 0.0f;
    private float _reportDefaultHeight = 0.0f;
    private bool _isReport = false;
    private bool _isNormalizingReport;
    private Tween _slideTween;
    private CCTVUIEffectController _uiEffectController;

    // 보고 제출 시 사용할 내부 선택값. UI 표시 문자열이 아니라 enum/id 값을 저장한다.
    private AreaId selectedAreaId = AreaId.None;
    private string selectedObjectId;
    private ReportTargetId selectedTargetId = ReportTargetId.None;
    private AnomalyReportType selectedReportType = AnomalyReportType.None;

    public AreaId SelectedAreaId => selectedAreaId;
    public string SelectedObjectId => selectedObjectId;
    public ReportTargetId SelectedTargetId => selectedTargetId;
    public AnomalyReportType SelectedReportType => selectedReportType;

    void Start()
    {
        Init();
    }

    public override void Init()
    {
        base.Init();

        Bind<TMP_Text>(typeof(Texts));
        Bind<GameObject>(typeof(Objects));
        Bind<Button>(typeof(Buttons));

        ResolveReferences();

        // 보고서 조작 버튼은 각각 다른 선택 리스트를 열지만, 최종 선택 처리는 공통 옵션 콜백으로 모은다.
        GetButton((int)Buttons.ReportSendButton).gameObject.BindEvent(OnClickReportSendButton);
        GetButton((int)Buttons.AreaReportButton).gameObject.BindEvent(OnClickAreaSelectButton);
        GetButton((int)Buttons.ObjectReportButton).gameObject.BindEvent(OnClickObjectSelectButton);
        GetButton((int)Buttons.TypeReportButton).gameObject.BindEvent(OnClickTypeSelectButton);

        EffectSetting();
        ReportHeightSetting();
        ReportContentSetting();
    }

    private void OnClickReportSendButton(PointerEventData eventData)
    {
        Debug.Log($"[INFO] CCTVSceneUI::OnClickReportSendButton - 보고서 전송 버튼 클릭 area={selectedAreaId}, object={selectedObjectId}, target={selectedTargetId}, type={selectedReportType}");

        ResolveReferences();

        if (anomalyService == null)
        {
            Debug.LogWarning("[WARN] CCTVSceneUI::OnClickReportSendButton - AnomalyService가 없어 보고 판정을 실행할 수 없습니다.");
            return;
        }

        // 보고 실패 성공 유무 확인
        bool isCorrectReport = anomalyService.TryReportAnomaly(
            selectedAreaId,
            selectedTargetId,
            selectedReportType,
            out AnomalyRuntime matchedRuntime
        );

        if (isCorrectReport)
        {
            string anomalyId = matchedRuntime != null && matchedRuntime.Definition != null
                ? matchedRuntime.Definition.AnomalyId
                : "Unknown";

            if (dayRuntimeController != null)
                dayRuntimeController.RegisterCorrectReport();

            if (GameManager.Instance != null)
                GameManager.Instance.NotifyDayCorrectReport(matchedRuntime);

            Debug.Log($"[INFO] CCTVSceneUI::OnClickReportSendButton - 정상 보고 anomaly={anomalyId}");
            StartCoroutine(CompleteSuccessfulReportRoutine(matchedRuntime));
        }
        else
        {
            Debug.Log("[INFO] CCTVSceneUI::OnClickReportSendButton - 오보고");

            if (dayRuntimeController != null)
                dayRuntimeController.RegisterWrongReport();

            if (GameManager.Instance != null)
                GameManager.Instance.NotifyDayWrongReport();

            PlayFalseReportNoise();
        }
    }

    #region Report Select Button Event
    private void OnClickAreaSelectButton(PointerEventData eventData)
    {
        Debug.Log("[INFO] CCTVSceneUI::OnClickAreaSelectButton - 구역 선택 버튼 클릭");

        // 현재 일차/채널 기준 구역 후보를 갱신한 뒤 구역 리스트만 표시한다.
        RefreshAreaReportContents();
        OpenReportSelectList(ReportSelectKind.Area);
    }

    private void OnClickObjectSelectButton(PointerEventData eventData)
    {
        Debug.Log("[INFO] CCTVSceneUI::OnClickObjectSelectButton - 오브젝트 선택 버튼 클릭");

        // 선택된 구역이 있으면 해당 구역, 없으면 현재 CCTV 구역의 보고 가능 오브젝트를 표시한다.
        RefreshObjectReportContents();
        OpenReportSelectList(ReportSelectKind.Object);
    }

    private void OnClickTypeSelectButton(PointerEventData eventData)
    {
        Debug.Log("[INFO] CCTVSceneUI::OnClickTypeSelectButton - 이상현상 타입 선택 버튼 클릭");

        // 이상현상 타입은 정답 힌트를 막기 위해 활성 이상현상 기준이 아닌 고정 타입 목록을 표시한다.
        RefreshTypeReportContents();
        OpenReportSelectList(ReportSelectKind.AnomalyType);
    }
    #endregion

    private void OpenReportSelectList(ReportSelectKind kind)
    {
        Debug.Log("[INFO] CCTVSceneUI::OpenReportSelectList - 보고서 선택 리스트 오픈");

        RectTransform panel = GetObject((int)Objects.ReportBackGround).GetComponent<RectTransform>();
        if (panel == null)
            return;

        // 세부 선택지를 보여줄 때는 보고 패널을 전체 높이까지 올린다.
        _slideTween?.Kill();

        _slideTween = panel
                .DOAnchorPosY(_reportFullHeight, 0.5f)
                .SetEase(Ease.OutQuint);

        // 세 컨텐츠 박스 중 현재 선택 종류에 맞는 박스만 켜서 같은 공간을 재사용한다.
        switch (kind)
        {
            case ReportSelectKind.Area:
                GetObject((int)Objects.AreaContentBox).SetActive(true);
                GetObject((int)Objects.ObjectContentBox).SetActive(false);
                GetObject((int)Objects.TypeContentBox).SetActive(false);
                break;
            case ReportSelectKind.Object:
                GetObject((int)Objects.ObjectContentBox).SetActive(true);
                GetObject((int)Objects.AreaContentBox).SetActive(false);
                GetObject((int)Objects.TypeContentBox).SetActive(false);
                break;
            case ReportSelectKind.AnomalyType:
                GetObject((int)Objects.TypeContentBox).SetActive(true);
                GetObject((int)Objects.AreaContentBox).SetActive(false);
                GetObject((int)Objects.ObjectContentBox).SetActive(false);
                break;
        }
    }

    private void ReportPanelTrigger()
    {
        RectTransform panel = GetObject((int)Objects.ReportBackGround).GetComponent<RectTransform>();

        if (_reportDefaultHeight == 0.0f || panel == null) return;

        _slideTween?.Kill();

        // W키 보고 패널 토글: 열 때는 기본 보고 높이, 닫을 때는 전체 높이 기준 아래로 내린다.
        if (!_isReport)
        {
            _slideTween = panel
                .DOAnchorPosY(_reportDefaultHeight, 0.5f)
                .SetEase(Ease.OutQuint);
        }
        else
        {
            _slideTween = panel
                .DOAnchorPosY(-1 * _reportFullHeight, 0.5f)
                .SetEase(Ease.OutQuint);
        }
        _isReport = !_isReport;
    }

    private void PlayFalseReportNoise()
    {
        ResolveReferences();

        if (screenEffectController == null)
        {
            Debug.LogWarning("[WARN] CCTVSceneUI::PlayFalseReportNoise - CCTVScreenEffectController가 없어 오보고 노이즈를 실행할 수 없습니다.");
            return;
        }

        if (falseReportNoiseProfile != null)
            screenEffectController.PlayNoise(falseReportNoiseProfile);
        else
            screenEffectController.PlayTransitionNoise(fallbackFalseReportNoiseDuration);
    }

    private IEnumerator CompleteSuccessfulReportRoutine(AnomalyRuntime runtime)
    {
        if (_isNormalizingReport || runtime == null || anomalyService == null)
            yield break;

        _isNormalizingReport = true;
        CloseReportPanel();
        SetReportSendInteractable(false);

        if (panController != null)
            panController.SetInputLocked(true);

        float noiseDuration = GetSuccessNoiseDuration();
        if (screenEffectController != null)
        {
            if (successReportNoiseProfile != null)
                screenEffectController.PlayNoise(successReportNoiseProfile);
            else
                screenEffectController.PlayTransitionNoise(fallbackSuccessReportNoiseDuration);
        }

        // 노이즈가 화면을 충분히 가린 중간 지점에 기준 상태를 복원한다.
        float restoreDelay = noiseDuration * 0.5f;
        if (restoreDelay > 0f)
            yield return new WaitForSecondsRealtime(restoreDelay);

        anomalyService.CompleteNormalization(runtime);

        float remainingDuration = Mathf.Max(0f, noiseDuration - restoreDelay);
        if (remainingDuration > 0f)
            yield return new WaitForSecondsRealtime(remainingDuration);

        if (panController != null)
            panController.SetInputLocked(false);

        SetReportSendInteractable(true);
        _isNormalizingReport = false;
    }

    private float GetSuccessNoiseDuration()
    {
        return successReportNoiseProfile != null
            ? successReportNoiseProfile.TotalTimedDuration
            : fallbackSuccessReportNoiseDuration;
    }

    private void CloseReportPanel()
    {
        RectTransform panel = GetObject((int)Objects.ReportBackGround)?.GetComponent<RectTransform>();
        if (panel == null)
            return;

        _slideTween?.Kill();
        _slideTween = panel
            .DOAnchorPosY(-_reportFullHeight, 0.2f)
            .SetEase(Ease.OutQuint);
        _isReport = false;
    }

    private void SetReportSendInteractable(bool interactable)
    {
        Button button = GetButton((int)Buttons.ReportSendButton);
        if (button != null)
            button.interactable = interactable;
    }

    private void ChangeReportImage()
    {
        TMP_Text errorReportText = GetText((int)Texts.ErrorReportText);
        if (errorReportText != null)
            errorReportText.text = $"오보고 : {_errorReportCount}";

        Image reportImage = GetObject((int)Objects.ReportBackGround).GetComponent<Image>();
        if (reportImage == null || ReportSprites == null || ReportSprites.Length == 0)
            return;

        int spriteIndex = Mathf.Clamp(_errorReportCount, 0, ReportSprites.Length - 1);
        Sprite reportSprite = ReportSprites[spriteIndex];
        if (reportSprite != null)
            reportImage.sprite = reportSprite;
    }

    private void EffectSetting()
    {
        // CCTV UI 셰이더 컨트롤러가 있으면 런타임 머티리얼 세팅을 먼저 완료한다.
        _uiEffectController = GetComponent<CCTVUIEffectController>();
        if (_uiEffectController == null)
            _uiEffectController = GetComponentInChildren<CCTVUIEffectController>(true);

        if (_uiEffectController != null)
            _uiEffectController.Setup();

        // GameManager의 보고 패널/채널 변경 이벤트와 연결한다.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OpenReport -= ReportPanelTrigger;
            GameManager.Instance.OpenReport += ReportPanelTrigger;
            GameManager.Instance.CCTVAreaChanged -= SetCCTVAreaInfo;
            GameManager.Instance.CCTVAreaChanged += SetCCTVAreaInfo;
            GameManager.Instance.DayTimeChanged -= SetDayTimeInfo;
            GameManager.Instance.DayFailureCountChanged -= SetFailureCountInfo;
            GameManager.Instance.DayMissedAnomaly -= OnMissedAnomaly;
            GameManager.Instance.DayTimeChanged += SetDayTimeInfo;
            GameManager.Instance.DayFailureCountChanged += SetFailureCountInfo;
            GameManager.Instance.DayMissedAnomaly += OnMissedAnomaly;
            SyncCurrentCCTVAreaInfo();
            SyncCurrentDayFailureCount();
        }
    }

    private void ReportHeightSetting()
    {
        RectTransform _reportTransform = GetObject((int)Objects.ReportBoundary).GetComponent<RectTransform>();

        if (_reportTransform == null)
        {
            Debug.LogError("[ERROR] CCTVSceneUI::Init - reportTransform이 없습니다.");
        }
        else
        {
            // ReportBoundary의 높이를 기준으로 기본 열림 위치와 전체 리스트 열림 위치를 계산한다.
            _reportFullHeight = _reportTransform.rect.height / 2;
            _reportDefaultHeight = _reportTransform.rect.height / 16;
        }
    }

    // 보고서 컨텐츠 세팅
    private void ReportContentSetting()
    {
        if (!ValidateReportContentParents())
            return;

        // 시작 시 한 번 생성하고, 버튼을 누를 때마다 최신 구역/오브젝트 상태로 다시 갱신한다.
        RefreshAreaReportContents();
        RefreshObjectReportContents();
        RefreshTypeReportContents();
    }

    private void RefreshAreaReportContents()
    {
        GameObject areaContentParent = GetObject((int)Objects.AreaContentBox);
        if (areaContentParent == null)
            return;

        ClearContent(areaContentParent.transform);

        // 구역 후보는 현재 일차에 생성된 CCTV 채널 기준이며, 중복 AreaId는 한 번만 표시한다.
        foreach (CCTVAreaDefinition area in GetReportAreaDefinitions())
        {
            if (area == null || area.AreaId == AreaId.None)
                continue;

            string label = string.IsNullOrWhiteSpace(area.DisplayName)
                ? CCTVReportLabelProvider.GetAreaLabel(area.AreaId)
                : area.DisplayName;

            CreateReportContentItem(areaContentParent.transform, new ReportSelectOption
            {
                Kind = ReportSelectKind.Area,
                Label = label,
                AreaId = area.AreaId,
                ObjectId = null,
                TargetId = ReportTargetId.None,
                ReportType = AnomalyReportType.None,
            });
        }
    }

    private void RefreshObjectReportContents()
    {
        GameObject objectContentParent = GetObject((int)Objects.ObjectContentBox);
        if (objectContentParent == null)
            return;

        ClearContent(objectContentParent.transform);

        CCTVAreaInstance instance = GetSelectedOrCurrentAreaInstance();
        if (instance == null)
            return;

        // 오보고 가능성을 위해 활성 이상현상만이 아니라 해당 구역의 모든 보고 가능 오브젝트를 후보로 보여준다.
        foreach (CCTVSceneObject sceneObject in instance.GetAllObjects())
        {
            if (sceneObject == null || !sceneObject.CanBeAnomalyTarget || sceneObject.TargetId == ReportTargetId.None)
                continue;

            string label = string.IsNullOrWhiteSpace(sceneObject.DisplayName)
                ? CCTVReportLabelProvider.GetTargetLabel(sceneObject.TargetId)
                : sceneObject.DisplayName;

            CreateReportContentItem(objectContentParent.transform, new ReportSelectOption
            {
                Kind = ReportSelectKind.Object,
                Label = label,
                AreaId = instance.Definition != null ? instance.Definition.AreaId : AreaId.None,
                ObjectId = sceneObject.ObjectId,
                TargetId = sceneObject.TargetId,
                ReportType = AnomalyReportType.None,
            });
        }
    }

    private void RefreshTypeReportContents()
    {
        GameObject typeContentParent = GetObject((int)Objects.TypeContentBox);
        if (typeContentParent == null)
            return;

        ClearContent(typeContentParent.transform);

        // 타입 후보는 고정 목록이다. 현재 활성 이상현상 타입만 보여주면 정답 힌트가 되기 때문이다.
        AnomalyReportType[] reportTypes =
        {
            AnomalyReportType.PositionChange,
            AnomalyReportType.Added,
            AnomalyReportType.Missing,
            AnomalyReportType.StateChange,
            AnomalyReportType.ShapeChange,
            AnomalyReportType.AbnormalBehavior,
        };

        foreach (AnomalyReportType reportType in reportTypes)
        {
            CreateReportContentItem(typeContentParent.transform, new ReportSelectOption
            {
                Kind = ReportSelectKind.AnomalyType,
                Label = CCTVReportLabelProvider.GetReportTypeLabel(reportType),
                AreaId = AreaId.None,
                ObjectId = null,
                TargetId = ReportTargetId.None,
                ReportType = reportType,
            });
        }
    }

    private void CreateReportContentItem(Transform parent, ReportSelectOption option)
    {
        if (parent == null)
            return;

        // 모든 선택지는 같은 ReportContentFrame 프리팹을 사용하고, 실제 의미는 option.Kind로 구분한다.
        ReportContentFrame frame = UIManager.Instance.MakeUIToParent<ReportContentFrame>(parent);
        if (frame == null)
            return;

        frame.Setup(option, OnReportOptionSelected);
    }

    private void OnReportOptionSelected(ReportSelectOption option)
    {
        // ReportContentFrame은 선택 판정을 하지 않고 option만 돌려준다. 실제 선택 상태는 여기서만 갱신한다.
        switch (option.Kind)
        {
            case ReportSelectKind.Area:
                selectedAreaId = option.AreaId;
                selectedObjectId = null;
                selectedTargetId = ReportTargetId.None;
                SetText(Texts.AreaReportText, option.Label);
                RefreshObjectReportContents();
                break;
            case ReportSelectKind.Object:
                if (option.AreaId != AreaId.None)
                    selectedAreaId = option.AreaId;

                selectedObjectId = option.ObjectId;
                selectedTargetId = option.TargetId;
                SetText(Texts.ObjectReportText, option.Label);
                break;
            case ReportSelectKind.AnomalyType:
                selectedReportType = option.ReportType;
                SetText(Texts.TypeReportText, option.Label);
                break;
        }

        Debug.Log($"[INFO] CCTVSceneUI::OnReportOptionSelected - kind={option.Kind}, label={option.Label}, area={selectedAreaId}, object={selectedObjectId}, target={selectedTargetId}, type={selectedReportType}");
    }

    public void SetCCTVAreaInfo(string channelLabel, string areaName)
    {
        SetText(Texts.AreaText, $"{channelLabel}\n{areaName}");
    }

    public void SetDayTimeInfo(float elapsedSec, float durationSec)
    {
        float safeDuration = Mathf.Max(0.01f, durationSec);
        float normalized = Mathf.Clamp01(elapsedSec / safeDuration);
        float displayTotalMinutes = normalized * 360f;
        int hour = Mathf.FloorToInt(displayTotalMinutes / 60f);
        int minute = Mathf.FloorToInt(displayTotalMinutes % 60f);

        SetText(Texts.TimeText, $"{hour:00}:{minute:00}");
    }

    public void SetFailureCountInfo(int currentCount, int maxCount)
    {
        _errorReportCount = Mathf.Max(0, currentCount);
        ChangeReportImage();
    }

    private void OnMissedAnomaly(AnomalyRuntime runtime)
    {
        string anomalyId = runtime != null && runtime.Definition != null ? runtime.Definition.AnomalyId : "Unknown";
        Debug.Log($"[INFO] CCTVSceneUI::OnMissedAnomaly - 미보고 노이즈 실행 anomaly={anomalyId}");
        PlayFalseReportNoise();
    }

    private void SyncCurrentCCTVAreaInfo()
    {
        if (GameManager.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentCCTVLabel))
            return;

        SetCCTVAreaInfo(GameManager.Instance.CurrentCCTVLabel, GameManager.Instance.CurrentAreaName);
    }

    private void SyncCurrentDayFailureCount()
    {
        if (GameManager.Instance == null)
            return;

        SetFailureCountInfo(GameManager.Instance.CurrentDayFailureCount, GameManager.Instance.CurrentDayMaxFailureCount);
    }

    // 선택된 구역/오브젝트/이상현상 타입을 UI에 표시한다. 선택 전에는 기본 텍스트를 보여준다.
    private void SetText(Texts textType, string value)
    {
        TMP_Text text = GetText((int)textType);
        if (text != null)
            text.text = value;
    }

    private IEnumerable<CCTVAreaDefinition> GetReportAreaDefinitions()
    {
        ResolveReferences();

        var addedAreaIds = new HashSet<AreaId>();

        // 테스트 씬 컨트롤러가 가진 채널 목록을 우선 사용해 현재 일차의 선택 가능 구역을 만든다.
        if (sceneController != null && sceneController.Channels != null)
        {
            foreach (CCTVChannelRuntime channel in sceneController.Channels)
            {
                CCTVAreaDefinition area = channel?.Area;
                if (area == null || area.AreaId == AreaId.None || !addedAreaIds.Add(area.AreaId))
                    continue;

                yield return area;
            }
        }

        // 채널 목록을 아직 만들지 못한 초기 타이밍에서는 현재 표시 중인 구역을 fallback으로 제공한다.
        if (areaView != null && areaView.CurrentArea != null && addedAreaIds.Add(areaView.CurrentArea.AreaId))
            yield return areaView.CurrentArea;
    }

    private CCTVAreaInstance GetSelectedOrCurrentAreaInstance()
    {
        ResolveReferences();

        if (areaView == null)
            return null;

        // 플레이어가 구역을 먼저 선택했다면 해당 구역의 오브젝트 리스트를 보여준다.
        if (selectedAreaId != AreaId.None && areaView.TryGetAreaInstance(selectedAreaId, out CCTVAreaInstance selectedInstance))
            return selectedInstance;

        // 구역 선택 전에는 현재 보고 있는 CCTV 구역을 기준으로 오브젝트를 보여준다.
        return areaView.CurrentInstance;
    }

    private bool ValidateReportContentParents()
    {
        GameObject areaContentParent = GetObject((int)Objects.AreaContentBox);
        GameObject objectContentParent = GetObject((int)Objects.ObjectContentBox);
        GameObject typeContentParent = GetObject((int)Objects.TypeContentBox);

        if (areaContentParent == null || objectContentParent == null || typeContentParent == null)
        {
            Debug.LogError("[ERROR] CCTVSceneUI::ReportContentSetting - 보고서 컨텐츠 부모 오브젝트가 없습니다.");
            return false;
        }

        return true;
    }

    private void ClearContent(Transform parent)
    {
        if (parent == null)
            return;

        // 리스트를 다시 만들 때 이전 ReportContentFrame 인스턴스가 누적되지 않도록 제거한다.
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);

            // Destroy는 프레임 종료 시점에 실행된다. 목록을 연 직후 다시 갱신하면
            // 이전 채널/구역의 버튼이 새 버튼과 한 프레임 겹쳐 보일 수 있으므로,
            // 먼저 즉시 숨긴 뒤 안전하게 제거한다.
            child.gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void ResolveReferences()
    {
        // Inspector 연결을 우선하되, 비어 있으면 씬에서 자동 탐색한다.
        if (sceneController == null)
            sceneController = FindObjectOfType<CCTVTestSceneController>();

        if (areaView == null)
            areaView = FindObjectOfType<CCTVAreaView>();

        if (anomalyService == null)
            anomalyService = FindObjectOfType<AnomalyService>();

        if (dayRuntimeController == null)
            dayRuntimeController = FindObjectOfType<DayRuntimeController>();

        if (screenEffectController == null)
            screenEffectController = FindObjectOfType<CCTVScreenEffectController>();

        if (panController == null)
            panController = FindObjectOfType<CCTVPanController>();
    }

    private void OnDestroy()
    {
        _slideTween?.Kill();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OpenReport -= ReportPanelTrigger;
            GameManager.Instance.CCTVAreaChanged -= SetCCTVAreaInfo;
            GameManager.Instance.DayTimeChanged -= SetDayTimeInfo;
            GameManager.Instance.DayFailureCountChanged -= SetFailureCountInfo;
            GameManager.Instance.DayMissedAnomaly -= OnMissedAnomaly;
        }
    }
}








