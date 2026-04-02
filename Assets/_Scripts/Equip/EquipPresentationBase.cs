using UnityEngine;

// 장비 연출의 공통 인터페이스 — 장착/해제/채굴/소진 이벤트를 서브클래스에서 구현
public abstract class EquipPresentationBase : MonoBehaviour
{
    public virtual void OnEquipped(Transform owner, EquipDefinition equip)
    {
    }

    public virtual void OnUnequipped()
    {
    }

    public virtual void PlayMineAction(Vector3 worldPosition)
    {
    }

    public virtual void PlayMineDepleted(Vector3 worldPosition)
    {
    }
}
