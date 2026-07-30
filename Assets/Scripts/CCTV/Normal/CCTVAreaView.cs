using System.Collections.Generic;
using UnityEngine;

public class CCTVAreaView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform areaRoot;
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("Options")]
    [SerializeField] private float areaSpacing = 100f;
    [SerializeField] private bool scaleBackgroundToDefinitionSize;

    private readonly Dictionary<AreaId, CCTVAreaInstance> instancesByAreaId = new Dictionary<AreaId, CCTVAreaInstance>();
    private readonly Dictionary<AreaId, Vector3> originsByAreaId = new Dictionary<AreaId, Vector3>();

    public CCTVAreaDefinition CurrentArea { get; private set; }
    public CCTVAreaInstance CurrentInstance { get; private set; }

    private void Awake()
    {
        EnsureAreaRoot();
        EnsureBackgroundRenderer();
    }

    public void Bind(CCTVAreaDefinition area)
    {
        ShowArea(area);
    }

    public void PrepareAreas(IEnumerable<CCTVAreaDefinition> areas)
    {
        EnsureAreaRoot();
        EnsureBackgroundRenderer();
        backgroundRenderer.gameObject.SetActive(false);

        if (areas == null)
            return;

        int areaIndex = 0;
        foreach (CCTVAreaDefinition area in areas)
        {
            if (area == null)
                continue;

            if (area.AreaId == AreaId.None)
            {
                Debug.LogWarning($"[CCTVAreaView] AreaId.None skipped. area={area.name}");
                continue;
            }

            if (instancesByAreaId.ContainsKey(area.AreaId))
            {
                Debug.LogWarning($"[CCTVAreaView] Duplicate area skipped. areaId={area.AreaId}, area={area.name}");
                continue;
            }

            Vector3 origin = transform.position + new Vector3(areaSpacing * areaIndex, 0f, 0f);
            CCTVAreaInstance instance = CreateAreaInstance(area, origin);
            if (instance == null)
                continue;

            instancesByAreaId.Add(area.AreaId, instance);
            originsByAreaId.Add(area.AreaId, origin);
            areaIndex++;
        }
    }

    /// <summary>
    /// 근무 도중 새 CCTV 채널이 해금될 때 해당 구역 인스턴스를 추가합니다.
    /// 이미 준비된 구역이면 중복 생성하지 않습니다.
    /// </summary>
    public bool TryPrepareAdditionalArea(CCTVAreaDefinition area)
    {
        EnsureAreaRoot();
        EnsureBackgroundRenderer();

        if (area == null || area.AreaId == AreaId.None)
            return false;

        if (instancesByAreaId.ContainsKey(area.AreaId))
            return true;

        Vector3 origin = transform.position + new Vector3(areaSpacing * instancesByAreaId.Count, 0f, 0f);
        CCTVAreaInstance instance = CreateAreaInstance(area, origin);
        if (instance == null)
            return false;

        instancesByAreaId.Add(area.AreaId, instance);
        originsByAreaId.Add(area.AreaId, origin);
        return true;
    }

    public void ShowArea(CCTVAreaDefinition area)
    {
        CurrentArea = area;
        EnsureBackgroundRenderer();

        if (area == null)
        {
            backgroundRenderer.sprite = null;
            CurrentInstance = null;
            return;
        }

        if (!instancesByAreaId.TryGetValue(area.AreaId, out CCTVAreaInstance instance))
        {
            Debug.LogWarning($"[CCTVAreaView] Area instance is not prepared. areaId={area.AreaId}, area={area.name}");
            CurrentInstance = null;
            return;
        }

        CurrentInstance = instance;
    }

    public bool TryGetAreaInstance(AreaId areaId, out CCTVAreaInstance instance)
    {
        return instancesByAreaId.TryGetValue(areaId, out instance);
    }

    public Vector3 GetAreaOrigin(AreaId areaId)
    {
        return originsByAreaId.TryGetValue(areaId, out Vector3 origin)
            ? origin
            : transform.position;
    }

    private CCTVAreaInstance CreateAreaInstance(CCTVAreaDefinition area, Vector3 origin)
    {
        GameObject instanceObject;

        if (area.AreaPrefab != null)
        {
            instanceObject = Instantiate(area.AreaPrefab, areaRoot);
            instanceObject.name = area.AreaPrefab.name;
        }
        else
        {
            instanceObject = new GameObject($"{area.AreaId}_AreaInstance");
            instanceObject.transform.SetParent(areaRoot, false);
            CreateFallbackBackground(instanceObject.transform, area);
            Debug.LogWarning($"[CCTVAreaView] AreaPrefab is missing. Fallback background was created. areaId={area.AreaId}");
        }

        instanceObject.transform.position = origin;

        CCTVAreaInstance instance = instanceObject.GetComponent<CCTVAreaInstance>();
        if (instance == null)
            instance = instanceObject.AddComponent<CCTVAreaInstance>();

        instance.Initialize(area, origin);
        return instance;
    }

    private void CreateFallbackBackground(Transform parent, CCTVAreaDefinition area)
    {
        var background = new GameObject("Background");
        background.transform.SetParent(parent, false);

        SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = area.BackgroundSprite;

        if (scaleBackgroundToDefinitionSize && renderer.sprite != null)
        {
            Vector2 spriteSize = renderer.sprite.bounds.size;
            Vector2 worldSize = area.WorldSize;

            if (spriteSize.x > 0f && spriteSize.y > 0f)
            {
                renderer.transform.localScale = new Vector3(
                    worldSize.x / spriteSize.x,
                    worldSize.y / spriteSize.y,
                    1f
                );
            }
        }
        else
        {
            renderer.transform.localScale = Vector3.one;
        }
    }

    private void EnsureBackgroundRenderer()
    {
        if (backgroundRenderer != null)
            return;

        Transform background = transform.Find("Background");
        if (background == null)
        {
            var go = new GameObject("Background");
            background = go.transform;
            background.SetParent(transform, false);
        }

        backgroundRenderer = background.GetComponent<SpriteRenderer>();
        if (backgroundRenderer == null)
            backgroundRenderer = background.gameObject.AddComponent<SpriteRenderer>();
    }

    private void EnsureAreaRoot()
    {
        if (areaRoot != null)
            return;

        Transform found = transform.Find("AreaRoot");
        if (found != null)
        {
            areaRoot = found;
            return;
        }

        var go = new GameObject("AreaRoot");
        areaRoot = go.transform;
        areaRoot.SetParent(transform, false);
    }
}
