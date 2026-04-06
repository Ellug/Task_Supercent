using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 카메라 연출 실행기
// - Play(): 이동 → holdDuration 후 onArrival 실행 → 복귀
// - 연출 중 새 요청은 큐잉 (_queueShots = true) 또는 무시
// - CameraController를 비활성화해 플레이어 추적을 중단
[DisallowMultipleComponent]
public class CameraDirector : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private CameraController _cameraController;
    [SerializeField] private bool _queueShots = false;

    public bool IsPlaying => _currentCoroutine != null;

    private Coroutine _currentCoroutine;
    private readonly Queue<QueuedShot> _queue = new();

    private sealed class QueuedShot
    {
        public CameraShot Shot;
        public Transform Target;
        public Func<IEnumerator> OnArrival;
    }

    // 연출 요청 — 진행 중이면 큐잉하거나 무시
    public void Play(CameraShot shot, Transform target, Func<IEnumerator> onArrival = null)
    {
        if (shot == null || target == null)
            return;

        if (IsPlaying)
        {
            if (_queueShots)
            {
                _queue.Enqueue(new QueuedShot
                {
                    Shot = shot,
                    Target = target,
                    OnArrival = onArrival,
                });
            }
            return;
        }

        _currentCoroutine = StartCoroutine(RunShot(shot, target, onArrival));
    }

    private IEnumerator RunShot(CameraShot shot, Transform target, Func<IEnumerator> onArrival)
    {
        if (_camera == null)
        {
            _currentCoroutine = null;
            yield break;
        }

        if (_cameraController != null)
            _cameraController.enabled = false;

        Vector3 originPos = _camera.transform.position;
        Quaternion originRot = _camera.transform.rotation;

        // 목적지로 이동
        yield return MoveCamera(originPos, originRot, target.position, target.rotation, shot.travelDuration, shot.curve);

        // 도착 대기
        float elapsed = 0f;
        while (elapsed < shot.holdDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 이벤트 실행 (코루틴 완료까지 대기)
        if (onArrival != null)
        {
            IEnumerator arrivalRoutine = onArrival.Invoke();
            if (arrivalRoutine != null)
                yield return StartCoroutine(arrivalRoutine);
        }

        // 복귀 목적지 — 플레이어 현재 위치 기준 포즈
        Vector3 returnFrom = _camera.transform.position;
        Quaternion returnFromRot = _camera.transform.rotation;

        Vector3 returnTo = originPos;
        Quaternion returnToRot = originRot;
        if (_cameraController != null && _cameraController.TryGetFollowPose(out Vector3 followPos, out Quaternion followRot))
        {
            returnTo = followPos;
            returnToRot = followRot;
        }

        yield return MoveCamera(returnFrom, returnFromRot, returnTo, returnToRot, shot.returnDuration, shot.curve);

        // 추적 재개
        if (_cameraController != null)
            _cameraController.enabled = true;

        _currentCoroutine = null;

        // 큐에 대기 중인 연출 실행
        if (_queue.Count > 0)
        {
            QueuedShot next = _queue.Dequeue();
            _currentCoroutine = StartCoroutine(RunShot(next.Shot, next.Target, next.OnArrival));
        }
    }

    // position/rotation을 duration 동안 보간 이동
    private IEnumerator MoveCamera(
        Vector3 fromPos, Quaternion fromRot,
        Vector3 toPos, Quaternion toRot,
        float duration, AnimationCurve curve)
    {
        if (duration <= 0f)
        {
            _camera.transform.SetPositionAndRotation(toPos, toRot);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            _camera.transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(fromPos, toPos, t),
                Quaternion.SlerpUnclamped(fromRot, toRot, t));
            yield return null;
        }

        _camera.transform.SetPositionAndRotation(toPos, toRot);
    }

    // 연출 즉시 중단 및 카메라 추적 복귀
    public void Stop()
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
            _currentCoroutine = null;
        }

        _queue.Clear();

        if (_cameraController != null)
            _cameraController.enabled = true;
    }
}
