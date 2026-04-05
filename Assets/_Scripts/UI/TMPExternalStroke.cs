using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public class TMPExternalStroke : MonoBehaviour
{    
    [SerializeField] private Color _strokeColor = Color.white;          // 외곽선 색상
    [SerializeField, Min(0f)] private float _strokeSize = 6f;           // 외곽선 반경(픽셀 단위)
    [SerializeField, Range(4, 32)] private int _sampleCount = 12;       // 원형 샘플 개수(많을수록 둥근 외곽)
    [SerializeField] private bool _autoSync = true;                     // 매 프레임 원본 텍스트 변경 동기화
    [SerializeField] private bool _hideGeneratedInHierarchy = true;     // 생성 레이어를 하이어라키에서 숨김
    [SerializeField] private string _layerKey = string.Empty;           // 동일 부모에서 레이어 이름 충돌 방지 키

    private const string LayerPrefix = "__TMPStroke_";

    private TextMeshProUGUI _source;
    private readonly List<TextMeshProUGUI> _layers = new();
    private bool _pendingRenderOrder;

#if UNITY_EDITOR
    private bool _editorSetupQueued;
    private bool _editorForceRebuild;
#endif
    private bool _runtimeSetupQueued;
    private bool _runtimeForceRebuild;

    // 프리팹 복제 인스턴스끼리 _layerKey가 같아도 충돌하지 않도록 인스턴스 식별자를 접두어에 포함
    private string Prefix => $"{LayerPrefix}{_layerKey}_{GetInstanceID():X8}_";
    private string LegacyPrefix => $"{LayerPrefix}{_layerKey}_";

    // 컴포넌트 추가 시 초기 세팅
    private void Reset()
    {
        RequestSetup(forceRebuild: true, applyRenderOrderNow: false);
    }

    void Awake()
    {
        RequestSetup(forceRebuild: true, applyRenderOrderNow: false);
    }

    // 재활성화 시 레이어 보정
    void OnEnable()
    {
        RequestSetup(forceRebuild: true, applyRenderOrderNow: false);
    }

    void OnDisable()
    {
        CleanupGeneratedLayers();
    }

    // 인스펙터 변경 시 레이어를 재생성하지 않고 값만 동기화
    private void OnValidate()
    {
#if UNITY_EDITOR
        QueueEditorSetup(forceRebuild: false);
#endif
    }

    // 자동 동기화가 켜져 있으면 매 프레임 반영
    void LateUpdate()
    {
        ApplyQueuedRuntimeSetup();

        if (_autoSync)
            EnsureSetup(forceRebuild: false, applyRenderOrderNow: true);
        else
            ApplyPendingRenderOrder();
    }

    void OnDestroy()
    {
        CleanupGeneratedLayers();
    }

    // 전체 상태를 점검하고 필요 시 레이어를 재구성/동기화
    private void EnsureSetup(bool forceRebuild, bool applyRenderOrderNow)
    {
        if (!TryGetSource()) return;

        if (string.IsNullOrEmpty(_layerKey))
            _layerKey = Guid.NewGuid().ToString("N")[..8];

        Transform parent = _source.transform.parent;
        if (parent == null) return;

        // 프리팹 "에셋" 컨텍스트에서는 Transform.SetParent로 자식 생성/정렬을 건드릴 수 없다.
        // (Prefab Stage가 아닌 순수 자산 검증 시 OnValidate가 호출되면 해당 에러가 발생)
        if (IsPersistentObject(_source) || IsPersistentObject(_source.gameObject) ||
            IsPersistentObject(parent) || IsPersistentObject(parent.gameObject))
        {
            _layers.Clear();
            _pendingRenderOrder = false;
            return;
        }

        CleanupLegacyLayers(parent);
        CleanupOrphanedLayers(parent);
        CollectLayers(parent);

        int targetCount = Mathf.Clamp(_sampleCount, 4, 32);
        if (forceRebuild || _layers.Count != targetCount)
            RebuildLayers(parent, targetCount);

        List<Vector2> offsets = BuildOffsets(Mathf.Max(0f, _strokeSize), targetCount);
        for (int i = 0; i < _layers.Count; i++)
        {
            TextMeshProUGUI layer = _layers[i];
            if (layer == null)
                continue;

            SyncLayer(layer, offsets[i], i + 1);
        }

        _pendingRenderOrder = true;
        if (applyRenderOrderNow)
            ApplyPendingRenderOrder();
    }

    // Awake/OnEnable/OnValidate처럼 제한된 타이밍에서는 에디터 딜레이로 넘겨 안전하게 재구성한다.
    private void RequestSetup(bool forceRebuild, bool applyRenderOrderNow)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorSetup(forceRebuild);
            return;
        }
#endif
        // Awake/OnEnable 직후에는 AddComponent/생성 타이밍 워닝이 발생할 수 있어 한 프레임 뒤로 미룬다.
        _runtimeForceRebuild |= forceRebuild;
        _runtimeSetupQueued = true;

        if (applyRenderOrderNow)
            _pendingRenderOrder = true;
    }

#if UNITY_EDITOR
    private void QueueEditorSetup(bool forceRebuild)
    {
        _editorForceRebuild |= forceRebuild;
        if (_editorSetupQueued)
            return;

        _editorSetupQueued = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            _editorSetupQueued = false;

            if (this == null || !isActiveAndEnabled)
                return;

            bool pendingForceRebuild = _editorForceRebuild;
            _editorForceRebuild = false;
            EnsureSetup(pendingForceRebuild, applyRenderOrderNow: false);
        };
    }
#endif

    private void ApplyQueuedRuntimeSetup()
    {
        if (!_runtimeSetupQueued)
            return;

        bool pendingForceRebuild = _runtimeForceRebuild;
        _runtimeForceRebuild = false;
        _runtimeSetupQueued = false;
        EnsureSetup(pendingForceRebuild, applyRenderOrderNow: false);
    }

    // 원본 TMP 텍스트 참조 캐시
    private bool TryGetSource()
    {
        if (_source == null)
            _source = GetComponent<TextMeshProUGUI>();

        return _source != null;
    }

    // 현재 부모의 외곽 레이어를 수집하고 이름순으로 정렬 -> 정렬 안 하면 텍스트 본체 오더가 안정적으로 맨 앞에 오지 않아서
    private void CollectLayers(Transform parent)
    {
        _layers.Clear();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!child.name.StartsWith(Prefix, StringComparison.Ordinal))
                continue;

            TextMeshProUGUI layer = child.GetComponent<TextMeshProUGUI>();
            if (layer != null)
                _layers.Add(layer);
        }

        _layers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    // 샘플 수에 맞게 외곽 레이어를 새로 생성
    private void RebuildLayers(Transform parent, int count)
    {
        for (int i = _layers.Count - 1; i >= 0; i--)
        {
            TextMeshProUGUI layer = _layers[i];
            if (layer != null)
                SafeDestroy(layer.gameObject);
        }
        _layers.Clear();

        for (int i = 0; i < count; i++)
        {
            GameObject go = new($"{Prefix}{i + 1:00}");
            // 리빌드 중 한 프레임 노출로 흰색 텍스트가 보이지 않게 생성 단계에서는 비활성화
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.layer = gameObject.layer;
            go.hideFlags = BuildGeneratedHideFlags();

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;

            go.AddComponent<CanvasRenderer>();
            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            TextMeshProUGUI layer = go.AddComponent<TextMeshProUGUI>();
            layer.raycastTarget = false;

            _layers.Add(layer);
        }
    }

    // 한 개 외곽 레이어를 원본 텍스트와 동기화
    private void SyncLayer(TextMeshProUGUI layer, Vector2 offset, int order)
    {
        RectTransform srcRect = _source.rectTransform;
        RectTransform dstRect = layer.rectTransform;

        dstRect.anchorMin = srcRect.anchorMin;
        dstRect.anchorMax = srcRect.anchorMax;
        dstRect.pivot = srcRect.pivot;
        dstRect.sizeDelta = srcRect.sizeDelta;
        dstRect.anchoredPosition = srcRect.anchoredPosition + offset;
        dstRect.localRotation = srcRect.localRotation;
        dstRect.localScale = srcRect.localScale;

        layer.name = $"{Prefix}{order:00}";
        layer.gameObject.hideFlags = BuildGeneratedHideFlags();
        layer.text = _source.text;
        layer.isRightToLeftText = _source.isRightToLeftText;
        layer.font = _source.font;
        layer.fontSharedMaterial = _source.fontSharedMaterial;
        layer.fontStyle = _source.fontStyle;
        layer.fontWeight = _source.fontWeight;
        layer.fontSize = _source.fontSize;
        layer.enableAutoSizing = _source.enableAutoSizing;
        layer.fontSizeMin = _source.fontSizeMin;
        layer.fontSizeMax = _source.fontSizeMax;
        layer.alignment = _source.alignment;
        // layer.textWrappingMode = _source.textWrappingMode;
        layer.overflowMode = _source.overflowMode;
        layer.characterSpacing = _source.characterSpacing;
        layer.wordSpacing = _source.wordSpacing;
        layer.lineSpacing = _source.lineSpacing;
        layer.paragraphSpacing = _source.paragraphSpacing;
        layer.margin = _source.margin;
        layer.richText = _source.richText;
        layer.extraPadding = _source.extraPadding;
        layer.maskable = _source.maskable;
        layer.raycastTarget = false;
        layer.color = new Color(_strokeColor.r, _strokeColor.g, _strokeColor.b, _source.color.a);

        LayoutElement layoutElement = layer.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = layer.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        if (!layer.gameObject.activeSelf)
            layer.gameObject.SetActive(true);
    }

    // 예약된 렌더 순서 갱신을 안전한 프레임에서만 실행
    private void ApplyPendingRenderOrder()
    {
        if (!_pendingRenderOrder || _source == null)
            return;

        ApplyRenderOrder();
        _pendingRenderOrder = false;
    }

    // 외곽 레이어를 아래쪽에, 원본 텍스트를 가장 위에 고정
    private void ApplyRenderOrder()
    {
        if (_source == null)
            return;

        int blockStart = _source.transform.GetSiblingIndex();
        for (int i = 0; i < _layers.Count; i++)
        {
            TextMeshProUGUI layer = _layers[i];
            if (layer == null)
                continue;

            blockStart = Mathf.Min(blockStart, layer.transform.GetSiblingIndex());
        }

        int order = 0;
        for (int i = 0; i < _layers.Count; i++)
        {
            TextMeshProUGUI layer = _layers[i];
            if (layer == null)
                continue;

            int targetIndex = blockStart + order;
            if (layer.transform.GetSiblingIndex() != targetIndex)
                layer.transform.SetSiblingIndex(targetIndex);
            order++;
        }

        int sourceIndex = blockStart + order;
        if (_source.transform.GetSiblingIndex() != sourceIndex)
            _source.transform.SetSiblingIndex(sourceIndex);
    }

    // 이 컴포넌트가 만든 외곽 레이어를 모두 삭제
    private void CleanupGeneratedLayers()
    {
        if (string.IsNullOrEmpty(_layerKey))
            return;

        Transform parent = transform.parent;
        if (_source != null && _source.transform.parent != null)
            parent = _source.transform.parent;

        if (parent == null)
            return;

        List<GameObject> toDelete = new();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith(Prefix, StringComparison.Ordinal))
                toDelete.Add(child.gameObject);
        }

        for (int i = 0; i < toDelete.Count; i++)
            SafeDestroy(toDelete[i]);
    }

    private void CleanupOrphanedLayers(Transform currentParent)
    {
        if (string.IsNullOrEmpty(_layerKey))
            return;

        Transform searchRoot = currentParent.root;
        TextMeshProUGUI[] allTexts = searchRoot.GetComponentsInChildren<TextMeshProUGUI>(true);

        for (int i = 0; i < allTexts.Length; i++)
        {
            TextMeshProUGUI layer = allTexts[i];
            if (layer == null || layer == _source)
                continue;

            if (!layer.name.StartsWith(Prefix, StringComparison.Ordinal))
                continue;

            if (layer.transform.parent != currentParent)
                SafeDestroy(layer.gameObject);
        }
    }

    // 구버전 prefix("__TMPStroke_{key}_")로 남아있는 레이어를 현재 부모에서 정리
    private void CleanupLegacyLayers(Transform currentParent)
    {
        if (string.IsNullOrEmpty(_layerKey))
            return;

        List<GameObject> toDelete = new();
        for (int i = 0; i < currentParent.childCount; i++)
        {
            Transform child = currentParent.GetChild(i);
            if (!child.name.StartsWith(LegacyPrefix, StringComparison.Ordinal))
                continue;

            if (child.name.StartsWith(Prefix, StringComparison.Ordinal))
                continue;

            toDelete.Add(child.gameObject);
        }

        for (int i = 0; i < toDelete.Count; i++)
            SafeDestroy(toDelete[i]);
    }

    private HideFlags BuildGeneratedHideFlags()
    {
        HideFlags flags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        if (_hideGeneratedInHierarchy)
            flags |= HideFlags.HideInHierarchy;

        return flags;
    }

    // 원형 분포의 오프셋 좌표를 생성
    private static List<Vector2> BuildOffsets(float radius, int count)
    {
        List<Vector2> offsets = new(count);
        float step = 2f * Mathf.PI / count;
        for (int i = 0; i < count; i++)
        {
            float angle = step * i;
            offsets.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        return offsets;
    }

    // 에디터/플레이모드 환경에 맞춰 안전하게 삭제
    private static void SafeDestroy(UnityEngine.Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (target != null)
                {
                    // 프리팹 자산 컨텍스트에서 생성된 오브젝트는 allowDestroyingAssets=true가 필요
                    bool isPersistentAsset = UnityEditor.EditorUtility.IsPersistent(target);
                    DestroyImmediate(target, isPersistentAsset);
                }
            };
            return;
        }
#endif

        Destroy(target);
    }

    private static bool IsPersistentObject(UnityEngine.Object target)
    {
#if UNITY_EDITOR
        return target != null && UnityEditor.EditorUtility.IsPersistent(target);
#else
        return false;
#endif
    }
}
