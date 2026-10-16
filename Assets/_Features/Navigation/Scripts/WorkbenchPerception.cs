using UnityEngine;

public enum EnemyAwareness { Patrol, Searching, Detected }

public class WorkbenchPerception
{
    private readonly float range;
    private readonly float minimumDot;
    private readonly float searchDuration;
    private readonly float closeRange;
    private readonly LayerMask mask;
    private readonly bool valid;

    private float searchLeft;

    public EnemyAwareness State { get; private set; }
    public Vector3 LastSeen { get; private set; }

    public WorkbenchPerception(float range, float angle, float searchDuration, LayerMask mask, float closeRange = 0)
    {
        this.range = range;
        this.searchDuration = searchDuration;
        this.mask = mask;
        this.closeRange = closeRange;
        minimumDot = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
        valid = float.IsFinite(range) && range > 0 && float.IsFinite(angle) && angle > 0 && angle <= 360 && float.IsFinite(searchDuration) && searchDuration >= 0;
        if (!valid) Debug.LogError($"Обзор врага: range={range}, angle={angle}, searchDuration={searchDuration}");
    }

    public bool CanSee(Vector3 observer, Vector3 forward, Vector3 target)
    {
        if (!valid || !Finite(observer) || !Finite(forward) || !Finite(target)) return false;
        Vector3 difference = target - observer;
        if (difference.sqrMagnitude > range * range) return false;
        difference.y = 0;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.000001f) return false;
        if (difference.sqrMagnitude > closeRange * closeRange && Vector3.Dot(forward.normalized, difference.normalized) + 0.000001f < minimumDot) return false;

        return !Physics.Linecast(observer + Vector3.up, target + Vector3.up, mask, QueryTriggerInteraction.Ignore);
    }

    public void Tick(Vector3 observer, Vector3 forward, Vector3 target, bool enabled, bool targetAlive, float delta)
    {
        if (!enabled || !targetAlive || !valid) { State = EnemyAwareness.Patrol; searchLeft = 0; return; }
        if (CanSee(observer, forward, target))
        {
            State = EnemyAwareness.Detected;
            LastSeen = target;
            searchLeft = searchDuration;
            return;
        }
        if (State == EnemyAwareness.Patrol) return;
        if (float.IsFinite(delta) && delta > 0) searchLeft = Mathf.Max(0, searchLeft - delta);
        State = searchLeft > 0 ? EnemyAwareness.Searching : EnemyAwareness.Patrol;
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
}
