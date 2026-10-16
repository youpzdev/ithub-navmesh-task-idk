using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEngine;

public static class WorkbenchReloadChecks
{
    [Serializable]
    public class Report { public int passed; public List<string> failures = new(); }

    public static string Check()
    {
        var report = new Report();
        if (!Application.isPlaying) return "Проверка перезагрузки требует Play Mode";
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        Expect(report, Read(bench, "routes") != null, "NavMeshWorkbench.routes=null после reload");
        var modes = (Array)Read(bench, "modes");
        for (int i = 0; i < modes.Length; i++)
        {
            var state = modes.GetValue(i);
            var markers = (ICollection)state.GetType().GetField("points").GetValue(state);
            Expect(report, markers.Count > 0, "ModeState[" + i + "].points=" + markers.Count + ", ожидались сохраненные точки");
        }
        foreach (var actor in UnityEngine.Object.FindObjectsByType<WorkbenchActor>(FindObjectsSortMode.None))
            Expect(report, Read(actor, "gait") != null, actor.name + ": gait=null после reload, ожидался WorkbenchGait");
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<WorkbenchEnemy>(FindObjectsSortMode.None))
        {
            Expect(report, Read(enemy, "gait") != null, enemy.name + ": gait=null после reload, ожидался WorkbenchGait");
            Expect(report, Read(enemy, "perception") != null, enemy.name + ": perception=null после reload, ожидался WorkbenchPerception");
        }
        foreach (var view in UnityEngine.Object.FindObjectsByType<WorkbenchThreatView>(FindObjectsSortMode.None))
        {
            Expect(report, Read(view, "color") != null, view.name + ": color=null после reload, ожидался MaterialPropertyBlock");
            var filter = (MeshFilter)Read(view, "view");
            Expect(report, Read(view, "mesh") != null && filter.sharedMesh == (Mesh)Read(view, "mesh"), view.name + ": runtime mesh потерян после reload");
        }

        return JsonUtility.ToJson(report);
    }

    public static string CheckReenable()
    {
        var report = new Report();
        foreach (var actor in UnityEngine.Object.FindObjectsByType<WorkbenchActor>(FindObjectsSortMode.None))
        {
            float health = actor.Health;
            bool dead = actor.Dead;
            actor.enabled = false; actor.enabled = true;
            Expect(report, Mathf.Approximately(actor.Health, health) && actor.Dead == dead,
                actor.name + ": после включения Health=" + actor.Health + ", Dead=" + actor.Dead + ", ожидались " + health + ", " + dead);
            Expect(report, Read(actor, "gait") != null, actor.name + ": gait не восстановлен при включении");
        }
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<WorkbenchEnemy>(FindObjectsSortMode.None))
        {
            bool threat = enemy.ThreatEnabled;
            enemy.enabled = false; enemy.enabled = true;
            Expect(report, Read(enemy, "gait") != null && Read(enemy, "perception") != null && enemy.ThreatEnabled == threat,
                enemy.name + ": gait/perception/ThreatEnabled не восстановлены при включении");
        }
        foreach (var view in UnityEngine.Object.FindObjectsByType<WorkbenchThreatView>(FindObjectsSortMode.None))
        {
            view.enabled = false; view.enabled = true;
            view.GetType().GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
            var edge = (LineRenderer)Read(view, "edge");
            var color = new MaterialPropertyBlock();
            edge.GetPropertyBlock(color);
            Expect(report, Read(view, "color") != null && Read(view, "mesh") != null,
                view.name + ": сектор не восстановлен при включении");
            Expect(report, color.GetColor("_BaseColor").a > 0,
                view.name + ": edge _BaseColor.a=" + color.GetColor("_BaseColor").a + ", ожидалось >0");
        }

        return JsonUtility.ToJson(report);
    }
    public static string CheckUndoCleanup()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (!Application.isPlaying || bench == null || bench.Rebuilding || bench.UndoCount == 0)
            return "Подготовка проверки: добавь одну точку, дождись NavMesh и выполни reload перед Undo";
        var modes = (Array)Read(bench, "modes");
        var state = modes.GetValue((int)bench.Mode);
        var history = (IList)state.GetType().GetField("history").GetValue(state);
        var snapshot = history[history.Count - 1];
        var savedPoints = (ICollection)snapshot.GetType().GetField("points").GetValue(snapshot);
        int pointCount = bench.PointCount;
        if (savedPoints.Count != pointCount - 1) return "Последним действием должно быть добавление одной точки";
        int before = UnityEngine.Object.FindObjectsByType<WorkbenchItem>(FindObjectsSortMode.None).Length;
        bench.Undo();
        int after = UnityEngine.Object.FindObjectsByType<WorkbenchItem>(FindObjectsSortMode.None).Length;
        Expect(report, bench.PointCount == pointCount - 1,
            "Undo: точек=" + bench.PointCount + ", ожидалось " + (pointCount - 1));
        Expect(report, after == before - 1,
            "Undo после reload: активных WorkbenchItem=" + after + ", ожидалось " + (before - 1) + ", до Undo=" + before);

        return JsonUtility.ToJson(report);
    }
    public static string CheckHudClicks()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        var hud = UnityEngine.Object.FindFirstObjectByType<WorkbenchHud>();
        if (!Application.isPlaying || bench == null || hud == null || bench.Rebuilding) return "Стенд не готов к проверке кнопок";
        var compare = (WorkbenchButton)Read(hud, "compare");
        var pause = (WorkbenchButton)Read(hud, "pause");
        bool compared = bench.CompareRoutes;
        bool paused = bench.Paused;
        compare.onClick.Invoke();
        Expect(report, bench.CompareRoutes != compared,
            "Кнопка сравнения после reload: CompareRoutes=" + bench.CompareRoutes + ", ожидалось " + !compared);
        compare.onClick.Invoke();
        Expect(report, bench.CompareRoutes == compared, "Повторное сравнение должно вернуть исходный CompareRoutes=" + compared);
        pause.onClick.Invoke();
        Expect(report, bench.Paused != paused,
            "Кнопка паузы после reload: Paused=" + bench.Paused + ", ожидалось " + !paused);
        Expect(report, Mathf.Approximately(Time.timeScale, bench.Paused ? 0 : bench.SimulationSpeed),
            "Пауза: timeScale=" + Time.timeScale + ", ожидалось " + (bench.Paused ? 0 : bench.SimulationSpeed));
        pause.onClick.Invoke();
        Expect(report, bench.Paused == paused, "Повторная пауза должна вернуть исходный Paused=" + paused);
        if (bench.CompareRoutes != compared) bench.ToggleCompare();
        if (bench.Paused != paused) bench.TogglePause();
        var helpRoot = (GameObject)Read(hud, "helpWindow");
        bool helpWasOpen = helpRoot.activeSelf;
        ((WorkbenchButton)Read(hud, "help")).onClick.Invoke();
        Expect(report, helpRoot.activeSelf && bench.HelpOpen, "Кнопка справки должна открыть окно и заблокировать ввод стенда");
        ((WorkbenchButton)Read(hud, "closeHelp")).onClick.Invoke();
        Expect(report, !helpRoot.activeSelf && !bench.HelpOpen, "Кнопка закрытия справки должна закрыть окно и освободить ввод");
        hud.ShowHelp(helpWasOpen);

        return JsonUtility.ToJson(report);
    }
    public static string CheckHudReenable()
    {
        var hud = UnityEngine.Object.FindFirstObjectByType<WorkbenchHud>();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (!Application.isPlaying || hud == null || bench == null || bench.Rebuilding) return "Стенд не готов к проверке включения HUD";
        var report = new Report();
        bool helpWasOpen = bench.HelpOpen;
        hud.ShowHelp(true);
        hud.enabled = false;
        bench.SetHelpOpen(false);
        hud.enabled = true;
        Expect(report, bench.HelpOpen, "HUD должен восстановить HelpOpen из открытого окна после включения");
        hud.ShowHelp(helpWasOpen);
        for (int i = 0; i < 3; i++) { hud.enabled = false; hud.enabled = true; }
        var clicks = JsonUtility.FromJson<Report>(CheckHudClicks());
        report.passed += clicks.passed;
        report.failures.AddRange(clicks.failures);

        return JsonUtility.ToJson(report);
    }
    private static object Read(object instance, string field) => instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
    private static void Expect(Report report, bool value, string failure) { if (value) report.passed++; else report.failures.Add(failure); }
}
