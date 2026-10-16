using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

public static class WorkbenchRoutePerformanceChecks
{
    [Serializable]
    public class Report
    {
        public int iterations;
        public int enemies;
        public double averageMs;
        public double maximumMs;
        public double shortestMs;
        public double safeMs;
        public double hiddenMs;
        public Vector3 start;
        public Vector3 goal;
        public bool closeBehindDetected;
        public bool farBehindHidden;
        public List<string> failures = new();
    }

    public static Report Check()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (bench == null) { report.failures.Add("Нет активного NavMeshWorkbench"); return report; }
        try
        {
            var routes = Read<WorkbenchRoutes>(bench, "routes");
            var guards = Read<WorkbenchEnemy[]>(bench, "enemies");
            var points = Read<List<WorkbenchItem>>(bench, "points");
            int index = Read<int>(bench, "targetIndex");
            if (routes == null || points.Count == 0) { report.failures.Add("Стенд еще не инициализирован или нет POI"); return report; }
            var positions = new Vector3[guards.Length];
            var directions = new Vector3[guards.Length];
            for (int i = 0; i < guards.Length; i++) { positions[i] = guards[i].transform.position; directions[i] = guards[i].transform.forward; }
            report.enemies = guards.Length;
            var start = bench.Actor.transform.position;
            var goal = points[Mathf.Clamp(index, 0, points.Count - 1)].transform.position;
            report.start = start;
            report.goal = goal;
            var batch = new Stopwatch();
            var strategyWatch = new Stopwatch();
            var totals = new double[3];
            for (int iteration = 0; iteration < 5; iteration++)
            {
                batch.Restart();
                for (int strategy = 0; strategy < 3; strategy++)
                {
                    strategyWatch.Restart();
                    var result = routes.Find(start, goal, (RouteStrategy)strategy, positions, directions);
                    strategyWatch.Stop();
                    totals[strategy] += strategyWatch.Elapsed.TotalMilliseconds;
                    if (result.Length == 0) report.failures.Add($"Итерация {iteration}, {(RouteStrategy)strategy}: нет пути между {start} и {goal}");
                }
                batch.Stop();
                report.averageMs += batch.Elapsed.TotalMilliseconds;
                report.maximumMs = Math.Max(report.maximumMs, batch.Elapsed.TotalMilliseconds);
                report.iterations++;
            }
            report.averageMs /= report.iterations;
            report.shortestMs = totals[0] / report.iterations;
            report.safeMs = totals[1] / report.iterations;
            report.hiddenMs = totals[2] / report.iterations;

            var isolated = new WorkbenchRoutes(bench.Settings, Read<LayerMask>(bench, "obstacleMask"));
            typeof(WorkbenchRoutes).GetField("directions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(isolated, new[] { Vector3.forward });
            var penalty = typeof(WorkbenchRoutes).GetMethod("Penalty", BindingFlags.Instance | BindingFlags.NonPublic);
            var enemy = new Vector3(1000, 0, 1000);
            float close = (float)penalty.Invoke(isolated, new object[] { enemy - Vector3.forward * (bench.Settings.ContactRange * 0.8f), RouteStrategy.Hidden, new[] { enemy } });
            float far = (float)penalty.Invoke(isolated, new object[] { enemy - Vector3.forward * (bench.Settings.ContactRange + 0.2f), RouteStrategy.Hidden, new[] { enemy } });
            report.closeBehindDetected = close > 0;
            report.farBehindHidden = Mathf.Approximately(far, 0);
            if (!report.closeBehindDetected) report.failures.Add($"Hidden сзади в контакте: penalty={close}, ожидалось > 0");
            if (!report.farBehindHidden) report.failures.Add($"Hidden сзади вне контакта: penalty={far}, ожидалось 0");
        }
        catch (Exception error)
        {
            report.failures.Add($"Замер маршрутов: {error.GetType().Name}: {error.Message}");
        }

        return report;
    }

    private static T Read<T>(NavMeshWorkbench bench, string field) => (T)typeof(NavMeshWorkbench).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bench);
}
