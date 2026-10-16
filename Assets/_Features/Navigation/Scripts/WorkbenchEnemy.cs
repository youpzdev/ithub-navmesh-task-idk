using UnityEngine;
using UnityEngine.AI;

public class WorkbenchEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorkbenchSettings settings;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform body;
    [SerializeField] private WorkbenchActor target;
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector3[] patrol;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;

    private int patrolIndex;

    private float nextAttack;
    private float nextPlan;

    private WorkbenchGait gait;
    private WorkbenchPerception perception;

    [field: SerializeField] public bool ThreatEnabled { get; set; } = true;
    public bool SeesTarget { get; private set; }
    public EnemyAwareness State => perception == null ? EnemyAwareness.Patrol : perception.State;

    public void Configure(WorkbenchSettings config, NavMeshAgent navigation, Transform visual, WorkbenchActor actor, Vector3[] points, LayerMask mask)
    {
        settings = config;
        agent = navigation;
        body = visual;
        target = actor;
        patrol = points;
        obstacleMask = mask;
    }

    public void ConfigureDust(ParticleSystem particles) { dust = particles; }

    public void ConfigureFeet(Transform left, Transform right)
    {
        leftFoot = left;
        rightFoot = right;
        gait?.ConfigureFeet(left, right);
    }

    private void OnEnable()
    {
        gait = new WorkbenchGait(agent, body, settings);
        gait.ConfigureFeet(leftFoot, rightFoot);
        gait.ConfigureDust(dust);
        agent.speed = settings.EnemySpeed;
        perception = new WorkbenchPerception(settings.SightRange, settings.SightAngle, settings.SearchDuration, obstacleMask, settings.ContactRange);
        nextPlan = 0;
        if (agent.isOnNavMesh) agent.isStopped = false;
    }

    private void OnDisable()
    {
        gait?.Dispose();
        gait = null;
        perception = null;
        SeesTarget = false;
        nextPlan = 0;
        if (agent.isOnNavMesh) agent.ResetPath();
        if (dust) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void ResetPatrol()
    {
        patrolIndex = 0; nextAttack = 0; nextPlan = 0;
        SeesTarget = false;
        perception = new WorkbenchPerception(settings.SightRange, settings.SightAngle, settings.SearchDuration, obstacleMask, settings.ContactRange);
        gait?.Dispose();
        gait = new WorkbenchGait(agent, body, settings);
        gait.ConfigureFeet(leftFoot, rightFoot);
        gait.ConfigureDust(dust);
        if (agent.isOnNavMesh) agent.ResetPath();
        if (patrol != null && patrol.Length > 0 && !agent.Warp(patrol[0]))
            Debug.LogError($"Не удалось вернуть {name} в патруль на NavMesh в {patrol[0]}", this);
        if (agent.isOnNavMesh) agent.isStopped = false;
        if (dust) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (Time.deltaTime <= 0 || !agent.isOnNavMesh) return;
        Vector3 difference = target.transform.position - transform.position;
        EnemyAwareness previous = perception.State;
        perception.Tick(transform.position, transform.forward, target.transform.position, ThreatEnabled, !target.Dead, Time.deltaTime);
        bool visible = perception.State == EnemyAwareness.Detected;
        if (previous != perception.State) nextPlan = 0;
        SeesTarget = visible;
        float stopDistance = Mathf.Min(settings.ContactRange * 0.9f, Mathf.Max(agent.radius + target.Agent.radius + 0.05f, settings.ContactRange * 0.8f));
        agent.stoppingDistance = visible ? stopDistance : 0.12f;
        agent.isStopped = visible && difference.sqrMagnitude <= stopDistance * stopDistance;
        agent.updateRotation = !visible;
        if (visible)
        {
            Vector3 facing = difference; facing.y = 0;
            if (facing.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), agent.angularSpeed * Time.deltaTime);
        }

        if (Time.time >= nextPlan)
        {
            nextPlan = Time.time + settings.ReplanInterval;
            if (SeesTarget) agent.SetDestination(target.transform.position);
            else if (perception.State == EnemyAwareness.Searching) agent.SetDestination(perception.LastSeen);
            else if (patrol != null && patrol.Length > 0)
            {
                if (Vector3.Distance(transform.position, patrol[patrolIndex]) <= settings.ArrivalDistance) patrolIndex = (patrolIndex + 1) % patrol.Length;
                agent.SetDestination(patrol[patrolIndex]);
            }
            else agent.ResetPath();
        }

        if (SeesTarget && difference.sqrMagnitude <= settings.ContactRange * settings.ContactRange && Vector3.Dot(transform.forward, difference.normalized) >= 0.5f && Time.time >= nextAttack)
        {
            nextAttack = Time.time + settings.AttackInterval;
            target.TakeHit(transform.position);
        }

        gait.Tick();
    }
}
