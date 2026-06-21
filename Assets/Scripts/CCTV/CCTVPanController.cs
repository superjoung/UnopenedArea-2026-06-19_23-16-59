using UnityEngine;
using UnityEngine.EventSystems;

public class CCTVPanController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Movement")]
    [SerializeField] private float panSpeed = 6f;
    [SerializeField] private float smoothTime = 0.08f;

    [Header("Input")]
    [SerializeField] private bool useKeyboard = true;
    [SerializeField] private bool useMouseEdge = true;
    [SerializeField] private float mouseEdgePixels = 48f;
    [SerializeField] private bool blockInputOverUI = true;

    private CCTVAreaDefinition currentArea;
    private bool inputLocked;
    private float targetX;
    private float velocityX;

    public CCTVAreaDefinition CurrentArea => currentArea;
    public bool InputLocked => inputLocked;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
            targetX = targetCamera.transform.position.x;
    }

    private void LateUpdate()
    {
        if (targetCamera == null || currentArea == null)
            return;

        if (!inputLocked)
        {
            float input = GetHorizontalInput();
            targetX += input * panSpeed * Time.deltaTime;
        }

        targetX = ClampCameraX(targetX);

        Vector3 position = targetCamera.transform.position;
        position.x = Mathf.SmoothDamp(position.x, targetX, ref velocityX, smoothTime);
        position.x = ClampCameraX(position.x);
        position.y = currentArea.AreaCenter.y;
        targetCamera.transform.position = position;
    }

    public void SetArea(CCTVAreaDefinition area)
    {
        currentArea = area;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || currentArea == null)
            return;

        targetCamera.orthographic = true;
        targetCamera.orthographicSize = currentArea.OrthographicSize;

        float minX = GetMinCameraX();
        float maxX = GetMaxCameraX();
        targetX = minX > maxX
            ? currentArea.AreaCenter.x
            : Mathf.Lerp(minX, maxX, currentArea.StartNormalizedX);

        Vector3 position = targetCamera.transform.position;
        position.x = targetX;
        position.y = currentArea.AreaCenter.y;
        targetCamera.transform.position = position;
        velocityX = 0f;
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;

        if (locked && targetCamera != null)
        {
            targetX = targetCamera.transform.position.x;
            velocityX = 0f;
        }
    }

    public void MoveToNormalized(float normalizedX)
    {
        if (currentArea == null)
            return;

        float minX = GetMinCameraX();
        float maxX = GetMaxCameraX();
        targetX = minX > maxX
            ? currentArea.AreaCenter.x
            : Mathf.Lerp(minX, maxX, Mathf.Clamp01(normalizedX));
    }

    private float GetHorizontalInput()
    {
        if (blockInputOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return 0f;
        }

        float input = 0f;

        if (useKeyboard)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                input -= 1f;

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                input += 1f;
        }

        if (useMouseEdge)
        {
            Vector3 mousePosition = Input.mousePosition;

            if (mousePosition.x <= mouseEdgePixels)
                input -= 1f;
            else if (mousePosition.x >= Screen.width - mouseEdgePixels)
                input += 1f;
        }

        return Mathf.Clamp(input, -1f, 1f);
    }

    private float ClampCameraX(float x)
    {
        float minX = GetMinCameraX();
        float maxX = GetMaxCameraX();

        if (minX > maxX)
            return currentArea.AreaCenter.x;

        return Mathf.Clamp(x, minX, maxX);
    }

    private float GetMinCameraX()
    {
        return currentArea.ViewWorldMinX + GetCameraHalfWidth();
    }

    private float GetMaxCameraX()
    {
        return currentArea.ViewWorldMaxX - GetCameraHalfWidth();
    }

    private float GetCameraHalfWidth()
    {
        return targetCamera.orthographicSize * targetCamera.aspect;
    }
}
