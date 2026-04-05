using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CarryBinding
{
    public ResourceData Resource = null;
    public Transform StackRoot = null;
    public int Capacity = 1;
    public float VerticalSpacing = 0f;
    public Vector3 LocalOffset = Vector3.zero;
}

// CarryBinding 설정 초기화, ResourceStack 슬롯 등록 담당
// 바인딩 순서: 0=Ore, 1=Money, 2=Cuff
public class PlayerCarryConfig
{
    public ResourceData OreResource { get; private set; }
    public ResourceData MoneyResource { get; private set; }

    private readonly Dictionary<ResourceData, CarryBinding> _bindingByResource = new();

    public IReadOnlyDictionary<ResourceData, CarryBinding> BindingByResource => _bindingByResource;

    public void Build(IReadOnlyList<CarryBinding> carryBindings, ResourceStack resourceStack)
    {
        _bindingByResource.Clear();

        for (int i = 0; i < carryBindings.Count; i++)
            RegisterBinding(carryBindings[i], resourceStack);

        OreResource   = carryBindings.Count > 0 ? carryBindings[0].Resource : null;
        MoneyResource = carryBindings.Count > 1 ? carryBindings[1].Resource : null;
    }

    // Resource, StackRoot 모두 설정된 항목만 등록하고 ResourceStack에 슬롯 등록
    private void RegisterBinding(CarryBinding source, ResourceStack resourceStack)
    {
        if (source.Resource == null || source.Resource.WorldViewPrefab == null || source.StackRoot == null)
            return;

        _bindingByResource[source.Resource] = source;
        resourceStack.RegisterSlot(source.Resource, Mathf.Max(1, source.Capacity));
    }
}
