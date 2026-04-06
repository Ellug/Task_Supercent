using UnityEngine;

// BoxCollider 영역을 rows x cols 슬롯으로 분할해 NPC 위치를 배정
// - 슬롯은 Z축 방향으로 먼저 채워지고, 오브젝트는 +Z를 향해 정렬
// - _fillReverse: true면 마지막 슬롯부터 역순으로 배정 (Jail용)
[DisallowMultipleComponent]
public class NpcGridArea : MonoBehaviour
{
    [SerializeField] private BoxCollider _box;
    [SerializeField, Min(1)] private int _rows = 1;   // X축 방향 줄 수
    [SerializeField, Min(1)] private int _cols = 1;   // Z축 방향 줄 수
    [SerializeField] private bool _fillReverse;

    private bool[] _occupied;

    public int Capacity => _rows * _cols;

    void Awake()
    {
        _occupied = new bool[Capacity];
    }

    // 빈 슬롯을 찾아 점유하고 월드 위치·회전 반환, 없으면 false
    public bool ClaimSlot(out int slotIndex, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        int cap = _occupied.Length;
        for (int i = 0; i < cap; i++)
        {
            int idx = _fillReverse ? (cap - 1 - i) : i;
            if (_occupied[idx])
                continue;

            _occupied[idx] = true;
            slotIndex = idx;
            worldPosition = SlotWorldPosition(idx);
            // 오브젝트가 그리드 오브젝트의 +Z를 바라보도록
            worldRotation = transform.rotation;
            return true;
        }

        slotIndex = -1;
        worldPosition = transform.position;
        worldRotation = transform.rotation;
        return false;
    }

    // 슬롯 점유 해제
    public void ReleaseSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _occupied.Length)
            return;

        _occupied[slotIndex] = false;
    }

    // col → Z축, row → X축으로 배치 (Z방향 먼저 채움)
    public Vector3 SlotWorldPosition(int slotIndex)
    {
        int row = slotIndex / _cols;  // X축 인덱스
        int col = slotIndex % _cols;  // Z축 인덱스

        Vector3 center = _box.center;
        Vector3 size = _box.size;

        float xStep = _rows > 1 ? size.x / (_rows - 1) : 0f;
        float zStep = _cols > 1 ? size.z / (_cols - 1) : 0f;

        float localX = center.x - size.x * 0.5f + row * xStep;
        float localY = center.y - size.y * 0.5f;
        float localZ = center.z - size.z * 0.5f + col * zStep;

        return transform.TransformPoint(new Vector3(localX, localY, localZ));
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_box == null)
            return;

        Gizmos.color = Color.cyan;
        int cap = _rows * _cols;
        for (int i = 0; i < cap; i++)
            Gizmos.DrawWireSphere(SlotWorldPosition(i), 0.15f);
    }
#endif
}
