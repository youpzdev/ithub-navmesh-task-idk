using System;
using System.Reflection;
using UnityEngine;

public static class WorkbenchGaitChecks
{
    [Serializable]
    private class StrideReport
    {
        public bool validSetup;
        public bool passed;
        public float stanceDistance;
        public float halfPeriod;
        public float expectedSpeed;
        public float actualFootSpeed;
        public float relativeSlip;
        public float maxRelativeSlip = 0.1f;
    }

    public static string CheckStrideMatch()
    {
        var report = new StrideReport();
        var actor = UnityEngine.Object.FindFirstObjectByType<WorkbenchActor>();
        if (!Application.isPlaying || !actor) return JsonUtility.ToJson(report);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var gait = (WorkbenchGait)typeof(WorkbenchActor).GetField("gait", flags).GetValue(actor);
        var left = (Transform)typeof(WorkbenchActor).GetField("leftFoot", flags).GetValue(actor);
        var settings = (WorkbenchSettings)typeof(WorkbenchActor).GetField("settings", flags).GetValue(actor);
        if (gait == null || !left) return JsonUtility.ToJson(report);
        var weightField = typeof(WorkbenchGait).GetField("weight", flags);
        var applyFoot = typeof(WorkbenchGait).GetMethod("ApplyFoot", flags);
        Vector3 rest = (Vector3)typeof(WorkbenchGait).GetField("leftRest", flags).GetValue(gait);
        Vector3 originalPosition = left.localPosition;
        float originalWeight = (float)weightField.GetValue(gait);
        try
        {
            weightField.SetValue(gait, 1f);
            applyFoot.Invoke(gait, new object[] { left, rest, 0f });
            Vector3 front = left.localPosition;
            applyFoot.Invoke(gait, new object[] { left, rest, Mathf.PI });
            report.stanceDistance = Mathf.Abs(front.z - left.localPosition.z);
            report.halfPeriod = settings.AnimationPeriod;
            report.expectedSpeed = settings.Speed;
            report.actualFootSpeed = report.stanceDistance / report.halfPeriod;
            report.relativeSlip = Mathf.Abs(report.expectedSpeed - report.actualFootSpeed) / report.expectedSpeed;
            report.validSetup = true;
            report.passed = report.relativeSlip <= report.maxRelativeSlip;
        }
        finally
        {
            weightField.SetValue(gait, originalWeight);
            left.localPosition = originalPosition;
        }

        return JsonUtility.ToJson(report);
    }

    [Serializable]
    private class StopReport
    {
        public bool validSetup;
        public bool passed;
        public string operation;
        public float speed;
        public float positionDelta;
        public float rotationDelta;
        public float scaleDelta;
        public float maxPositionDelta = 0.015f;
        public float maxRotationDelta = 1f;
        public float maxScaleDelta = 0.001f;
    }

    [Serializable]
    private class FeetReport
    {
        public bool validSetup;
        public bool passed;
        public int samples;
        public int bothAirborne;
        public bool leftSupport;
        public bool rightSupport;
        public float leftMaxLift;
        public float rightMaxLift;
        public float leftMinStride;
        public float leftMaxStride;
        public float rightMinStride;
        public float rightMaxStride;
    }

    public static string CheckAlternatingFeet()
    {
        var report = new FeetReport();
        var actor = UnityEngine.Object.FindFirstObjectByType<WorkbenchActor>();
        if (!Application.isPlaying || !actor) return JsonUtility.ToJson(report);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var gait = (WorkbenchGait)typeof(WorkbenchActor).GetField("gait", flags).GetValue(actor);
        var left = (Transform)typeof(WorkbenchActor).GetField("leftFoot", flags).GetValue(actor);
        var right = (Transform)typeof(WorkbenchActor).GetField("rightFoot", flags).GetValue(actor);
        if (gait == null || !left || !right) return JsonUtility.ToJson(report);
        var phaseField = typeof(WorkbenchGait).GetField("phase", flags);
        var weightField = typeof(WorkbenchGait).GetField("weight", flags);
        var apply = typeof(WorkbenchGait).GetMethod("ApplyPose", flags);
        float phase = (float)phaseField.GetValue(gait);
        float weight = (float)weightField.GetValue(gait);
        Vector3 leftRest = (Vector3)typeof(WorkbenchGait).GetField("leftRest", flags).GetValue(gait);
        Vector3 rightRest = (Vector3)typeof(WorkbenchGait).GetField("rightRest", flags).GetValue(gait);
        report.validSetup = true;
        try
        {
            weightField.SetValue(gait, 1f);
            for (int i = 0; i < 32; i++)
            {
                phaseField.SetValue(gait, i * Mathf.PI * 2 / 32);
                apply.Invoke(gait, null);
                Vector3 l = left.localPosition - leftRest;
                Vector3 r = right.localPosition - rightRest;
                if (l.y > 0.005f && r.y > 0.005f) report.bothAirborne++;
                if (l.y < 0.005f && r.y > 0.05f) report.leftSupport = true;
                if (r.y < 0.005f && l.y > 0.05f) report.rightSupport = true;
                report.leftMaxLift = Mathf.Max(report.leftMaxLift, l.y);
                report.rightMaxLift = Mathf.Max(report.rightMaxLift, r.y);
                report.leftMinStride = Mathf.Min(report.leftMinStride, l.z);
                report.leftMaxStride = Mathf.Max(report.leftMaxStride, l.z);
                report.rightMinStride = Mathf.Min(report.rightMinStride, r.z);
                report.rightMaxStride = Mathf.Max(report.rightMaxStride, r.z);
                report.samples++;
            }
        }
        finally
        {
            phaseField.SetValue(gait, phase);
            weightField.SetValue(gait, weight);
            apply.Invoke(gait, null);
        }
        report.passed = report.bothAirborne == 0 && report.leftSupport && report.rightSupport
            && report.leftMaxLift >= 0.095f && report.rightMaxLift >= 0.095f
            && report.leftMinStride <= -0.15f && report.rightMinStride <= -0.15f
            && report.leftMaxStride >= 0.15f && report.rightMaxStride >= 0.15f;

        return JsonUtility.ToJson(report);
    }

    public static string CheckStopContinuity()
    {
        var report = new StopReport { operation = "Play Mode, moving actor, Halt continuity" };
        var actor = UnityEngine.Object.FindFirstObjectByType<WorkbenchActor>();
        if (!Application.isPlaying || !actor || actor.Dead || actor.Agent.velocity.magnitude <= 0.2f)
            return JsonUtility.ToJson(report);

        var body = (Transform)typeof(WorkbenchActor).GetField("body", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
        Vector3 position = body.localPosition;
        Quaternion rotation = body.localRotation;
        Vector3 scale = body.localScale;
        report.validSetup = true;
        report.speed = actor.Agent.velocity.magnitude;
        actor.Halt();
        report.positionDelta = Vector3.Distance(position, body.localPosition);
        report.rotationDelta = Quaternion.Angle(rotation, body.localRotation);
        report.scaleDelta = Vector3.Distance(scale, body.localScale);
        report.passed = report.positionDelta <= report.maxPositionDelta && report.rotationDelta <= report.maxRotationDelta
            && report.scaleDelta <= report.maxScaleDelta;

        return JsonUtility.ToJson(report);
    }
}
