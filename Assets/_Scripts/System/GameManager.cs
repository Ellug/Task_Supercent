using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private InteractionZoneFlowLibrary _interactionZoneFlowLibrary;
    [SerializeField] private StageProgressManager _stageProgressManager;
    [SerializeField] private EndingUI _endingUI;

    protected override void Awake()
    {
        base.Awake();
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        _stageProgressManager.Initialize(_interactionZoneFlowLibrary);
    }

    public void ShowEndingPanel()
    {
        _endingUI.Show();
        AudioManager.Instance.PlaySFX(2);
    }
}
