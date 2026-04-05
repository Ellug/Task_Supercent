using System.Collections;
using UnityEngine;

// 생산된 Cuff 뷰를 spawnPoint → pathway → endpoint 순서로 이동시키고
// endpoint 도달 시 콜백으로 CollectZone 적재를 트리거하는 레일 런타임
public sealed class CuffRailRuntime
{
    private readonly MonoBehaviour _runner;       // 코루틴 실행 주체
    private readonly Transform _spawnPoint;
    private readonly Transform _pathway;
    private readonly Transform _endpoint;
    private readonly ResourceData _resource;
    private readonly float _moveSpeed;

    public CuffRailRuntime(
        MonoBehaviour runner,
        Transform spawnPoint,
        Transform pathway,
        Transform endpoint,
        ResourceData resource,
        float moveSpeed)
    {
        _runner    = runner;
        _spawnPoint = spawnPoint;
        _pathway   = pathway;
        _endpoint  = endpoint;
        _resource  = resource;
        _moveSpeed = Mathf.Max(0.1f, moveSpeed);
    }

    // Cuff 1개를 레일에 올림. endpoint 도달 시 onArrived 호출
    public void Launch(System.Action onArrived)
    {
        if (_resource == null || _resource.WorldViewPrefab == null)
        {
            onArrived?.Invoke();
            return;
        }

        GameObject view = PooledViewBridge.Spawn(
            _resource.WorldViewPrefab,
            _spawnPoint.position,
            _spawnPoint.rotation,
            null,
            true);

        _runner.StartCoroutine(MoveAlongRail(view, onArrived));
    }

    private IEnumerator MoveAlongRail(GameObject view, System.Action onArrived)
    {
        // spawnPoint → pathway
        yield return MoveToTarget(view, _pathway);

        // pathway → endpoint
        yield return MoveToTarget(view, _endpoint);

        // 도달 — 뷰 반납 후 콜백
        PooledViewBridge.Release(view);
        onArrived?.Invoke();
    }

    private IEnumerator MoveToTarget(GameObject view, Transform target)
    {
        if (view == null || target == null)
            yield break;

        while (view != null)
        {
            view.transform.position = Vector3.MoveTowards(
                view.transform.position,
                target.position,
                _moveSpeed * Time.deltaTime);

            if (Vector3.Distance(view.transform.position, target.position) < 0.01f)
                break;

            yield return null;
        }
    }
}
