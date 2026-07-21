using UnityEngine;

public enum FieldCameraFollowUpdateMode
{
    LateUpdate = 0,
    FixedUpdate = 1,
    None = 2,
}

public class FieldCameraFollowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FieldPlayerProfile profile;
    [SerializeField] private Transform target;

    [Header("Options")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.5f, -10f);
    [SerializeField] private bool clampX = true;
    [SerializeField] private FieldCameraFollowUpdateMode updateMode = FieldCameraFollowUpdateMode.LateUpdate;

    [Header("Pixel Snap")]
    [SerializeField] private bool snapToPixelGrid;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;

    private Vector3 velocity;

    private void FixedUpdate()
    {
        if (updateMode == FieldCameraFollowUpdateMode.FixedUpdate)
            FollowTarget(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        if (updateMode == FieldCameraFollowUpdateMode.LateUpdate)
            FollowTarget(Time.deltaTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        velocity = Vector3.zero;
    }

    private void FollowTarget(float deltaTime)
    {
        if (updateMode == FieldCameraFollowUpdateMode.None)
            return;

        if (target == null || profile == null)
            return;

        Vector3 desired = target.position + offset;
        if (clampX)
            desired.x = Mathf.Clamp(desired.x, profile.CameraXMin, profile.CameraXMax);

        desired.z = offset.z;
        float smoothTime = Mathf.Max(0.0001f, profile.CameraFollowSmooth);
        Vector3 nextPosition = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, deltaTime);

        if (snapToPixelGrid)
            nextPosition = SnapToPixelGrid(nextPosition);

        transform.position = nextPosition;
    }

    private Vector3 SnapToPixelGrid(Vector3 position)
    {
        float unit = 1f / Mathf.Max(1f, pixelsPerUnit);
        position.x = Mathf.Round(position.x / unit) * unit;
        position.y = Mathf.Round(position.y / unit) * unit;
        return position;
    }
}
