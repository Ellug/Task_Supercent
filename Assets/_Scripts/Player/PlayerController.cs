using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerModel), typeof(PlayerView), typeof(EquipBase))]
[RequireComponent(typeof(ResourceStack), typeof(PlayerCarryVisualizer))]
public class PlayerController : MonoBehaviour, IInteractionActor
{
    private const string PlayerMapName = "Player";
    private const string MoveActionName = "Move";

    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("References")]
    [SerializeField] private PlayerModel _model;
    [SerializeField] private PlayerView _view;
    [SerializeField] private EquipBase _equip;
    [SerializeField] private ResourceStack _resourceStack;
    [SerializeField] private PlayerCarryVisualizer _carryVisualizer;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private FloatingJoystickInput _floatingJoystickInput;

    [Header("Mine")]
    [SerializeField, Range(-1f, 1f)] private float _mineForwardDotMin = 0f;
    [SerializeField, Min(0f)] private float _mineRangeVisualHoldDuration = 1f;
    [SerializeField] private MineArea _mineArea;
    [SerializeField, Min(0f)] private float _mineAreaExitDistance = 2f;

    [Header("Interaction Transfer")]
    [SerializeField, Min(0f)] private float _transferTickInterval = 0.01f;
    [SerializeField, Min(1)] private int _transferAmountPerTick = 1;
    [SerializeField, Min(0f)] private float _maxPopupInterval = 2f;

    private InputAction _moveAction;
    private bool _isInMaxLoop;
    private float _nextMaxPopupTime;
    private Vector2 _lastMoveInput;
    private float _mineRangeVisualOffTime = -1f;
    private BoxCollider _mineAreaCollider;
    private readonly Dictionary<ResourceData, int> _lastCarryCountByResource = new();
    private int _carryCapacityBonus;

    public EquipBase Equip => _equip;
    public ResourceStack CarryStack => _resourceStack;
    public PlayerCarryVisualizer CarryVisualizer => _carryVisualizer;
    public Vector2 LastMoveInput => _lastMoveInput;
    public float SubmitTickInterval => Mathf.Max(0f, _transferTickInterval);
    public int SubmitAmountPerTick => Mathf.Max(1, _transferAmountPerTick);
    public float CollectTickInterval => Mathf.Max(0f, _transferTickInterval);
    public int CollectAmountPerTick => Mathf.Max(1, _transferAmountPerTick);

    void Awake()
    {
        if (_model == null) _model = GetComponent<PlayerModel>();
        if (_view == null) _view = GetComponent<PlayerView>();
        if (_equip == null) _equip = GetComponent<EquipBase>();
        if (_resourceStack == null) _resourceStack = GetComponent<ResourceStack>();
        if (_carryVisualizer == null) _carryVisualizer = GetComponent<PlayerCarryVisualizer>();
        if (_resourceManager == null) throw new InvalidOperationException("[PlayerController] _resourceManager is required.");

        BindActions();
    }

    void OnEnable()
    {
        ToggleMoveAction(true);
        _resourceStack.Changed += OnCarryStackChanged;
        _equip.LevelChanged += OnEquipLevelChanged;
    }

    void OnDisable()
    {
        ToggleMoveAction(false);
        _resourceStack.Changed -= OnCarryStackChanged;
        _equip.LevelChanged -= OnEquipLevelChanged;
        _view.StopMove();
        _lastMoveInput = Vector2.zero;
    }

    void FixedUpdate()
    {
        Vector2 moveInput = ReadMoveInput();
        // 입력 정규화 후 이동 적용
        Vector2 finalMoveInput = _model.ComposeMoveInput(moveInput);
        _lastMoveInput = finalMoveInput;

        _view.ApplyMove(finalMoveInput, _model.MoveSpeed);
    }

    // 드래그 조이스틱 입력 우선, 없으면 기존 InputAction 입력 사용
    private Vector2 ReadMoveInput()
    {
        if (_floatingJoystickInput != null && _floatingJoystickInput.IsDragging)
            return _floatingJoystickInput.MoveInput;

        if (_moveAction == null)
            return Vector2.zero;

        return _moveAction.ReadValue<Vector2>();
    }

    // 이동 입력 없음 + 실제 속도 임계값 이하일 때 인터랙션 가능 상태로 판단
    public bool IsInteractionReady(float stopSpeedThreshold = 0.05f)
    {
        if (_lastMoveInput.sqrMagnitude > 0.0001f)
            return false;

        return _view.PlanarSpeed <= stopSpeedThreshold;
    }

    public bool TryAcquireEquip(EquipData equip)
    {
        return _equip.TryAcquire(equip);
    }

    public bool HasEquipOrBetter(EquipData equip)
    {
        return _equip.HasEquipOrBetter(equip);
    }

    void Update()
    {
        TryAutoMineForwardMine();
    }

    private void BindActions()
    {
        // Player/Move 액션 조회
        InputActionAsset actionAsset = _inputActions != null ? _inputActions : InputSystem.actions;
        if (actionAsset == null)
            return;

        InputActionMap playerMap = actionAsset.FindActionMap(PlayerMapName, false);
        if (playerMap == null)
            return;

        _moveAction = playerMap.FindAction(MoveActionName, false);
    }

    private void ToggleMoveAction(bool enabled)
    {
        if (_moveAction == null)
            return;

        if (enabled)
            _moveAction.Enable();
        else
            _moveAction.Disable();
    }

    // 전방 광산을 자동 채굴하고 성공 시 채굴 산출 자원을 적재
    private void TryAutoMineForwardMine()
    {
        EquipData currentEquip = _equip.CurrentEquip;

        if (currentEquip == null)
        {
            _equip.SetMiningRangeVisual(false);
            return;
        }

        bool hasMineInRange = _resourceManager.TryGetMineInFront(transform.position, transform.forward, currentEquip.MineRange, _mineForwardDotMin, out Mine mine);

        if (hasMineInRange)
        {
            _mineRangeVisualOffTime = -1f;
            _equip.SetMiningRangeVisual(true);
        }
        else
        {
            // Mine Area 밖으로 일정 거리 이상 벗어났으면 즉시 off
            if (IsOutsideMineArea(_mineAreaExitDistance))
            {
                _mineRangeVisualOffTime = -1f;
                _equip.SetMiningRangeVisual(false);
            }
            else
            {
                if (_mineRangeVisualOffTime < 0f)
                    _mineRangeVisualOffTime = Time.time + _mineRangeVisualHoldDuration;

                if (Time.time >= _mineRangeVisualOffTime)
                    _equip.SetMiningRangeVisual(false);
            }
        }

        if (!hasMineInRange) return;

        ResourceData predictedResource = mine.YieldResource;
        bool isFull = predictedResource != null && _resourceStack.GetRemaining(predictedResource) <= 0;

        if (isFull)
        {
            if (!_isInMaxLoop || Time.time >= _nextMaxPopupTime)
            {
                MaxPopupUI.Instance.ShowAt(transform);
                _nextMaxPopupTime = Time.time + Mathf.Max(0f, _maxPopupInterval);
            }

            _isInMaxLoop = true;

            // 곡괭이는 MAX면 채굴 중단, 드릴/불도저는 채굴(파괴)은 수행
            if (_equip.IsPickaxe)
                return;
        }
        else
        {
            _isInMaxLoop = false;
        }

        if (!_equip.TryMine(mine, out ResourceData yieldResource, out int yieldAmount, out bool depleted))
            return;

        if (!depleted || yieldResource == null || yieldAmount <= 0)
            return;

        // MAX 상태면 Stack 적재 생략
        if (isFull)
            return;

        if (!TryAddCarriedResource(yieldResource, yieldAmount, out int added))
            return;

        _carryVisualizer.PlayIncomingTransfer(yieldResource, mine.transform.position, added);
    }

    // 채굴 산출 자원을 스택에 적재
    private bool TryAddCarriedResource(ResourceData resource, int amount, out int added)
    {
        return _resourceStack.TryAdd(resource, amount, out added) && added > 0;
    }

    // Mine Area BoxCollider 경계에서 exitDistance 이상 벗어났는지 확인
    private bool IsOutsideMineArea(float exitDistance)
    {
        if (_mineArea == null)
            return false;

        if (_mineAreaCollider == null)
            _mineAreaCollider = _mineArea.GetComponent<BoxCollider>();

        if (_mineAreaCollider == null)
            return false;

        Vector3 closest = _mineAreaCollider.ClosestPoint(transform.position);
        return (transform.position - closest).sqrMagnitude > exitDistance * exitDistance;
    }

    // 장비 레벨 변경 시 누적 CarryCapacityBonus 재계산 후 Ore 슬롯 용량 갱신
    private void OnEquipLevelChanged(int level, EquipData equip)
    {
        int bonus = equip != null ? equip.CarryCapacityBonus : 0;
        if (bonus == _carryCapacityBonus)
            return;

        _carryCapacityBonus = bonus;
        _carryVisualizer.ApplyOreCarryCapacityBonus(_carryCapacityBonus);
    }

    // 플레이어 캐리 스택 증감 시 입출력 SFX 재생
    private void OnCarryStackChanged(ResourceData resource, int newCount, int capacity)
    {
        int previous = _lastCarryCountByResource.TryGetValue(resource, out int value) ? value : 0;
        _lastCarryCountByResource[resource] = newCount;

        if (newCount == previous)
            return;

        if (resource != null && resource.IsMoney && newCount > previous)
        {
            AudioManager.TryPlaySFX(4);
            return;
        }

        AudioManager.TryPlaySFX(0);
    }
}
