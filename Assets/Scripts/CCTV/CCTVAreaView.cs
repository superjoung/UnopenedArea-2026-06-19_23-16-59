using UnityEngine;

public class CCTVAreaView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("Options")]
    [SerializeField] private bool scaleBackgroundToDefinitionSize;

    public CCTVAreaDefinition CurrentArea { get; private set; }

    private void Awake()
    {
        EnsureBackgroundRenderer();
    }

    public void Bind(CCTVAreaDefinition area)
    {
        CurrentArea = area;
        EnsureBackgroundRenderer();

        if (area == null)
        {
            backgroundRenderer.sprite = null;
            return;
        }

        transform.position = new Vector3(area.AreaCenter.x, area.AreaCenter.y, transform.position.z);

        backgroundRenderer.sprite = area.BackgroundSprite;
        backgroundRenderer.transform.localPosition = Vector3.zero;

        if (scaleBackgroundToDefinitionSize && backgroundRenderer.sprite != null)
        {
            Vector2 spriteSize = backgroundRenderer.sprite.bounds.size;
            Vector2 worldSize = area.WorldSize;

            if (spriteSize.x > 0f && spriteSize.y > 0f)
            {
                backgroundRenderer.transform.localScale = new Vector3(
                    worldSize.x / spriteSize.x,
                    worldSize.y / spriteSize.y,
                    1f
                );
            }
        }
        else
        {
            backgroundRenderer.transform.localScale = Vector3.one;
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
}
