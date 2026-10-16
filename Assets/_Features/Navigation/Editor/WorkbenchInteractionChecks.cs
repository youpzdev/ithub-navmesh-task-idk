using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class WorkbenchInteractionChecks
{
    [Serializable]
    public class Report { public int passed; public List<string> failures = new(); }

    public static string Check()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (bench == null || !Application.isPlaying || bench.Rebuilding) return "Стенд не готов к проверке";
        if (!bench.Paused) bench.TogglePause();
        bench.SetTool(0);
        var items = Read<List<WorkbenchItem>>(bench, "points");
        int selected = items.Count - 2;
        var camera = Camera.main;
        Vector3 screen = camera.WorldToScreenPoint(items[selected].transform.position + Vector3.up * 0.3f);
        Click(bench, screen, true); Click(bench, screen, false);
        Expect(report, Read<int>(bench, "targetIndex") == selected, "Клик по POI: выбран индекс " + Read<int>(bench, "targetIndex") + ", ожидался " + selected);

        bench.Actor.Agent.Warp(new Vector3(-10, 0, -6));
        bench.SetStrategy(0);
        bench.ReturnToStart();
        var lines = Read<LineRenderer[]>(bench, "routeLines");
        var line = lines[0];
        float lineDistance = line.positionCount == 0 ? float.PositiveInfinity : Vector3.Distance(line.GetPosition(0), bench.Actor.transform.position + Vector3.up * 0.1f);
        Expect(report, lineDistance < 0.2f, "Возврат на паузе: начало линии отстоит на " + lineDistance + ", ожидалось <0.2");

        int count = bench.PointCount;
        int history = bench.UndoCount;
        bool added = bench.AddPoint(new Vector3(-11, 0, 0));
        Expect(report, added && bench.PointCount == count + 1 && bench.UndoCount == history + 1, "Добавление точки должно записать одно действие отмены");
        bench.Undo();
        Expect(report, bench.PointCount == count, "Отмена добавления точки: количество=" + bench.PointCount + ", ожидалось " + count);

        return JsonUtility.ToJson(report);
    }

    public static string CheckThreatViews()
    {
        var report = new Report();
        foreach (var view in UnityEngine.Object.FindObjectsByType<WorkbenchThreatView>(FindObjectsSortMode.None))
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var color = view.GetType().GetField("color", flags).GetValue(view);
            var filter = (MeshFilter)view.GetType().GetField("view", flags).GetValue(view);
            Expect(report, color != null, view.name + ": MaterialPropertyBlock должен быть создан перед LateUpdate");
            Expect(report, filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0, view.name + ": сектор обзора должен содержать вершины");
        }

        return JsonUtility.ToJson(report);
    }

    public static string CheckHelpInput()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        var hud = UnityEngine.Object.FindFirstObjectByType<WorkbenchHud>();
        hud.ShowHelp(true);
        bool paused = bench.Paused;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Space)); InputSystem.Update();
        typeof(NavMeshWorkbench).GetMethod("HandleInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bench, null);
        Expect(report, bench.Paused == paused, "Space под справкой не должен менять паузу стенда");
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); InputSystem.Update();
        hud.ShowHelp(false);

        return JsonUtility.ToJson(report);
    }

    private static void Click(NavMeshWorkbench bench, Vector3 position, bool down)
    {
        var state = new MouseState { position = position };
        if (down) state = state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(Mouse.current, state); InputSystem.Update();
        typeof(NavMeshWorkbench).GetMethod("HandleInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bench, null);
    }

    private static T Read<T>(NavMeshWorkbench bench, string name) => (T)typeof(NavMeshWorkbench).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bench);
    private static void Expect(Report report, bool ok, string failure) { if (ok) report.passed++; else report.failures.Add(failure); }
}
