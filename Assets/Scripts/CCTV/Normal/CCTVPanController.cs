using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

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
    private CCTVAreaInstance currentInstance;
    private bool inputLocked;
    private float targetX;
    private float velocityX;
    private readonly Dictionary<AreaId, float> savedTargetXByAreaId = new Dictionary<AreaId, float>();

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
        position.y = currentInstance != null
            ? currentInstance.GetViewWorldCenterY()
            : currentArea.AreaCenter.y;
        targetCamera.transform.position = position;
    }

    public void SetArea(CCTVAreaDefinition area)
    {
        SaveCurrentAreaPosition();
        currentArea = area;
        currentInstance = null;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || currentArea == null)
            return;

        targetCamera.orthographic = true;
        targetCamera.orthographicSize = currentArea.OrthographicSize;

        float minX = GetMinCameraX();
        float maxX = GetMaxCameraX();
        targetX = GetInitialOrSavedTargetX(minX, maxX, currentArea.AreaCenter.x);

        Vector3 position = targetCamera.transform.position;
        position.x = targetX;
        position.y = currentArea.AreaCenter.y;
        targetCamera.transform.position = position;
        velocityX = 0f;
    }

    public void SetAreaInstance(CCTVAreaInstance instance)
    {
        SaveCurrentAreaPosition();
        currentInstance = instance;
        currentArea = instance != null ? instance.Definition : null;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || currentArea == null || currentInstance == null)
            return;

        targetCamera.orthographic = true;
        targetCamera.orthographicSize = currentArea.OrthographicSize;

        float minX = GetMinCameraX();
        float maxX = GetMaxCameraX();
        targetX = GetInitialOrSavedTargetX(
            minX,
            maxX,
            currentInstance.WorldOrigin.x + currentArea.AreaCenter.x);

        Vector3 position = targetCamera.transform.position;
        position.x = targetX;
        position.y = currentInstance.GetViewWorldCenterY();
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
            ? currentInstance != null ? currentInstance.WorldOrigin.x + currentArea.AreaCenter.x : currentArea.AreaCenter.x
            : Mathf.Lerp(minX, maxX, Mathf.Clamp01(normalizedX));
    }

    /// <summary>연출에서 특정 CCTV 오브젝트 위치를 즉시 보여줄 때 사용합니다.</summary>
    public void SnapToWorldX(float worldX)
    {
        if (currentArea == null || targetCamera == null)
            return;

        targetX = ClampCameraX(worldX);
        velocityX = 0f;

        Vector3 position = targetCamera.transform.position;
        position.x = targetX;
        targetCamera.transform.position = position;
    }

    /// <summary>새 근무를 시작하거나 디버그 초기화를 할 때 채널별 카메라 기억값을 비웁니다.</summary>
    public void ClearSavedAreaPositions()
    {
        savedTargetXByAreaId.Clear();
    }

    private void SaveCurrentAreaPosition()
    {
        if (currentArea == null)
            return;

        // 보간 중이어도 플레이어가 마지막으로 바라보던 목표 위치를 유지한다.
        savedTargetXByAreaId[currentArea.AreaId] = targetX;
    }

    private float GetInitialOrSavedTargetX(float minX, float maxX, float fallbackCenterX)
    {
        if (minX > maxX)
            return fallbackCenterX;

        if (currentArea != null && savedTargetXByAreaId.TryGetValue(currentArea.AreaId, out float savedX))
            return Mathf.Clamp(savedX, minX, maxX);

        return Mathf.Lerp(minX, maxX, currentArea.StartNormalizedX);
    }

    private float GetHorizontalInput()
    {
        float input = 0f;

        // 키보드 이동은 마우스 포인터의 UI 위치와 무관하게 처리한다.
        // 전체 화면 ReportBoundary가 레이캐스트를 받더라도 A/D 입력은 유지되어야 한다.
        if (useKeyboard)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                input -= 1f;

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                input += 1f;
        }

        bool isPointerOverUi = blockInputOverUI &&
                               EventSystem.current != null &&
                               EventSystem.current.IsPointerOverGameObject();

        // UI 위에서는 마우스 가장자리 이동만 차단한다.
        if (useMouseEdge && !isPointerOverUi)
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
        {
            return currentInstance != null
                ? currentInstance.WorldOrigin.x + currentArea.AreaCenter.x
                : currentArea.AreaCenter.x;
        }

        return Mathf.Clamp(x, minX, maxX);
    }

    private float GetMinCameraX()
    {
        if (currentInstance != null)
            return currentInstance.GetViewWorldMinX() + GetCameraHalfWidth();

        return currentArea.ViewWorldMinX + GetCameraHalfWidth();
    }

    private float GetMaxCameraX()
    {
        if (currentInstance != null)
            return currentInstance.GetViewWorldMaxX() - GetCameraHalfWidth();

        return currentArea.ViewWorldMaxX - GetCameraHalfWidth();
    }

    private float GetCameraHalfWidth()
    {
        return targetCamera.orthographicSize * targetCamera.aspect;
    }
}
