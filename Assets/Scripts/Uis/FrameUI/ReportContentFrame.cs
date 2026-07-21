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
            displayText.text = option.Label;
    }

    private void OnClickReportContentFrame(PointerEventData eventData)
    {
        _onSelected?.Invoke(_option);
    }
}

