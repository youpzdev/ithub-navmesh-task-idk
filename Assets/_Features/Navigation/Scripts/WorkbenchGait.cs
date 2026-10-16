using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

public class WorkbenchGait
{
    private readonly NavMeshAgent agent;
    private readonly Transform body;
    private readonly WorkbenchSettings settings;
    private readonly Vector3 restPosition;
    private readonly Quaternion restRotation;
    private readonly Vector3 restScale;

    private Transform leftFoot;
    private Transform rightFoot;
    private Vector3 leftRest;
    private Vector3 rightRest;
    private ParticleSystem dust;

    private Tween blend;

    private float phase;
    private float weight;
    private Vector3 lastPosition;

    private bool walking;
    private bool suspended;
    private bool paused;

    public WorkbenchGait(NavMeshAgent navigation, Transform visual, WorkbenchSettings config)
    {
        agent = navigation;
        body = visual;
        settings = config;
        restPosition = body.localPosition;
        restRotation = body.localRotation;
        restScale = body.localScale;
        lastPosition = agent.transform.position;
    }

    public void Tick(bool canWalk = true)
    {
        Vector3 position = agent.transform.position;
        Vector3 displacement = position - lastPosition;
        displacement.y = 0;
        lastPosition = position;
        if (suspended || paused || Time.deltaTime <= 0) return;
        float distance = canWalk ? displacement.magnitude : 0;
        float maximumStep = Mathf.Max(agent.speed, settings.Speed) * Time.deltaTime * 2 + 0.25f;
        if (distance > maximumStep) distance = 0;
        if (distance > 0.00001f && !walking) SetWalking(true);
        else if (distance <= 0.00001f && walking) SetWalking(false);
        if (distance <= 0) return;
        float previousPhase = phase;
        float advancedPhase = phase + distance * Mathf.PI / Mathf.Max(settings.Speed * settings.AnimationPeriod, 0.0001f);
        phase = Mathf.Repeat(advancedPhase, Mathf.PI * 2);
        ApplyPose();
        if (weight < 0.15f) return;
        int firstLanding = Mathf.FloorToInt(previousPhase / Mathf.PI) + 1;
        int lastLanding = Mathf.FloorToInt(advancedPhase / Mathf.PI);
        for (int landing = firstLanding; landing <= lastLanding; landing++) EmitFootfall(landing % 2 == 0 ? leftFoot : rightFoot);
    }

    public void ConfigureFeet(Transform left, Transform right)
    {
        leftFoot = left;
        rightFoot = right;
        if (left) leftRest = left.localPosition;
        if (right) rightRest = right.localPosition;
    }

    public void ConfigureDust(ParticleSystem particles) { dust = particles; }

    public void Stop() { lastPosition = agent.transform.position; SetWalking(false); }

    public void Suspend(bool reset = false)
    {
        suspended = true;
        walking = false;
        lastPosition = agent.transform.position;
        blend?.Kill();
        if (reset) ResetPose();
    }

    public void Resume()
    {
        suspended = false;
        weight = 0;
        lastPosition = agent.transform.position;
    }

    public void SetPaused(bool value)
    {
        paused = value;
        lastPosition = agent.transform.position;
        if (value) blend?.Pause();
        else if (!suspended) blend?.Play();
    }

    public void Dispose()
    {
        blend?.Kill();
        blend = null;
        walking = false;
        suspended = false;
        weight = 0;
        lastPosition = agent.transform.position;
        ResetPose();
    }

    private void SetWalking(bool value)
    {
        if (walking == value) return;
        walking = value;
        blend?.Kill();
        blend = DOTween.To(() => weight, progress => { weight = progress; ApplyPose(); }, value ? 1f : 0f, 0.15f).SetEase(Ease.InOutSine);
    }

    private void ApplyPose()
    {
        if (suspended) return;
        float step = Mathf.Sin(phase);
        float bounce = Mathf.Clamp(settings.BounceHeight, 0, 0.035f) * step * step * weight;
        float roll = Mathf.Clamp(settings.SwayAngle, 0, 4) * step * weight;
        body.localPosition = restPosition + new Vector3(-step * 0.025f * weight, bounce, 0);
        body.localRotation = restRotation * Quaternion.Euler(0, 0, roll);
        ApplyFoot(leftFoot, leftRest, phase);
        ApplyFoot(rightFoot, rightRest, phase + Mathf.PI);
    }

    private void ApplyFoot(Transform foot, Vector3 rest, float footPhase)
    {
        if (!foot) return;
        float cyclePosition = Mathf.Repeat(footPhase / (Mathf.PI * 2), 1);
        float stride = settings.Speed * settings.AnimationPeriod * 0.5f;
        float forward;
        float lift = 0;
        if (cyclePosition < 0.5f) forward = Mathf.Lerp(stride, -stride, cyclePosition * 2);
        else
        {
            float swing = (cyclePosition - 0.5f) * 2;
            forward = Mathf.Lerp(-stride, stride, Mathf.SmoothStep(0, 1, swing));
            lift = Mathf.Sin(swing * Mathf.PI) * 0.11f;
        }
        foot.localPosition = rest + new Vector3(0, lift * weight, forward * weight);
    }

    private void EmitFootfall(Transform foot)
    {
        if (!dust || !foot) return;
        Vector3 point = foot.position - agent.transform.forward * settings.FootDustDistance + agent.transform.right * Mathf.Sign(foot.localPosition.x) * 0.12f + Vector3.up * 0.03f;
        Vector3 velocity = -agent.transform.forward * 0.7f + Vector3.up * 0.45f;
        if (dust.main.simulationSpace != ParticleSystemSimulationSpace.World)
        {
            point = dust.transform.InverseTransformPoint(point);
            velocity = dust.transform.InverseTransformDirection(velocity);
        }
        for (int i = 0; i < settings.FootDustCount; i++)
        {
            var spread = Random.insideUnitCircle * 0.45f;
            var emission = new ParticleSystem.EmitParams
            {
                position = point,
                velocity = velocity + new Vector3(spread.x, 0, spread.y),
                startColor = Color.white,
                startSize = Random.Range(settings.FootDustSize.x, settings.FootDustSize.y),
                applyShapeToPosition = false
            };
            dust.Emit(emission, 1);
        }
    }

    private void ResetPose()
    {
        body.localPosition = restPosition;
        body.localRotation = restRotation;
        body.localScale = restScale;
        if (leftFoot) leftFoot.localPosition = leftRest;
        if (rightFoot) rightFoot.localPosition = rightRest;
    }
}
