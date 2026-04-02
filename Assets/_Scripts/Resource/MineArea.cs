using System.Collections.Generic;
using UnityEngine;

public class MineArea : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField, Min(1)] private int _xCount = 6;
    [SerializeField, Min(1)] private int _yCount = 20;
    [SerializeField, Min(0f)] private float _respawnSeconds = 8f;

    [Header("Area (Local Space)")]
    [SerializeField] private Vector2 _areaSize = new(12f, 40f);
    [SerializeField] private Vector3 _localCenterOffset = Vector3.zero;
    [SerializeField] private BoxCollider _areaCollider;

    public int XCount => Mathf.Max(1, _xCount);
    public int YCount => Mathf.Max(1, _yCount);
    public float RespawnSeconds => Mathf.Max(0f, _respawnSeconds);

    // 셀 좌표가 그리드 범위 안인지 확인
    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < XCount && cell.y >= 0 && cell.y < YCount;
    }

    // 그리드 전체 셀 좌표를 순서대로 열거
    public IEnumerable<Vector2Int> EnumerateAllCells()
    {
        for (int y = 0; y < YCount; y++)
        {
            for (int x = 0; x < XCount; x++)
                yield return new Vector2Int(x, y);
        }
    }

    // 셀 좌표를 월드 중심 위치로 변환
    public Vector3 GetCellCenterWorld(Vector2Int cell)
    {
        if (!IsInside(cell))
            return transform.position;

        Vector2 areaSize = GetAreaSize();
        float cellWidth = areaSize.x / XCount;
        float cellHeight = areaSize.y / YCount;

        float minX = -areaSize.x * 0.5f;
        float minZ = -areaSize.y * 0.5f;

        float centerX = minX + (cellWidth * (cell.x + 0.5f));
        float centerZ = minZ + (cellHeight * (cell.y + 0.5f));

        Vector3 local = GetLocalCenterOffset() + new Vector3(centerX, 0f, centerZ);
        return transform.TransformPoint(local);
    }

    // BoxCollider 또는 직접 설정한 areaSize 반환
    private Vector2 GetAreaSize()
    {
        if (_areaCollider != null)
            return new Vector2(Mathf.Max(0.1f, _areaCollider.size.x), Mathf.Max(0.1f, _areaCollider.size.z));

        return new Vector2(Mathf.Max(0.1f, _areaSize.x), Mathf.Max(0.1f, _areaSize.y));
    }

    // BoxCollider 또는 직접 설정한 중심 오프셋 반환
    private Vector3 GetLocalCenterOffset()
    {
        if (_areaCollider != null)
            return _areaCollider.center;

        return _localCenterOffset;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector2 areaSize = GetAreaSize();
        Vector3 localCenter = GetLocalCenterOffset();

        int xCount = XCount;
        int yCount = YCount;
        if (xCount <= 0 || yCount <= 0)
            return;

        float minX = localCenter.x - (areaSize.x * 0.5f);
        float minZ = localCenter.z - (areaSize.y * 0.5f);
        float localY = localCenter.y;

        Matrix4x4 prevMatrix = Gizmos.matrix;
        Color prevColor = Gizmos.color;

        // 로컬 좌표 기준으로 그리면 영역/분할선이 항상 동일 기준으로 맞는다.
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);

        for (int x = 0; x <= xCount; x++)
        {
            float t = (float)x / xCount;
            float xPos = minX + (areaSize.x * t);
            Vector3 from = new(xPos, localY, minZ);
            Vector3 to = new(xPos, localY, minZ + areaSize.y);
            Gizmos.DrawLine(from, to);
        }

        for (int y = 0; y <= yCount; y++)
        {
            float t = (float)y / yCount;
            float zPos = minZ + (areaSize.y * t);
            Vector3 from = new(minX, localY, zPos);
            Vector3 to = new(minX + areaSize.x, localY, zPos);
            Gizmos.DrawLine(from, to);
        }

        Gizmos.matrix = prevMatrix;
        Gizmos.color = prevColor;
    }
#endif
}
