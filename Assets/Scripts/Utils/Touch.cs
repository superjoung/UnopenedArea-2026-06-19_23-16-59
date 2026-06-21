using UnityEngine;
using UnityEngine.EventSystems;

namespace Utils
{
    public static class TouchUtils
    {
        public enum TouchState { None, Began, Moved, Ended }; //모바일과 에디터 모두 가능하게 터치 & 마우스 처리
                                                              // 모바일 or 에디터 마우스 터치좌표 처리
        public static void TouchSetUp(ref TouchState touchState, ref Vector2 touchPosition)
        {
#if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current.IsPointerOverGameObject() == false) { touchState = TouchState.Began; }
            }
            else if (Input.GetMouseButton(0)) { if (EventSystem.current.IsPointerOverGameObject() == false) { touchState = TouchState.Moved; } }
            else if (Input.GetMouseButtonUp(0)) { if (EventSystem.current.IsPointerOverGameObject() == false) { touchState = TouchState.Ended; } }
            else touchState = TouchState.None;
            touchPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
#else
                if (Input.touchCount > 0)
                {

                    Touch touch = Input.GetTouch(0);
                    if (EventSystem.current.IsPointerOverGameObject(touch.fingerId) == true) return;
                    if (touch.phase == TouchPhase.Began) touchState = TouchState.Began;
                    else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) touchState = TouchState.Moved;
                    else if (touch.phase == TouchPhase.Ended) if (touchState != TouchState.None) touchState = TouchState.Ended;
                    touchPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                }
                else touchState = TouchState.None;
#endif
        }
    }
}