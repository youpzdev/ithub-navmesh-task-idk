using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorkbenchPolishChecks
{
    [Serializable]
    public class Report { public int passed; public List<string> failures = new(); }

    public static string Check()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (bench == null || bench.Rebuilding) return "Стенд не готов";
        int before = Playing(bench.Settings.PlaceSound);
        bool placed = bench.PlaceObstacle(new Vector3(-12, 0, -3), 0);
        int after = Playing(bench.Settings.PlaceSound);
        Expect(report, placed, "Не удалось поставить тестовый блок в (-12,0,-3)");
        Expect(report, after == before, "Звук установки сразу после спавна: было=" + before + ", стало=" + after + ", ожидалось без изменения до приземления");
        foreach (var system in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            Expect(report, renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null, system.name + ": ожидались объемные Mesh-частицы, получено " + renderer.renderMode);
            var gradient = system.colorOverLifetime.color.gradient;
            Expect(report, system.colorOverLifetime.enabled && gradient.Evaluate(0).a < 0.01f && gradient.Evaluate(1).a < 0.01f, system.name + ": alpha должна плавно начинаться и заканчиваться на 0");
            Expect(report, system.sizeOverLifetime.enabled, system.name + ": должен быть плавный размер по времени жизни");
        }

        return JsonUtility.ToJson(report);
    }

    private static int Playing(AudioClip clip)
    {
        int count = 0;
        foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (source.clip == clip && source.isPlaying) count++;

        return count;
    }
    private static void Expect(Report report, bool ok, string failure) { if (ok) report.passed++; else report.failures.Add(failure); }
}
