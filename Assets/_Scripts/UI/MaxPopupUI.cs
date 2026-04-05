using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MaxPopupUI : MonoBehaviour
{
    public static MaxPopupUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RectTransform _popupParent;
    [SerializeField] private GameObject _popupPrefab;
    [SerializeField] private Camera _targetCamera;

    [Header("Display")]
    [SerializeField, Min(0.01f)] private float _duration = 0.45f;
    [SerializeField, Min(0f)] private float _riseDistance = 0.45f;
    [SerializeField] private Vector3 _defaultWorldOffset = new(0f, 2.2f, 0f);
    [SerializeField, Min(0f)] private float _defaultTowardCameraOffset = 0.4f;

    private sealed class ActivePopup
    {
        public GameObject View;
        public RectTransform Rect;
        public float Elapsed;
        public Vector3 AnchorWorldPosition;
        public Transform FollowTarget;
        public Vector3 WorldOffset;
        public float TowardCameraOffset;
    }

    private readonly List<ActivePopup> _activePopups = new();
    private Canvas _popupCanvas;

    void Awake()
    {
        if (Instance != null && Instance != this)
            throw new InvalidOperationException("[MaxPopupUI] Multiple instances are not allowed.");

        Instance = this;

        if (_popupParent == null)
            throw new InvalidOperationException("[MaxPopupUI] _popupParent is required.");

        if (_popupPrefab == null)
            throw new InvalidOperationException("[MaxPopupUI] _popupPrefab is required.");

        _popupCanvas = _popupParent.GetComponentInParent<Canvas>();
        if (_popupCanvas == null)
            throw new InvalidOperationException("[MaxPopupUI] _popupParent must be under a Canvas.");
    }

    void LateUpdate()
    {
        if (_activePopups.Count == 0)
            return;

        Camera camera = ResolveCamera();
        if (camera == null)
            return;

        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            ActivePopup popup = _activePopups[i];
            popup.Elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(popup.Elapsed / _duration);
            UpdatePopupPosition(popup, camera, t);

            if (t < 1f)
                continue;

            if (popup.View != null)
                Destroy(popup.View);

            _activePopups.RemoveAt(i);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        for (int i = 0; i < _activePopups.Count; i++)
        {
            ActivePopup popup = _activePopups[i];
            if (popup?.View != null)
                Destroy(popup.View);
        }

        _activePopups.Clear();
    }

    // 기본 오프셋 기준으로 팝업 출력
    public void ShowAt(Transform target)
    {
        if (target == null)
            return;

        ShowInternal(target.position, target, _defaultWorldOffset, _defaultTowardCameraOffset);
    }

    // 기본 카메라 오프셋 기준으로 팝업 출력
    public void ShowAt(Vector3 worldPosition, Vector3 worldOffset)
    {
        ShowAt(worldPosition, worldOffset, _defaultTowardCameraOffset);
    }

    // 위치/오프셋 지정 팝업 출력
    public void ShowAt(Vector3 worldPosition, Vector3 worldOffset, float towardCameraOffset)
    {
        ShowInternal(worldPosition, null, worldOffset, towardCameraOffset);
    }

    private void ShowInternal(Vector3 anchorWorldPosition, Transform followTarget, Vector3 worldOffset, float towardCameraOffset)
    {
        Camera camera = ResolveCamera();
        if (camera == null)
            return;

        GameObject popupView = Instantiate(_popupPrefab, _popupParent);
        RectTransform popupRect = popupView.transform as RectTransform;
        if (popupRect == null)
        {
            Destroy(popupView);
            throw new InvalidOperationException("[MaxPopupUI] _popupPrefab must use RectTransform.");
        }

        ActivePopup popup = new()
        {
            View = popupView,
            Rect = popupRect,
            Elapsed = 0f,
            AnchorWorldPosition = anchorWorldPosition,
            FollowTarget = followTarget,
            WorldOffset = worldOffset,
            TowardCameraOffset = Mathf.Max(0f, towardCameraOffset),
        };

        _activePopups.Add(popup);
        UpdatePopupPosition(popup, camera, 0f);
    }

    // 팝업 월드 위치 기준점 계산
    private static Vector3 GetStartWorldPosition(
        Vector3 worldPosition,
        Vector3 worldOffset,
        Camera camera,
        float towardCameraOffset)
    {
        Vector3 targetPosition = worldPosition + worldOffset;

        Vector3 towardCamera = camera.transform.position - worldPosition;
        towardCamera.y = 0f;
        if (towardCamera.sqrMagnitude > 0.0001f)
            targetPosition += towardCamera.normalized * Mathf.Max(0f, towardCameraOffset);

        return targetPosition;
    }

    // 팝업 화면 위치 갱신 (월드 -> 스크린 -> 캔버스)
    private void UpdatePopupPosition(ActivePopup popup, Camera camera, float t)
    {
        if (popup.Rect == null)
            return;

        if (popup.FollowTarget != null)
            popup.AnchorWorldPosition = popup.FollowTarget.position;

        Vector3 worldPosition = GetStartWorldPosition(
            popup.AnchorWorldPosition,
            popup.WorldOffset,
            camera,
            popup.TowardCameraOffset);

        worldPosition += Vector3.up * (_riseDistance * t);
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z <= 0f)
        {
            if (popup.View.activeSelf)
                popup.View.SetActive(false);
            return;
        }

        if (!popup.View.activeSelf)
            popup.View.SetActive(true);

        Camera eventCamera = _popupCanvas != null && _popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : camera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_popupParent, screenPosition, eventCamera, out Vector2 localPoint))
            popup.Rect.anchoredPosition = localPoint;
    }

    private Camera ResolveCamera()
    {
        if (_targetCamera != null && _targetCamera.isActiveAndEnabled)
            return _targetCamera;

        _targetCamera = Camera.main;
        return _targetCamera;
    }
}
