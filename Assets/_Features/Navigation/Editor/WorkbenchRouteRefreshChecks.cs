using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class WorkbenchRouteRefreshChecks
{
    [Serializable]
    public class Report { public int passed; public List<string> failures = new(); }

    public static Report Check()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (bench.Rebuilding) { report.failures.Add("Дождись NavMesh"); return report; }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var lines = (LineRenderer[])typeof(NavMeshWorkbench).GetField("routeLines", flags).GetValue(bench);
        Vector3 saved = bench.Actor.transform.position;
        bool paused = bench.Paused;
        if (!paused) bench.TogglePause();
        bench.SetStrategy(0);
        typeof(NavMeshWorkbench).GetField("nextPlan", flags).SetValue(bench, Time.time + 1);
        bench.Actor.Agent.Warp(saved + Vector3.forward * 0.35f);
        typeof(NavMeshWorkbench).GetMethod("Update", flags).Invoke(bench, null);
        float distance = lines[0].positionCount > 0 ? Vector3.Distance(lines[0].GetPosition(0), bench.Actor.transform.position + Vector3.up * 0.1f) : float.PositiveInfinity;
        Expect(report, distance < 0.01f, $"Начало маршрута отстает от агента на {distance}, ожидалось <0.01");
        Expect(report, bench.Settings.ReplanInterval <= 0.15f, $"Интервал пересчета {bench.Settings.ReplanInterval}, ожидалось <=0.15");
        bench.Actor.Agent.Warp(saved);
        bench.SetStrategy(-1);
        if (!paused) bench.TogglePause();

        return report;
    }

    private static void Expect(Report report, bool ok, string message) { if (ok) report.passed++; else report.failures.Add(message); }
}
