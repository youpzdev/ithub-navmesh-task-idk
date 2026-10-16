using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class WorkbenchContactScenarioChecks
{
    [Serializable]
    private class Sample
    {
        public int frame;
        public float elapsed;
        public float distance;
        public float facing;
        public float health;
        public string state;
    }

    [Serializable]
    private class Report
    {
        public bool running;
        public bool restored;
        public bool passed;
        public string stage;
        public int frames;
        public int contactFrames;
        public int detectionFailures;
        public int facingFailures;
        public float healthLost;
        public float expectedHealthLost;
        public List<string> failures = new();
        public List<Sample> snapshots = new();
    }

    private class GuardState
    {
        public WorkbenchEnemy enemy;
        public NavMeshAgent agent;
        public bool enabled;
        public bool active;
        public bool threat;
        public Vector3 position;
        public Quaternion rotation;
    }

    private static Report report = new();
    private static NavMeshWorkbench bench;
    private static WorkbenchActor actor;
    private static WorkbenchEnemy guard;
    private static NavMeshAgent guardAgent;
    private static List<GuardState> guards;
    private static WorkbenchMode originalMode;
    private static bool originalPaused;
    private static bool originalEnabled;
    private static bool originalActorPaused;
    private static bool originalActorStopped;
    private static bool originalEditorPaused;
    private static float originalScale;
    private static float originalSpeed;
    private static float originalHealth;
    private static Vector3 originalPosition;
    private static Quaternion originalRotation;
    private static Vector3 modePosition;
    private static Quaternion modeRotation;
    private static float modeHealth;
    private static bool prepared;
    private static float startedAt;
    private static float nextSample;
    private static int lastFrame;
    private static double deadline;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Begin()
    {
        if (report.running) return Result();
        report = new Report();
        bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (!Application.isPlaying || !bench || bench.Rebuilding || bench.Actor.Dead)
        {
            report.failures.Add("Ожидались Play Mode, готовый NavMesh и живой агент");
            return Result();
        }
        actor = bench.Actor;
        originalMode = bench.Mode;
        originalPaused = bench.Paused;
        originalEnabled = bench.enabled;
        originalScale = Time.timeScale;
        originalEditorPaused = EditorApplication.isPaused;
        originalSpeed = bench.SimulationSpeed;
        originalActorPaused = Read<bool>(actor, "paused");
        originalActorStopped = actor.Agent.isStopped;
        originalPosition = actor.transform.position;
        originalRotation = actor.transform.rotation;
        originalHealth = actor.Health;
        guards = new List<GuardState>();
        foreach (var enemy in Read<WorkbenchEnemy[]>(bench, "enemies"))
            guards.Add(new GuardState { enemy = enemy, agent = Read<NavMeshAgent>(enemy, "agent"), enabled = enemy.enabled,
                active = enemy.gameObject.activeSelf, threat = enemy.ThreatEnabled, position = enemy.transform.position, rotation = enemy.transform.rotation });
        if (guards.Count == 0) { report.failures.Add("В сцене нет врагов"); return Result(); }

        prepared = false;
        report.running = true;
        report.stage = "WaitingForBake";
        deadline = EditorApplication.timeSinceStartup + 25;
        EditorApplication.isPaused = false;
        if (bench.Paused) bench.TogglePause();
        bench.SetSpeed(1);
        bench.enabled = true;
        bench.SetMode((int)WorkbenchMode.Strategies);
        EditorApplication.update += Tick;

        return Result();
    }

    public static string Result() => JsonUtility.ToJson(report);

    public static string Cancel()
    {
        if (report.running) { report.failures.Add("Проверка отменена"); BeginRestore(); }

        return Result();
    }

    private static void Tick()
    {
        try
        {
            if (!Application.isPlaying || !bench) { report.failures.Add("Play Mode завершился до восстановления"); Finish(false); return; }
            if (EditorApplication.timeSinceStartup > deadline)
            {
                report.failures.Add("Превышен предел ожидания текущей стадии " + report.stage);
                if (report.stage == "Restoring") { RestorePoses(); Finish(false); }
                else BeginRestore();
                return;
            }
            if (report.stage == "WaitingForBake")
            {
                if (bench.Rebuilding) return;
                Prepare();
                return;
            }
            if (report.stage == "Restoring")
            {
                if (bench.Rebuilding) return;
                RestorePoses();
                Finish(true);
                return;
            }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            float elapsed = Time.time - startedAt;
            float distance = Vector3.Distance(actor.transform.position, guard.transform.position);
            Vector3 direction = actor.transform.position - guard.transform.position;
            direction.y = 0;
            float facing = direction.sqrMagnitude > 0.0001f ? Vector3.Dot(guard.transform.forward, direction.normalized) : 1;
            report.frames++;
            if (distance <= bench.Settings.ContactRange - 0.01f)
            {
                report.contactFrames++;
                if (guard.State != EnemyAwareness.Detected) report.detectionFailures++;
                if (elapsed > 0.4f && facing < 0.5f) report.facingFailures++;
            }
            if (elapsed >= nextSample)
            {
                nextSample = elapsed + 0.1f;
                report.snapshots.Add(new Sample { frame = lastFrame, elapsed = elapsed, distance = distance, facing = facing, health = actor.Health, state = guard.State.ToString() });
            }
            if (elapsed < 3f) return;
            report.healthLost = bench.Settings.MaxHealth - actor.Health;
            report.expectedHealthLost = bench.Settings.HitDamage * 2;
            if (report.contactFrames == 0) report.failures.Add("Не наблюдалось реальных кадров контакта");
            if (report.detectionFailures > 0) report.failures.Add("Контакт не удерживал Detected: кадров " + report.detectionFailures);
            if (report.facingFailures > 0) report.failures.Add("Враг не повернулся к цели после 0.4с: кадров " + report.facingFailures);
            if (report.healthLost + 0.001f < report.expectedHealthLost)
                report.failures.Add($"Потеря здоровья {report.healthLost}, ожидалось не менее {report.expectedHealthLost}");
            BeginRestore();
        }
        catch (Exception error)
        {
            report.failures.Add(error.GetType().Name + ": " + error.Message);
            if (report.stage == "Restoring") Finish(false);
            else BeginRestore();
        }
    }

    private static void Prepare()
    {
        modePosition = actor.transform.position;
        modeRotation = actor.transform.rotation;
        modeHealth = actor.Health;
        bench.enabled = false;
        foreach (var state in guards) state.enemy.enabled = false;
        guard = guards[0].enemy;
        guardAgent = guards[0].agent;
        actor.SetPaused(false);
        actor.ResetAt(new Vector3(-2, 0, -0.95f), bench.Settings.MaxHealth);
        if (Vector3.Distance(actor.transform.position, new Vector3(-2, 0, -0.95f)) > 0.1f)
            throw new InvalidOperationException("Actor Warp(-2,0,-0.95) не выполнен");
        actor.Halt();
        actor.Agent.isStopped = true;
        if (!guardAgent.Warp(new Vector3(-2, 0, 0))) throw new InvalidOperationException("Guard Warp(-2,0,0) не выполнен");
        guard.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        guard.ThreatEnabled = true;
        guard.enabled = true;
        Physics.SyncTransforms();
        prepared = true;
        report.stage = "SamplingActualFrames";
        startedAt = Time.time;
        nextSample = 0;
        lastFrame = Time.frameCount;
    }

    private static void BeginRestore()
    {
        report.stage = "Restoring";
        deadline = EditorApplication.timeSinceStartup + 15;
        if (!bench || !Application.isPlaying) { Finish(false); return; }
        foreach (var state in guards) state.enemy.enabled = false;
        if (prepared)
        {
            actor.ResetAt(modePosition, modeHealth);
            actor.transform.rotation = modeRotation;
        }
        bench.enabled = true;
        bench.SetMode((int)originalMode);
    }

    private static void RestorePoses()
    {
        actor.ResetAt(originalPosition, originalHealth);
        actor.transform.rotation = originalRotation;
        actor.SetPaused(originalActorPaused);
        if (actor.Agent.isOnNavMesh) actor.Agent.isStopped = originalActorStopped;
        foreach (var state in guards)
        {
            state.enemy.enabled = false;
            if (state.agent.isOnNavMesh) state.agent.Warp(state.position);
            else state.enemy.transform.position = state.position;
            state.enemy.transform.rotation = state.rotation;
            state.enemy.ThreatEnabled = state.threat;
            state.enemy.gameObject.SetActive(state.active);
            state.enemy.enabled = state.enabled;
        }
        bench.SetSpeed(originalSpeed);
        if (bench.Paused != originalPaused) bench.TogglePause();
        bench.enabled = originalEnabled;
        Time.timeScale = originalScale;
        EditorApplication.isPaused = originalEditorPaused;
    }

    private static void Finish(bool restored)
    {
        EditorApplication.update -= Tick;
        report.restored = restored;
        report.running = false;
        report.passed = restored && report.frames > 0 && report.failures.Count == 0;
        report.stage = "Complete";
    }

    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
}
