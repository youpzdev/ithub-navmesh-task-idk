using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class WorkbenchRouteChecks
{
    [Serializable]
    public class Report
    {
        public int passed;
        public List<string> failures = new();
        public float shortestLength;
        public float safeLength;
        public float hiddenLength;
        public float shortestExposure;
        public float hiddenExposure;
    }

    public static Report Check()
    {
        var report = new Report();
        if (NavMesh.CalculateTriangulation().vertices.Length > 0)
        {
            report.failures.Add("Для изолированной проверки нужен редактор без зарегистрированного NavMesh; существующие данные не удалены");

            return report;
        }
        var settings = ScriptableObject.CreateInstance<WorkbenchSettings>();
        var wall = new GameObject("RouteCheckWall");
        var collider = wall.AddComponent<BoxCollider>();
        wall.layer = 30;
        wall.transform.position = new Vector3(0, 1, 2);
        collider.size = new Vector3(8, 2, 0.6f);
        Physics.SyncTransforms();
        NavMeshData data = null;
        NavMeshDataInstance instance = default;
        try
        {
            var sources = new List<NavMeshBuildSource>
            {
                Box(new Vector3(0, -0.2f, 0), new Vector3(28, 0.4f, 22), 0),
                Box(wall.transform.position, collider.size, 1)
            };
            var build = NavMesh.GetSettingsByID(0);
            data = NavMeshBuilder.BuildNavMeshData(build, sources, new Bounds(Vector3.zero, new Vector3(30, 8, 24)), Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("Bake fixture вернул null");
            instance = NavMesh.AddNavMeshData(data);
            var routes = new WorkbenchRoutes(settings, 1 << 30);
            routes.Rebuild();
            var start = new Vector3(-10, 0, 0);
            var goal = new Vector3(10, 0, 0);
            var enemies = new[] { Vector3.zero };
            var shortest = routes.Find(start, goal, RouteStrategy.Shortest, enemies);
            var safe = routes.Find(start, goal, RouteStrategy.Safe, enemies);
            var hidden = routes.Find(start, goal, RouteStrategy.Hidden, enemies);
            AssertPath(report, "Shortest", shortest, start, goal);
            AssertPath(report, "Safe", safe, start, goal);
            AssertPath(report, "Hidden", hidden, start, goal);
            report.shortestLength = Length(shortest);
            report.safeLength = Length(safe);
            report.hiddenLength = Length(hidden);
            report.shortestExposure = Exposure(shortest, settings.SightRange, 1 << 30);
            report.hiddenExposure = Exposure(hidden, settings.SightRange, 1 << 30);
            Expect(report, report.safeLength > report.shortestLength + 0.5f, $"Safe обходит угрозу: длина={report.safeLength}, ожидалось > {report.shortestLength + 0.5f}");
            Expect(report, report.hiddenExposure < report.shortestExposure * 0.8f, $"Hidden использует укрытие: видимый путь={report.hiddenExposure}, ожидалось < {report.shortestExposure * 0.8f}");
            var moved = routes.Find(start, goal, RouteStrategy.Safe, new[] { new Vector3(0, 0, -7) });
            AssertPath(report, "Moved enemy", moved, start, goal);
            Expect(report, Difference(safe, moved) > 0.5f, "Перемещение врага не изменило Safe маршрут без Rebuild");
            var emptyEnemies = routes.Find(start, goal, RouteStrategy.Safe, Array.Empty<Vector3>());
            Expect(report, emptyEnemies.Length > 0 && !routes.Risky, "Маршрут без врагов должен существовать без Risky");
            Expect(report, routes.Find(new Vector3(float.NaN, 0, 0), goal, RouteStrategy.Safe, enemies).Length == 0, "NaN start должен вернуть пустой маршрут");
            Expect(report, routes.Find(start, new Vector3(200, 0, 0), RouteStrategy.Shortest, enemies).Length == 0, "Цель за ареной должна вернуть пустой маршрут");

            instance.Remove();
            UnityEngine.Object.DestroyImmediate(data);
            sources.Add(Box(new Vector3(0, 1, 0), new Vector3(1, 2, 24), 1));
            data = NavMeshBuilder.BuildNavMeshData(build, sources, new Bounds(Vector3.zero, new Vector3(30, 8, 24)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            routes.Rebuild();
            foreach (RouteStrategy strategy in Enum.GetValues(typeof(RouteStrategy)))
                Expect(report, routes.Find(start, goal, strategy, enemies).Length == 0, $"Перекрытый проход: {strategy} должен вернуть пустой маршрут");

            instance.Remove();
            UnityEngine.Object.DestroyImmediate(data);
            sources.RemoveAt(sources.Count - 1);
            data = NavMeshBuilder.BuildNavMeshData(build, sources, new Bounds(Vector3.zero, new Vector3(30, 8, 24)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            routes.Rebuild();
            AssertPath(report, "Restored passage", routes.Find(start, goal, RouteStrategy.Hidden, enemies), start, goal);

            instance.Remove();
            UnityEngine.Object.DestroyImmediate(data);
            build.agentRadius = 0.1f;
            build.overrideVoxelSize = true;
            build.voxelSize = 0.025f;
            sources = new()
            {
                Box(new Vector3(0, -0.2f, 0), new Vector3(28, 0.4f, 22), 0),
                Box(new Vector3(0, 1, 0), new Vector3(0.1f, 2, 8), 1)
            };
            data = NavMeshBuilder.BuildNavMeshData(build, sources, new Bounds(Vector3.zero, new Vector3(30, 8, 24)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            routes.Rebuild();
            var distantThreat = new[] { new Vector3(100, 0, 100) };
            foreach (var strategy in new[] { RouteStrategy.Safe, RouteStrategy.Hidden })
            {
                var aroundWall = routes.Find(start, goal, strategy, distantThreat);
                AssertPath(report, $"Thin wall {strategy}", aroundWall, start, goal);
                Expect(report, Length(aroundWall) > Vector3.Distance(start, goal) + 1, $"Thin wall {strategy}: ожидался обход стены, длина={Length(aroundWall)}");
            }

            instance.Remove();
            UnityEngine.Object.DestroyImmediate(data);
            build.agentRadius = 0.2f;
            sources = new()
            {
                Box(new Vector3(0, -0.2f, 0), new Vector3(28, 0.4f, 22), 0),
                Box(new Vector3(-7.125f, 1, 0), new Vector3(13.75f, 2, 24), 1),
                Box(new Vector3(7.125f, 1, 0), new Vector3(13.75f, 2, 24), 1)
            };
            data = NavMeshBuilder.BuildNavMeshData(build, sources, new Bounds(Vector3.zero, new Vector3(30, 8, 24)), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            routes.Rebuild();
            var narrowStart = new Vector3(0, 0, -8);
            var narrowGoal = new Vector3(0, 0, 8);
            AssertPath(report, "Narrow shortest", routes.Find(narrowStart, narrowGoal, RouteStrategy.Shortest, distantThreat), narrowStart, narrowGoal);
            foreach (var strategy in new[] { RouteStrategy.Safe, RouteStrategy.Hidden })
            {
                var narrow = routes.Find(narrowStart, narrowGoal, strategy, distantThreat);
                AssertPath(report, $"Narrow {strategy}", narrow, narrowStart, narrowGoal);
                Expect(report, routes.Risky, $"Narrow {strategy}: упрощенный маршрут при отказе сетки должен иметь Risky");
            }
        }
        catch (Exception error)
        {
            report.failures.Add($"Проверка маршрутов: {error.GetType().Name}: {error.Message}");
        }
        finally
        {
            if (instance.valid) instance.Remove();
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(wall);
            UnityEngine.Object.DestroyImmediate(settings);
        }

        return report;
    }

    private static NavMeshBuildSource Box(Vector3 position, Vector3 size, int area) => new()
    {
        shape = NavMeshBuildSourceShape.Box,
        transform = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one),
        size = size,
        area = area
    };

    private static void AssertPath(Report report, string name, Vector3[] path, Vector3 start, Vector3 goal)
    {
        Expect(report, path.Length >= 2, $"{name}: точек={path.Length}, ожидалось >= 2");
        if (path.Length < 2) return;
        Expect(report, Vector3.Distance(path[0], start) < 0.5f && Vector3.Distance(path[^1], goal) < 0.5f, $"{name}: начало={path[0]}, конец={path[^1]}, ожидалось {start} и {goal}");
        for (int i = 1; i < path.Length; i++)
            Expect(report, !NavMesh.Raycast(path[i - 1], path[i], out _, NavMesh.AllAreas), $"{name}: сегмент {i - 1}-{i} пересекает границу NavMesh");
    }

    private static float Length(Vector3[] path)
    {
        float result = 0;
        for (int i = 1; i < path.Length; i++) result += Vector3.Distance(path[i - 1], path[i]);

        return result;
    }

    private static float Exposure(Vector3[] path, float range, LayerMask mask)
    {
        float result = 0;
        for (int i = 1; i < path.Length; i++)
        {
            float length = Vector3.Distance(path[i - 1], path[i]);
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / 0.15f));
            for (int j = 0; j < samples; j++)
            {
                var point = Vector3.Lerp(path[i - 1], path[i], (j + 0.5f) / samples);
                var offset = -point;
                offset.y = 0;
                if (offset.magnitude < range && !Physics.Raycast(point + Vector3.up * 0.8f, offset.normalized, offset.magnitude, mask, QueryTriggerInteraction.Ignore)) result += length / samples;
            }
        }

        return result;
    }

    private static float Difference(Vector3[] first, Vector3[] second)
    {
        float difference = 0;
        foreach (var point in first)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 1; i < second.Length; i++)
            {
                var segment = second[i] - second[i - 1];
                float t = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(point - second[i - 1], segment) / segment.sqrMagnitude) : 0;
                nearest = Mathf.Min(nearest, Vector3.Distance(point, second[i - 1] + segment * t));
            }
            difference = Mathf.Max(difference, nearest);
        }

        return difference;
    }

    private static void Expect(Report report, bool condition, string failure)
    {
        if (condition) report.passed++;
        else report.failures.Add(failure);
    }
}
