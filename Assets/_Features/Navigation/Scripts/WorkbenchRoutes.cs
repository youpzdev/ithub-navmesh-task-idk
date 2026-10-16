using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class WorkbenchRoutes
{
    private readonly WorkbenchSettings settings;
    private readonly LayerMask obstacleMask;

    private Vector3[] nodes = Array.Empty<Vector3>();
    private List<int>[] neighbours = Array.Empty<List<int>>();
    private bool[] valid = Array.Empty<bool>();
    private Vector3[] directions;

    private int columns;
    private int rows;

    public bool Risky { get; private set; }

    public WorkbenchRoutes(WorkbenchSettings settings, LayerMask obstacleMask)
    {
        this.settings = settings;
        this.obstacleMask = obstacleMask;
    }

    public void Rebuild()
    {
        nodes = Array.Empty<Vector3>();
        valid = Array.Empty<bool>();
        neighbours = Array.Empty<List<int>>();
        if (!ConfigurationValid()) return;

        columns = Mathf.CeilToInt(settings.ArenaSize.x / settings.GridStep) + 1;
        rows = Mathf.CeilToInt(settings.ArenaSize.y / settings.GridStep) + 1;
        nodes = new Vector3[columns * rows];
        valid = new bool[nodes.Length];
        neighbours = new List<int>[nodes.Length];
        float stepX = settings.ArenaSize.x / (columns - 1);
        float stepZ = settings.ArenaSize.y / (rows - 1);
        float sampleRadius = Mathf.Min(stepX, stepZ) * 0.4f;

        for (int z = 0; z < rows; z++)
        for (int x = 0; x < columns; x++)
        {
            int index = z * columns + x;
            var position = new Vector3(x * stepX - settings.ArenaSize.x * 0.5f, 0, z * stepZ - settings.ArenaSize.y * 0.5f);
            valid[index] = NavMesh.SamplePosition(position, out var hit, sampleRadius, NavMesh.AllAreas);
            nodes[index] = hit.position;
            neighbours[index] = new List<int>(8);
        }

        for (int z = 0; z < rows; z++)
        for (int x = 0; x < columns; x++)
        {
            int index = z * columns + x;
            if (!valid[index]) continue;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nx >= columns || nz < 0 || nz >= rows) continue;
                int next = nz * columns + nx;
                if (valid[next] && Clear(nodes[index], nodes[next])) neighbours[index].Add(next);
            }
        }
    }

    public Vector3[] Find(Vector3 start, Vector3 goal, RouteStrategy strategy, Vector3[] enemies, Vector3[] forward = null)
    {
        Risky = false;
        directions = forward;
        if (!ConfigurationValid() || !Finite(start) || !Finite(goal) || !Enum.IsDefined(typeof(RouteStrategy), strategy)) return Array.Empty<Vector3>();
        if (!Inside(start) || !Inside(goal)) return Array.Empty<Vector3>();
        if (!NavMesh.SamplePosition(start, out var from, settings.GridStep * 0.5f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(goal, out var to, settings.GridStep * 0.5f, NavMesh.AllAreas)) return Array.Empty<Vector3>();
        if (enemies != null)
            foreach (var enemy in enemies)
                if (!Finite(enemy)) { Debug.LogWarning("Расчет маршрута: enemies содержит некорректную позицию"); return Array.Empty<Vector3>(); }

        var direct = new NavMeshPath();
        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, direct) || direct.status != NavMeshPathStatus.PathComplete) return Array.Empty<Vector3>();
        if (strategy == RouteStrategy.Shortest || enemies == null || enemies.Length == 0) return direct.corners;
        if (nodes.Length == 0) Rebuild();
        if (nodes.Length == 0) return GraphFallback(direct);

        var starts = Connections(from.position);
        var goals = Connections(to.position);
        if (starts.Count == 0 || goals.Count == 0) return GraphFallback(direct);
        var goalSet = new HashSet<int>(goals);
        var costs = new float[nodes.Length];
        var scores = new float[nodes.Length];
        var penalties = new float[nodes.Length];
        var parents = new int[nodes.Length];
        var closed = new bool[nodes.Length];
        var open = new RouteHeap();
        for (int i = 0; i < nodes.Length; i++)
        {
            costs[i] = float.PositiveInfinity;
            parents[i] = -1;
            if (valid[i]) penalties[i] = Penalty(nodes[i], strategy, enemies);
        }
        foreach (int index in starts)
        {
            costs[index] = SegmentCost(from.position, nodes[index], strategy, enemies);
            scores[index] = costs[index] + Vector3.Distance(nodes[index], to.position);
            open.Push(index, scores[index]);
        }

        int end = -1;
        float best = float.PositiveInfinity;
        while (open.Count > 0)
        {
            var item = open.Pop();
            int current = item.index;
            if (closed[current] || item.score > scores[current]) continue;
            if (item.score >= best) break;
            closed[current] = true;
            if (goalSet.Contains(current))
            {
                float total = costs[current] + SegmentCost(nodes[current], to.position, strategy, enemies);
                if (total < best) { best = total; end = current; }
            }
            foreach (int next in neighbours[current])
            {
                if (closed[next]) continue;
                float distance = Vector3.Distance(nodes[current], nodes[next]);
                float nextCost = costs[current] + distance * (1 + (penalties[current] + penalties[next]) * 0.5f);
                if (nextCost >= costs[next]) continue;
                costs[next] = nextCost;
                parents[next] = current;
                scores[next] = nextCost + Vector3.Distance(nodes[next], to.position);
                open.Push(next, scores[next]);
            }
        }
        if (end < 0) return GraphFallback(direct);

        var result = new List<Vector3> { to.position };
        for (int index = end; index >= 0; index = parents[index]) result.Add(nodes[index]);
        result.Add(from.position);
        result.Reverse();
        var route = Simplify(result, strategy, enemies);
        for (int i = 1; i < route.Length; i++)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(route[i - 1], route[i]) / (settings.GridStep * 0.5f)));
            for (int j = 0; j <= samples; j++)
                if (Penalty(Vector3.Lerp(route[i - 1], route[i], (float)j / samples), strategy, enemies) > 0.01f) Risky = true;
        }

        return route;
    }

    private Vector3[] GraphFallback(NavMeshPath completePath)
    {
        Risky = true;

        return completePath.corners;
    }

    private List<int> Connections(Vector3 point)
    {
        var result = new List<int>();
        float radius = settings.GridStep * 2;
        for (int i = 0; i < nodes.Length; i++)
            if (valid[i] && (nodes[i] - point).sqrMagnitude <= radius * radius && Clear(point, nodes[i])) result.Add(i);

        return result;
    }

    private Vector3[] Simplify(List<Vector3> path, RouteStrategy strategy, Vector3[] enemies)
    {
        var result = new List<Vector3> { path[0] };
        int start = 0;
        while (start < path.Count - 1)
        {
            int furthest = start + 1;
            float original = 0;
            for (int end = start + 1; end < path.Count; end++)
            {
                original += SegmentCost(path[end - 1], path[end], strategy, enemies);
                if (Clear(path[start], path[end]) && SegmentCost(path[start], path[end], strategy, enemies) <= original + 0.001f) furthest = end;
            }
            result.Add(path[furthest]);
            start = furthest;
        }

        return result.ToArray();
    }

    private float SegmentCost(Vector3 from, Vector3 to, RouteStrategy strategy, Vector3[] enemies)
    {
        float distance = Vector3.Distance(from, to);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / (settings.GridStep * 0.5f)));
        float penalty = 0;
        for (int i = 0; i < samples; i++) penalty += Penalty(Vector3.Lerp(from, to, (i + 0.5f) / samples), strategy, enemies);

        return distance * (1 + penalty / samples);
    }

    private float Penalty(Vector3 position, RouteStrategy strategy, Vector3[] enemies)
    {
        float penalty = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            var offset = enemy - position;
            offset.y = 0;
            float distance = offset.magnitude;
            if (distance >= settings.SightRange) continue;
            float proximity = 1 - distance / settings.SightRange;
            bool visible = distance < 0.001f || !Physics.Raycast(position + Vector3.up * 0.8f, offset.normalized, distance, obstacleMask, QueryTriggerInteraction.Ignore);
            if (distance > settings.ContactRange && directions != null && i < directions.Length && Vector3.Angle(directions[i], -offset) > settings.SightAngle * 0.5f) visible = false;
            penalty += strategy == RouteStrategy.Safe ? settings.SafeWeight * proximity * proximity : visible ? settings.HiddenWeight * (0.5f + proximity * 0.5f) : 0;
        }

        return penalty;
    }

    private bool ConfigurationValid()
    {
        if (settings == null) { Debug.LogError("Расчет маршрута: не назначен settings"); return false; }
        if (!float.IsFinite(settings.GridStep) || settings.GridStep < 0.2f || !float.IsFinite(settings.ArenaSize.x) || !float.IsFinite(settings.ArenaSize.y) || settings.ArenaSize.x <= 0 || settings.ArenaSize.y <= 0 ||
            settings.ArenaSize.x / settings.GridStep > 200 || settings.ArenaSize.y / settings.GridStep > 200)
        { Debug.LogError($"Расчет маршрута: arenaSize={settings.ArenaSize}, gridStep={settings.GridStep}"); return false; }
        if (!float.IsFinite(settings.SightRange) || settings.SightRange <= 0 || !float.IsFinite(settings.SafeWeight) || settings.SafeWeight < 0 || !float.IsFinite(settings.HiddenWeight) || settings.HiddenWeight < 0)
        { Debug.LogError($"Расчет маршрута: sightRange={settings.SightRange}, safeWeight={settings.SafeWeight}, hiddenWeight={settings.HiddenWeight}"); return false; }

        return true;
    }

    private bool Inside(Vector3 point) => Mathf.Abs(point.x) <= settings.ArenaSize.x * 0.5f && Mathf.Abs(point.z) <= settings.ArenaSize.y * 0.5f && Mathf.Abs(point.y) <= settings.GridStep;
    private static bool Finite(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
    private static bool Clear(Vector3 from, Vector3 to) => !NavMesh.Raycast(from, to, out _, NavMesh.AllAreas);

    private class RouteHeap
    {
        private readonly List<(int index, float score)> items = new();
        public int Count => items.Count;

        public void Push(int index, float score)
        {
            items.Add((index, score));
            int child = items.Count - 1;
            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (items[parent].score <= score) break;
                items[child] = items[parent];
                child = parent;
            }
            items[child] = (index, score);
        }

        public (int index, float score) Pop()
        {
            var result = items[0];
            var last = items[^1];
            items.RemoveAt(items.Count - 1);
            if (items.Count == 0) return result;
            int parent = 0;
            while (parent * 2 + 1 < items.Count)
            {
                int child = parent * 2 + 1;
                if (child + 1 < items.Count && items[child + 1].score < items[child].score) child++;
                if (last.score <= items[child].score) break;
                items[parent] = items[child];
                parent = child;
            }
            items[parent] = last;

            return result;
        }
    }
}
