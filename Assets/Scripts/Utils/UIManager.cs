using System.Collections.Generic;
using UnityEngine;
using UI.Util;

public enum UIName
{
    None,
}

public class UIManager : Singleton<UIManager>
{
    public Queue<string> LogQueue = new Queue<string>(); // 레거시 코드
    private Dictionary<UIName, Stack<BaseUI>> _uiStackDict = new Dictionary<UIName, Stack<BaseUI>>();
    private int _order = 10;

    private Stack<BaseUI> _popupStack = new Stack<BaseUI>(); // 오브젝트 말고 컴포넌트를 담음. 팝업 캔버스 UI 들을 담는다.
    private BaseUI _sceneUI = null; // 현재의 고정 캔버스 UI

    public BaseUI SceneUI { get { return _sceneUI; } private set { _sceneUI = value; } }

    // UI_Root 오브젝트에 계층 구조로 Canvas UI 정렬
    public GameObject Root()
    {
        var root = GameObject.Find("@UI_Root");
        if (root == null)
            root = new GameObject { name = "@UI_Root" };

        return root;
    }

    public void SetCanvas(GameObject go, bool sorting = false)
    {
        var canvas = go.GetOrAddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        if (sorting)
            canvas.sortingOrder = _order++;
        else
            canvas.sortingOrder = 0;
    }

    // 고정 UI 각 씬에 맞는 고정 UI 띄어주기
    public T ShowSceneUI<T>(string name = null) where T : BaseUI
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        GameObject go = ResourceManager.Instance.Instantiate($"UI/Scene/{name}");
        T sceneUI = go.GetOrAddComponent<T>();
        _sceneUI = sceneUI;

        go.transform.SetParent(Root().transform);

        return sceneUI;
    }

    // 팝업창 (나중에 사라질 UI들)
    public T ShowPopupUI<T>(string name = null) where T : BaseUI
    {
        if (string.IsNullOrEmpty(name)) // 이름을 안받았을 때
            name = typeof(T).Name;

        GameObject go = ResourceManager.Instance.Instantiate($"UI/PopUp/{name}");
        T popup = go.GetOrAddComponent<T>();
        _popupStack.Push(popup);

        go.transform.SetParent(Root().transform);

        return popup;
    }

    public void ClosePopupUI(BaseUI popup) // 안전 차원
    {
        if (_popupStack.Count == 0) // 비어있는 스택이라면 삭제 불가
            return;

        if (_popupStack.Peek() != popup)
        {
            Debug.Log("Close Popup Failed!"); // 스택의 가장 위에있는 Peek() 것만 삭제할 수 잇기 때문에 popup이 Peek()가 아니면 삭제 못함
            return;
        }

        ClosePopupUI();
    }

    public void ClosePopupUI()
    {
        if (_popupStack.Count == 0)
            return;

        BaseUI popup = _popupStack.Pop();
        ResourceManager.Instance.Destroy(popup.gameObject);
        popup = null;
        _order--; // order 줄이기
    }

    public T MakeSubItem<T>(Transform parent = null, string name = null) where T : BaseUI
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        GameObject go = ResourceManager.Instance.Instantiate($"UI/Frame/{name}", parent);

        return UIUtils.GetOrAddComponent<T>(go);
    }

    public T MakeSubItem<T>(Vector3 position, Quaternion rotation, Transform parent = null, string name = null) where T : BaseUI
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        GameObject go = ResourceManager.Instance.Instantiate($"UI/Frame/{name}", position, rotation, parent);

        return UIUtils.GetOrAddComponent<T>(go);
    }
    // 해당 UI에 존재하는 하이라이팅 생성하기
    public T MakeHighlighting<T>(Transform parent, string name = null) where T : BaseUI
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        GameObject go = ResourceManager.Instance.Instantiate($"UI/HighLighting/{name}", parent);

        BaseUI baseUI = UIUtils.GetOrAddComponent<T>(go);
        // 하이라이팅 그룹 추가
        if (!_uiStackDict.ContainsKey(baseUI.ID))
        {
            _uiStackDict.Add(baseUI.ID, new Stack<BaseUI>());
        }
        _uiStackDict[baseUI.ID].Push(baseUI);

        return UIUtils.GetOrAddComponent<T>(go);
    }
    // 오브젝트에 생성
    public T MakeUIToParent<T>(Transform parent, string name = null) where T : BaseUI
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        GameObject go = ResourceManager.Instance.Instantiate($"UI/Frame/{name}", parent);

        BaseUI baseUI = UIUtils.GetOrAddComponent<T>(go);
        if (!_uiStackDict.ContainsKey(baseUI.ID))
        {
            _uiStackDict.Add(baseUI.ID, new Stack<BaseUI>());
        }
        _uiStackDict[baseUI.ID].Push(baseUI);

        return UIUtils.GetOrAddComponent<T>(go);
    }

    public void CloseUI(UIName uiType)
    {
        if (!_uiStackDict.TryGetValue(uiType, out var popupStack)
            || _uiStackDict[uiType].Count == 0)
            return;

        var popup = _uiStackDict[uiType].Pop();
        ResourceManager.Instance.Destroy(popup.gameObject);
        popup = null;

        CheckUICountAndRemove();
    }

    public void CloseUI(BaseUI baseUI)
    {
        var uiType = baseUI.ID;
        if (!_uiStackDict.TryGetValue(uiType, out var popupStack)
            || _uiStackDict[uiType].Count == 0)
            return;

        if (baseUI != popupStack.Peek())
        {
            Debug.Log("Close Popup Failed");
            return;
        }

        CloseUI(uiType);
    }

    public void CloseAllUI()
    {
        foreach (var kv in _uiStackDict)
        {
            var uiType = kv.Key;
            var uiStack = kv.Value;
            while (uiStack.Count != 0)
            {
                var popup = uiStack.Pop();
                ResourceManager.Instance.Destroy(popup.gameObject);
                popup = null;
            }
        }

        CheckUICountAndRemove();
    }

    public void CloseAllGroupUI(UIName uiType)
    {
        if (!_uiStackDict.TryGetValue(uiType, out var popupStack)
            || _uiStackDict[uiType].Count == 0)
            return;

        while (popupStack.Count != 0)
        {
            var popup = popupStack.Pop();
            ResourceManager.Instance.Destroy(popup.gameObject);
            popup = null;
        }

        CheckUICountAndRemove();
    }

    public T FindUI<T>(string name = null) where T : BaseUI
    {
        T sceneUI = null;
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        foreach (Transform child in Root().transform)
        {
            if (name == child.name)
            {
                sceneUI = child.gameObject.GetComponent<T>();
            }
        }

        return sceneUI;
    }

    private void CheckUICountAndRemove()
    {
        var uiType = new List<UIName>();
        foreach (var popupUI in _uiStackDict.Keys) uiType.Add(popupUI);
        for (var i = 0; i < _uiStackDict.Count; i++)
            if (_uiStackDict.GetValueOrDefault(uiType[i]).Count == 0)
                _uiStackDict.Remove(uiType[i]);
        CheckUICountInScene();
    }

    private void CheckUICountInScene()
    {
        Debug.Log($"[INFO] UIManager::CheckUICountInScene() - popupCount : {_uiStackDict.Count}");
        foreach (var popupKey in _uiStackDict.Keys) Debug.Log($"[INFO] UIManager::CheckUICountInScene() - uiList : {popupKey}");
    }

    public int GetAllUICount()
    {
        return _uiStackDict.Count;
    }

    public void Clear()
    {
        CloseAllUI();
    }
}
