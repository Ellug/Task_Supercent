using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Game/UI/Stage Guide Indicator UI")]
public class StageGuideIndicatorUI : MonoBehaviour
{
    private enum GuideStep
    {
        None = 0,
        ToMineIndicatorPoint = 1,
        ToCuffFactorySubmit = 2,
        ToCollectCuff = 3,
        ToDeskSubmit = 4,
        ToCollectMoney = 5,
    }

    [Header("Bindings")]
    [SerializeField] private StageProgressManager _stageProgressManager;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private Transform _defaultPlayer;
    [SerializeField] private Transform _mineIndicatorPoint;
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private ResourceData _oreResource;
    [SerializeField, Min(1)] private int _oreTargetAmount = 10;

    [Header("Shared Target")]
    [SerializeField] private Vector3 _targetWorldOffset = new(0f, 1.2f, 0f);

    [Header("Direction Arrow UI")]
    [SerializeField] private RectTransform _directionArrowParent;
    [SerializeField] private GameObject _directionArrowPrefab;
    [SerializeField, Min(0f)] private float _directionDistanceOffset = 1.2f;
    [SerializeField, Range(-180f, 180f)] private float _directionArrowTiltX = 60f;

    [Header("World Bounce Arrow")]
    [SerializeField] private Transform _worldArrowParent;
    [SerializeField] private GameObject _worldArrowPrefab;
    [SerializeField] private Vector3 _worldArrowOffset = new(0f, 2f, 0f);
    [SerializeField, Min(0f)] private float _worldArrowRotateSpeed = 180f;
    [SerializeField, Min(0f)] private float _worldArrowBounceAmplitude = 0.2f;
    [SerializeField, Min(0f)] private float _worldArrowBounceFrequency = 1.8f;
    [SerializeField] private float _worldArrowScale = 0.1f;

    private bool _isBoundToStage;
    private bool _isBoundToPlayerStack;
    private bool _firstOreMined;
    private bool _oreTargetTriggered;

    private GuideStep _step = GuideStep.None;
    private Transform _activeTarget;
    private Canvas _directionArrowCanvas;
    private GameObject _directionArrowView;
    private RectTransform _directionArrowRect;

    private GameObject _worldArrowView;
    private Quaternion _worldArrowBaseRotation = Quaternion.identity;
    private float _worldArrowStartTime;

    void Awake()
    {
        if (_directionArrowParent != null)
            _directionArrowCanvas = _directionArrowParent.GetComponentInParent<Canvas>();
    }

    void OnEnable()
    {
        BindEvents();

        if (_stageProgressManager != null && _stageProgressManager.IsInitialized)
            EnterInitialStep();
    }

    void OnDisable()
    {
        UnbindEvents();
        HideAll();
    }

    void LateUpdate()
    {
        UpdateActiveGuideArrow();
    }

    public void ClearTarget()
    {
        _activeTarget = null;
        HideAll();
    }

    public bool SetTarget(Transform target, InteractionZoneId zoneId = InteractionZoneId.None)
    {
        if (target == null)
            return false;

        _activeTarget = target;
        _worldArrowStartTime = Time.time;
        return true;
    }

    private void BindEvents()
    {
        if (!_isBoundToStage && _stageProgressManager != null)
        {
            _stageProgressManager.Initialized += OnStageInitialized;
            _stageProgressManager.ZoneStarted += OnZoneStarted;
            _isBoundToStage = true;
        }

        if (!_isBoundToPlayerStack && _playerController != null && _playerController.CarryStack != null)
        {
            _playerController.CarryStack.Changed += OnPlayerCarryChanged;
            _isBoundToPlayerStack = true;
        }
    }

    private void UnbindEvents()
    {
        if (_isBoundToStage && _stageProgressManager != null)
        {
            _stageProgressManager.Initialized -= OnStageInitialized;
            _stageProgressManager.ZoneStarted -= OnZoneStarted;
        }
        _isBoundToStage = false;

        if (_isBoundToPlayerStack && _playerController != null && _playerController.CarryStack != null)
            _playerController.CarryStack.Changed -= OnPlayerCarryChanged;

        _isBoundToPlayerStack = false;
    }

    // Mine 포인트로 첫 번째 가이드 시작
    private void EnterInitialStep()
    {
        if (_mineIndicatorPoint == null)
        {
            Debug.LogWarning("[StageGuideIndicatorUI] Mine indicator point is missing.");
            return;
        }

        _step = GuideStep.ToMineIndicatorPoint;
        SetTarget(_mineIndicatorPoint);
    }

    private void OnStageInitialized()
    {
        if (_step == GuideStep.None)
            EnterInitialStep();
    }

    // 광석 목표량 달성 시 다음 단계로, 돈 수령 완료 시 가이드 종료
    private void OnPlayerCarryChanged(ResourceData resource, int newCount, int _)
    {
        if (_step == GuideStep.ToCollectMoney && resource != null && resource.IsMoney && newCount > 0)
        {
            ClearTarget();
            _step = GuideStep.None;
            return;
        }

        if (_oreTargetTriggered)
            return;

        if (_step != GuideStep.ToMineIndicatorPoint)
            return;

        if (!IsOreResource(resource))
            return;

        if (!_firstOreMined && newCount > 0)
        {
            _firstOreMined = true;
            ClearTarget();
        }

        if (newCount < Mathf.Max(1, _oreTargetAmount))
            return;

        _oreTargetTriggered = true;
        _step = GuideStep.ToCuffFactorySubmit;
        TrySetTargetZone(InteractionZoneId.SubmitCuffFactory);
    }

    // _oreResource가 지정된 경우 그것만, 아니면 돈 이외의 리소스를 광석으로 취급
    private bool IsOreResource(ResourceData resource)
    {
        if (resource == null)
            return false;

        if (_oreResource != null)
            return resource == _oreResource;

        return !resource.IsMoney;
    }

    // 인터랙션 존 진입 시 단계별 다음 타겟으로 전환
    private void OnZoneStarted(InteractionZone zone)
    {
        if (zone == null)
            return;

        switch (_step)
        {
            case GuideStep.ToCuffFactorySubmit:
                if (zone.ZoneId != InteractionZoneId.SubmitCuffFactory)
                    return;
                _step = GuideStep.ToCollectCuff;
                TrySetTargetZone(InteractionZoneId.CollectCuff);
                return;
            case GuideStep.ToCollectCuff:
                if (zone.ZoneId != InteractionZoneId.CollectCuff)
                    return;
                _step = GuideStep.ToDeskSubmit;
                TrySetTargetZone(InteractionZoneId.SubmitDesk);
                return;
            case GuideStep.ToDeskSubmit:
                if (zone.ZoneId != InteractionZoneId.SubmitDesk)
                    return;
                _step = GuideStep.ToCollectMoney;
                TrySetTargetZone(InteractionZoneId.CollectMoney);
                return;
        }
    }

    private bool TrySetTargetZone(InteractionZoneId zoneId)
    {
        if (_stageProgressManager == null)
            return false;

        if (!_stageProgressManager.TryGetZone(zoneId, out InteractionZone zone) || zone == null)
            return false;

        return SetTarget(zone.transform, zoneId);
    }

    // 타겟 화면 안: 월드 화살표 / 화면 밖: 방향 화살표
    private void UpdateActiveGuideArrow()
    {
        if (_activeTarget == null)
        {
            HideAll();
            return;
        }

        if (_targetCamera == null)
        {
            HideAll();
            return;
        }

        if (_defaultPlayer == null)
        {
            HideAll();
            return;
        }

        Vector3 targetBasePosition = _activeTarget.position;
        Vector3 targetPosition = targetBasePosition + _targetWorldOffset;

        // 오브젝트 자체가 화면 안이면 2번(월드) 화살표를 우선 사용
        if (IsOnScreen(_targetCamera, targetBasePosition))
        {
            HideDirectionArrow();
            UpdateWorldBounceArrow(targetPosition);
            return;
        }

        HideWorldBounceArrow();
        UpdateDirectionArrow(_targetCamera, _defaultPlayer.position, targetPosition);
    }

    // 뷰포트 기준 화면 안에 있는지 판별
    private static bool IsOnScreen(Camera camera, Vector3 worldPosition)
    {
        Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
        if (viewport.z <= 0f)
            return false;

        if (viewport.x < 0f || viewport.x > 1f)
            return false;

        if (viewport.y < 0f || viewport.y > 1f)
            return false;

        return true;
    }

    // 플레이어 기준 화면 방향으로 UI 화살표 위치 및 회전 갱신
    private void UpdateDirectionArrow(Camera camera, Vector3 sourceWorldPosition, Vector3 targetWorldPosition)
    {
        if (!EnsureDirectionArrowView())
            return;

        // 타겟 방향으로 소스를 약간 오프셋해서 플레이어 몸에 가리지 않게
        Vector3 towardTarget = targetWorldPosition - sourceWorldPosition;
        towardTarget.y = 0f;
        if (towardTarget.sqrMagnitude > 0.0001f)
            sourceWorldPosition += towardTarget.normalized * _directionDistanceOffset;

        Vector3 sourceScreenPosition = camera.WorldToScreenPoint(sourceWorldPosition);
        if (sourceScreenPosition.z <= 0f)
        {
            HideDirectionArrow();
            return;
        }

        if (!_directionArrowView.activeSelf)
            _directionArrowView.SetActive(true);

        Camera eventCamera = _directionArrowCanvas != null && _directionArrowCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : camera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _directionArrowParent,
                sourceScreenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            _directionArrowRect.anchoredPosition = localPoint;
        }

        // 프리팹이 +X를 향하므로 Atan2(y,x) 기준 그대로 사용
        Vector3 targetScreenPosition = camera.WorldToScreenPoint(targetWorldPosition);
        Vector2 screenDirection = new(
            targetScreenPosition.x - sourceScreenPosition.x,
            targetScreenPosition.y - sourceScreenPosition.y);

        if (targetScreenPosition.z <= 0f)
            screenDirection = -screenDirection;

        if (screenDirection.sqrMagnitude <= 0.0001f)
            screenDirection = Vector2.up;

        float angle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg;
        _directionArrowRect.localRotation = Quaternion.Euler(_directionArrowTiltX, 0f, angle);
    }

    // 타겟 위에서 바운스하며 Y축 회전, +X가 아래를 향하도록 Z -90° 고정
    private void UpdateWorldBounceArrow(Vector3 targetPosition)
    {
        if (!EnsureWorldArrowView())
            return;

        if (!_worldArrowView.activeSelf)
            _worldArrowView.SetActive(true);

        float elapsed = Time.time - _worldArrowStartTime;
        float bounce = _worldArrowBounceAmplitude * Mathf.Sin(elapsed * Mathf.PI * 2f * _worldArrowBounceFrequency);

        Transform arrowTransform = _worldArrowView.transform;
        arrowTransform.position = targetPosition + _worldArrowOffset + Vector3.up * bounce;

        float rotateY = elapsed * _worldArrowRotateSpeed;
        arrowTransform.rotation = Quaternion.Euler(0f, rotateY, 0f) * Quaternion.Euler(0f, 0f, -90f) * _worldArrowBaseRotation;
    }

    public void HideAll()
    {
        HideDirectionArrow();
        HideWorldBounceArrow();
    }

    private void HideDirectionArrow()
    {
        if (_directionArrowView != null)
            _directionArrowView.SetActive(false);
    }

    private void HideWorldBounceArrow()
    {
        if (_worldArrowView != null)
            _worldArrowView.SetActive(false);
    }

    // 방향 화살표 프리팹 인스턴스 보장 (최초 1회 생성)
    private bool EnsureDirectionArrowView()
    {
        if (_directionArrowView != null && _directionArrowRect != null)
            return true;

        if (_directionArrowParent == null)
        {
            Debug.LogWarning("[StageGuideIndicatorUI] _directionArrowParent is required.");
            return false;
        }

        if (_directionArrowPrefab == null)
        {
            Debug.LogWarning("[StageGuideIndicatorUI] _directionArrowPrefab is required.");
            return false;
        }

        _directionArrowView = Instantiate(_directionArrowPrefab, _directionArrowParent);
        _directionArrowRect = _directionArrowView.transform as RectTransform;
        if (_directionArrowRect == null)
        {
            Destroy(_directionArrowView);
            _directionArrowView = null;
            Debug.LogWarning("[StageGuideIndicatorUI] _directionArrowPrefab must use RectTransform.");
            return false;
        }

        _directionArrowView.SetActive(false);
        return true;
    }

    // 월드 화살표 프리팹 인스턴스 보장 (최초 1회 생성)
    private bool EnsureWorldArrowView()
    {
        if (_worldArrowView != null)
            return true;

        if (_worldArrowPrefab == null)
        {
            Debug.LogWarning("[StageGuideIndicatorUI] _worldArrowPrefab is required.");
            return false;
        }

        _worldArrowView = Instantiate(_worldArrowPrefab, _worldArrowParent);
        _worldArrowView.transform.localScale = Vector3.one * _worldArrowScale;
        _worldArrowBaseRotation = _worldArrowView.transform.rotation;
        _worldArrowView.SetActive(false);
        return true;
    }
}
