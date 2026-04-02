using UnityEngine;

public class EquipVisualPresentation : EquipPresentationBase
{
    [SerializeField] private GameObject _visualRoot;
    [SerializeField] private Animator _animator;
    [SerializeField] private string _mineTrigger = "Mine";
    [SerializeField] private ParticleSystem _mineFx;
    [SerializeField] private ParticleSystem _mineDepletedFx;

    // 장비 비주얼 활성화
    public override void OnEquipped(Transform owner, EquipDefinition equip)
    {
        gameObject.SetActive(true);

        if (_visualRoot != null)
            _visualRoot.SetActive(true);
    }

    // 비주얼 비활성화
    public override void OnUnequipped()
    {
        if (_visualRoot != null)
            _visualRoot.SetActive(false);

        gameObject.SetActive(false);
    }

    // 채굴 애니메이션 트리거 및 타격 이펙트 재생
    public override void PlayMineAction(Vector3 worldPosition)
    {
        if (_animator != null && _mineTrigger.Length > 0)
            _animator.SetTrigger(_mineTrigger);

        if (_mineFx != null)
        {
            _mineFx.transform.position = worldPosition;
            _mineFx.Play();
        }
    }

    // 광산 소진 이펙트 재생
    public override void PlayMineDepleted(Vector3 worldPosition)
    {
        if (_mineDepletedFx != null)
        {
            _mineDepletedFx.transform.position = worldPosition;
            _mineDepletedFx.Play();
        }
    }
}
