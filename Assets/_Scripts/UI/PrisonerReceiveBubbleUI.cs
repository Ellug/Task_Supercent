using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 수령 중인 Prisoner 머리 위에 UI 말풍선 2종 표시
// - _receiveBubblePrefab: 쇠고랑 진행도 (fillImage + 수량 텍스트)
// - _chatBubblePrefab: 텍스트 전용 말풍선 (No Cell 등)
// - 싱글턴, LateUpdate에서 followTarget 위치 추적
[DisallowMultipleComponent]
public class PrisonerReceiveBubbleUI : MonoBehaviour
{
    public static PrisonerReceiveBubbleUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RectTransform _popupParent;
    [SerializeField] private GameObject _receiveBubblePrefab;
    [SerializeField] private GameObject _chatBubblePrefab;
    [SerializeField] private Camera _targetCamera;

    [Header("Display")]
    [SerializeField] private Vector3 _worldOffset = new(0f, 2.2f, 0f);
    [SerializeField, Min(0f)] private float _towardCameraOffset = 0.4f;

    private Canvas _popupCanvas;

    // Receive 버블 (진행도)
    private GameObject _receiveView;
    private RectTransform _receiveRect;
    private TMP_Text _receiveText;
    private Image _fillImage;
    private Prisoner _receiveTarget;

    // Chat 버블 (텍스트)
    private GameObject _chatView;
    private RectTransform _chatRect;
    private TMP_Text _chatText;
    private Prisoner _chatTarget;

    void Awake()
    {
        if (Instance != null && Instance != this)
            throw new InvalidOperationException("[PrisonerReceiveBubbleUI] Multiple instances are not allowed.");

        Instance = this;

        if (_popupParent == null)
            throw new InvalidOperationException("[PrisonerReceiveBubbleUI] _popupParent is required.");

        _popupCanvas = _popupParent.GetComponentInParent<Canvas>();
        if (_popupCanvas == null)
            throw new InvalidOperationException("[PrisonerReceiveBubbleUI] _popupParent must be under a Canvas.");
    }

    // 매 프레임 각 버블 위치 추적
    void LateUpdate()
    {
        if (_receiveTarget == null) HideReceive();
        else if (_receiveView != null && _receiveView.activeSelf)
            UpdatePosition(_receiveRect, _receiveTarget);

        if (_chatTarget == null) HideChat();
        else if (_chatView != null && _chatView.activeSelf)
            UpdatePosition(_chatRect, _chatTarget);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_receiveView != null) Destroy(_receiveView);
        if (_chatView != null) Destroy(_chatView);
    }

    // Receive 버블 표시 및 진행도 초기화
    public void ShowFor(Prisoner prisoner, int currentCuff, int maxCuff)
    {
        if (prisoner == null || !EnsureReceiveView())
            return;

        _receiveTarget = prisoner;
        _receiveView.SetActive(true);
        ApplyProgress(currentCuff, maxCuff);
        UpdatePosition(_receiveRect, prisoner);
    }

    // 동일 Prisoner면 진행도만 갱신, 다르면 ShowFor로 재시작
    public void UpdateFor(Prisoner prisoner, int currentCuff, int maxCuff)
    {
        if (prisoner == null)
            return;

        if (_receiveTarget != prisoner || _receiveView == null || !_receiveView.activeSelf)
        {
            ShowFor(prisoner, currentCuff, maxCuff);
            return;
        }

        ApplyProgress(currentCuff, maxCuff);
    }

    // 현재 추적 대상인 경우에만 Receive 버블 숨김
    public void HideFor(Prisoner prisoner)
    {
        if (prisoner != null && _receiveTarget == prisoner)
            HideReceive();
    }

    public void HideReceive()
    {
        _receiveTarget = null;
        if (_receiveView != null && _receiveView.activeSelf)
            _receiveView.SetActive(false);
    }

    // Chat 버블 표시
    public void ShowMessageFor(Prisoner prisoner, string message)
    {
        if (prisoner == null || !EnsureChatView())
            return;

        _chatTarget = prisoner;
        _chatView.SetActive(true);

        if (_chatText != null)
            _chatText.text = string.IsNullOrWhiteSpace(message) ? "!" : message;

        UpdatePosition(_chatRect, prisoner);
    }

    // 현재 추적 대상인 경우에만 Chat 버블 숨김
    public void HideChatFor(Prisoner prisoner)
    {
        if (prisoner != null && _chatTarget == prisoner)
            HideChat();
    }

    public void HideChat()
    {
        _chatTarget = null;
        if (_chatView != null && _chatView.activeSelf)
            _chatView.SetActive(false);
    }

    // 두 버블 모두 숨김
    public void Hide()
    {
        HideReceive();
        HideChat();
    }

    // Receive 버블 프리팹 인스턴스 보장
    private bool EnsureReceiveView()
    {
        if (_receiveView != null)
            return true;

        if (_receiveBubblePrefab == null)
        {
            Debug.LogWarning("[PrisonerReceiveBubbleUI] _receiveBubblePrefab is required.");
            return false;
        }

        _receiveView = Instantiate(_receiveBubblePrefab, _popupParent);
        _receiveRect = _receiveView.transform as RectTransform;
        _receiveText = _receiveView.GetComponentInChildren<TMP_Text>(true);
        _fillImage = FindFillImage(_receiveView.transform);
        _receiveView.SetActive(false);
        return true;
    }

    // Chat 버블 프리팹 인스턴스 보장
    private bool EnsureChatView()
    {
        if (_chatView != null)
            return true;

        if (_chatBubblePrefab == null)
        {
            Debug.LogWarning("[PrisonerReceiveBubbleUI] _chatBubblePrefab is required.");
            return false;
        }

        _chatView = Instantiate(_chatBubblePrefab, _popupParent);
        _chatRect = _chatView.transform as RectTransform;
        _chatText = _chatView.GetComponentInChildren<TMP_Text>(true);
        _chatView.SetActive(false);
        return true;
    }

    // 텍스트(남은 수량)와 fillAmount 갱신
    private void ApplyProgress(int currentCuff, int maxCuff)
    {
        int safeMax = Mathf.Max(1, maxCuff);
        int safeCurrent = Mathf.Clamp(currentCuff, 0, safeMax);

        if (_receiveText != null)
            _receiveText.text = (safeMax - safeCurrent).ToString();

        if (_fillImage != null)
            _fillImage.fillAmount = safeCurrent / (float)safeMax;
    }

    // 대상 Prisoner 위치를 스크린 좌표로 변환해 버블 앵커 갱신
    private void UpdatePosition(RectTransform bubbleRect, Prisoner prisoner)
    {
        if (bubbleRect == null || prisoner == null)
            return;

        Camera camera = ResolveCamera();
        if (camera == null)
            return;

        Vector3 worldPos = GetDisplayWorldPosition(prisoner.transform.position, camera);
        Vector3 screenPos = camera.WorldToScreenPoint(worldPos);

        // 카메라 뒤에 있으면 숨김
        if (screenPos.z <= 0f)
        {
            bubbleRect.gameObject.SetActive(false);
            return;
        }

        if (!bubbleRect.gameObject.activeSelf)
            bubbleRect.gameObject.SetActive(true);

        Camera eventCamera = _popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_popupParent, screenPos, eventCamera, out Vector2 localPoint))
            bubbleRect.anchoredPosition = localPoint;
    }

    // 월드 오프셋 + 카메라 방향으로 약간 앞으로 이동해 겹침 방지
    private Vector3 GetDisplayWorldPosition(Vector3 worldAnchor, Camera camera)
    {
        Vector3 position = worldAnchor + _worldOffset;
        Vector3 towardCamera = camera.transform.position - worldAnchor;
        towardCamera.y = 0f;
        if (towardCamera.sqrMagnitude > 0.0001f)
            position += towardCamera.normalized * Mathf.Max(0f, _towardCameraOffset);
        return position;
    }

    // Filled 타입 Image 우선 탐색, 없으면 첫 번째 Image 반환
    private static Image FindFillImage(Transform root)
    {
        if (root == null)
            return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].type == Image.Type.Filled)
                return images[i];
        }

        return images.Length > 0 ? images[0] : null;
    }

    // _targetCamera 유효하면 사용, 아니면 Camera.main으로 갱신
    private Camera ResolveCamera()
    {
        if (_targetCamera != null && _targetCamera.isActiveAndEnabled)
            return _targetCamera;

        _targetCamera = Camera.main;
        return _targetCamera;
    }
}
