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
    public bool HasAnimator { get; }
    public bool AnimatorEnabled { get; }

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

        Animator animator = sceneObject.GetComponent<Animator>();
        HasAnimator = animator != null;
        AnimatorEnabled = animator != null && animator.enabled;
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

        if (HasAnimator)
        {
            Animator animator = sceneObject.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = AnimatorEnabled;
        }
    }
}
