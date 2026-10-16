using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class WorkbenchGaitSpeedChecks
{
    [Serializable]
    private class CaseResult
    {
        public float timeScale;
        public float agentSpeed;
        public float distance;
        public float phaseTravel;
        public float expectedPhasePerMeter;
        public float actualPhasePerMeter;
        public float relativeError;
        public float idlePhaseTravel;
        public float peakSpeed;
        public int actualFrames;
        public int backwardPhaseFrames;
        public bool passed;
    }

    [Serializable]
    private class Report
    {
        public bool running;
        public bool restored;
        public bool passed;
        public string stage;
        public List<CaseResult> cases = new();
        public List<string> failures = new();
    }

    [Serializable]
    private class DustReport
    {
        public bool validSetup;
        public bool passed;
        public bool mesh;
        public bool fade;
        public int particles;
        public int whiteParticles;
        public int freshParticles;
        public int freshNearFeet;
        public int correctlySized;
    }

    public static string CheckDust()
    {
        var result = new DustReport();
        var npc = UnityEngine.Object.FindFirstObjectByType<WorkbenchActor>();
        if (!Application.isPlaying || !npc) return JsonUtility.ToJson(result);
        var dust = Read<ParticleSystem>(npc, "stepDust");
        var config = Read<WorkbenchSettings>(npc, "settings");
        var left = Read<Transform>(npc, "leftFoot");
        var right = Read<Transform>(npc, "rightFoot");
        if (!dust || !left || !right) return JsonUtility.ToJson(result);
        var particles = new ParticleSystem.Particle[dust.main.maxParticles];
        int count = dust.GetParticles(particles);
        result.validSetup = true;
        result.mesh = dust.GetComponent<ParticleSystemRenderer>().renderMode == ParticleSystemRenderMode.Mesh;
        var colors = dust.colorOverLifetime;
        result.fade = colors.enabled && colors.color.Evaluate(0).a < 0.01f && colors.color.Evaluate(1).a < 0.01f
            && colors.color.Evaluate(0.3f).a > 0.1f;
        result.particles = count;
        for (int i = 0; i < count; i++)
        {
            var particle = particles[i];
            Color color = particle.startColor;
            if (color.r >= 0.97f && color.g >= 0.97f && color.b >= 0.97f) result.whiteParticles++;
            if (particle.startSize >= config.FootDustSize.x - 0.001f && particle.startSize <= config.FootDustSize.y + 0.001f) result.correctlySized++;
            if (particle.startLifetime - particle.remainingLifetime > 0.06f) continue;
            result.freshParticles++;
            Vector3 position = dust.main.simulationSpace == ParticleSystemSimulationSpace.World ? particle.position : dust.transform.TransformPoint(particle.position);
            if (Mathf.Min(Vector3.Distance(position, left.position), Vector3.Distance(position, right.position)) <= config.FootDustDistance + 0.2f) result.freshNearFeet++;
        }
        result.passed = result.mesh && result.fade && count > 0 && result.whiteParticles == count && result.correctlySized == count
            && result.freshParticles > 0 && result.freshNearFeet == result.freshParticles;

        return JsonUtility.ToJson(result);
    }

    private static Report report = new();
    private static NavMeshWorkbench bench;
    private static WorkbenchActor actor;
    private static WorkbenchGait gait;
    private static WorkbenchEnemy[] enemies;
    private static bool[] enemyEnabled;
    private static bool benchEnabled;
    private static bool benchPaused;
    private static bool actorPaused;
    private static bool actorStopped;
    private static bool editorPaused;
    private static Vector3 savedPosition;
    private static Quaternion savedRotation;
    private static Vector3[] savedPath;
    private static float savedHealth;
    private static float savedSpeed;
    private static float savedScale;
    private static CaseResult current;
    private static Vector3 lastPosition;
    private static float lastPhase;
    private static float stageStart;
    private static int lastFrame;
    private static int caseIndex;
    private static bool matrix;
    private static double deadline;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string BeginSpeedCap() => Begin(false);
    public static string BeginMatrix() => Begin(true);
    public static string Result() => JsonUtility.ToJson(report);

    private static string Begin(bool allCases)
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
        gait = Read<WorkbenchGait>(actor, "gait");
        if (gait == null) { report.failures.Add("У агента нет gait"); return Result(); }
        matrix = allCases;
        benchEnabled = bench.enabled;
        benchPaused = bench.Paused;
        actorPaused = Read<bool>(actor, "paused");
        actorStopped = actor.Agent.isStopped;
        editorPaused = EditorApplication.isPaused;
        savedScale = Time.timeScale;
        savedSpeed = actor.Agent.speed;
        savedHealth = actor.Health;
        savedPosition = actor.transform.position;
        savedRotation = actor.transform.rotation;
        savedPath = actor.Agent.hasPath ? actor.Agent.path.corners : null;
        enemies = Read<WorkbenchEnemy[]>(bench, "enemies");
        enemyEnabled = new bool[enemies.Length];
        for (int i = 0; i < enemies.Length; i++) { enemyEnabled[i] = enemies[i].enabled; enemies[i].enabled = false; }
        bench.enabled = false;
        if (bench.Paused) bench.TogglePause();
        EditorApplication.isPaused = false;
        report.running = true;
        deadline = EditorApplication.timeSinceStartup + 90;
        caseIndex = 0;
        EditorApplication.update += Tick;
        try { StartCase(); }
        catch (Exception error) { report.failures.Add(error.Message); Restore(); }

        return Result();
    }

    private static void StartCase()
    {
        float scale = matrix ? new[] { 0.5f, 1f, 2f }[caseIndex / 2] : 1f;
        float speed = matrix && caseIndex % 2 == 0 ? bench.Settings.Speed * 0.45f : bench.Settings.Speed * 2;
        current = new CaseResult { timeScale = scale, agentSpeed = speed, expectedPhasePerMeter = Mathf.PI / (bench.Settings.Speed * bench.Settings.AnimationPeriod) };
        report.cases.Add(current);
        actor.SetPaused(false);
        actor.ResetAt(new Vector3(-10, 0, -7), bench.Settings.MaxHealth);
        actor.Agent.speed = speed;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(actor.transform.position, new Vector3(-10, 0, 7), actor.Agent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
            throw new InvalidOperationException("Для проверки шага нужен полный реальный путь (-10,-7)-(-10,7)");
        actor.Follow(path.corners);
        Time.timeScale = scale;
        report.stage = "Warmup";
        stageStart = Time.time;
        lastFrame = Time.frameCount;
    }

    private static void Tick()
    {
        try
        {
            if (!Application.isPlaying || !actor) { report.failures.Add("Play Mode завершился до восстановления"); Finish(false); return; }
            if (EditorApplication.timeSinceStartup > deadline) { report.failures.Add("Превышено время проверки шага"); Restore(); return; }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            float elapsed = Time.time - stageStart;
            if (report.stage == "Warmup")
            {
                if (elapsed < 0.6f) return;
                report.stage = "Moving";
                stageStart = Time.time;
                lastPosition = actor.transform.position;
                lastPhase = Read<float>(gait, "phase");
                return;
            }
            float phase = Read<float>(gait, "phase");
            float phaseDelta = Mathf.DeltaAngle(lastPhase * Mathf.Rad2Deg, phase * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            if (report.stage == "Moving")
            {
                Vector3 displacement = actor.transform.position - lastPosition;
                displacement.y = 0;
                current.distance += displacement.magnitude;
                current.phaseTravel += phaseDelta;
                if (phaseDelta < -0.001f) current.backwardPhaseFrames++;
                current.peakSpeed = Mathf.Max(current.peakSpeed, actor.Agent.velocity.magnitude);
                current.actualFrames++;
                lastPosition = actor.transform.position;
                lastPhase = phase;
                if (elapsed < 1f) return;
                current.actualPhasePerMeter = current.phaseTravel / Mathf.Max(current.distance, 0.0001f);
                current.relativeError = Mathf.Abs(current.actualPhasePerMeter - current.expectedPhasePerMeter) / current.expectedPhasePerMeter;
                actor.Halt();
                actor.Agent.isStopped = true;
                report.stage = "Settling";
                stageStart = Time.time;
                return;
            }
            if (report.stage == "Settling")
            {
                if (elapsed < 0.3f) return;
                lastPhase = phase;
                report.stage = "Idle";
                stageStart = Time.time;
                return;
            }
            current.idlePhaseTravel += Mathf.Abs(phaseDelta);
            lastPhase = phase;
            if (elapsed < 0.3f) return;
            current.passed = current.distance > 0.5f && current.actualFrames > 5 && current.relativeError <= 0.1f && current.idlePhaseTravel <= 0.01f && current.backwardPhaseFrames == 0;
            if (!current.passed) report.failures.Add($"scale={current.timeScale} speed={current.agentSpeed}: phase/m={current.actualPhasePerMeter}, ожидалось {current.expectedPhasePerMeter}, idlePhase={current.idlePhaseTravel}");
            caseIndex++;
            if (matrix && caseIndex < 6) StartCase();
            else Restore();
        }
        catch (Exception error) { report.failures.Add(error.GetType().Name + ": " + error.Message); Restore(); }
    }

    public static string Cancel()
    {
        if (report.running) { report.failures.Add("Проверка отменена"); Restore(); }

        return Result();
    }

    private static void Restore()
    {
        EditorApplication.update -= Tick;
        if (!Application.isPlaying || !actor) { Finish(false); return; }
        actor.Agent.speed = savedSpeed;
        actor.ResetAt(savedPosition, savedHealth);
        actor.transform.rotation = savedRotation;
        if (savedPath != null && savedPath.Length > 0) actor.Follow(savedPath);
        actor.SetPaused(actorPaused);
        if (actor.Agent.isOnNavMesh) actor.Agent.isStopped = actorStopped;
        for (int i = 0; i < enemies.Length; i++) enemies[i].enabled = enemyEnabled[i];
        if (bench.Paused != benchPaused) bench.TogglePause();
        bench.enabled = benchEnabled;
        Time.timeScale = savedScale;
        EditorApplication.isPaused = editorPaused;
        Finish(true);
    }

    private static void Finish(bool restored)
    {
        EditorApplication.update -= Tick;
        report.restored = restored;
        report.running = false;
        report.stage = "Complete";
        report.passed = restored && report.cases.Count > 0 && report.failures.Count == 0;
    }

    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
}
