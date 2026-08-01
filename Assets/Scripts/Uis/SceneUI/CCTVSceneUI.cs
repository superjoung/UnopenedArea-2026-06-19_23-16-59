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
    [SerializeField] private Day1FlowController day1FlowController;

    [Header("Initial Monitoring Intro")]
    [Tooltip("첫 감시 진입 직후 입력을 막고 HUD를 숨겨 둘 시간입니다.")]
    [SerializeField, Min(0f)] private float initialMonitoringIntroDelay = 0.5f;
    [Tooltip("첫 감시 진입 중 숨길 HUD 루트입니다. Q/E 안내, 이동 안내, Day 1 업무 안내가 들어 있는 부모를 넣으십시오.")]
    [SerializeField] private GameObject[] initialMonitoringHiddenRoots;

    [Header("Report Selected Label Layout")]
    [Tooltip("보고서 상단 선택 버튼에서 이 글자 수를 초과하면 한 줄 유지를 위해 글자 크기를 줄입니다.")]
    [SerializeField, Min(1)] private int reportLabelShrinkAfterCharacterCount = 5;
    [SerializeField, Range(0.8f, 1f)] private float reportLongLabelFontSizeMultiplier = 0.96f;

    [Header("Success Report Noise")]
    [SerializeField] private CCTVNoiseProfile successReportNoiseProfile;
    [SerializeField, Min(0.01f)] private float fallbackSuccessReportNoiseDuration = 1.1f;

    [Header("False Report Noise")]
    [SerializeField] private CCTVNoiseProfile falseReportNoiseProfile;
    [SerializeField, Min(0.01f)] private float fallbackFalseReportNoiseDuration = 2f;

    [Header("Missed Signal Loss")]
    [SerializeField] private CCTVNoiseProfile missedSignalLossNoiseProfile;
    [SerializeField, Min(0f)] private float missedFeedFreezeDuration = 0.1f;
    [SerializeField, Min(0.02f)] private float missedTimeGlitchInterval = 0.05f;

    [Header("Terminal Failure Signal Loss")]
    [Tooltip("마지막 실패 때만 사용하는 긴 신호 유실 프로필입니다. 일반 미보고 노이즈와 분리됩니다.")]
    [SerializeField] private CCTVNoiseProfile terminalFailureNoiseProfile;
    [Tooltip("프로필을 지정하지 않았을 때 사용하는 마지막 실패 노이즈 시간입니다.")]
    [SerializeField, Min(0.02f)] private float fallbackTerminalFailureNoiseDuration = 2f;

    public Sprite[] ReportSprites;  // 오보고에 따른 보고서 이미지 변경 리스트

    // 보고 패널은 기본 높이까지만 열리는 상태와 선택 리스트를 보여주는 전체 높이 상태를 나눠 사용한다.
    private float _reportFullHeight = 0.0f;
    private float _reportDefaultHeight = 0.0f;
    private bool _isReport = false;
    private bool _isNormalizingReport;
    private Tween _slideTween;
    private CCTVUIEffectController _uiEffectController;
    private Coroutine missedSignalLossCoroutine;
    private Coroutine initialMonitoringIntroCoroutine;
    private bool initialMonitoringIntroPlayed;

    // 보고 제출 시 사용할 내부 선택값. UI 표시 문자열이 아니라 enum/id 값을 저장한다.
    private AreaId selectedAreaId = AreaId.None;
    private string selectedObjectId;
    private ReportTargetId selectedTargetId = ReportTargetId.None;
    private AnomalyReportType selectedReportType = AnomalyReportType.None;

    // 화면에 보이는 문구와 실제 보고 값이 어긋나지 않도록, 각 선택 목록을 유지한다.
    private readonly List<ReportSelectOption> areaReportOptions = new List<ReportSelectOption>();
    private readonly List<ReportSelectOption> objectReportOptions = new List<ReportSelectOption>();
    private readonly List<ReportSelectOption> typeReportOptions = new List<ReportSelectOption>();
    private readonly Dictionary<TMP_Text, ReportLabelLayoutDefaults> reportLabelLayoutDefaults = new Dictionary<TMP_Text, ReportLabelLayoutDefaults>();

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

    /// <summary>
    /// CCTV 화면의 보조 버튼 OnClick에 연결합니다.
    /// 메인룸으로 나가도 감시 시간과 이상현상 타이머는 멈추지 않습니다.
    /// </summary>
    public void ExitToMainRoom()
    {
        ResolveReferences();
        day1FlowController?.ExitCCTVToMainRoom();
    }

    /// <summary>
    /// 첫 감시 진입에만 적용되는 짧은 준비 연출입니다.
    /// HUD를 숨기고 Q/E, 화면 이동, 보고 입력을 막았다가 지정 시간 뒤 HUD와 입력을 함께 복구합니다.
    /// </summary>
    public void PlayInitialMonitoringIntro()
    {
        if (initialMonitoringIntroPlayed)
            return;

        initialMonitoringIntroPlayed = true;
        ResolveReferences();

        if (initialMonitoringIntroCoroutine != null)
            StopCoroutine(initialMonitoringIntroCoroutine);

        SetInitialMonitoringHudVisible(false);
        sceneController?.SetCCTVInputEnabled(false);
        panController?.SetInputLocked(true);
        GameManager.Instance?.SetReportInputEnabled(false);

        initialMonitoringIntroCoroutine = StartCoroutine(InitialMonitoringIntroRoutine());
    }

    private IEnumerator InitialMonitoringIntroRoutine()
    {
        if (initialMonitoringIntroDelay > 0f)
            yield return new WaitForSecondsRealtime(initialMonitoringIntroDelay);

        SetInitialMonitoringHudVisible(true);
        sceneController?.SetCCTVInputEnabled(true);
        panController?.SetInputLocked(false);
        GameManager.Instance?.SetReportInputEnabled(true);
        initialMonitoringIntroCoroutine = null;
    }

    private void SetInitialMonitoringHudVisible(bool visible)
    {
        if (initialMonitoringHiddenRoots == null)
            return;

        foreach (GameObject root in initialMonitoringHiddenRoots)
        {
            if (root != null)
                root.SetActive(visible);
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
            GameManager.Instance.DayWrongReportCountChanged -= SetFailureCountInfo;
            GameManager.Instance.DayMissedAnomaly -= OnMissedAnomaly;
            GameManager.Instance.DayTimeChanged += SetDayTimeInfo;
            GameManager.Instance.DayWrongReportCountChanged += SetFailureCountInfo;
            GameManager.Instance.DayMissedAnomaly += OnMissedAnomaly;
            SyncCurrentCCTVAreaInfo();
            SyncCurrentDayWrongReportCount();
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
        areaReportOptions.Clear();

        // 구역 후보는 현재 일차에 생성된 CCTV 채널 기준이며, 중복 AreaId는 한 번만 표시한다.
        foreach (CCTVAreaDefinition area in GetReportAreaDefinitions())
        {
            if (area == null || area.AreaId == AreaId.None)
                continue;

            string label = string.IsNullOrWhiteSpace(area.DisplayName)
                ? CCTVReportLabelProvider.GetAreaLabel(area.AreaId)
                : area.DisplayName;

            var option = new ReportSelectOption
            {
                Kind = ReportSelectKind.Area,
                Label = label,
                AreaId = area.AreaId,
                ObjectId = null,
                TargetId = ReportTargetId.None,
                ReportType = AnomalyReportType.None,
            };
            areaReportOptions.Add(option);
            CreateReportContentItem(areaContentParent.transform, option);
        }

        EnsureAreaSelection();
    }

    private void RefreshObjectReportContents()
    {
        GameObject objectContentParent = GetObject((int)Objects.ObjectContentBox);
        if (objectContentParent == null)
            return;

        ClearContent(objectContentParent.transform);
        objectReportOptions.Clear();

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

            var option = new ReportSelectOption
            {
                Kind = ReportSelectKind.Object,
                Label = label,
                AreaId = instance.Definition != null ? instance.Definition.AreaId : AreaId.None,
                ObjectId = sceneObject.ObjectId,
                TargetId = sceneObject.TargetId,
                ReportType = AnomalyReportType.None,
            };
            objectReportOptions.Add(option);
            CreateReportContentItem(objectContentParent.transform, option);
        }

        EnsureObjectSelection();
    }

    private void RefreshTypeReportContents()
    {
        GameObject typeContentParent = GetObject((int)Objects.TypeContentBox);
        if (typeContentParent == null)
            return;

        ClearContent(typeContentParent.transform);
        typeReportOptions.Clear();

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
            var option = new ReportSelectOption
            {
                Kind = ReportSelectKind.AnomalyType,
                Label = CCTVReportLabelProvider.GetReportTypeLabel(reportType),
                AreaId = AreaId.None,
                ObjectId = null,
                TargetId = ReportTargetId.None,
                ReportType = reportType,
            };
            typeReportOptions.Add(option);
            CreateReportContentItem(typeContentParent.transform, option);
        }

        EnsureTypeSelection();
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
                ApplyAreaSelection(option);
                break;
            case ReportSelectKind.Object:
                ApplyObjectSelection(option);
                break;
            case ReportSelectKind.AnomalyType:
                ApplyTypeSelection(option);
                break;
        }

        Debug.Log($"[INFO] CCTVSceneUI::OnReportOptionSelected - kind={option.Kind}, label={option.Label}, area={selectedAreaId}, object={selectedObjectId}, target={selectedTargetId}, type={selectedReportType}");
    }

    /// <summary>
    /// 목록을 최초 생성했을 때만 0번 항목을 실제 보고값으로 확정한다.
    /// 이후 사용자가 적은 선택은 후보 목록이 달라져도 자동으로 덮어쓰지 않는다.
    /// </summary>
    private void EnsureAreaSelection()
    {
        if (areaReportOptions.Count == 0)
            return;

        if (selectedAreaId == AreaId.None)
            ApplyAreaSelection(areaReportOptions[0]);
    }

    private void EnsureObjectSelection()
    {
        if (objectReportOptions.Count == 0)
            return;

        if (selectedTargetId == ReportTargetId.None)
            ApplyObjectSelection(objectReportOptions[0]);
    }

    private void EnsureTypeSelection()
    {
        if (typeReportOptions.Count == 0)
            return;

        if (selectedReportType == AnomalyReportType.None)
            ApplyTypeSelection(typeReportOptions[0]);
    }

    private void ApplyAreaSelection(ReportSelectOption option)
    {
        selectedAreaId = option.AreaId;
        SetText(Texts.AreaReportText, option.Label);
        RefreshObjectReportContents();
    }

    private void ApplyObjectSelection(ReportSelectOption option)
    {
        if (option.AreaId != AreaId.None)
            selectedAreaId = option.AreaId;

        selectedObjectId = option.ObjectId;
        selectedTargetId = option.TargetId;
        SetText(Texts.ObjectReportText, option.Label);
    }

    private void ApplyTypeSelection(ReportSelectOption option)
    {
        selectedReportType = option.ReportType;
        SetText(Texts.TypeReportText, option.Label);
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
        Debug.Log($"[INFO] CCTVSceneUI::OnMissedAnomaly - 미보고 신호 유실 연출 실행 anomaly={anomalyId}");

        if (missedSignalLossCoroutine != null)
            StopCoroutine(missedSignalLossCoroutine);

        missedSignalLossCoroutine = StartCoroutine(PlayMissedSignalLossAfterEmergencyRoutine());
    }

    private IEnumerator PlayMissedSignalLossAfterEmergencyRoutine()
    {
        ResolveReferences();
        while (day1FlowController != null && day1FlowController.IsEmergencyPresentationLocked)
            yield return null;

        yield return PlayMissedSignalLossRoutine();
        missedSignalLossCoroutine = null;
    }

    private IEnumerator PlayMissedSignalLossRoutine()
    {
        ResolveReferences();
        if (screenEffectController == null)
        {
            PlayFalseReportNoise();
            yield break;
        }

        if (missedFeedFreezeDuration > 0f)
            yield return screenEffectController.FreezeFeedRoutine(missedFeedFreezeDuration);

        float noiseDuration = missedSignalLossNoiseProfile != null
            ? missedSignalLossNoiseProfile.TotalTimedDuration
            : fallbackFalseReportNoiseDuration;

        if (missedSignalLossNoiseProfile != null)
            screenEffectController.PlayNoise(missedSignalLossNoiseProfile);
        else
            screenEffectController.PlayTransitionNoise(noiseDuration);

        yield return StartCoroutine(GlitchTimeTextRoutine(noiseDuration));
    }

    /// <summary>
    /// 결과 실패 연출에서 호출합니다. 일반 미보고 신호 유실과 별도 프로필을 사용합니다.
    /// 프로필을 쓸 경우 해당 Profile의 Fade In + Hold + Fade Out 합이 실제 지속 시간입니다.
    /// </summary>
    public void PlayTerminalFailureNoise()
    {
        ResolveReferences();
        if (screenEffectController == null)
            return;

        if (terminalFailureNoiseProfile != null)
            screenEffectController.PlayNoise(terminalFailureNoiseProfile);
        else
            screenEffectController.PlaySustainedTransitionNoise(fallbackTerminalFailureNoiseDuration);
    }

    private IEnumerator GlitchTimeTextRoutine(float duration)
    {
        TMP_Text timeText = GetText((int)Texts.TimeText);
        string originalText = timeText != null ? timeText.text : string.Empty;
        float elapsed = 0f;

        while (timeText != null && elapsed < duration)
        {
            timeText.text = $"{Random.Range(0, 100):00}:{Random.Range(0, 100):00}";
            yield return new WaitForSecondsRealtime(missedTimeGlitchInterval);
            elapsed += missedTimeGlitchInterval;
        }

        if (timeText != null)
        {
            if (dayRuntimeController != null)
                SetDayTimeInfo(dayRuntimeController.ElapsedSec, dayRuntimeController.DurationSec);
            else
                timeText.text = originalText;
        }
    }

    private void SyncCurrentCCTVAreaInfo()
    {
        if (GameManager.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentCCTVLabel))
            return;

        SetCCTVAreaInfo(GameManager.Instance.CurrentCCTVLabel, GameManager.Instance.CurrentAreaName);
    }

    private void SyncCurrentDayWrongReportCount()
    {
        if (GameManager.Instance == null)
            return;

        SetFailureCountInfo(GameManager.Instance.CurrentDayWrongReportCount, GameManager.Instance.CurrentDayMaxWrongReportCount);
    }

    // 선택된 구역/오브젝트/이상현상 타입을 UI에 표시한다. 선택 전에는 기본 텍스트를 보여준다.
    private void SetText(Texts textType, string value)
    {
        TMP_Text text = GetText((int)textType);
        if (text == null)
            return;

        if (textType == Texts.AreaReportText ||
            textType == Texts.ObjectReportText ||
            textType == Texts.TypeReportText)
        {
            ApplyReportSelectedLabel(text, value);
            return;
        }

        text.text = value;
    }

    private void ApplyReportSelectedLabel(TMP_Text text, string value)
    {
        if (!reportLabelLayoutDefaults.TryGetValue(text, out ReportLabelLayoutDefaults defaults))
        {
            defaults = new ReportLabelLayoutDefaults(text.fontSize, text.enableAutoSizing, text.enableWordWrapping);
            reportLabelLayoutDefaults.Add(text, defaults);
        }

        string safeValue = value ?? string.Empty;
        bool isLongLabel = safeValue.Length > reportLabelShrinkAfterCharacterCount;
        text.enableWordWrapping = isLongLabel ? false : defaults.WordWrapping;
        text.enableAutoSizing = isLongLabel ? false : defaults.AutoSizing;
        text.fontSize = isLongLabel
            ? defaults.FontSize * reportLongLabelFontSizeMultiplier
            : defaults.FontSize;
        text.text = safeValue;
    }

    private readonly struct ReportLabelLayoutDefaults
    {
        public readonly float FontSize;
        public readonly bool AutoSizing;
        public readonly bool WordWrapping;

        public ReportLabelLayoutDefaults(float fontSize, bool autoSizing, bool wordWrapping)
        {
            FontSize = fontSize;
            AutoSizing = autoSizing;
            WordWrapping = wordWrapping;
        }
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
                if (!IsReportSelectableArea(area) || !addedAreaIds.Add(area.AreaId))
                    continue;

                yield return area;
            }
        }

        // 채널 목록을 아직 만들지 못한 초기 타이밍에서는 현재 표시 중인 구역을 fallback으로 제공한다.
        if (areaView != null && IsReportSelectableArea(areaView.CurrentArea) && addedAreaIds.Add(areaView.CurrentArea.AreaId))
            yield return areaView.CurrentArea;
    }

    // 미보고 단계에서만 표시되는 제어실 외부/내부 채널은 분위기와 실패 연출용 영상이다.
    // 실제 이상현상 보고 장소 후보에는 넣지 않는다.
    private static bool IsReportSelectableArea(CCTVAreaDefinition area)
    {
        if (area == null || area.AreaId == AreaId.None)
            return false;

        return area.AreaId != AreaId.ControlRoomExterior &&
               area.AreaId != AreaId.CCTVRoom;
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

        if (day1FlowController == null)
            day1FlowController = FindFirstObjectByType<Day1FlowController>();
    }

    private void OnDestroy()
    {
        _slideTween?.Kill();

        if (missedSignalLossCoroutine != null)
            StopCoroutine(missedSignalLossCoroutine);

        CancelInitialMonitoringIntro();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OpenReport -= ReportPanelTrigger;
            GameManager.Instance.CCTVAreaChanged -= SetCCTVAreaInfo;
            GameManager.Instance.DayTimeChanged -= SetDayTimeInfo;
            GameManager.Instance.DayWrongReportCountChanged -= SetFailureCountInfo;
            GameManager.Instance.DayMissedAnomaly -= OnMissedAnomaly;
        }
    }

    private void OnDisable()
    {
        CancelInitialMonitoringIntro();
    }

    private void CancelInitialMonitoringIntro()
    {
        if (initialMonitoringIntroCoroutine == null)
            return;

        StopCoroutine(initialMonitoringIntroCoroutine);
        initialMonitoringIntroCoroutine = null;
        SetInitialMonitoringHudVisible(true);
        sceneController?.SetCCTVInputEnabled(true);
        panController?.SetInputLocked(false);
        GameManager.Instance?.SetReportInputEnabled(true);
    }
}








