using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class WorkbenchContactChecks
{
    [Serializable]
    public class Report { public int passed; public List<string> failures = new(); }

    public static Report Check()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        var enemy = UnityEngine.Object.FindFirstObjectByType<WorkbenchEnemy>();
        if (!enemy) { report.failures.Add("Нужен режим Стратегии с активным врагом"); return report; }
        var perception = (WorkbenchPerception)typeof(WorkbenchEnemy).GetField("perception", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
        Vector3 origin = new(1000, 0, 1000);
        float contact = bench.Settings.ContactRange;
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0, i * 45, 0) * Vector3.forward;
            bool seen = perception.CanSee(origin, Vector3.forward, origin + direction * (contact - 0.05f));
            Expect(report, seen, $"Контакт под углом {i * 45}: получено {seen}, ожидалось обнаружение");
        }
        Expect(report, !perception.CanSee(origin, Vector3.forward, origin - Vector3.forward * (contact + 0.05f)), "За пределом контакта сзади цель не должна обнаруживаться");
        var wall = new GameObject("ContactCheckWall"); wall.layer = 9;
        wall.transform.position = origin + new Vector3(0, 1, -0.5f);
        wall.AddComponent<BoxCollider>().size = new Vector3(2, 2, 0.1f);
        try
        {
            Physics.SyncTransforms();
            Expect(report, !perception.CanSee(origin, Vector3.forward, origin - Vector3.forward), "Стена должна блокировать ближнее обнаружение");
        }
        finally { UnityEngine.Object.DestroyImmediate(wall); Physics.SyncTransforms(); }

        return report;
    }

    private static void Expect(Report report, bool ok, string message) { if (ok) report.passed++; else report.failures.Add(message); }
}
