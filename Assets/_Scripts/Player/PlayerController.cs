using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerModel), typeof(PlayerView), typeof(EquipBase))]
[RequireComponent(typeof(ResourceStack))]
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

    [Header("Carry")]
    [SerializeField] private ResourceData _oreResource;
    [SerializeField] private GameObject _oreStackPrefab;
    [SerializeField] private Transform _oreStackRoot;
    [SerializeField, Min(1)] private int _oreStackCapacity = 30;
    [SerializeField, Min(0f)] private float _oreStackSpacing = 0.35f;
    [SerializeField] private Vector3 _oreStackOffset = Vector3.zero;
    [SerializeField, Range(-1f, 1f)] private float _mineForwardDotMin = 0f;

    private InputAction _moveAction;
    private readonly List<GameObject> _spawnedOreViews = new();
    private bool _loggedStackMax;
    private Vector2 _lastMoveInput;

    public EquipBase Equip => _equip;
    public ResourceStack CarryStack => _resourceStack;

    void Awake()
    {
        if (_model == null)
            _model = GetComponent<PlayerModel>();

        if (_view == null)
            _view = GetComponent<PlayerView>();

        if (_equip == null)
            _equip = GetComponent<EquipBase>();

        if (_resourceStack == null)
            _resourceStack = GetComponent<ResourceStack>();

        if (_oreStackRoot == null)
            _oreStackRoot = transform;

        if (_resourceStack != null && _oreResource != null)
            _resourceStack.ConfigureSingleSlot(_oreResource, _oreStackRoot, _oreStackCapacity, _oreStackSpacing, _oreStackOffset);
    }

    // 스폰된 광석 뷰 오브젝트 일괄 파괴
    void OnDestroy()
    {
        for (int i = 0; i < _spawnedOreViews.Count; i++)
        {
            if (_spawnedOreViews[i] != null)
                Destroy(_spawnedOreViews[i]);
        }

        _spawnedOreViews.Clear();
    }

    void OnEnable()
    {
        // Move 액션 바인딩 및 활성화
        BindActions();

        ToggleAction(_moveAction, true);
    }

    void OnDisable()
    {
        // Move 액션 비활성화
        ToggleAction(_moveAction, false);
        _view?.StopMove();
        _lastMoveInput = Vector2.zero;
    }

    void FixedUpdate()
    {
        if (_moveAction == null || _model == null || _view == null) return;

        Vector2 moveInput = _moveAction.ReadValue<Vector2>();
        // 입력 정규화 후 이동 적용
        Vector2 finalMoveInput = _model.ComposeMoveInput(moveInput);
        _lastMoveInput = finalMoveInput;

        _view.ApplyMove(finalMoveInput, _model.MoveSpeed);
    }

    // 이동 입력 없음 + 실제 속도 임계값 이하일 때 인터랙션 가능 상태로 판단
    public bool IsInteractionReady(float stopSpeedThreshold = 0.05f)
    {
        if (_lastMoveInput.sqrMagnitude > 0.0001f)
            return false;

        if (_view == null)
            return false;

        return _view.PlanarSpeed <= stopSpeedThreshold;
    }

    public bool TryAcquireEquip(EquipDefinition equip)
    {
        return _equip != null && _equip.TryAcquire(equip);
    }

    public bool HasEquipOrBetter(EquipDefinition equip)
    {
        return _equip != null && _equip.HasEquipOrBetter(equip);
    }

    void Update()
    {
        TryAutoMineForwardMine();
    }

    private void BindActions()
    {
        // Player/Move 액션 조회
        InputActionAsset actionAsset = _inputActions != null ? _inputActions : InputSystem.actions;
        InputActionMap playerMap = actionAsset.FindActionMap(PlayerMapName, false);

        _moveAction = playerMap.FindAction(MoveActionName, false);
    }

    // 액션을 enabled 상태로 전환 — 이미 같은 상태면 무시
    private void ToggleAction(InputAction action, bool enabled)
    {
        if (action == null) return;

        if (enabled && !action.enabled)
            action.Enable();
        else if (!enabled && action.enabled)
            action.Disable();
    }

    // 전방 광산을 자동 채굴하고 성공 시 광석 뷰 적재
    private void TryAutoMineForwardMine()
    {
        if (_equip == null) return;

        ResourceManager resourceManager = ResourceManager.Instance;
        EquipDefinition currentEquip = _equip.CurrentEquip;

        if (resourceManager == null || currentEquip == null)
        {
            _equip.SetMiningRangeVisual(false);
            return;
        }

        bool hasMineInRange = resourceManager.TryGetMineInFront(transform.position, transform.forward, currentEquip.MineRange, _mineForwardDotMin, out Mine mine);
        _equip.SetMiningRangeVisual(hasMineInRange);

        if (!hasMineInRange)
            return;

        // 적재 공간이 없으면 채굴 중단
        if (_resourceStack != null && _oreResource != null && _resourceStack.GetRemaining(_oreResource) <= 0)
        {
            if (!_loggedStackMax)
            {
                Debug.Log("MAX");
                _loggedStackMax = true;
            }

            return;
        }

        _loggedStackMax = false;

        if (!_equip.TryMine(mine, out ResourceData yieldResource, out int yieldAmount, out bool depleted))
            return;

        if (!depleted || yieldResource != _oreResource || yieldAmount <= 0)
            return;

        AddOreStackVisuals(yieldAmount);
    }

    // amount만큼 광석 뷰를 스폰하고 ResourceStack에 적재
    private void AddOreStackVisuals(int amount)
    {
        if (_resourceStack == null || _oreResource == null)
            return;

        for (int i = 0; i < amount; i++)
        {
            if (!_resourceStack.TryGetNextWorldPosition(_oreResource, out Vector3 spawnWorldPosition))
                return;

            if (!_resourceStack.TryAdd(_oreResource, 1, out int added) || added <= 0)
                return;

            if (_oreStackPrefab == null)
                continue;

            GameObject oreView = Instantiate(_oreStackPrefab, spawnWorldPosition, Quaternion.identity, _oreStackRoot);

            Collider oreCollider = oreView.GetComponent<Collider>();
            if (oreCollider != null)
                oreCollider.enabled = false;

            _spawnedOreViews.Add(oreView);
        }
    }
}

