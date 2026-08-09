using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class ReportContentFrame : BaseUI, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    enum Buttons
    {
        ReportContentFrame,   // 보고서 아이템 선택 버튼
    }

    enum Texts
    {
        DisplayText,           // 보고서 아이템 이름 텍스트
    }

    public override UIName ID => UIName.ReportContentFrame;
    protected override bool IsSorting => false;

    private ReportSelectOption _option;
    private Action<ReportSelectOption> _onSelected;
    private bool _isInitialized;

    [Header("Label Layout")]
    [Tooltip("이 글자 수를 초과한 선택지는 한 줄 유지를 위해 글자 크기를 줄입니다.")]
    [SerializeField, Min(1)] private int shrinkAfterCharacterCount = 5;
    [Tooltip("하단 선택지의 기본 글자 크기입니다.")]
    [SerializeField, Min(1f)] private float shortLabelFontSize = 32f;
    [SerializeField, Range(0.8f, 1f)] private float longLabelFontSizeMultiplier = 0.9f;

    [Header("Anomaly Type Hover Help")]
    [Tooltip("현상 설명 패널에 사용할 postit2 스프라이트입니다.")]
    [SerializeField] private Sprite anomalyTooltipBackground;
    [Tooltip("현상 설명에 사용할 공용 TMP 폰트입니다.")]
    [SerializeField] private TMP_FontAsset anomalyTooltipFont;
    [SerializeField] private Vector2 anomalyTooltipSize = new Vector2(460f, 190f);
    [SerializeField] private Vector2 anomalyTooltipOffset = new Vector2(30f, 30f);
    [SerializeField, Min(1f)] private float anomalyTooltipFontSize = 28f;

    void Start()
    {
        if (!_isInitialized)
            Init();
    }

    public override void Init()
    {
        if (_isInitialized)
            return;

        _objects.Clear();
        Bind<TMP_Text>(typeof(Texts));
        Bind<Button>(typeof(Buttons));

        // 버튼 이벤트 등록
        GetButton((int)Buttons.ReportContentFrame).gameObject.BindEvent(OnClickReportContentFrame);
        _isInitialized = true;
    }

    public void Setup(ReportSelectOption option, Action<ReportSelectOption> onSelected)
    {
        if (!_isInitialized)
            Init();

        _option = option;
        _onSelected = onSelected;

        TMP_Text displayText = GetText((int)Texts.DisplayText);
        if (displayText != null)
            ApplyLabel(displayText, option.Label);
    }

    private void ApplyLabel(TMP_Text displayText, string label)
    {
        string safeLabel = label ?? string.Empty;
        bool isLongLabel = safeLabel.Length > shrinkAfterCharacterCount;
        displayText.enableWordWrapping = false;
        displayText.enableAutoSizing = false;
        displayText.fontSize = isLongLabel
            ? shortLabelFontSize * longLabelFontSizeMultiplier
            : shortLabelFontSize;
        displayText.text = safeLabel;
    }

    private void OnClickReportContentFrame(PointerEventData eventData)
    {
        _onSelected?.Invoke(_option);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowAnomalyTypeTooltip(eventData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (_option.Kind != ReportSelectKind.AnomalyType)
            return;

        ReportTypeHoverTooltip.Move(this, eventData.position, anomalyTooltipOffset);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ReportTypeHoverTooltip.Hide(this);
    }

    private void OnDisable()
    {
        ReportTypeHoverTooltip.Hide(this);
    }

    private void OnDestroy()
    {
        ReportTypeHoverTooltip.Hide(this);
    }

    private void ShowAnomalyTypeTooltip(PointerEventData eventData)
    {
        if (_option.Kind != ReportSelectKind.AnomalyType ||
            _option.ReportType == AnomalyReportType.None)
        {
            return;
        }

        string description = CCTVReportLabelProvider.GetReportTypeDescription(_option.ReportType);
        if (string.IsNullOrWhiteSpace(description))
            return;

        ReportTypeHoverTooltip.Show(
            this,
            transform as RectTransform,
            eventData.position,
            _option.Label,
            description,
            anomalyTooltipBackground,
            anomalyTooltipFont,
            anomalyTooltipSize,
            anomalyTooltipOffset,
            anomalyTooltipFontSize);
    }
}

/// <summary>
/// 현상 선택지들이 공유하는 단일 호버 패널입니다.
/// 선택지 목록의 Mask에 잘리지 않도록 최상위 Canvas 바로 아래에 생성합니다.
/// </summary>
internal static class ReportTypeHoverTooltip
{
    private static ReportContentFrame _owner;
    private static GameObject _root;
    private static RectTransform _rect;
    private static RectTransform _canvasRect;
    private static Canvas _canvas;
    private static Image _background;
    private static TextMeshProUGUI _descriptionText;

    public static void Show(
        ReportContentFrame owner,
        RectTransform source,
        Vector2 screenPosition,
        string title,
        string description,
        Sprite backgroundSprite,
        TMP_FontAsset fontAsset,
        Vector2 size,
        Vector2 offset,
        float fontSize)
    {
        if (owner == null || source == null)
            return;

        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        Canvas rootCanvas = sourceCanvas != null ? sourceCanvas.rootCanvas : null;
        RectTransform rootCanvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        if (rootCanvas == null || rootCanvasRect == null)
            return;

        EnsureCreated(rootCanvas, rootCanvasRect);

        _owner = owner;
        _rect.sizeDelta = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        _background.sprite = backgroundSprite;
        _background.type = Image.Type.Simple;
        _background.preserveAspect = false;

        if (fontAsset != null)
            _descriptionText.font = fontAsset;

        _descriptionText.fontSize = Mathf.Max(1f, fontSize);
        _descriptionText.text = $"<b>{title}</b>\n<size=86%>{description}</size>";
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        Move(owner, screenPosition, offset);
    }

    public static void Move(ReportContentFrame owner, Vector2 screenPosition, Vector2 offset)
    {
        if (owner == null || owner != _owner || _root == null || !_root.activeSelf ||
            _canvas == null || _canvasRect == null || _rect == null)
        {
            return;
        }

        Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Vector2 halfSize = _rect.sizeDelta * 0.5f;
        Vector2 desired = localPoint + offset + halfSize;
        Rect bounds = _canvasRect.rect;

        if (desired.x + halfSize.x > bounds.xMax)
            desired.x = localPoint.x - offset.x - halfSize.x;
        if (desired.y + halfSize.y > bounds.yMax)
            desired.y = localPoint.y - offset.y - halfSize.y;

        desired.x = Mathf.Clamp(desired.x, bounds.xMin + halfSize.x, bounds.xMax - halfSize.x);
        desired.y = Mathf.Clamp(desired.y, bounds.yMin + halfSize.y, bounds.yMax - halfSize.y);
        _rect.anchoredPosition = desired;
    }

    public static void Hide(ReportContentFrame owner)
    {
        if (owner == null || owner != _owner)
            return;

        _owner = null;
        if (_root != null)
            _root.SetActive(false);
    }

    private static void EnsureCreated(Canvas canvas, RectTransform canvasRect)
    {
        if (_root != null && _canvas == canvas)
            return;

        if (_root != null)
            UnityEngine.Object.Destroy(_root);

        _canvas = canvas;
        _canvasRect = canvasRect;

        _root = new GameObject(
            "ReportTypeHoverTooltip",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        _root.layer = canvas.gameObject.layer;
        _root.transform.SetParent(canvasRect, false);

        _rect = _root.GetComponent<RectTransform>();
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);

        _background = _root.GetComponent<Image>();
        _background.color = Color.white;
        _background.raycastTarget = false;

        CanvasGroup canvasGroup = _root.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject textObject = new GameObject(
            "DescriptionText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = _root.layer;
        textObject.transform.SetParent(_root.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(34f, 25f);
        textRect.offsetMax = new Vector2(-34f, -25f);

        _descriptionText = textObject.GetComponent<TextMeshProUGUI>();
        _descriptionText.color = new Color32(39, 35, 25, 255);
        _descriptionText.alignment = TextAlignmentOptions.MidlineLeft;
        _descriptionText.textWrappingMode = TextWrappingModes.Normal;
        _descriptionText.overflowMode = TextOverflowModes.Overflow;
        _descriptionText.richText = true;
        _descriptionText.raycastTarget = false;

        _root.SetActive(false);
    }
}

