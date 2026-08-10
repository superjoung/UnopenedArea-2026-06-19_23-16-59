using UnityEngine;

public enum Day1AreaMode
{
    None = 0,
    MainRoom = 1,
    CCTV = 2,
    Field = 3,
}

/// <summary>
/// Day 1의 메인룸, CCTV, 외부 현장 표현을 전환합니다.
/// 게임 진행 규칙은 Day1FlowController가, 화면/공간 활성화는 이 컴포넌트가 담당합니다.
/// </summary>
public class Day1AreaTransitionController : MonoBehaviour
{
    [Header("Area Roots")]
    [SerializeField] private GameObject mainRoomRoot;
    [SerializeField] private GameObject mainRoomPrefab;
    [SerializeField] private string mainRoomResourcePath = "Prefabs/AreaPrefabs/Area_MainRoom";
    [SerializeField] private GameObject cctvSystemRoot;
    [SerializeField] private GameObject cctvUiRoot;
    [SerializeField] private GameObject cctvScreenCanvasRoot;
    [SerializeField] private FieldModeController fieldModeController;

    [Header("Cameras")]
    [SerializeField] private Camera mainRoomCamera;
    [SerializeField] private Camera cctvCamera;
    [SerializeField] private bool keepDisplayCameraActiveDuringCctv = true;

    [Header("Sound Events")]
    [SerializeField] private UnityEngine.Events.UnityEvent onEnteredCctv;
    [SerializeField] private UnityEngine.Events.UnityEvent onReturnedToMainRoom;

    public Day1AreaMode CurrentMode { get; private set; } = Day1AreaMode.None;
    public System.Action<Day1AreaMode> ModeChanged;

    private int mainRoomCameraCullingMask;
    private CameraClearFlags mainRoomCameraClearFlags;
    private bool mainRoomCameraSettingsCached;

    private void Awake() => ResolveReferences();
    private void Start() => EnterMainRoom();

    private void LateUpdate()
    {
        // CCTVScreenRigBootstrap이 늦게 만든 Canvas_CCTV만 메인룸 상태에서 숨긴다.
        // 공간 전환 자체를 다시 실행하지 않아 CCTV 진입 카메라 상태를 덮어쓰지 않는다.
        if (CurrentMode != Day1AreaMode.MainRoom || cctvScreenCanvasRoot != null)
            return;

        GameObject canvas = GameObject.Find("Canvas_CCTV");
        if (canvas != null)
        {
            cctvScreenCanvasRoot = canvas;
            SetActive(cctvScreenCanvasRoot, false);
        }
    }

    public void EnterMainRoom()
    {
        ResolveReferences();
        if (fieldModeController != null && fieldModeController.IsFieldModeActive)
            fieldModeController.ExitFieldMode();

        SetCctvPresentationActive(false);
        SetActive(mainRoomRoot, true);
        SetMainRoomCameraMode(true);
        SetCameraEnabled(cctvCamera, false);
        SoundManager.Instance?.ExitCctvBgmMode();
        onReturnedToMainRoom?.Invoke();
        ChangeMode(Day1AreaMode.MainRoom);
    }

    public bool EnterCCTV()
    {
        ResolveReferences();
        if (cctvSystemRoot == null)
        {
            Debug.LogWarning("[Day1AreaTransitionController] CCTVSystem root is missing.");
            return false;
        }

        if (fieldModeController != null && fieldModeController.IsFieldModeActive)
            fieldModeController.ExitFieldMode();

        SetActive(mainRoomRoot, false);
        SetMainRoomCameraMode(false);
        SetCctvPresentationActive(true);
        SetCameraEnabled(cctvCamera, true);
        SoundManager.Instance?.EnterCctvBgmMode();
        onEnteredCctv?.Invoke();
        ChangeMode(Day1AreaMode.CCTV);
        return true;
    }

    public bool EnterField()
    {
        ResolveReferences();
        if (fieldModeController == null)
        {
            Debug.LogWarning("[Day1AreaTransitionController] FieldModeController is missing.");
            return false;
        }

        SetActive(mainRoomRoot, false);
        SetCameraEnabled(mainRoomCamera, false);
        SetCctvPresentationActive(false);
        SetCameraEnabled(cctvCamera, false);

        if (!fieldModeController.EnterFieldMode())
        {
            EnterMainRoom();
            return false;
        }

        ChangeMode(Day1AreaMode.Field);
        return true;
    }

    private void SetCctvPresentationActive(bool active)
    {
        SetActive(cctvSystemRoot, active);
        SetActive(cctvUiRoot, active);
        SetActive(cctvScreenCanvasRoot, active);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static void SetCameraEnabled(Camera target, bool enabled)
    {
        if (target != null)
            target.enabled = enabled;
    }

    private void SetMainRoomCameraMode(bool renderMainRoom)
    {
        if (mainRoomCamera == null)
            return;

        CacheMainRoomCameraSettings();
        if (renderMainRoom)
        {
            mainRoomCamera.cullingMask = mainRoomCameraCullingMask;
            mainRoomCamera.clearFlags = mainRoomCameraClearFlags;
            mainRoomCamera.enabled = true;
            return;
        }

        if (!keepDisplayCameraActiveDuringCctv)
        {
            mainRoomCamera.enabled = false;
            return;
        }

        // CCTV는 RawImage가 화면을 채우므로, 이 카메라는 Game 뷰 Display 1을 유지하는 역할만 합니다.
        mainRoomCamera.cullingMask = 0;
        mainRoomCamera.clearFlags = CameraClearFlags.SolidColor;
        mainRoomCamera.backgroundColor = Color.black;
        mainRoomCamera.enabled = true;
    }

    private void CacheMainRoomCameraSettings()
    {
        if (mainRoomCameraSettingsCached || mainRoomCamera == null)
            return;

        mainRoomCameraCullingMask = mainRoomCamera.cullingMask;
        mainRoomCameraClearFlags = mainRoomCamera.clearFlags;
        mainRoomCameraSettingsCached = true;
    }

    private void ChangeMode(Day1AreaMode nextMode)
    {
        if (CurrentMode == nextMode)
            return;

        CurrentMode = nextMode;
        ModeChanged?.Invoke(nextMode);
        Debug.Log($"[Day1AreaTransitionController] Mode={nextMode}");
    }

    private void ResolveReferences()
    {
        // Inspector에 씬 인스턴스 대신 프리팹 에셋을 넣어도 런타임 인스턴스로 전환한다.
        if (mainRoomRoot != null && !mainRoomRoot.scene.IsValid())
        {
            mainRoomPrefab = mainRoomRoot;
            mainRoomRoot = null;
        }

        if (mainRoomRoot == null)
        {
            GameObject target = GameObject.Find("Area_MainRoom");
            if (target != null)
            {
                mainRoomRoot = target;
            }
            else
            {
                GameObject prefab = mainRoomPrefab != null
                    ? mainRoomPrefab
                    : Resources.Load<GameObject>(mainRoomResourcePath);

                if (prefab != null)
                {
                    mainRoomRoot = Instantiate(prefab);
                    mainRoomRoot.name = "Area_MainRoom";
                    if (mainRoomRoot.GetComponent<MainRoomInteractionController>() == null)
                        mainRoomRoot.AddComponent<MainRoomInteractionController>();

                    Debug.Log($"[Day1AreaTransitionController] Spawned main room from Resources/{mainRoomResourcePath}.");
                }
                else
                {
                    Debug.LogWarning($"[Day1AreaTransitionController] Main-room prefab is missing. path=Resources/{mainRoomResourcePath}");
                }
            }
        }

        if (cctvSystemRoot == null)
        {
            CCTVTestSceneController sceneController = FindFirstObjectByType<CCTVTestSceneController>(FindObjectsInactive.Include);
            if (sceneController != null) cctvSystemRoot = sceneController.gameObject;
        }

        if (cctvUiRoot == null)
        {
            CCTVSceneUI sceneUi = FindFirstObjectByType<CCTVSceneUI>(FindObjectsInactive.Include);
            if (sceneUi != null) cctvUiRoot = sceneUi.gameObject;
        }

        if (cctvScreenCanvasRoot == null)
        {
            GameObject target = GameObject.Find("Canvas_CCTV");
            if (target != null) cctvScreenCanvasRoot = target;
        }

        if (fieldModeController == null)
            fieldModeController = FindFirstObjectByType<FieldModeController>(FindObjectsInactive.Include);

        if (mainRoomCamera == null)
        {
            GameObject target = GameObject.Find("MainCamera");
            if (target != null) mainRoomCamera = target.GetComponent<Camera>();
        }

        if (cctvCamera == null)
        {
            GameObject target = GameObject.Find("CCTVCamera");
            if (target != null) cctvCamera = target.GetComponent<Camera>();
        }
    }
}
