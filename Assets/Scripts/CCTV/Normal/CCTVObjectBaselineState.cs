using UnityEngine;

public sealed class CCTVObjectBaselineState
{
    public bool ActiveSelf { get; }
    public Vector3 LocalPosition { get; }
    public Quaternion LocalRotation { get; }
    public Vector3 LocalScale { get; }
    public Sprite Sprite { get; }
    public Color SpriteColor { get; }
    public int SortingOrder { get; }

    public CCTVObjectBaselineState(CCTVSceneObject sceneObject)
    {
        Transform tr = sceneObject.transform;
        ActiveSelf = sceneObject.gameObject.activeSelf;
        LocalPosition = tr.localPosition;
        LocalRotation = tr.localRotation;
        LocalScale = tr.localScale;

        SpriteRenderer renderer = sceneObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Sprite = renderer.sprite;
            SpriteColor = renderer.color;
            SortingOrder = renderer.sortingOrder;
        }
        else
        {
            SpriteColor = Color.white;
        }
    }

    public void Restore(CCTVSceneObject sceneObject)
    {
        Transform tr = sceneObject.transform;
        sceneObject.gameObject.SetActive(ActiveSelf);
        tr.localPosition = LocalPosition;
        tr.localRotation = LocalRotation;
        tr.localScale = LocalScale;

        SpriteRenderer renderer = sceneObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = Sprite;
            renderer.color = SpriteColor;
            renderer.sortingOrder = SortingOrder;
        }
    }
}
