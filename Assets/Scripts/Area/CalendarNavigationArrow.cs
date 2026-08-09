using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CalendarNavigationArrow : MonoBehaviour
{
    [SerializeField] private MainRoomCalendarController calendarController;
    [SerializeField] private int direction = 1;
    [SerializeField, Min(1f)] private float clickScaleMultiplier = 1.08f;
    [SerializeField, Min(0.01f)] private float clickDuration = 0.08f;

    private Vector3 defaultLocalScale;
    private Coroutine clickRoutine;

    private void Awake()
    {
        defaultLocalScale = transform.localScale;
    }

    public void Configure(MainRoomCalendarController controller, int navigationDirection)
    {
        calendarController = controller;
        direction = navigationDirection < 0 ? -1 : 1;
        defaultLocalScale = transform.localScale;
    }

    private void OnMouseUpAsButton()
    {
        if (clickRoutine != null || calendarController == null || !calendarController.IsOpen)
            return;

        clickRoutine = StartCoroutine(PlayClickFeedback());
    }

    private IEnumerator PlayClickFeedback()
    {
        Vector3 enlargedScale = defaultLocalScale * clickScaleMultiplier;
        yield return ScaleTo(enlargedScale, clickDuration);
        yield return ScaleTo(defaultLocalScale, clickDuration);
        clickRoutine = null;
        calendarController?.Navigate(direction);
    }

    private IEnumerator ScaleTo(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(startScale, targetScale, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private void OnDisable()
    {
        if (clickRoutine != null)
            StopCoroutine(clickRoutine);
        clickRoutine = null;
        transform.localScale = defaultLocalScale;
    }
}
