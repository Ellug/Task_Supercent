using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EndingUI : MonoBehaviour
{
    [SerializeField] private GameObject _endingPanel;
    [SerializeField] private GameObject _endingLogo;
    [SerializeField] private GameObject _endingIcon;
    [SerializeField] private GameObject _endingContinue;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _logoDuration = 0.4f;
    [SerializeField, Min(0f)] private float _iconDelay = 0.2f;
    [SerializeField, Min(0f)] private float _iconDuration = 0.35f;
    [SerializeField, Min(0f)] private float _continueDelay = 0.15f;
    [SerializeField, Min(0f)] private float _continueDuration = 0.35f;

    [Header("Bounce")]
    [SerializeField] private float _overshoot = 1.2f;
    [SerializeField] private float _undershoot = 0.92f;

    void Awake()
    {
        // 패널 비활성 상태에서도 자식 오브젝트 초기 스케일 세팅
        if (_endingLogo != null) _endingLogo.transform.localScale = new Vector3(1f, 0f, 1f);
        if (_endingIcon != null) _endingIcon.transform.localScale = Vector3.zero;
        if (_endingContinue != null) _endingContinue.transform.localScale = Vector3.zero;
    }

    public void Show()
    {
        if (_endingPanel != null)
            _endingPanel.SetActive(true);

        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        // Logo: Y축만 0→overshoot→1, X는 고정 1 (아래서 위로 커지는 느낌)
        if (_endingLogo != null)
        {
            _endingLogo.transform.localScale = new Vector3(1f, 0f, 1f);
            yield return StartCoroutine(BounceScale(
                _endingLogo.transform,
                from: new Vector3(1f, 0f, 1f),
                peak: new Vector3(1f, _overshoot, 1f),
                under: new Vector3(1f, _undershoot, 1f),
                to: Vector3.one,
                duration: _logoDuration));
        }

        yield return new WaitForSeconds(_iconDelay);

        // Icon: 0→overshoot→undershoot→1
        if (_endingIcon != null)
        {
            _endingIcon.transform.localScale = Vector3.zero;
            StartCoroutine(BounceScale(
                _endingIcon.transform,
                from: Vector3.zero,
                peak: Vector3.one * _overshoot,
                under: Vector3.one * _undershoot,
                to: Vector3.one,
                duration: _iconDuration));
        }

        yield return new WaitForSeconds(_continueDelay);

        // Continue: 0→overshoot→undershoot→1
        if (_endingContinue != null)
        {
            _endingContinue.transform.localScale = Vector3.zero;
            StartCoroutine(BounceScale(
                _endingContinue.transform,
                from: Vector3.zero,
                peak: Vector3.one * _overshoot,
                under: Vector3.one * _undershoot,
                to: Vector3.one,
                duration: _continueDuration));
        }
    }

    // 0→peak(40%) → peak→under(30%) → under→to(30%)
    private IEnumerator BounceScale(Transform target, Vector3 from, Vector3 peak, Vector3 under, Vector3 to, float duration)
    {
        float t1 = duration * 0.4f;
        float t2 = duration * 0.3f;
        float t3 = duration * 0.3f;

        yield return ScaleTo(target, from, peak, t1);
        yield return ScaleTo(target, peak, under, t2);
        yield return ScaleTo(target, under, to, t3);

        target.localScale = to;
    }

    private IEnumerator ScaleTo(Transform target, Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            target.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.LerpUnclamped(from, to, elapsed / duration);
            yield return null;
        }

        target.localScale = to;
    }

    public void Close()
    {
        _endingPanel.SetActive(false);
    }
}
