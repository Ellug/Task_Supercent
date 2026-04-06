using System.Collections;
using UnityEngine;

// 오브젝트가 활성화될 때 스케일 팝인 애니메이션 재생
// 단계: 0 → peak → undershoot → 1
[DisallowMultipleComponent]
public class PopInScale : MonoBehaviour
{
    [SerializeField] private float _totalDuration = 1f;

    [Tooltip("단계 1 종료 시점 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _peakTime = 0.4f;
    [Tooltip("단계 2 종료 시점 (0~1), peakTime보다 커야 함)")]
    [SerializeField, Range(0f, 1f)] private float _undershootTime = 0.7f;

    [Tooltip("1단계 목표 스케일")]
    [SerializeField] private Vector3 _peakScale = new(0.8f, 1.2f, 1f);
    [Tooltip("2단계 목표 스케일")]
    [SerializeField] private Vector3 _undershootScale = new(1.1f, 0.8f, 1f);
    [Tooltip("3단계 목표 스케일 (최종)")]
    [SerializeField] private Vector3 _finalScale = Vector3.one;

    private Coroutine _routine;

    void OnEnable()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        transform.localScale = Vector3.zero;
        _routine = StartCoroutine(Play());
    }

    void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator Play()
    {
        float duration = Mathf.Max(0.01f, _totalDuration);
        float t1 = Mathf.Clamp01(_peakTime) * duration;
        float t2 = Mathf.Clamp(Mathf.Max(_peakTime, _undershootTime), 0f, 1f) * duration;

        yield return LerpScale(Vector3.zero, _peakScale, t1);
        yield return LerpScale(_peakScale, _undershootScale, t2 - t1);
        yield return LerpScale(_undershootScale, _finalScale, duration - t2);

        transform.localScale = _finalScale;
        _routine = null;
    }

    private IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.LerpUnclamped(from, to, elapsed / duration);
            yield return null;
        }

        transform.localScale = to;
    }
}
