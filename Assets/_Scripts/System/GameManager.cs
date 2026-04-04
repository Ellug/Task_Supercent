using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private InteractionZoneFlowLibrary _interactionZoneFlowLibrary;
    [SerializeField] private StageProgressManager _stageProgressManager;

    void Start()
    {
        _stageProgressManager.Initialize(_interactionZoneFlowLibrary);
    }
}
