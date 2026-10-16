using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

public class WorkbenchActor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorkbenchSettings settings;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform body;
    [SerializeField] private ParticleSystem stepDust;
    [SerializeField] private Transform healthBar;
    [SerializeField] private Transform healthFill;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;
    [SerializeField] private WorkbenchDamageView damagePrefab;
    [SerializeField] private Camera damageCamera;
    [SerializeField] private Renderer[] flashTargets = System.Array.Empty<Renderer>();

    private MaterialPropertyBlock[] flashBlocks;
    private Color[] restingColors;
    private Tween flash;
    private readonly List<WorkbenchDamageView> damageViews = new();

    [SerializeField] private Vector3[] corners;
    [SerializeField] private int cornerIndex;
    [SerializeField] private Vector3 bodyPosition;
    [SerializeField] private Vector3 bodyScale;
    [SerializeField] private Quaternion bodyRotation;
    [SerializeField] private Vector3 healthFillScale;

    private Sequence motion;
    private Tween knockback;
    private WorkbenchGait gait;

    private bool reacting;
    private bool blocked;
    [SerializeField] private bool paused;

    [field: SerializeField] public bool Arrived { get; private set; }
    [field: SerializeField] public bool Dead { get; private set; }
    [field: SerializeField] public float Health { get; private set; }
    public float LastDamage { get; private set; }
    public float FlashStrength { get; private set; }
    public NavMeshAgent Agent => agent;

    public void Configure(WorkbenchSettings config, NavMeshAgent navigation, Transform visual, ParticleSystem dust)
    {
        settings = config;
        agent = navigation;
        body = visual;
        stepDust = dust;
    }

    public void ConfigureHealthBar(Transform bar, Transform fill) { healthBar = bar; healthFill = fill; }

    public void ConfigureFeet(Transform left, Transform right)
    {
        leftFoot = left;
        rightFoot = right;
        gait?.ConfigureFeet(left, right);
    }

    public void ConfigureDamage(WorkbenchDamageView popup, Camera camera, Renderer[] renderers)
    {
        damagePrefab = popup; damageCamera = camera; flashTargets = renderers;
    }

    private void Start()
    {
        Pooling.Prewarm(damagePrefab.gameObject, 8);
    }

    private void Awake()
    {
        bodyPosition = body.localPosition;
        bodyScale = body.localScale;
        bodyRotation = body.localRotation;
        if (healthFill) healthFillScale = healthFill.localScale;
        Health = settings.MaxHealth;
        agent.speed = settings.Speed;
        UpdateHealthBar();
    }

    private void OnEnable()
    {
        flashBlocks = new MaterialPropertyBlock[flashTargets.Length];
        restingColors = new Color[flashTargets.Length];
        for (int i = 0; i < flashTargets.Length; i++)
        {
            flashBlocks[i] = new MaterialPropertyBlock();
            restingColors[i] = flashTargets[i].sharedMaterial.GetColor("_BaseColor");
        }
        gait = new WorkbenchGait(agent, body, settings);
        gait.ConfigureFeet(leftFoot, rightFoot);
        gait.ConfigureDust(stepDust);
        gait.SetPaused(paused);
        reacting = false;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = paused || Dead;
            if (!Dead && !Arrived && corners != null && cornerIndex < corners.Length) agent.SetDestination(corners[cornerIndex]);
        }
        UpdateHealthBar();
    }

    private void OnDisable()
    {
        ClearDamageFeedback();
        StopMotion();
        gait?.Dispose();
        gait = null;
        knockback?.Kill();
        reacting = false;
        if (agent.isOnNavMesh) agent.isStopped = true;
        if (stepDust) stepDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (Dead || paused || !agent.isOnNavMesh) return;
        if (corners != null && !Arrived && !agent.pathPending && !reacting)
        {
            float distance = Vector3.Distance(transform.position, corners[cornerIndex]);
            if (distance <= settings.ArrivalDistance)
            {
                cornerIndex++;
                if (cornerIndex >= corners.Length) { Arrived = true; agent.ResetPath(); }
                else agent.SetDestination(corners[cornerIndex]);
            }
        }

        if (!reacting) gait.Tick(corners != null && !Arrived);
    }

    public void Follow(Vector3[] path)
    {
        if (Dead || path == null || path.Length == 0 || !agent.isOnNavMesh) return;
        corners = path;
        cornerIndex = path.Length > 1 ? 1 : 0;
        Arrived = Vector3.Distance(transform.position, path[^1]) <= settings.ArrivalDistance;
        blocked = false;
        agent.isStopped = paused;
        if (!Arrived) agent.SetDestination(corners[cornerIndex]);
        else agent.ResetPath();
    }

    public void Halt()
    {
        corners = null;
        Arrived = false;
        if (agent.isOnNavMesh) agent.ResetPath();
        if (!reacting) gait.Stop();
    }

    public void ResetAt(Vector3 position, float health)
    {
        ClearDamageFeedback();
        Halt();
        knockback?.Kill();
        StopMotion();
        Dead = false;
        reacting = false;
        blocked = false;
        body.gameObject.SetActive(true);
        if (leftFoot) leftFoot.gameObject.SetActive(true);
        if (rightFoot) rightFoot.gameObject.SetActive(true);
        if (!agent.Warp(position)) Debug.LogError($"Не удалось вернуть {name} на NavMesh в {position}", this);
        SetHealth(health);
        gait.Resume();
    }

    public void SetHealth(float value)
    {
        Health = Mathf.Clamp(value, 0, settings.MaxHealth);
        UpdateHealthBar();
        if (Health <= 0 && !Dead) Die();
    }

    public void TakeHit(Vector3 from)
    {
        if (Dead || paused) return;
        Vector3 direction = transform.position - from;
        direction.y = 0;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : -transform.forward;
        float before = Health;
        SetHealth(Health - settings.HitDamage);
        LastDamage = before - Health;
        if (LastDamage > 0) ShowDamageFeedback(LastDamage);
        WorkbenchEffects.Emit(transform.position + Vector3.up, settings.AgentColor, 18);
        WorkbenchEffects.Shake(0.12f);
        Audio.Play(settings.HitSound, transform.position, 0.65f);
        if (Dead) return;

        StopMotion();
        knockback?.Kill();
        reacting = true;
        Vector3 localDirection = transform.InverseTransformDirection(direction);
        Vector3 tilt = new(localDirection.z * 28, 0, -localDirection.x * 28);
        motion = DOTween.Sequence().Append(body.DOLocalRotate(tilt, settings.HitReactionDuration * 0.3f))
            .Join(body.DOScale(Vector3.Scale(bodyScale, new Vector3(1.2f, 0.75f, 1.2f)), settings.HitReactionDuration * 0.3f))
            .Append(body.DOLocalRotateQuaternion(bodyRotation, settings.HitReactionDuration * 0.7f).SetEase(Ease.OutBack))
            .Join(body.DOScale(bodyScale, settings.HitReactionDuration * 0.7f)).OnComplete(FinishReaction);

        float travelled = 0;
        knockback = DOVirtual.Float(0, settings.Knockback, settings.HitReactionDuration, distance =>
        {
            if (!agent.isOnNavMesh) return;
            Vector3 start = agent.nextPosition;
            Vector3 target = start + direction * (distance - travelled);
            if (NavMesh.Raycast(start, target, out NavMeshHit hit, agent.areaMask)) target = Vector3.MoveTowards(hit.position, start, 0.05f);
            agent.Move(target - start);
            travelled = distance;
        }).SetEase(Ease.OutQuad);
    }

    public void Celebrate()
    {
        if (Dead) return;
        StopMotion();
        reacting = true;
        motion = DOTween.Sequence().Append(body.DOLocalMoveY(bodyPosition.y + settings.ArrivalJump, settings.AnimationPeriod).SetEase(Ease.OutQuad))
            .Join(body.DOLocalRotate(new Vector3(0, 0, 15), settings.AnimationPeriod))
            .Append(body.DOLocalMove(bodyPosition, settings.AnimationPeriod).SetEase(Ease.InQuad))
            .Join(body.DOLocalRotateQuaternion(bodyRotation, settings.AnimationPeriod)).OnComplete(FinishReaction);
        WorkbenchEffects.Emit(transform.position, settings.HiddenColor, 20);
        Audio.Play(settings.ArriveSound, transform.position, 0.45f);
    }

    public void Blocked()
    {
        if (blocked || Dead) return;
        Halt();
        blocked = true;
        StopMotion();
        reacting = true;
        motion = DOTween.Sequence().Append(body.DOPunchRotation(new Vector3(0, 0, 22), settings.HitReactionDuration, 4, 0.5f)).OnComplete(FinishReaction);
    }

    public void SetPaused(bool value)
    {
        paused = value;
        if (agent.isOnNavMesh) agent.isStopped = value;
        if (value) { motion?.Pause(); knockback?.Pause(); }
        else { motion?.Play(); knockback?.Play(); }
        gait.SetPaused(value);
    }

    private void StopMotion()
    {
        gait?.Suspend(true);
        motion?.Kill();
        body.localPosition = bodyPosition;
        body.localScale = bodyScale;
        body.localRotation = bodyRotation;
    }

    private void FinishReaction() { reacting = false; gait.Resume(); }

    private void Die()
    {
        Dead = true;
        Halt();
        StopMotion();
        knockback?.Kill();
        body.gameObject.SetActive(false);
        if (leftFoot) leftFoot.gameObject.SetActive(false);
        if (rightFoot) rightFoot.gameObject.SetActive(false);
        WorkbenchEffects.Scatter(transform.position + Vector3.up, settings.AgentColor);
        WorkbenchEffects.Shake(0.23f);
    }

    private void ShowDamageFeedback(float damage)
    {
        flash?.Kill();
        ApplyFlash(1);
        flash = DOVirtual.Float(1, 0, 0.12f, ApplyFlash).SetDelay(0.06f).SetEase(Ease.OutQuad);
        var popup = Pooling.Instantiate(damagePrefab.gameObject, transform.position + Vector3.up * 2.8f).GetComponent<WorkbenchDamageView>();
        damageViews.Add(popup);
        popup.Show(damage, damageCamera, this);
    }

    private void ApplyFlash(float strength)
    {
        FlashStrength = strength;
        for (int i = 0; i < flashTargets.Length; i++)
        {
            flashTargets[i].GetPropertyBlock(flashBlocks[i]);
            flashBlocks[i].SetColor("_BaseColor", Color.Lerp(restingColors[i], Color.white, strength));
            flashBlocks[i].SetFloat("_HitFlash", strength);
            flashTargets[i].SetPropertyBlock(flashBlocks[i]);
        }
    }

    public void ForgetDamageView(WorkbenchDamageView popup) { damageViews.Remove(popup); }

    private void ClearDamageFeedback()
    {
        flash?.Kill(); flash = null;
        if (flashBlocks != null) ApplyFlash(0);
        for (int i = damageViews.Count - 1; i >= 0; i--) if (damageViews[i]) damageViews[i].Release();
        damageViews.Clear();
    }

    private void UpdateHealthBar()
    {
        if (!healthBar) return;
        healthBar.gameObject.SetActive(Health > 0 && Health < settings.MaxHealth);
        if (healthFill) healthFill.localScale = Vector3.Scale(healthFillScale, new Vector3(Health / settings.MaxHealth, 1, 1));
    }
}
