using System.Collections.Generic;
using UnityEngine;

// Zone 완료 시 구매 업그레이드 단계 갱신
public sealed class ZonePurchaseUpgradeService
{
    private readonly Dictionary<InteractionZoneId, List<int>> _ruleIndicesByTrigger = new();
    private readonly List<InteractionZonePurchaseUpgradeData> _rules = new();
    private readonly HashSet<int> _appliedRuleIndices = new();

    private ZoneRegistry _zoneRegistry;
    private EquipLevelLibrary _equipLevelLibrary;

    // PurchaseUpgrades 규칙 초기화
    public void Initialize(
        IReadOnlyList<InteractionZonePurchaseUpgradeData> purchaseRules,
        ZoneRegistry zoneRegistry,
        EquipLevelLibrary equipLevelLibrary)
    {
        _zoneRegistry = zoneRegistry;
        _equipLevelLibrary = equipLevelLibrary;

        Clear();

        if (purchaseRules == null || purchaseRules.Count == 0)
            return;

        for (int i = 0; i < purchaseRules.Count; i++)
        {
            InteractionZonePurchaseUpgradeData rule = purchaseRules[i];
            _rules.Add(rule);

            if (!_ruleIndicesByTrigger.TryGetValue(rule.TriggerZoneId, out List<int> indices))
            {
                indices = new List<int>();
                _ruleIndicesByTrigger.Add(rule.TriggerZoneId, indices);
            }

            indices.Add(i);
        }
    }

    // triggerZoneId에 연결된 구매 업그레이드 규칙 적용
    public void ApplyForTrigger(InteractionZoneId triggerZoneId)
    {
        if (!_ruleIndicesByTrigger.TryGetValue(triggerZoneId, out List<int> ruleIndices))
            return;

        for (int i = 0; i < ruleIndices.Count; i++)
        {
            int ruleIndex = ruleIndices[i];
            if (_appliedRuleIndices.Contains(ruleIndex))
                continue;

            InteractionZonePurchaseUpgradeData rule = _rules[ruleIndex];
            if (!_zoneRegistry.TryGetZone(rule.TargetZoneId, out InteractionZone targetZone))
                continue;

            EquipData equip = ResolveEquip(rule.EquipId);
            if (equip == null)
            {
                Debug.LogWarning($"[ZonePurchaseUpgradeService] Equip not found for id: {rule.EquipId}");
                continue;
            }

            targetZone.ConfigurePurchaseStep(
                targetZone.Resource,
                targetZone.AmountPerTick,
                rule.RequiredAmount,
                equip,
                rule.CompleteOnce,
                rule.ZoneEnabledAfterUpgrade,
                true);

            _appliedRuleIndices.Add(ruleIndex);
        }
    }

    // 런타임 데이터 초기화
    public void Clear()
    {
        _ruleIndicesByTrigger.Clear();
        _rules.Clear();
        _appliedRuleIndices.Clear();
    }

    // EquipLevelLibrary에서 equipId로 EquipData 조회
    private EquipData ResolveEquip(string equipId)
    {
        if (string.IsNullOrEmpty(equipId))
            return null;

        if (_equipLevelLibrary != null)
        {
            EquipData fromLibrary = _equipLevelLibrary.GetById(equipId);
            if (fromLibrary != null)
                return fromLibrary;
        }

        EquipBase[] equipBases = Object.FindObjectsByType<EquipBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < equipBases.Length; i++)
        {
            EquipBase equipBase = equipBases[i];
            if (equipBase == null)
                continue;

            EquipData equip = equipBase.GetDataById(equipId);
            if (equip != null)
                return equip;
        }

        return null;
    }
}
