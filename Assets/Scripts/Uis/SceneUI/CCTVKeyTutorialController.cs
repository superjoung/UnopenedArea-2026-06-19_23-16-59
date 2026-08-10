using System;
using UnityEngine;

/// <summary>
/// Day 1 첫 CCTV 진입에서 A/D 이동, Q/E 채널 전환, W 보고 순서로
/// KeyPanel 안내를 단계적으로 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CCTVKeyTutorialController : MonoBehaviour
{
    [Header("Guide Roots")]
    [SerializeField] private GameObject top;
    [SerializeField] private GameObject middle;
    [SerializeField] private GameObject down;

    [Header("Pan Training")]
    [SerializeField] private CCTVPanController panController;
    [Tooltip("현재 CCTV의 전체 가로 이동 가능 범위 중 이 비율만큼 이동하면 Q/E를 해금합니다.")]
    [SerializeField, Range(0.05f, 1f)] private float requiredNormalizedPanSpan = 0.25f;

    private Action panTrainingCompleted;
    private float observedMinNormalizedX;
    private float observedMaxNormalizedX;
    private bool trackingPan;

    public bool IsGuideActive => gameObject.activeSelf;
    public bool IsPanTraining => trackingPan;

    private void Awake()
    {
        ResolveReferences();
        SetGuideRoots(false, false, false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!trackingPan || PausePanelController.IsPaused)
            return;

        if (panController == null)
        {
            CompletePanTraining();
            return;
        }

        if (!panController.CanPanHorizontally)
        {
            // 고정 카메라 프리팹을 잘못 첫 채널로 배치해도 튜토리얼이 막히지는 않게 한다.
            CompletePanTraining();
            return;
        }

        float normalizedX = panController.CurrentNormalizedX;
        observedMinNormalizedX = Mathf.Min(observedMinNormalizedX, normalizedX);
        observedMaxNormalizedX = Mathf.Max(observedMaxNormalizedX, normalizedX);

        if (observedMaxNormalizedX - observedMinNormalizedX >= requiredNormalizedPanSpan)
            CompletePanTraining();
    }

    public void BeginPanTraining(Action onCompleted)
    {
        ResolveReferences();
        panTrainingCompleted = onCompleted;
        trackingPan = true;
        gameObject.SetActive(true);
        SetGuideRoots(false, true, false);

        float normalizedX = panController != null ? panController.CurrentNormalizedX : 0.5f;
        observedMinNormalizedX = normalizedX;
        observedMaxNormalizedX = normalizedX;
    }

    public void ShowChannelGuide()
    {
        trackingPan = false;
        gameObject.SetActive(true);
        SetGuideRoots(true, false, false);
    }

    public void ShowReportGuide()
    {
        trackingPan = false;
        gameObject.SetActive(true);
        SetGuideRoots(false, false, true);
    }

    public void HideGuide()
    {
        trackingPan = false;
        panTrainingCompleted = null;
        SetGuideRoots(false, false, false);
        gameObject.SetActive(false);
    }

    private void CompletePanTraining()
    {
        if (!trackingPan)
            return;

        trackingPan = false;
        Action callback = panTrainingCompleted;
        panTrainingCompleted = null;
        callback?.Invoke();
    }

    private void SetGuideRoots(bool showTop, bool showMiddle, bool showDown)
    {
        if (top != null)
            top.SetActive(showTop);
        if (middle != null)
            middle.SetActive(showMiddle);
        if (down != null)
            down.SetActive(showDown);
    }

    private void ResolveReferences()
    {
        if (top == null)
            top = FindDirectChild("Top");
        if (middle == null)
            middle = FindDirectChild("Middle");
        if (down == null)
            down = FindDirectChild("Down");
        if (panController == null)
            panController = FindFirstObjectByType<CCTVPanController>(FindObjectsInactive.Include);
    }

    private GameObject FindDirectChild(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.gameObject : null;
    }
}
