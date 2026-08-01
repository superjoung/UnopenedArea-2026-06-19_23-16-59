using UnityEngine;

/// <summary>메인룸의 전화기, CCTV, 문 클릭을 Day 1 진행 상태에 맞춰 제어합니다.</summary>
public class MainRoomInteractionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Day1FlowController day1FlowController;
    [SerializeField] private MainRoomInteractionTarget phoneTarget;
    [SerializeField] private MainRoomInteractionTarget cctvTarget;
    [SerializeField] private MainRoomInteractionTarget doorTarget;
    [SerializeField] private TransitionEffect transitionEffect;
    private Day1FlowController subscribedFlowController;
    private bool inputLocked;

    public bool InputLocked => inputLocked;

    private void Awake() => ResolveReferences();
    private void OnEnable() { ResolveReferences(); Subscribe(); RefreshAvailability(); }
    private void Start() => RefreshAvailability();
    private void OnDisable() => Unsubscribe();

    public void TryInteract(MainRoomInteractionType interactionType)
    {
        if (inputLocked || DayTitleController.IsBlockingWorldInteractions || day1FlowController == null) return;
        switch (interactionType)
        {
            case MainRoomInteractionType.Phone: day1FlowController.AcceptPhoneMission(); break;
            case MainRoomInteractionType.CCTV:
                if (transitionEffect == null)
                    transitionEffect = FindFirstObjectByType<TransitionEffect>();

                if (transitionEffect == null || !transitionEffect.TryPlayCctvEntry())
                    day1FlowController.EnterCCTVFromMainRoom();
                break;
            case MainRoomInteractionType.Door: day1FlowController.EnterFieldFromMainRoom(); break;
        }
    }

    /// <summary>결과 연출처럼 메인룸 오브젝트의 클릭 반응 자체를 막아야 할 때 사용합니다.</summary>
    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (locked)
        {
            if (phoneTarget != null) phoneTarget.ForceHideOutline();
            if (cctvTarget != null) cctvTarget.ForceHideOutline();
            if (doorTarget != null) doorTarget.ForceHideOutline();
        }
    }

    private void HandleStateChanged(Day1FlowState state) => RefreshAvailability();

    private void RefreshAvailability()
    {
        Day1FlowState state = day1FlowController != null ? day1FlowController.State : Day1FlowState.None;
        if (phoneTarget != null) phoneTarget.SetAvailable(state == Day1FlowState.Briefing);
        if (cctvTarget != null) cctvTarget.SetAvailable(state == Day1FlowState.BaselineReview ||
                                                        state == Day1FlowState.EmergencyRecovery ||
                                                        state == Day1FlowState.Monitoring);
        if (doorTarget != null) doorTarget.SetAvailable(state == Day1FlowState.EmergencyDispatch);
    }

    private void Subscribe()
    {
        if (day1FlowController == null || subscribedFlowController == day1FlowController) return;
        Unsubscribe();
        subscribedFlowController = day1FlowController;
        subscribedFlowController.StateChanged += HandleStateChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedFlowController == null) return;
        subscribedFlowController.StateChanged -= HandleStateChanged;
        subscribedFlowController = null;
    }

    private void ResolveReferences()
    {
        if (day1FlowController == null) day1FlowController = FindFirstObjectByType<Day1FlowController>();
        if (transitionEffect == null) transitionEffect = FindFirstObjectByType<TransitionEffect>();
        foreach (MainRoomInteractionTarget target in GetComponentsInChildren<MainRoomInteractionTarget>(true))
        {
            if (target == null) continue;
            if (target.InteractionType == MainRoomInteractionType.Phone && phoneTarget == null) phoneTarget = target;
            if (target.InteractionType == MainRoomInteractionType.CCTV && cctvTarget == null) cctvTarget = target;
            if (target.InteractionType == MainRoomInteractionType.Door && doorTarget == null) doorTarget = target;
        }
    }
}
