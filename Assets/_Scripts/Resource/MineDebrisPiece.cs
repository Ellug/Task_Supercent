using UnityEngine;

// 광산 파편 1개 단위 연출 — 초기 속도/중력/수명으로 이동 후 풀 반환
public class MineDebrisPiece : MonoBehaviour, IPoolable
{
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private Vector2 _spinSpeedRange = new(180f, 540f);

    private Vector3 _velocity;
    private Vector3 _spinVelocity;
    private float _gravity;
    private float _despawnTime;
    private bool _isPlaying;

    // 파편 이동 파라미터 적용 후 재생 시작
    public void Play(Vector3 velocity, float lifetime, float uniformScale, float gravity)
    {
        _velocity = velocity;
        _gravity = Mathf.Max(0f, gravity);
        _despawnTime = Time.time + Mathf.Max(0.05f, lifetime);
        _spinVelocity = Random.onUnitSphere * Random.Range(
            Mathf.Min(_spinSpeedRange.x, _spinSpeedRange.y),
            Mathf.Max(_spinSpeedRange.x, _spinSpeedRange.y));
        _isPlaying = true;

        Transform target = _visualRoot != null ? _visualRoot : transform;
        target.localScale = Vector3.one * Mathf.Max(0.05f, uniformScale);
    }

    void Update()
    {
        if (!_isPlaying)
            return;

        float deltaTime = Time.deltaTime;
        _velocity += Vector3.down * (_gravity * deltaTime);
        transform.position += _velocity * deltaTime;
        transform.Rotate(_spinVelocity * deltaTime, Space.Self);

        if (Time.time < _despawnTime)
            return;

        _isPlaying = false;
        PooledViewBridge.Release(gameObject);
    }

    public void OnSpawned()
    {
        _isPlaying = false;
        _velocity = Vector3.zero;
        _spinVelocity = Vector3.zero;
    }

    public void OnDespawned()
    {
        _isPlaying = false;
        _velocity = Vector3.zero;
        _spinVelocity = Vector3.zero;
    }
}
