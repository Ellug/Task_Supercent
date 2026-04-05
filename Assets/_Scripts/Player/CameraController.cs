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
        if (_playerModel == null) return;

        // xRot/distance 기반 오프셋 역산
        float xRad = _xRot * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(xRad) * _distance;
        float verticalDistance = Mathf.Sin(xRad) * _distance;

        Vector3 offset = Vector3.back * horizontalDistance;
        offset.y = verticalDistance;

        // 타겟 팔로우 및 LookAt
        transform.position = _playerModel.transform.position + offset;
        transform.LookAt(_playerModel.transform.position);
    }
}
