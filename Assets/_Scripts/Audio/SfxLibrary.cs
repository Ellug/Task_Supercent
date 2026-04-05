using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct SfxEntry
{
    [SerializeField] private int _id;
    [SerializeField] private AudioClip _clip;
    [SerializeField, Range(0f, 1f)] private float _volume;

    public readonly int Id => _id;
    public AudioClip Clip => _clip;
    public readonly float Volume => _volume > 0f ? _volume : 1f;
}

// int ID 기반 SFX 클립 매핑 ScriptableObject
[CreateAssetMenu(menuName = "Game/Audio/SFX Library", fileName = "SfxLibrary")]
public class SfxLibrary : ScriptableObject
{
    [SerializeField] private List<SfxEntry> _entries = new();

    private Dictionary<int, SfxEntry> _map;

    public bool TryGet(int id, out SfxEntry entry)
    {
        if (_map == null)
            BuildMap();

        return _map.TryGetValue(id, out entry);
    }

    private void BuildMap()
    {
        _map = new Dictionary<int, SfxEntry>(_entries.Count);
        for (int i = 0; i < _entries.Count; i++)
        {
            SfxEntry e = _entries[i];
            if (e.Clip != null)
                _map[e.Id] = e;
        }
    }

    void OnValidate()
    {
        _map = null;
    }
}
