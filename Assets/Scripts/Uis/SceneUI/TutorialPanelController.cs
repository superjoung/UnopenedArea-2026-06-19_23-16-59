using System;
using UnityEngine;

/// <summary>
/// Day 1 전화 브리핑 뒤 표시하는 조작 안내 패널입니다.
/// 닫힐 때까지 메인룸 입력과 상황 UI를 잠그고, 닫힌 뒤 대기 중인 흐름을 재개합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MainSceneUI mainSceneUI;
    [SerializeField] private MainRoomInteractionController mainRoomInteractionController;

    private Action closedCallback;

    public bool IsOpen => gameObject.activeSelf;

    public void Show(Action onClosed)
    {
        ResolveReferences();
        closedCallback = onClosed;
        mainSceneUI?.SetTemporarilySuppressed(true);
        mainRoomInteractionController?.SetInputLocked(true);
        gameObject.SetActive(true);
    }

    /// <summary>내부 X 버튼과 PausePanelController의 Esc 처리에서 호출합니다.</summary>
    public void ClosePanel()
    {
        if (!IsOpen)
            return;

        gameObject.SetActive(false);
        mainRoomInteractionController?.SetInputLocked(false);
        mainSceneUI?.SetTemporarilySuppressed(false);

        Action callback = closedCallback;
        closedCallback = null;
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        if (!IsOpen)
            return;

        mainRoomInteractionController?.SetInputLocked(false);
        mainSceneUI?.SetTemporarilySuppressed(false);
        closedCallback = null;
    }

    private void ResolveReferences()
    {
        if (mainSceneUI == null)
            mainSceneUI = FindFirstObjectByType<MainSceneUI>(FindObjectsInactive.Include);
        if (mainRoomInteractionController == null)
            mainRoomInteractionController = FindFirstObjectByType<MainRoomInteractionController>(FindObjectsInactive.Include);
    }
}
