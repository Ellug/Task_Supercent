public sealed class NpcStateMachine
{
    public INpcState CurrentState { get; private set; }
    public string CurrentStateName => CurrentState != null ? CurrentState.Name : string.Empty;

    // 동일 상태로의 전환은 무시, 이전 상태 Exit → 새 상태 Enter 순서 보장
    public void ChangeState(INpcState nextState)
    {
        if (ReferenceEquals(CurrentState, nextState))
            return;

        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState?.Enter();
    }

    public void Tick(float deltaTime)
    {
        CurrentState?.Tick(deltaTime);
    }
}
