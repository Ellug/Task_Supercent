using UnityEngine;
using UnityEngine.InputSystem;

// 플로팅 조이스틱 입력
[DisallowMultipleComponent]
public class FloatingJoystickInput : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _spawnRoot;
    [SerializeField] private RectTransform _baseVisual;
    [SerializeField] private RectTransform _thumbVisual;

    [Header("Drag")]
    [SerializeField, Min(1f)] private float _maxDragDistance = 120f;
    [SerializeField, Min(0f)] private float _thumbEdgeOverflow = 4f;

    private Vector2 _startLocalPosition;
    private Vector2 _startAnchoredPosition;
    private Vector2 _moveInput;
    private bool _isDragging;

    public Vector2 MoveInput => _moveInput;
    public bool IsDragging => _isDragging;

    void Awake()
    {
        _baseVisual.gameObject.SetActive(false);
        _thumbVisual.gameObject.SetActive(false);
    }

    void Update()
    {
        if (HandleTouchInput())
            return;

        HandleMouseInput();
    }

    // 터치 입력 처리
    private bool HandleTouchInput()
    {
        Touchscreen touchScreen = Touchscreen.current;
        if (touchScreen == null)
            return false;

        var touch = touchScreen.primaryTouch;
        bool hasTouchInput = touch.press.isPressed || touch.press.wasPressedThisFrame || touch.press.wasReleasedThisFrame;
        if (!hasTouchInput)
            return false;

        Vector2 screenPosition = touch.position.ReadValue();
        if (touch.press.wasPressedThisFrame)
            BeginDrag(screenPosition);

        if (touch.press.isPressed && _isDragging)
            UpdateDrag(screenPosition);

        if (touch.press.wasReleasedThisFrame)
            EndDrag();

        return true;
    }

    // 마우스 입력 처리
    private void HandleMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 screenPosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
            BeginDrag(screenPosition);

        if (mouse.leftButton.isPressed && _isDragging)
            UpdateDrag(screenPosition);

        if (mouse.leftButton.wasReleasedThisFrame)
            EndDrag();
    }

    // 드래그 시작
    private void BeginDrag(Vector2 screenPosition)
    {
        _isDragging = true;
        _moveInput = Vector2.zero;
        _startLocalPosition = ScreenToLocalPosition(screenPosition);
        _startAnchoredPosition = LocalToAnchoredPosition(_baseVisual, _startLocalPosition);

        _baseVisual.anchoredPosition = _startAnchoredPosition;
        _thumbVisual.anchoredPosition = _startAnchoredPosition;
        _baseVisual.gameObject.SetActive(true);
        _thumbVisual.gameObject.SetActive(true);
    }

    // 드래그 갱신
    private void UpdateDrag(Vector2 screenPosition)
    {
        float dragLimit = GetEffectiveDragDistance();
        Vector2 currentLocalPosition = ScreenToLocalPosition(screenPosition);
        Vector2 localDelta = currentLocalPosition - _startLocalPosition;
        Vector2 clampedLocalDelta = Vector2.ClampMagnitude(localDelta, dragLimit);

        Vector2 thumbLocalPosition = _startLocalPosition + clampedLocalDelta;
        _thumbVisual.anchoredPosition = LocalToAnchoredPosition(_thumbVisual, thumbLocalPosition);
        _moveInput = clampedLocalDelta / Mathf.Max(1f, dragLimit);
    }

    // 드래그 종료
    private void EndDrag()
    {
        _isDragging = false;
        _moveInput = Vector2.zero;
        _thumbVisual.anchoredPosition = _baseVisual.anchoredPosition;

        _baseVisual.gameObject.SetActive(false);
        _thumbVisual.gameObject.SetActive(false);
    }

    // 스크린 좌표 -> 스폰 루트 로컬 좌표
    private Vector2 ScreenToLocalPosition(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _spawnRoot,
            screenPosition,
            ResolveUiCamera(),
            out Vector2 localPoint);
        return localPoint;
    }

    // 스폰 루트 로컬 좌표 -> 대상 RectTransform 앵커드 좌표
    private Vector2 LocalToAnchoredPosition(RectTransform target, Vector2 localPosition)
    {
        Rect parentRect = _spawnRoot.rect;
        Vector2 pointFromBottomLeft = localPosition + new Vector2(parentRect.width * _spawnRoot.pivot.x, parentRect.height * _spawnRoot.pivot.y);
        Vector2 anchorReference = new Vector2(parentRect.width * target.anchorMin.x, parentRect.height * target.anchorMin.y);
        return pointFromBottomLeft - anchorReference;
    }

    private Camera ResolveUiCamera()
    {
        if (_canvas == null)
            return null;

        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return _canvas.worldCamera;
    }

    // 썸 최대 이동 반경 계산
    private float GetEffectiveDragDistance()
    {
        float baseRadius = Mathf.Min(_baseVisual.rect.width, _baseVisual.rect.height) * 0.5f;
        float thumbRadius = Mathf.Min(_thumbVisual.rect.width, _thumbVisual.rect.height) * 0.5f;
        float visualLimit = Mathf.Max(1f, baseRadius - thumbRadius + _thumbEdgeOverflow);
        return Mathf.Min(_maxDragDistance, visualLimit);
    }
}
