using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class ReportContentFrame : BaseUI
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
    [SerializeField, Min(1f)] private float shortLabelFontSize = 44f;
    [SerializeField, Range(0.8f, 1f)] private float longLabelFontSizeMultiplier = 0.92f;

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
}

