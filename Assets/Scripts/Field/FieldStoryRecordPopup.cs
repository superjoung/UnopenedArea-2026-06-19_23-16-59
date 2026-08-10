using TMPro;
using UnityEngine;

/// <summary>
/// 여러 현장 기록물이 함께 사용하는 단일 확대 팝업입니다.
/// 팝업의 위치와 배경은 고정하고, 기록물마다 이미지와 텍스트만 교체합니다.
/// </summary>
public class FieldStoryRecordPopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    /// <summary>
    /// 공용 팝업의 배경/이미지는 유지하고, 기록물별 텍스트만 교체합니다.
    /// </summary>
    public void Show(string title, string body)
    {
        ResolveReferences();

        if (titleText != null)
            titleText.text = title ?? string.Empty;
        if (bodyText != null)
            bodyText.text = body ?? string.Empty;

        if (popupRoot != null)
            popupRoot.SetActive(true);
    }

    public void Hide()
    {
        ResolveReferences();
        if (popupRoot != null && popupRoot.activeSelf)
            popupRoot.SetActive(false);
    }

    private void Awake()
    {
        ResolveReferences();

        // NotePanel can start inactive. In that case Awake is first invoked
        // by Show() after popupRoot.SetActive(true); hiding here would turn
        // the panel straight back off on the same frame.
        // The field interaction owns the initial hidden state instead.
    }

    private void ResolveReferences()
    {
        if (popupRoot == null)
            popupRoot = gameObject;

        if (titleText == null)
        {
            Transform target = transform.Find("TitleText");
            if (target != null)
                titleText = target.GetComponent<TMP_Text>();
        }

        if (bodyText == null)
        {
            Transform target = transform.Find("BodyText");
            if (target != null)
                bodyText = target.GetComponent<TMP_Text>();
        }
    }
}
