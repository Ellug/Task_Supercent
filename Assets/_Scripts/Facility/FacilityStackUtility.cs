using System.Collections.Generic;
using UnityEngine;

// 시설 스택 시각화 공통 유틸
public static class FacilityStackUtility
{
    // null이 된 뷰 항목을 리스트에서 제거
    public static void CleanupMissing(List<GameObject> views)
    {
        for (int i = views.Count - 1; i >= 0; i--)
        {
            if (views[i] == null)
                views.RemoveAt(i);
        }
    }

    // columns 기반 열/층 적층 좌표 계산
    // useTopY가 true면 root의 Collider/Renderer 최상단 Y를 기준으로 사용
    public static Vector3 GetColumnLayerWorldPosition(
        Transform root,
        int index,
        int columns,
        float columnSpacing,
        float layerSpacing,
        Vector3 localOffset,
        bool useTopY)
    {
        if (root == null)
            return Vector3.zero;

        int safeColumns = Mathf.Max(1, columns);
        int column = index % safeColumns;
        int layer = index / safeColumns;

        float centeredColumn = column - ((safeColumns - 1) * 0.5f);
        float xOffset = centeredColumn * Mathf.Max(0f, columnSpacing);
        float yOffset = layer * Mathf.Max(0f, layerSpacing);

        Vector3 axisX = GetHorizontalAxis(root.right, Vector3.right);
        Vector3 axisZ = GetHorizontalAxis(root.forward, Vector3.forward);
        Vector3 basePosition = root.position;

        if (useTopY && TryGetTopY(root, out float topY))
            basePosition.y = topY;

        basePosition += axisX * localOffset.x;
        basePosition += axisZ * localOffset.z;
        basePosition += Vector3.up * localOffset.y;

        return basePosition + (axisX * xOffset) + (Vector3.up * yOffset);
    }

    // rows*columns 셀 + layer 적층 좌표 계산 (spacing 기준 폴백용)
    public static Vector3 GetGridLayerWorldPosition(
        Transform root,
        int index,
        int columns,
        int rows,
        float columnSpacing,
        float rowSpacing,
        float layerSpacing,
        Vector3 localOffset)
    {
        if (root == null)
            return Vector3.zero;

        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);
        int cellsPerLayer = safeColumns * safeRows;

        int layer = index / cellsPerLayer;
        int gridIndex = index % cellsPerLayer;
        int column = gridIndex % safeColumns;
        int row = gridIndex / safeColumns;

        float centeredColumn = column - ((safeColumns - 1) * 0.5f);
        float centeredRow = row - ((safeRows - 1) * 0.5f);

        Vector3 axisX = GetHorizontalAxis(root.right, Vector3.right);
        Vector3 axisZ = GetHorizontalAxis(root.forward, Vector3.forward);

        Vector3 position = root.position;
        position += axisX * localOffset.x;
        position += axisZ * localOffset.z;
        position += Vector3.up * localOffset.y;
        position += axisX * (centeredColumn * Mathf.Max(0f, columnSpacing));
        position += axisZ * (centeredRow * Mathf.Max(0f, rowSpacing));
        position += Vector3.up * (layer * Mathf.Max(0f, layerSpacing));
        return position;
    }

    // BoxCollider 영역을 columns*rows로 분할했을 때, index 셀 중심(+layer)를 월드 좌표로 반환
    public static bool TryGetAreaGridLayerWorldPosition(
        BoxCollider collider,
        int index,
        int columns,
        int rows,
        float layerSpacing,
        Vector3 localOffset,
        out Vector3 position)
    {
        position = default;
        if (!TryGetAreaGridBasis(collider, out Vector3 center, out Vector3 columnAxis, out Vector3 rowAxis, out float halfColumn, out float halfRow))
            return false;

        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);
        int cellsPerLayer = safeColumns * safeRows;

        int layer = index / cellsPerLayer;
        int gridIndex = index % cellsPerLayer;
        int column = gridIndex % safeColumns;
        int row = gridIndex / safeColumns;

        float colSize = (halfColumn * 2f) / safeColumns;
        float rowSize = (halfRow * 2f) / safeRows;

        float colOffset = (-halfColumn + (colSize * 0.5f)) + (column * colSize);
        float rowOffset = (-halfRow + (rowSize * 0.5f)) + (row * rowSize);
        float yOffset = localOffset.y + (layer * Mathf.Max(0f, layerSpacing));

        position = center;
        position += columnAxis * (colOffset + localOffset.x);
        position += rowAxis * (rowOffset + localOffset.z);
        position += Vector3.up * yOffset;
        return true;
    }

    // 수평 축 벡터 계산
    public static Vector3 GetHorizontalAxis(Vector3 sourceAxis, Vector3 fallback)
    {
        Vector3 axis = sourceAxis;
        axis.y = 0f;

        if (axis.sqrMagnitude < 0.0001f)
            axis = fallback;

        return axis.normalized;
    }

    private static bool TryGetAreaGridBasis(
        BoxCollider collider,
        out Vector3 center,
        out Vector3 columnAxis,
        out Vector3 rowAxis,
        out float halfColumn,
        out float halfRow)
    {
        center = default;
        columnAxis = default;
        rowAxis = default;
        halfColumn = 0f;
        halfRow = 0f;

        if (collider == null)
            return false;

        Transform zoneTransform = collider.transform;
        center = zoneTransform.TransformPoint(collider.center);

        float sx = Mathf.Abs(collider.size.x * zoneTransform.lossyScale.x) * 0.5f;
        float sy = Mathf.Abs(collider.size.y * zoneTransform.lossyScale.y) * 0.5f;
        float sz = Mathf.Abs(collider.size.z * zoneTransform.lossyScale.z) * 0.5f;

        columnAxis = GetHorizontalAxis(zoneTransform.right, Vector3.right);
        halfColumn = sx;

        rowAxis = GetHorizontalAxis(zoneTransform.up, Vector3.forward);
        halfRow = sy;

        if (rowAxis.sqrMagnitude < 0.0001f || Mathf.Abs(Vector3.Dot(columnAxis, rowAxis)) > 0.95f)
        {
            rowAxis = GetHorizontalAxis(zoneTransform.forward, Vector3.forward);
            halfRow = sz;
        }

        if (rowAxis.sqrMagnitude < 0.0001f || Mathf.Abs(Vector3.Dot(columnAxis, rowAxis)) > 0.95f)
            return false;

        return halfColumn > 0.0001f && halfRow > 0.0001f;
    }

    // root 하위 활성 Collider → Renderer 순으로 최상단 Y 탐색
    private static bool TryGetTopY(Transform root, out float topY)
    {
        topY = root.position.y;
        bool found = false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
                continue;

            if (!found || collider.bounds.max.y > topY)
                topY = collider.bounds.max.y;

            found = true;
        }

        if (found)
            return true;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!found || renderer.bounds.max.y > topY)
                topY = renderer.bounds.max.y;

            found = true;
        }

        return found;
    }
}
