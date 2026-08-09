using System;
using UnityEngine;

/// <summary>
/// Day 3 전용 서버실 현장을 구성하고 손상된 관측 기록 복구 상호작용을 처리합니다.
/// 공용 FieldMode의 플레이어, 카메라, 바닥 충돌과 복귀 문은 유지하고 Day 1 시각 요소만 교체합니다.
/// </summary>
public class Day3RecordRecoveryController : MonoBehaviour
{
    private const string DefaultCompletionFlag = "D3_OBSERVATION_RECORD_RESTORED";
    private const string ServerRoomFieldName = "Area_ServerRoom_FieldMode";

    [Header("Field Setup")]
    [SerializeField] private Transform fieldModeRoot;
    [SerializeField] private FieldPlayerMovementController playerMovement;
    [SerializeField] private Transform returnDoor;
    [SerializeField] private Day1FlowController dayFlowController;
    [SerializeField] private FieldModeController fieldModeController;
    [SerializeField] private MainSceneUI mainSceneUI;

    [Header("Placement")]
    [SerializeField] private Vector3 playerStartLocalPosition = new Vector3(-12.5f, 0.129f, 0f);
    [SerializeField] private Vector3 returnDoorLocalPosition = new Vector3(-14.25f, 0.32f, 0f);
    [SerializeField] private string terminalObjectName = "Computer_0";

    [Header("Recovery Interaction")]
    [SerializeField, Min(0.1f)] private float interactionRange = 1.75f;
    [SerializeField, Min(0.1f)] private float requiredHoldSeconds = 2.5f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private string completionStoryFlag = DefaultCompletionFlag;

    [Header("Recovery UI")]
    [SerializeField] private Font interfaceFont;
    [SerializeField] private Sprite progressBarBackgroundSprite;
    [SerializeField] private Sprite progressBarFillSprite;

    private Transform serverRoomField;
    private Transform terminal;
    private SpriteRenderer terminalRenderer;
    private Color terminalNormalColor = Color.white;
    private bool screenOpen;
    private bool waitingForInitialRelease;
    private bool recoveryCompleted;
    private bool completionAcknowledged;
    private bool objectiveUiSuppressed;
    private float heldSeconds;
    private float completionInputUnlockAt;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle promptStyle;
    private GUIStyle progressStyle;

    private static readonly Color PanelOuterColor = new Color(0.02f, 0.035f, 0.04f, 0.98f);
    private static readonly Color PanelInnerColor = new Color(0.055f, 0.075f, 0.08f, 0.98f);
    private static readonly Color AccentColor = new Color(0.55f, 1f, 0.9f, 1f);

    public bool IsRecoveryCompleted => recoveryCompleted;
    public float HoldProgress => requiredHoldSeconds > 0f ? Mathf.Clamp01(heldSeconds / requiredHoldSeconds) : 0f;

    private void Awake()
    {
        ResolveReferences();

        if (!IsDay3())
        {
            enabled = false;
            return;
        }

        ConfigureDay3Field();
    }

    private void Update()
    {
        if (PausePanelController.IsPaused)
            return;

        if (fieldModeRoot == null || !fieldModeRoot.gameObject.activeInHierarchy)
        {
            SetObjectiveUiSuppressed(false);
            return;
        }

        if (terminal == null || playerMovement == null || dayFlowController == null)
        {
            SetObjectiveUiSuppressed(false);
            ResolveReferences();
            return;
        }

        float distance = Vector2.Distance(playerMovement.transform.position, terminal.position);
        bool inRange = distance <= interactionRange;
        bool shouldSuppressObjective = screenOpen ||
                                       (!recoveryCompleted &&
                                        dayFlowController.State == Day1FlowState.EmergencyDispatch &&
                                        inRange);
        SetObjectiveUiSuppressed(shouldSuppressObjective);

        if (recoveryCompleted)
        {
            RestoreTerminalColor();
            if (!completionAcknowledged && Time.unscaledTime >= completionInputUnlockAt &&
                (Input.GetKeyDown(interactionKey) || Input.GetKeyDown(KeyCode.Escape)))
            {
                completionAcknowledged = true;
                screenOpen = false;
                SetObjectiveUiSuppressed(false);
                SetPlayerInputEnabled(true);
            }
            return;
        }

        if (dayFlowController.State != Day1FlowState.EmergencyDispatch)
        {
            if (screenOpen)
                CloseRecoveryScreen();
            RestoreTerminalColor();
            return;
        }

        if (!screenOpen)
        {
            RestoreTerminalColor();
            if (inRange && Input.GetKeyDown(interactionKey))
                OpenRecoveryScreen();
            return;
        }

        RestoreTerminalColor();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseRecoveryScreen();
            return;
        }

        if (waitingForInitialRelease)
        {
            if (!Input.GetKey(interactionKey))
                waitingForInitialRelease = false;
            return;
        }

        if (!inRange)
        {
            CloseRecoveryScreen();
            return;
        }

        if (Input.GetKey(interactionKey))
        {
            heldSeconds += Time.unscaledDeltaTime;
            if (heldSeconds >= requiredHoldSeconds)
                CompleteRecovery();
        }
        else if (heldSeconds > 0f)
        {
            heldSeconds = 0f;
        }
    }

    private void ConfigureDay3Field()
    {
        if (fieldModeRoot == null)
        {
            Debug.LogError("[Day3RecordRecoveryController] FieldModeRoot is missing.", this);
            enabled = false;
            return;
        }

        DisableLegacyDay1FieldContent();

        serverRoomField = FindChildByName(fieldModeRoot, ServerRoomFieldName);
        if (serverRoomField == null)
        {
            Debug.LogError($"[Day3RecordRecoveryController] Server-room field was not found under FieldModeRoot. name={ServerRoomFieldName}", this);
            enabled = false;
            return;
        }

        terminal = FindChildByName(serverRoomField, terminalObjectName);
        if (terminal == null)
        {
            Debug.LogError($"[Day3RecordRecoveryController] Terminal was not found. name={terminalObjectName}", this);
            enabled = false;
            return;
        }

        terminalRenderer = terminal.GetComponent<SpriteRenderer>();
        if (terminalRenderer != null)
            terminalNormalColor = terminalRenderer.color;

        if (playerMovement != null)
            playerMovement.transform.localPosition = playerStartLocalPosition;
        if (returnDoor != null)
            returnDoor.localPosition = returnDoorLocalPosition;

        RestoreTerminalColor();
        Debug.Log("[Day3RecordRecoveryController] Day 3 server-room field configured.");
    }

    private void DisableLegacyDay1FieldContent()
    {
        MissedApproachBackgroundVariants missedVariants = fieldModeRoot.GetComponent<MissedApproachBackgroundVariants>();
        if (missedVariants != null)
            missedVariants.enabled = false;

        foreach (FieldBreakerPanel breakerPanel in fieldModeRoot.GetComponentsInChildren<FieldBreakerPanel>(true))
            breakerPanel.enabled = false;

        foreach (Transform child in fieldModeRoot)
        {
            string childName = child.name;
            bool isLegacyVisual = childName.StartsWith("ElecPanel", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(childName, "Bg1", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(childName, "Bg2", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(childName, "Bg3", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(childName, "Square", StringComparison.OrdinalIgnoreCase) ||
                                  childName.StartsWith("Light (", StringComparison.OrdinalIgnoreCase);

            if (isLegacyVisual)
                child.gameObject.SetActive(false);
        }
    }

    private void OpenRecoveryScreen()
    {
        screenOpen = true;
        waitingForInitialRelease = true;
        heldSeconds = 0f;
        SetPlayerInputEnabled(false);
    }

    private void CloseRecoveryScreen()
    {
        screenOpen = false;
        waitingForInitialRelease = false;
        heldSeconds = 0f;
        SetPlayerInputEnabled(true);
    }

    private void CompleteRecovery()
    {
        recoveryCompleted = true;
        heldSeconds = requiredHoldSeconds;
        completionInputUnlockAt = Time.unscaledTime + 0.5f;
        StoryFlagStore.Set(completionStoryFlag);
        fieldModeController?.PlayPowerRestoreLightEffect();
        dayFlowController.CompleteEmergencyObjective();
        Debug.Log($"[Day3RecordRecoveryController] Observation record restored. flag={completionStoryFlag}");
    }

    private void SetPlayerInputEnabled(bool inputEnabled)
    {
        if (playerMovement != null)
            playerMovement.SetInputEnabled(inputEnabled);
    }

    private void RestoreTerminalColor()
    {
        if (terminalRenderer != null)
            terminalRenderer.color = terminalNormalColor;
    }

    private void SetObjectiveUiSuppressed(bool suppressed)
    {
        if (mainSceneUI == null)
            mainSceneUI = FindFirstObjectByType<MainSceneUI>(FindObjectsInactive.Include);

        if (mainSceneUI == null || objectiveUiSuppressed == suppressed)
            return;

        objectiveUiSuppressed = suppressed;
        mainSceneUI.SetTemporarilySuppressed(suppressed);
    }

    private void ResolveReferences()
    {
        if (dayFlowController == null)
            dayFlowController = FindFirstObjectByType<Day1FlowController>(FindObjectsInactive.Include);
        if (fieldModeController == null)
            fieldModeController = FindFirstObjectByType<FieldModeController>(FindObjectsInactive.Include);
        if (mainSceneUI == null)
            mainSceneUI = FindFirstObjectByType<MainSceneUI>(FindObjectsInactive.Include);
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<FieldPlayerMovementController>(FindObjectsInactive.Include);

        if (fieldModeRoot == null)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                Transform candidate = FindChildByName(root.transform, "FieldModeRoot");
                if (candidate != null)
                {
                    fieldModeRoot = candidate;
                    break;
                }
            }
        }

        if (returnDoor == null && fieldModeRoot != null)
            returnDoor = FindChildByName(fieldModeRoot, "Door");
    }

    private bool IsDay3()
    {
        DayRuntimeController runtime = FindFirstObjectByType<DayRuntimeController>(FindObjectsInactive.Include);
        return runtime != null && runtime.CurrentDayDefinition != null && runtime.CurrentDayDefinition.Day == 3;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return null;
    }

    private void OnGUI()
    {
        if (!enabled || fieldModeRoot == null || !fieldModeRoot.gameObject.activeInHierarchy || terminal == null || playerMovement == null)
            return;

        EnsureGuiStyles();
        GUI.depth = -1000;

        if (!screenOpen && !recoveryCompleted && dayFlowController != null &&
            dayFlowController.State == Day1FlowState.EmergencyDispatch &&
            Vector2.Distance(playerMovement.transform.position, terminal.position) <= interactionRange)
        {
            Rect promptRect = new Rect(Screen.width * 0.5f - 240f, Screen.height - 110f, 480f, 52f);
            DrawSolidRect(promptRect, PanelOuterColor);
            DrawOutline(promptRect, AccentColor, 2f);
            GUI.Label(promptRect, "[E] 기록 단말 열기", promptStyle);
        }

        if (!screenOpen)
            return;

        float width = Mathf.Min(760f, Screen.width - 80f);
        float height = 390f;
        Rect panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        DrawSolidRect(panelRect, PanelOuterColor);
        DrawOutline(panelRect, new Color(0.2f, 0.28f, 0.28f, 1f), 3f);
        Rect innerPanelRect = new Rect(panelRect.x + 8f, panelRect.y + 8f, panelRect.width - 16f, panelRect.height - 16f);
        DrawSolidRect(innerPanelRect, PanelInnerColor);
        DrawOutline(innerPanelRect, AccentColor, 1f);

        Rect contentRect = new Rect(panelRect.x + 42f, panelRect.y + 34f, panelRect.width - 84f, panelRect.height - 68f);
        GUI.Label(new Rect(contentRect.x, contentRect.y - 4f, contentRect.width, 18f),
            "OBSERVATION RECORD // O-06", progressStyle);
        GUI.Label(new Rect(contentRect.x, contentRect.y + 16f, contentRect.width, 36f),
            recoveryCompleted ? "관측 기록 복구 완료" : "손상된 관측 기록", titleStyle);

        DrawSolidRect(new Rect(contentRect.x, contentRect.y + 55f, contentRect.width, 2f), AccentColor);

        string body = recoveryCompleted
            ? "복구 데이터 일부:\nO-06 / 이전 관측자 연결 식별값 확인\n기록 복구 플래그가 저장되었습니다.\n\n[E] 확인 후 제어실로 복귀하십시오."
            : waitingForInitialRelease
                ? "복구 대상: 관측 기록 O-06\n데이터 블록이 손상되어 접근할 수 없습니다.\n\nE키를 놓은 뒤 다시 길게 눌러 복구하십시오."
                : "복구 대상: 관측 기록 O-06\nE키를 길게 눌러 손상된 데이터 블록을 복구하십시오.\n키를 놓으면 진행도가 초기화됩니다.";

        GUI.Label(new Rect(contentRect.x, contentRect.y + 68f, contentRect.width, 145f), body, bodyStyle);

        if (!recoveryCompleted && !waitingForInitialRelease)
        {
            float barWidth = Mathf.Min(480f, contentRect.width);
            Rect barBack = new Rect(contentRect.center.x - barWidth * 0.5f, contentRect.y + 225f, barWidth, 60f);
            DrawProgressBar(barBack, HoldProgress);
            GUI.Label(new Rect(barBack.x, barBack.y + 64f, barBack.width, 28f),
                $"RECOVERY {HoldProgress * 100f:0}%", progressStyle);
        }

        if (!recoveryCompleted)
            GUI.Label(new Rect(contentRect.x, contentRect.yMax - 28f, contentRect.width, 28f), "[Esc] 단말 닫기", bodyStyle);
    }

    private void EnsureGuiStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box) { font = interfaceFont };

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.font = interfaceFont;
        titleStyle.fontSize = 28;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = AccentColor;

        bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.font = interfaceFont;
        bodyStyle.fontSize = 20;
        bodyStyle.wordWrap = true;
        bodyStyle.alignment = TextAnchor.UpperLeft;
        bodyStyle.normal.textColor = new Color(0.9f, 0.95f, 0.93f, 1f);

        promptStyle = new GUIStyle(GUI.skin.label);
        promptStyle.font = interfaceFont;
        promptStyle.fontSize = 22;
        promptStyle.alignment = TextAnchor.MiddleCenter;
        promptStyle.normal.textColor = Color.white;

        progressStyle = new GUIStyle(GUI.skin.label);
        progressStyle.font = interfaceFont;
        progressStyle.fontSize = 16;
        progressStyle.alignment = TextAnchor.MiddleCenter;
        progressStyle.normal.textColor = AccentColor;
    }

    private void DrawProgressBar(Rect rect, float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (progressBarBackgroundSprite != null)
        {
            DrawSprite(rect, progressBarBackgroundSprite, Color.white, 1f);
        }
        else
        {
            DrawSolidRect(rect, new Color(0.15f, 0.19f, 0.19f, 1f));
            DrawOutline(rect, new Color(0.65f, 0.7f, 0.68f, 1f), 2f);
        }

        Rect fillRect = new Rect(
            rect.x + rect.width / 120f,
            rect.y + rect.height / 15f,
            rect.width * 118f / 120f,
            rect.height * 13f / 15f);

        if (progressBarFillSprite != null)
            DrawSprite(fillRect, progressBarFillSprite, AccentColor, progress);
        else
            DrawSolidRect(new Rect(fillRect.x, fillRect.y, fillRect.width * progress, fillRect.height), AccentColor);
    }

    private static void DrawSprite(Rect rect, Sprite sprite, Color tint, float horizontalFill)
    {
        if (sprite == null || sprite.texture == null || horizontalFill <= 0f)
            return;

        horizontalFill = Mathf.Clamp01(horizontalFill);
        Rect textureRect = sprite.textureRect;
        Rect uv = new Rect(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width * horizontalFill,
            textureRect.height / sprite.texture.height);
        rect.width *= horizontalFill;

        Color previousColor = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
        GUI.color = previousColor;
    }

    private static void DrawOutline(Rect rect, Color color, float thickness)
    {
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void OnDisable()
    {
        SetObjectiveUiSuppressed(false);

        if (screenOpen && !recoveryCompleted)
            SetPlayerInputEnabled(true);
    }
}
