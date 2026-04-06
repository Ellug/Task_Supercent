using UnityEngine;

// MAX 라벨 Canvas UI 1회 생성 + 활성/비활성 + 매 프레임 월드 위치 추적 런타임
public sealed class FacilityMaxLabelRuntime
{
    private readonly GameObject _instance;
    private readonly RectTransform _rect;
    private readonly Transform _anchor;
    private readonly Vector3 _worldOffset;
    private readonly Canvas _canvas;

    public FacilityMaxLabelRuntime(GameObject prefab, RectTransform popupParent, Canvas canvas, Transform anchor, Vector3 worldOffset)
    {
        if (prefab == null || popupParent == null || canvas == null || anchor == null)
            return;

        _canvas = canvas;
        _anchor = anchor;
        _worldOffset = worldOffset;

        _instance = Object.Instantiate(prefab, popupParent);
        _rect = _instance.transform as RectTransform;
        _instance.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (_instance == null)
            return;

        if (_instance.activeSelf == visible)
            return;

        _instance.SetActive(visible);
    }

    // LateUpdate에서 호출 — 월드 위치를 Canvas 로컬 좌표로 변환해 위치 갱신
    public void Tick(Camera camera)
    {
        if (_instance == null || !_instance.activeSelf || _rect == null || camera == null || _anchor == null)
            return;

        Vector3 worldPos = _anchor.position + _worldOffset;
        Vector3 screenPos = camera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0f)
        {
            _rect.gameObject.SetActive(false);
            return;
        }

        if (!_rect.gameObject.activeSelf)
            _rect.gameObject.SetActive(true);

        Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect.parent as RectTransform, screenPos, eventCamera, out Vector2 localPoint))
        {
            _rect.anchoredPosition = localPoint;
        }
    }

    public void Dispose()
    {
        if (_instance != null)
            Object.Destroy(_instance);
    }
}
