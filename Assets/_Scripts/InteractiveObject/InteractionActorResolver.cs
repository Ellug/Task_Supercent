using UnityEngine;

public static class InteractionActorResolver
{
    // 콜라이더의 부모 계층에서 IInteractionActor를 탐색해 반환
    public static bool TryResolve(Collider triggerCollider, out IInteractionActor actor)
    {
        actor = null;
        if (triggerCollider == null)
            return false;

        MonoBehaviour[] candidates = triggerCollider.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is not IInteractionActor resolvedActor)
                continue;

            actor = resolvedActor;
            return true;
        }

        return false;
    }
}
