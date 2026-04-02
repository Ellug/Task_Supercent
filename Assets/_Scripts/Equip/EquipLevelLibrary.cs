using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct EquipLevelEntry
{
    [SerializeField] private int _level;
    [SerializeField] private EquipDefinition _equip;

    public int Level => _level;
    public EquipDefinition Equip => _equip;
}

// 레벨별 EquipDefinition 매핑 ScriptableObject
[CreateAssetMenu(menuName = "Game/Equip Level Library", fileName = "EquipLevelLibrary")]
public class EquipLevelLibrary : ScriptableObject
{
    [SerializeField] private List<EquipLevelEntry> _entries = new();

    public IReadOnlyList<EquipLevelEntry> Entries => _entries;

    // id로 EquipDefinition 검색 — 없으면 null 반환
    public EquipDefinition GetById(string id)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            EquipDefinition equip = _entries[i].Equip;
            if (equip == null || string.IsNullOrEmpty(equip.Id))
                continue;

            if (string.Equals(equip.Id, id, System.StringComparison.Ordinal))
                return equip;
        }

        return null;
    }

    // equip이 등록된 레벨을 out으로 반환 — 없으면 false
    public bool TryGetLevel(EquipDefinition equip, out int level)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Equip != equip)
                continue;

            level = _entries[i].Level;
            return true;
        }

        level = 0;
        return false;
    }

    // level 이하 엔트리 중 가장 높은 레벨의 EquipDefinition 반환
    public EquipDefinition GetEquipForLevel(int level)
    {
        EquipDefinition best = null;
        int bestLevel = int.MinValue;

        for (int i = 0; i < _entries.Count; i++)
        {
            EquipLevelEntry entry = _entries[i];
            if (entry.Equip == null || entry.Level > level)
                continue;

            if (entry.Level >= bestLevel)
            {
                best = entry.Equip;
                bestLevel = entry.Level;
            }
        }

        return best;
    }
}
