using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UI.Util;
using UI.Define;
using Object = UnityEngine.Object;

public abstract class BaseUI : MonoBehaviour
{
    protected Dictionary<Type, Object[]> _objects = new Dictionary<Type, Object[]>();
    protected abstract bool IsSorting { get; } // UI sortingOrder 설정 변수. 하위 UI에서 정의

    public abstract UIName ID { get; }

    public virtual void Init() // 초기화. UI sortingOrder 여부 설정.
    {
        UIManager.Instance.SetCanvas(gameObject, IsSorting);
        _objects.Clear(); // 딕셔너리 초기화
    }

    // component -> object로 업캐스팅
    protected void Bind<T>(Type type) where T : Object
    {
        // Enum으로 분류해둔 자식 오브젝트 string형태로 전부 받기 Reflection 사용
        string[] names = Enum.GetNames(type);
        Object[] objects = new Object[names.Length];
        _objects.Add(typeof(T), objects); // Dictionary 에 추가


        // T 에 속하는 오브젝트들을 Dictionary의 Value인 objects 배열의 원소들에 하나하나 추가
        for (int i = 0; i < objects.Length; i++)
        {
            if (typeof(T) == typeof(GameObject))
            {
                objects[i] = UIUtils.FindChild(gameObject, names[i], true);
            }
            else
            {
                objects[i] = UIUtils.FindChild<T>(gameObject, names[i], true);
            }

            if (objects[i] == null)
            {
                Debug.Log($"Failed to bind({names[i]})");
            }
        }
    }

    protected T Get<T>(int idx) where T : Object
    {
        Object[] objects = null;
        // 자식들 중 해당 이름의 자식이 있으면 return (정수형태로 바꿔서 검수)
        if (_objects.TryGetValue(typeof(T), out objects) == false)
        {
            Debug.LogWarning("[WARN]BaseUI(Get) - The specified child name does not exist.");
            return null;
        }

        return objects[idx] as T;
    }

    // 추후 컴포넌트 종류가 늘어나면 추가
    protected GameObject GetObject(int idx) { return Get<GameObject>(idx); } // 오브젝트로서 가져오기
    protected TMP_Text GetText(int idx) { return Get<TMP_Text>(idx); } // Text로서 가져오기
    protected Button GetButton(int idx) { return Get<Button>(idx); } // Button로서 가져오기
    protected Image GetImage(int idx) { return Get<Image>(idx); } // Image로서 가져오기
    protected Slider GetSlider(int idx) { return Get<Slider>(idx); } // slider로서 가져오기
    protected InputField GetInputField(int idx) { return Get<InputField>(idx); } // InputField로서 가져오기
    protected Dropdown GetDropdown(int idx) { return Get<Dropdown>(idx); } // Dropdown으로서 가져오기
    // 이벤트 델리게이트 연결 (확장 메서드로 사용)
    public static void BindEvent(GameObject go, Action<PointerEventData> action, UIDefine.UIEvent type = UIDefine.UIEvent.Click)
    {
        UIEventHandler evt = UIUtils.GetOrAddComponent<UIEventHandler>(go);

        switch (type)
        {
            case UIDefine.UIEvent.Click:
                evt.OnClickHandler -= action;
                evt.OnClickHandler += action;
                break;
            case UIDefine.UIEvent.Drag:
                evt.OnDragHandler -= action;
                evt.OnDragHandler += action;
                break;
        }
    }
}