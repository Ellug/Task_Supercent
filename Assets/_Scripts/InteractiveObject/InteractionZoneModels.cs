using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum InteractionZoneType
{
    PurchaseEquip = 0,
    SubmitResource = 1,
    CollectResource = 2,
}

public enum InteractionZoneTransitionOperation
{
    SetZoneEnabled = 0,
    SetGameObjectActive = 1,
    SetCompleted = 2,
    ChangeType = 3,
    ApplyLibrary = 4,
}

// 인터랙션 존 완료 시 다른 존에 적용할 단일 트랜지션 명령
[Serializable]
public struct InteractionZoneTransition
{
    [SerializeField] private InteractionZone _target;
    [SerializeField] private InteractionZoneTransitionOperation _operation;
    [SerializeField] private bool _boolValue;
    [SerializeField] private InteractionZoneType _typeValue;
    [FormerlySerializedAs("_dataValue")]
    [FormerlySerializedAs("_definitionValue")]
    [SerializeField] private InteractionZoneLibrary _libraryValue;

    public InteractionZone Target => _target;
    public InteractionZoneTransitionOperation Operation => _operation;
    public bool BoolValue => _boolValue;
    public InteractionZoneType TypeValue => _typeValue;
    public InteractionZoneLibrary LibraryValue => _libraryValue;
}

