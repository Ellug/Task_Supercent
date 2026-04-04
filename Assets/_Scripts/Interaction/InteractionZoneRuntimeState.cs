using UnityEngine;

[System.Serializable]
public class InteractionZoneRuntimeState
{
    [SerializeField] private bool _started;
    [SerializeField] private bool _completed;
    [SerializeField, Min(0)] private int _processedAmount;
    [SerializeField, Min(0)] private int _storedAmount;

    public bool Started => _started;
    public bool Completed => _completed;
    public int ProcessedAmount => _processedAmount;
    public int StoredAmount => _storedAmount;

    // 진행 상태 초기화 — storedAmount를 initialStoredAmount로 설정
    public void ResetProgress(int initialStoredAmount = 0)
    {
        _started = false;
        _completed = false;
        _processedAmount = 0;
        _storedAmount = Mathf.Max(0, initialStoredAmount);
    }

    // 완료 상태 설정
    public void SetCompleted(bool completed)
    {
        _completed = completed;
    }

    // 최초 인터랙션 성공 플래그 설정
    public void MarkStarted()
    {
        _started = true;
    }

    // 처리량 누적
    public void AddProcessed(int amount)
    {
        if (amount <= 0)
            return;

        _processedAmount += amount;
    }

    // 보관량 증감 — 0 미만으로 내려가지 않음
    public void AddStored(int amount)
    {
        _storedAmount = Mathf.Max(0, _storedAmount + amount);
    }

    // 보관량과 처리량 동시 누적
    public void AddStoredAndProcessed(int amount)
    {
        if (amount <= 0)
            return;

        _storedAmount += amount;
        _processedAmount += amount;
    }
}
