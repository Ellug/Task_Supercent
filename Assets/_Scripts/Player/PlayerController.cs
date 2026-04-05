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

    [Header("Mine")]
    [SerializeField, Range(-1f, 1f)] private float _mineForwardDotMin = 0f;

    [Header("Interaction Transfer")]
    [SerializeField, Min(0f)] private float _transferTickInterval = 0.01f;
    [SerializeField, Min(1)] private int _transferAmountPerTick = 1;
    [SerializeField, Min(0f)] private float _maxPopupInterval = 2f;

    private InputAction _moveAction;
    private bool _isInMaxLoop;
    private float _nextMaxPopupTime;
    private Vector2 _lastMoveInput;

    public EquipBase Equip => _equip;
    public ResourceStack CarryStack => _resourceStack;
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

        BindActions();
    }

    void OnEnable()
    {
        ToggleMoveAction(true);
    }

    void OnDisable()
    {
        ToggleMoveAction(false);
        _view.StopMove();
        _lastMoveInput = Vector2.zero;
    }

    void FixedUpdate()
    {
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
        ResourceManager resourceManager = ResourceManager.Instance;
        EquipData currentEquip = _equip.CurrentEquip;

        if (currentEquip == null)
        {
            _equip.SetMiningRangeVisual(false);
            return;
        }

        bool hasMineInRange = resourceManager.TryGetMineInFront(transform.position, transform.forward, currentEquip.MineRange, _mineForwardDotMin, out Mine mine);
        _equip.SetMiningRangeVisual(hasMineInRange);

        if (!hasMineInRange) return;

        ResourceData predictedResource = mine.YieldResource;

        // 적재 공간이 없으면 채굴 중단
        if (predictedResource != null && _resourceStack.GetRemaining(predictedResource) <= 0)
        {
            if (!_isInMaxLoop || Time.time >= _nextMaxPopupTime)
            {
                // 적재 한도 도달 팝업 출력
                MaxPopupUI.Instance.ShowAt(transform);
                _nextMaxPopupTime = Time.time + Mathf.Max(0f, _maxPopupInterval);
            }

            _isInMaxLoop = true;
            return;
        }

        _isInMaxLoop = false;

        if (!_equip.TryMine(mine, out ResourceData yieldResource, out int yieldAmount, out bool depleted))
            return;

        if (!depleted || yieldResource == null || yieldAmount <= 0)
            return;

        TryAddCarriedResource(yieldResource, yieldAmount);
    }

    // 채굴 산출 자원을 스택에 적재
    private bool TryAddCarriedResource(ResourceData resource, int amount)
    {
        return _resourceStack.TryAdd(resource, amount, out int added) && added > 0;
    }
}
