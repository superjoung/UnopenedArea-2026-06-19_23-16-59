using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UI.Util;
using UI.Define;
using System;
using UnityEngine.EventSystems;

public static class Extension
{
    // 확장 메서드
    public static T GetOrAddComponent<T>(this GameObject go) where T : UnityEngine.Component
    {
        return UIUtils.GetOrAddComponent<T>(go);
    }

    // 확장메서드
    public static void BindEvent(this GameObject go, Action<PointerEventData> action, UIDefine.UIEvent type = UIDefine.UIEvent.Click)
    {
        BaseUI.BindEvent(go, action, type);
    }
}
