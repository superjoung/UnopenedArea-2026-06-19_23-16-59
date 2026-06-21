using System.Collections.Generic;
using UnityEngine;

public class PoolManager : Singleton<PoolManager>
{
    #region Pool
    class Pool
    {
        public GameObject Original { get; private set; }   // 이 Pool에서 복사할 원본 오브젝트
        public Transform Root { get; set; }                // Pool 오브젝트들을 담을 루트 Transform

        Stack<Poolable> _poolStack = new Stack<Poolable>();

        // 지정된 개수만큼 Poolable 오브젝트를 미리 생성하여 Pool에 등록합니다.
        public void Init(GameObject original, int count = 5)
        {
            Original = original;
            Root = new GameObject().transform;
            Root.name = $"{Original.name}_Root";

            for (int i = 0; i < count; i++)
                Push(Create());
        }

        // Poolable 오브젝트를 새로 생성합니다.
        Poolable Create()
        {
            GameObject go = Instantiate<GameObject>(Original);
            go.name = Original.name;
            go.SetActive(false);

            Poolable poolable = go.GetOrAddComponent<Poolable>();
            poolable.poolKey = Original.name;

            return poolable;
        }

        // 사용이 끝난 오브젝트를 Pool에 반환합니다.
        public void Push(Poolable poolable)
        {
            if (poolable == null)
                return;

            poolable.transform.parent = Root;
            poolable.gameObject.SetActive(false);

            poolable.isUsing = false;

            _poolStack.Push(poolable);
        }

        // Pool에서 사용 가능한 오브젝트를 가져옵니다.
        public Poolable Pop(Transform parent)
        {
            Poolable poolable = null;

            // 사용 가능한 오브젝트가 있을 경우 꺼내고, 없으면 새로 생성합니다.
            while (_poolStack.Count > 0)
            {
                poolable = _poolStack.Pop();
                if (!poolable.gameObject.activeSelf)
                    break;
            }

            if (poolable == null || poolable.gameObject.activeSelf)
                poolable = Create();

            poolable.gameObject.SetActive(true);
            poolable.transform.parent = parent;
            poolable.isUsing = true;

            return poolable;
        }
    }
    #endregion

    Dictionary<string, Pool> _pool = new Dictionary<string, Pool>();
    Transform _root;

    // PoolManager의 루트 오브젝트를 생성하여 초기화합니다.
    public void Init()
    {
        if (_root == null)
        {
            _root = new GameObject { name = "@Pool_Root" }.transform;
            Object.DontDestroyOnLoad(_root);
        }
    }

    // 일정 시간이 지난 후 오브젝트를 Pool로 반환하거나 파괴합니다.
    public void Push(Poolable poolable, float time)
    {
        string key = poolable.poolKey;

        if (!_pool.ContainsKey(key))
        {
            Debug.LogWarning($"Pool 없음: {key}");
            GameObject.Destroy(poolable.gameObject, time);
            return;
        }

        _pool[key].Push(poolable);
    }

    // 원본 오브젝트에 해당하는 Pool에서 사용 가능한 오브젝트를 꺼냅니다.
    public Poolable Pop(GameObject original, Transform parent = null)
    {
        if (!_pool.ContainsKey(original.name))
            CreatePool(original);

        return _pool[original.name].Pop(parent);
    }

    // 새로운 원본 오브젝트에 대한 Pool을 생성합니다.
    public void CreatePool(GameObject original, int count = 5)
    {
        if (_pool.ContainsKey(original.name))
            return;
        Pool pool = new Pool();
        pool.Init(original, count);
        pool.Root.parent = _root;

        _pool.Add(original.name, pool);
    }

    // 해당 이름의 Pool에서 원본 오브젝트를 가져옵니다.
    public GameObject GetOriginal(string name)
    {
        if (!_pool.ContainsKey(name))
            return null;

        return _pool[name].Original;
    }

    // 모든 Pool을 제거하고 초기화합니다.
    public void Clear()
    {
        foreach (Transform child in _root)
        {
            GameObject.Destroy(child.gameObject);
        }

        _pool.Clear();
    }
}
