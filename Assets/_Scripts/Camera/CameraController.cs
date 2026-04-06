using System;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private PlayerModel _playerModel;
    [SerializeField] private float _xRot = 60;
    [SerializeField, Min(0f)] private float _distance = 25f;

    void Awake()
    {
        if (_playerModel == null)
            throw new InvalidOperationException("[CameraController] _playerModel is required.");
    }

    // xRot/distance로 카메라 오프셋 계산 후 플레이어 추적
    void LateUpdate()
    {
        if (!TryGetFollowPose(out Vector3 position, out Quaternion rotation))
            return;

        transform.SetPositionAndRotation(position, rotation);
    }

    // 현재 플레이어 위치 기준으로 카메라가 따라가야 할 목표 포즈를 계산
    public bool TryGetFollowPose(out Vector3 position, out Quaternion rotation)
    {
        if (_playerModel == null)
        {
            position = transform.position;
            rotation = transform.rotation;
            return false;
        }

        float xRad = _xRot * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(xRad) * _distance;
        float verticalDistance = Mathf.Sin(xRad) * _distance;

        Vector3 offset = Vector3.back * horizontalDistance;
        offset.y = verticalDistance;

        Vector3 playerPosition = _playerModel.transform.position;
        position = playerPosition + offset;

        Vector3 forward = playerPosition - position;
        rotation = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up)
            : transform.rotation;

        return true;
    }
}
