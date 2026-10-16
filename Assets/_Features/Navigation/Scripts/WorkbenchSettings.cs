using UnityEngine;

public enum RouteStrategy { Shortest, Safe, Hidden }
public enum WorkbenchMode { Patrol, Obstacles, Strategies }
public enum EditTool { Select, Block, Wall, Point }

public class WorkbenchSettings : ScriptableObject
{
    [Header("Navigation")]
    [SerializeField] private float speed = 3.2f;
    [SerializeField] private float enemySpeed = 1.5f;
    [SerializeField] private float arrivalDistance = 0.28f;
    [SerializeField] private float pointWait = 0.8f;
    [SerializeField] private float gridStep = 0.8f;
    [SerializeField] private float replanInterval = 0.15f;
    [SerializeField] private Vector2 arenaSize = new(28, 22);
    [SerializeField] private float safeWeight = 12f;
    [SerializeField] private float hiddenWeight = 22f;

    [Header("Combat")]
    [SerializeField] private float sightRange = 6.5f;
    [SerializeField] private float sightAngle = 100f;
    [SerializeField] private float searchDuration = 1.4f;
    [SerializeField] private float contactRange = 1.2f;
    [SerializeField] private float attackInterval = 1.1f;
    [SerializeField] private float hitDamage = 18f;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float lowHealth = 30f;
    [SerializeField] private float knockback = 0.85f;

    [Header("Presentation")]
    [SerializeField] private Color agentColor = new(0.15f, 0.64f, 0.95f);
    [SerializeField] private Color enemyColor = new(0.95f, 0.27f, 0.25f);
    [SerializeField] private Color safeColor = new(1f, 0.64f, 0.15f);
    [SerializeField] private Color hiddenColor = new(0.24f, 0.85f, 0.55f);
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip placeSound;
    [SerializeField] private AudioClip removeSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip arriveSound;
    [SerializeField] private AudioClip ambience;
    [SerializeField] private float hitReactionDuration = 0.3f;
    [SerializeField] private float deathEffectDuration = 1.2f;
    [SerializeField] private float fragmentLifetime = 1.1f;
    [SerializeField] private float bounceHeight = 0.045f;
    [SerializeField] private float swayAngle = 5f;
    [SerializeField] private float animationPeriod = 0.28f;
    [SerializeField] private float arrivalJump = 0.45f;

    [Header("Footstep dust")]
    [SerializeField] private Vector2 footDustSize = new(0.28f, 0.42f);
    [SerializeField] private float footDustDistance = 0.35f;
    [SerializeField] private int footDustCount = 6;

    public float Speed { get => speed; set => speed = value; }
    public float EnemySpeed { get => enemySpeed; set => enemySpeed = value; }
    public float ArrivalDistance { get => arrivalDistance; set => arrivalDistance = value; }
    public float PointWait { get => pointWait; set => pointWait = value; }
    public float GridStep { get => gridStep; set => gridStep = value; }
    public float ReplanInterval { get => replanInterval; set => replanInterval = value; }
    public Vector2 ArenaSize { get => arenaSize; set => arenaSize = value; }
    public float SafeWeight { get => safeWeight; set => safeWeight = value; }
    public float HiddenWeight { get => hiddenWeight; set => hiddenWeight = value; }
    public float SightRange { get => sightRange; set => sightRange = value; }
    public float SightAngle { get => sightAngle; set => sightAngle = value; }
    public float SearchDuration { get => searchDuration; set => searchDuration = value; }
    public float ContactRange { get => contactRange; set => contactRange = value; }
    public float AttackInterval { get => attackInterval; set => attackInterval = value; }
    public float HitDamage { get => hitDamage; set => hitDamage = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }
    public float LowHealth { get => lowHealth; set => lowHealth = value; }
    public float Knockback { get => knockback; set => knockback = value; }
    public Color AgentColor { get => agentColor; set => agentColor = value; }
    public Color EnemyColor { get => enemyColor; set => enemyColor = value; }
    public Color SafeColor { get => safeColor; set => safeColor = value; }
    public Color HiddenColor { get => hiddenColor; set => hiddenColor = value; }
    public AudioClip ClickSound { get => clickSound; set => clickSound = value; }
    public AudioClip PlaceSound { get => placeSound; set => placeSound = value; }
    public AudioClip RemoveSound { get => removeSound; set => removeSound = value; }
    public AudioClip HitSound { get => hitSound; set => hitSound = value; }
    public AudioClip ArriveSound { get => arriveSound; set => arriveSound = value; }
    public AudioClip Ambience { get => ambience; set => ambience = value; }
    public float HitReactionDuration { get => hitReactionDuration; set => hitReactionDuration = value; }
    public float DeathEffectDuration { get => deathEffectDuration; set => deathEffectDuration = value; }
    public float FragmentLifetime { get => fragmentLifetime; set => fragmentLifetime = value; }
    public float BounceHeight { get => bounceHeight; set => bounceHeight = value; }
    public float SwayAngle { get => swayAngle; set => swayAngle = value; }
    public float AnimationPeriod { get => animationPeriod; set => animationPeriod = value; }
    public float ArrivalJump { get => arrivalJump; set => arrivalJump = value; }
    public Vector2 FootDustSize { get => footDustSize; set => footDustSize = value; }
    public float FootDustDistance { get => footDustDistance; set => footDustDistance = value; }
    public int FootDustCount { get => footDustCount; set => footDustCount = value; }
}
