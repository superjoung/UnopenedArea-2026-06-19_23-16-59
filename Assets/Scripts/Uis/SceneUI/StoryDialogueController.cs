using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전화 수신 뒤 사용하는 간단한 대화 진행기입니다.
/// 이 컴포넌트는 항상 활성 상태인 MainSceneUI 같은 부모에 붙이고,
/// storyUiRoot에는 평소 비활성인 StoryUI를 할당합니다.
/// </summary>
public class StoryDialogueController : MonoBehaviour
{
    [Serializable]
    public struct DialogueLine
    {
        public string speakerName;
        [TextArea(2, 5)] public string dialogue;
    }

    [Header("Story UI")]
    [SerializeField] private GameObject storyUiRoot;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Fallback Story UI")]
    [SerializeField] private Sprite fallbackBackgroundSprite;
    [SerializeField] private TMP_FontAsset fallbackFontAsset;

    [Header("Flow References")]
    [SerializeField] private MainSceneUI mainSceneUI;
    [SerializeField] private MainRoomInteractionController mainRoomInteractionController;
    [Tooltip("StoryUI 외에 대화 중 함께 숨길 UI 루트가 있으면 넣으십시오.")]
    [SerializeField] private GameObject[] hideWhilePlaying;

    [Header("Typewriter")]
    [Tooltip("전화 수신 연출이 끝난 뒤 첫 대사창을 표시하기 전 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float firstDialogueDelay = 0.5f;
    [SerializeField, Min(0.005f)] private float characterInterval = 0.035f;
    [SerializeField] private KeyCode[] advanceKeys = { KeyCode.Space, KeyCode.Return };

    [Header("Phone Dialogue")]
    [SerializeField] private DialogueLine[] phoneDialogue =
    {
        new DialogueLine { speakerName = "관리관", dialogue = "지금부터 CCTV 감시 업무를 시작한다." },
        new DialogueLine { speakerName = "관리관", dialogue = "기준 화면과 다른 이상 현상을 발견하면 즉시 보고해." },
    };

    private Coroutine typingRoutine;
    private Coroutine startDialogueRoutine;
    private Action finishedCallback;
    private int currentLineIndex;
    private bool isPlaying;
    private bool inputReady;
    private bool lineFullyShown;

    public bool HasDialogue => phoneDialogue != null && phoneDialogue.Length > 0;
    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        ResolveReferences();
        if (storyUiRoot != null)
            storyUiRoot.SetActive(false);
    }

    private void Update()
    {
        if (isPlaying && inputReady && ReceivedAdvanceInput())
            Advance();
    }

    /// <summary>Day flow가 전화 수신 후 호출합니다.</summary>
    public void PlayPhoneDialogue(Action onFinished)
    {
        if (isPlaying)
            return;

        ResolveReferences();
        if (!HasDialogue)
        {
            onFinished?.Invoke();
            return;
        }

        EnsureFallbackStoryUi();

        isPlaying = true;
        inputReady = false;
        finishedCallback = onFinished;
        currentLineIndex = 0;
        mainSceneUI?.SetTemporarilySuppressed(true);
        mainRoomInteractionController?.SetInputLocked(true);
        SetAdditionalUiVisible(false);

        if (storyUiRoot != null)
            storyUiRoot.SetActive(false);

        startDialogueRoutine = StartCoroutine(BeginDialogueAfterDelay());
    }

    private IEnumerator BeginDialogueAfterDelay()
    {
        if (firstDialogueDelay > 0f)
            yield return new WaitForSecondsRealtime(firstDialogueDelay);

        if (!isPlaying)
            yield break;

        if (storyUiRoot != null)
            storyUiRoot.SetActive(true);

        inputReady = true;
        ShowCurrentLine();
        startDialogueRoutine = null;
    }

    private void Advance()
    {
        // 출력 도중 어떤 클릭/키 입력이 오면 우선 현재 문장만 완성한다.
        if (!lineFullyShown)
        {
            CompleteCurrentLineImmediately();
            return;
        }

        currentLineIndex++;
        if (currentLineIndex < phoneDialogue.Length)
        {
            ShowCurrentLine();
            return;
        }

        FinishDialogue();
    }

    private void ShowCurrentLine()
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        DialogueLine line = phoneDialogue[currentLineIndex];
        if (speakerNameText != null)
            speakerNameText.text = line.speakerName ?? string.Empty;

        if (dialogueText == null)
        {
            lineFullyShown = true;
            return;
        }

        dialogueText.text = line.dialogue ?? string.Empty;
        dialogueText.maxVisibleCharacters = 0;
        lineFullyShown = false;
        typingRoutine = StartCoroutine(TypeCurrentLine());
    }

    private IEnumerator TypeCurrentLine()
    {
        dialogueText.ForceMeshUpdate();
        int visibleCount = dialogueText.textInfo.characterCount;
        for (int i = 1; i <= visibleCount; i++)
        {
            dialogueText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(characterInterval);
        }

        lineFullyShown = true;
        typingRoutine = null;
    }

    private void CompleteCurrentLineImmediately()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (dialogueText != null)
            dialogueText.maxVisibleCharacters = int.MaxValue;
        lineFullyShown = true;
    }

    private void FinishDialogue()
    {
        if (startDialogueRoutine != null)
            StopCoroutine(startDialogueRoutine);
        startDialogueRoutine = null;

        if (typingRoutine != null)
            StopCoroutine(typingRoutine);
        typingRoutine = null;

        isPlaying = false;
        inputReady = false;
        lineFullyShown = false;
        if (storyUiRoot != null)
            storyUiRoot.SetActive(false);

        SetAdditionalUiVisible(true);
        mainRoomInteractionController?.SetInputLocked(false);
        mainSceneUI?.SetTemporarilySuppressed(false);

        Action callback = finishedCallback;
        finishedCallback = null;
        callback?.Invoke();
    }

    private bool ReceivedAdvanceInput()
    {
        if (Input.GetMouseButtonDown(0))
            return true;

        if (advanceKeys == null)
            return false;

        foreach (KeyCode key in advanceKeys)
        {
            if (Input.GetKeyDown(key))
                return true;
        }

        return false;
    }

    private void SetAdditionalUiVisible(bool visible)
    {
        if (hideWhilePlaying == null)
            return;

        foreach (GameObject uiRoot in hideWhilePlaying)
        {
            if (uiRoot != null)
                uiRoot.SetActive(visible);
        }
    }

    private void ResolveReferences()
    {
        if (mainSceneUI == null)
            mainSceneUI = GetComponent<MainSceneUI>();
        if (mainSceneUI == null)
            mainSceneUI = FindFirstObjectByType<MainSceneUI>();
        if (mainRoomInteractionController == null)
            mainRoomInteractionController = FindFirstObjectByType<MainRoomInteractionController>();

        // 현재 MainSceneUI > StoryUI > (SituationText, ObjectiveText) 구조라면
        // Inspector 연결 없이도 기본 참조를 채운다. 직접 연결한 값은 덮어쓰지 않는다.
        if (storyUiRoot == null)
        {
            Transform storyRoot = transform.Find("StoryUI");
            if (storyRoot != null)
                storyUiRoot = storyRoot.gameObject;
        }

        if (storyUiRoot == null || (speakerNameText != null && dialogueText != null))
            return;

        foreach (TMP_Text text in storyUiRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null)
                continue;

            if (speakerNameText == null && text.name == "SituationText")
                speakerNameText = text;
            else if (dialogueText == null && text.name == "ObjectiveText")
                dialogueText = text;
        }
    }

    /// <summary>
    /// 일부 Day 씬은 CommonRoot가 풀린 채 StoryUI 자식만 누락되어 있습니다.
    /// Day1·Day2 CommonRoot의 StoryUI 규격과 같은 런타임 UI를 생성합니다.
    /// </summary>
    private void EnsureFallbackStoryUi()
    {
        if (storyUiRoot != null)
            return;

        GameObject root = new GameObject("StoryUI_Runtime", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        ApplyRect(
            rootRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(457f, -162f),
            new Vector2(100f, 100f));

        GameObject imageObject = new GameObject(
            "Image",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(root.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        ApplyRect(
            imageRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(127.524414f, -39.956898f),
            new Vector2(684.0383f, 258.4344f));
        Image background = imageObject.GetComponent<Image>();
        background.sprite = fallbackBackgroundSprite;
        background.color = Color.white;
        background.raycastTarget = true;

        TMP_FontAsset font = fallbackFontAsset != null
            ? fallbackFontAsset
            : mainSceneUI != null ? mainSceneUI.ObjectiveText?.font : TMP_Settings.defaultFontAsset;

        speakerNameText = CreateRuntimeText(
            root.transform,
            "NameText",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(279f, -12f),
            new Vector2(838.75f, 50f),
            33.8f,
            FontStyles.Bold,
            font);
        dialogueText = CreateRuntimeText(
            root.transform,
            "DialougeText",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(313f, -29f),
            new Vector2(1004.81f, 50f),
            24.27f,
            FontStyles.Normal,
            font);

        storyUiRoot = root;
        storyUiRoot.SetActive(false);
    }

    private static TMP_Text CreateRuntimeText(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        float fontSize,
        FontStyles fontStyle,
        TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        ApplyRect(rect, anchorMin, anchorMax, anchoredPosition, sizeDelta);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void ApplyRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
    private void OnDisable()
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);
        typingRoutine = null;

        if (startDialogueRoutine != null)
            StopCoroutine(startDialogueRoutine);
        startDialogueRoutine = null;
    }
}
