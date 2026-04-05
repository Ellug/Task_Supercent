using System.Collections.Generic;

// 중복 없이 Prisoner를 유지하는 큐
public sealed class UniquePrisonerQueue
{
    private readonly Queue<Prisoner> _queue = new();
    private readonly HashSet<Prisoner> _set = new();

    public int Count => _queue.Count;

    public bool Enqueue(Prisoner prisoner)
    {
        if (prisoner == null)
            return false;

        if (!_set.Add(prisoner))
            return false;

        _queue.Enqueue(prisoner);
        return true;
    }

    public bool TryPeek(out Prisoner prisoner)
    {
        while (_queue.Count > 0)
        {
            prisoner = _queue.Peek();
            if (prisoner != null && _set.Contains(prisoner))
                return true;

            _queue.Dequeue();
        }

        prisoner = null;
        return false;
    }

    public bool TryDequeue(out Prisoner prisoner)
    {
        while (_queue.Count > 0)
        {
            prisoner = _queue.Dequeue();
            if (prisoner == null)
                continue;

            if (!_set.Remove(prisoner))
                continue;

            return true;
        }

        prisoner = null;
        return false;
    }

    public bool Remove(Prisoner prisoner)
    {
        if (prisoner == null)
            return false;

        return _set.Remove(prisoner);
    }

    public void Clear()
    {
        _queue.Clear();
        _set.Clear();
    }

    public List<Prisoner> Snapshot()
    {
        List<Prisoner> snapshot = new();
        foreach (Prisoner prisoner in _queue)
        {
            if (prisoner != null && _set.Contains(prisoner))
                snapshot.Add(prisoner);
        }

        return snapshot;
    }
}
