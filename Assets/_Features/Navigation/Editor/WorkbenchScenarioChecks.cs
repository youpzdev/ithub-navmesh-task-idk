using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class WorkbenchScenarioChecks
{
    [Serializable]
    public class Report { public bool finished; public int passed; public List<string> failures = new(); public List<string> errors = new(); }
    public static Report Result { get; private set; } = new();
    private static readonly Queue<(float delay, Action action)> steps = new();
    private static double next;
    private static NavMeshWorkbench bench;
    private static Vector3 pausedPosition;
    private static int count;
    private static float healthBefore;

    public static string Begin()
    {
        bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (!EditorApplication.isPlaying || bench == null) return "Сначала запусти сцену";
        Result = new Report(); steps.Clear(); EditorApplication.update -= Tick; Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        Add(0, () => { if (bench.Paused) bench.TogglePause(); bench.SetMode(1); bench.ResetMode(); });
        Add(1, () => { Expect(!bench.Rebuilding, "NavMesh обновился после смены режима"); Expect(bench.PlaceObstacle(new Vector3(-8, 0, -7), 0), "Постановка блока"); });
        Add(1, () => { Expect(!bench.Rebuilding, "NavMesh обновился после установки"); Expect(!NavMesh.SamplePosition(new Vector3(-8, 0, -7), out _, 0.15f, NavMesh.AllAreas), "Блок исключил пол под собой из NavMesh"); bench.Undo(); });
        Add(1, () => { Expect(NavMesh.SamplePosition(new Vector3(-8, 0, -7), out _, 0.15f, NavMesh.AllAreas), "Отмена восстановила NavMesh"); bench.SetMode(2); });
        Add(1, () => { count = bench.PointCount; Expect(bench.AddPoint(new Vector3(-11, 0, 0)), "Добавление POI"); bench.SetMode(0); });
        Add(1, () => bench.SetMode(2));
        Add(1, () => { Expect(bench.PointCount == count + 1, "Смена режима сохранила POI"); bench.Undo(); });
        Add(1, () => { Expect(bench.PointCount == count, "Отмена после возврата в режим"); if (!bench.Paused) bench.TogglePause(); pausedPosition = bench.Actor.transform.position; });
        Add(0.3f, () => { Expect(Vector3.Distance(pausedPosition, bench.Actor.transform.position) < 0.005f, "Пауза остановила навигацию"); bench.SetSpeed(2); Expect(Time.timeScale == 0, "Выбор скорости не отменил паузу"); bench.TogglePause(); Expect(Mathf.Approximately(Time.timeScale, 2), "Продолжение применило скорость 2x"); bench.SetSpeed(1); });
        Add(0.2f, () => { bench.SetSituation(1); bench.Actor.Halt(); bench.Actor.Agent.Warp(new Vector3(-2, 0, 0)); var guards = UnityEngine.Object.FindObjectsByType<WorkbenchEnemy>(FindObjectsSortMode.None); var guard = guards[0]; var nav = guard.GetComponent<NavMeshAgent>(); nav.Warp(new Vector3(-2, 0, -0.9f)); guard.transform.rotation = Quaternion.identity; healthBefore = bench.Health; });
        Add(0.6f, () => { Expect(bench.Health < healthBefore, "Настоящий контакт врага нанес урон"); for (int i = 0; i < 8; i++) bench.Actor.TakeHit(bench.Actor.transform.position + Vector3.back); Expect(bench.Actor.Dead, "Контактный урон привел к гибели"); });
        Add(1.8f, () => { Expect(!bench.Actor.Dead && Mathf.Approximately(bench.Health, bench.Settings.MaxHealth), "Возврат восстановил здоровье"); Expect(bench.Paused, "После гибели ожидается Продолжить"); Expect(bench.PointCount == count, "Гибель сохранила POI"); bench.SetSituation(0); bench.ResetMode(); });
        Add(1, () => { if (bench.Paused) bench.TogglePause(); bench.SetMode(0); });
        next = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;

        return "Scenario checks started";
    }

    private static void Add(float delay, Action action) { steps.Enqueue((delay, action)); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(); return; }
        if (EditorApplication.timeSinceStartup < next) return;
        if (steps.Count == 0) { Finish(); return; }
        var step = steps.Dequeue();
        try { step.action(); } catch (Exception exception) { Result.failures.Add(exception.GetType().Name + ": " + exception.Message); }
        next = EditorApplication.timeSinceStartup + (steps.Count == 0 ? 0 : steps.Peek().delay);
    }
    private static void Expect(bool condition, string operation) { if (condition) Result.passed++; else Result.failures.Add(operation); }
    private static void Log(string condition, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Result.errors.Add(condition); }
    private static void Finish() { Result.finished = true; EditorApplication.update -= Tick; Application.logMessageReceived -= Log; }
}
