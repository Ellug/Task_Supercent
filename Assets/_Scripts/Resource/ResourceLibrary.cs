using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Resource Library", fileName = "ResourceLibrary")]
public class ResourceLibrary : ScriptableObject
{
    [SerializeField] private List<ResourceData> _resources = new();

    public IReadOnlyList<ResourceData> Resources => _resources;

    // id로 ResourceData 검색 — 없으면 null 반환
    public ResourceData GetById(string id)
    {
        for (int i = 0; i < _resources.Count; i++)
        {
            ResourceData resource = _resources[i];
            if (resource == null)
                continue;

            if (string.Equals(resource.Id, id, StringComparison.Ordinal))
                return resource;
        }

        return null;
    }
}

