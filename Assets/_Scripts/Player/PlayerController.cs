using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerModel), typeof(PlayerView))]
public class PlayerController : MonoBehaviour
{
    private const string PlayerMapName = "Player";
    private const string MoveActionName = "Move";

    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("References")]
    [SerializeField] private PlayerModel _model;
    [SerializeField] private PlayerView _view;

    private InputAction _moveAction;

    void Awake()
    {
        if (_model == null)
            _model = GetComponent<PlayerModel>();

        if (_view == null)
            _view = GetComponent<PlayerView>();
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
    }

    void FixedUpdate()
    {
        if (_moveAction == null || _model == null || _view == null) return;

        Vector2 moveInput = _moveAction.ReadValue<Vector2>();
        // 입력 정규화 후 이동 적용
        Vector2 finalMoveInput = _model.ComposeMoveInput(moveInput);

        _view.ApplyMove(finalMoveInput, _model.MoveSpeed, 1, Time.fixedDeltaTime);
    }

    private void BindActions()
    {
        // Player/Move 액션 조회
        InputActionAsset actionAsset = _inputActions != null ? _inputActions : InputSystem.actions;
        InputActionMap playerMap = actionAsset.FindActionMap(PlayerMapName, false);

        _moveAction = playerMap.FindAction(MoveActionName, false);
    }

    private void ToggleAction(InputAction action, bool enabled)
    {
        if (action == null) return;

        if (enabled && !action.enabled)
            action.Enable();
        else if (!enabled && action.enabled)
            action.Disable();
    }
}
