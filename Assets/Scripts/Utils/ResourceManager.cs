using UnityEngine;
public class ResourceManager : Singleton<ResourceManager>
{
    public T Load<T>(string path) where T : Object
    {
        if (typeof(T) == typeof(GameObject))
        {
            var name = path;
            var index = name.LastIndexOf('/');
            if (index >= 0)
                name = name.Substring(index + 1);
        }

        return Resources.Load<T>(path);
    }

    public Sprite LoadSprite(string name)
    {
        var path = $"Prefabs/SpriteIcon/{name}";

        var original = Resources.Load<Sprite>(path);
        if (original == null)
        {
            Debug.LogWarning($"[WARNING] ResourceManager(LoadSprite) 해당 스프라이트가 존재하지 않습니다. - {path}");
            return null;
        }

        return original;
    }

    public GameObject Instantiate(string path, Transform parent = null)
    {
        var original = Load<GameObject>($"Prefabs/{path}");
        if (original == null)
        {
            Debug.LogWarning($"[WARNING] ResourceManager(Instantiate) 해당 프리팹이 존재하지 않습니다. - {path}");
            return null;
        }

        if (original.GetComponent<Poolable>() != null)
            return PoolManager.Instance.Pop(original, parent).gameObject;

        var go = GameObject.Instantiate(original, parent);
        if (parent != null)
        {
            go.transform.position = parent.position;
        }
        go.name = original.name;

        return go;
    }


    public GameObject Instantiate(string path, Vector3 position, Transform parent = null)
    {
        var original = Load<GameObject>($"Prefabs/{path}");
        if (original == null)
        {
            Debug.LogWarning($"[WARNING] ResourceManager(Instantiate) 해당 프리팹이 존재하지 않습니다. - {path}");
            return null;
        }

        if (original.GetComponent<Poolable>() != null)
            return PoolManager.Instance.Pop(original, parent).gameObject
;
        var go = GameObject.Instantiate(original, position, Quaternion.identity, parent);
        if (parent != null)
        {
            go.transform.position = parent.position + position;
        }
        go.name = original.name;

        return go;
    }
    public GameObject Instantiate(string path, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var original = Load<GameObject>($"Prefabs/{path}");
        if (original == null)
        {
            Debug.LogWarning($"[WARNING] ResourceManager(Instantiate) 해당 프리팹이 존재하지 않습니다. - {path}");
            return null;
        }

        if (original.GetComponent<Poolable>() != null)
            return PoolManager.Instance.Pop(original, parent).gameObject
                ;

        var go = GameObject.Instantiate(original, position, rotation, parent);
        if (parent != null)
        {
            go.transform.position = parent.position + position;
        }
        go.name = original.name;

        return go;
    }

    public GameObject Instantiate(GameObject original, Vector3 position, Transform parent = null)
    {
        if (original == null)
        {
            Debug.LogWarning($"[WARNING] ResourceManager(Instantiate) 해당 프리팹이 존재하지 않습니다. - {original.name}");
            return null;
        }

        if (original.GetComponent<Poolable>() != null)
            return PoolManager.Instance.Pop(original, parent).gameObject
                ;

        var go = GameObject.Instantiate(original, position, Quaternion.identity, parent);
        if (parent != null)
        {
            go.transform.position = parent.position + position;
        }

        go.name = original.name;

        return go;
    }
    public GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (original == null)
        {
            Debug.LogWarning($"[WARNING] ResourceManager(Instantiate) 해당 프리팹이 존재하지 않습니다. - {original.name}");
            return null;
        }

        var go = GameObject.Instantiate(original, position, rotation, parent);
        if (parent != null)
        {
            go.transform.position = parent.position + position;
        }

        go.name = original.name;

        return go;
    }

    public void Destroy(GameObject obj, float time = 0)
    {
        if (obj == null) return;

        Poolable poolable = obj.GetComponent<Poolable>();
        if (poolable != null)
        {
            PoolManager.Instance.Push(poolable, time);
            return;
        }
        Object.Destroy(obj, time);
        obj = null;
    }
}