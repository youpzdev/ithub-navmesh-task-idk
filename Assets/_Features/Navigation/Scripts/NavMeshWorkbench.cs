using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class NavMeshWorkbench : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorkbenchSettings settings;
    [SerializeField] private NavMeshSurface surface;
    [SerializeField] private WorkbenchActor actor;
    [SerializeField] private WorkbenchEnemy[] enemies;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Transform objectsRoot;
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField] private GameObject pointPrefab;
    [SerializeField] private Transform[] previews;
    [SerializeField] private LineRenderer[] routeLines;
    [SerializeField] private MeshFilter navMeshView;
    [SerializeField] private MeshFilter coverView;
    [SerializeField] private LayerMask obstacleMask;

    [SerializeField] private ModeState[] modes = { new(), new(), new() };
    [SerializeField] private List<WorkbenchItem> obstacles = new();
    [SerializeField] private List<WorkbenchItem> points = new();
    private WorkbenchRoutes routes;
    private AsyncOperation rebuild;
    private bool dirty;
    private bool queuedRebuild;
    private bool pathBlocked;
    private bool risk;
    private float nextPlan;
    private float waitUntil;
    private float diedAt = -1;
    [SerializeField] private int targetIndex;
    private int rotation;
    [SerializeField] private int situation;
    private int dragging = -1;
    private Snapshot beforeDrag;
    private Vector3 pointerPoint;
    private bool pointerValid;
    private bool restoreActor;
    [SerializeField] private bool begun;
    private bool resetEnemies;

    [field: SerializeField] public WorkbenchMode Mode { get; private set; }
    [field: SerializeField] public EditTool Tool { get; private set; }
    [field: SerializeField] public RouteStrategy Strategy { get; private set; }
    [field: SerializeField] public bool Automatic { get; private set; } = true;
    [field: SerializeField] public bool CompareRoutes { get; private set; }
    [field: SerializeField] public bool ShowNavMesh { get; private set; }
    [field: SerializeField] public bool ShowCover { get; private set; }
    [field: SerializeField] public bool Paused { get; private set; }
    [field: SerializeField] public float SimulationSpeed { get; private set; } = 1;
    public string Status { get; private set; } = "Патруль";
    public float Health => actor.Health;
    public int UndoCount => modes[(int)Mode].history.Count;
    public int PointCount => points.Count;
    public int Situation => situation;
    public bool Risky => risk;
    public bool HelpOpen { get; private set; }
    public bool Rebuilding => rebuild != null || dirty;
    public bool PathBlocked => pathBlocked;
    public WorkbenchActor Actor => actor;
    public WorkbenchSettings Settings => settings;
    private ModeState Current => modes[(int)Mode];

    public void Configure(WorkbenchSettings config, NavMeshSurface navigation, WorkbenchActor npc, WorkbenchEnemy[] guards, Camera view, Transform runtimeRoot, GameObject[] blocks, GameObject marker, Transform[] ghosts, LineRenderer[] lines, MeshFilter meshView, MeshFilter hiddenView, LayerMask mask)
    {
        settings = config;
        surface = navigation;
        actor = npc;
        enemies = guards;
        sceneCamera = view;
        objectsRoot = runtimeRoot;
        obstaclePrefabs = blocks;
        pointPrefab = marker;
        previews = ghosts;
        routeLines = lines;
        navMeshView = meshView;
        coverView = hiddenView;
        obstacleMask = mask;
    }

    public void SetHelpOpen(bool value) { HelpOpen = value; }

    private void OnEnable()
    {
        if (!begun) return;
        routes = new WorkbenchRoutes(settings, obstacleMask);
        Time.timeScale = Paused ? 0 : SimulationSpeed;
        dirty = true;
    }

    private void OnDisable()
    {
        if (begun) SaveState();
    }

    private void Start()
    {
        Pooling.Prewarm(obstaclePrefabs[0], 30);
        Pooling.Prewarm(obstaclePrefabs[1], 20);
        Pooling.Prewarm(pointPrefab, 24);
        routes = new WorkbenchRoutes(settings, obstacleMask);
        for (int i = 0; i < modes.Length; i++) ResetState(modes[i]);
        begun = true;
        LoadState();
        if (settings.Ambience != null)
        {
            var source = GetComponent<AudioSource>();
            source.clip = settings.Ambience; source.loop = true; source.volume = 0.13f; source.Play();
        }
    }

    private void Update()
    {
        HandleCamera();
        HandleInput();
        if (dirty && rebuild == null) BeginRebuild();
        if (rebuild != null && rebuild.isDone) FinishRebuild();
        if (Rebuilding || actor.Dead) { UpdateDeath(); return; }
        UpdateRouteOrigins();
        if (Paused) return;
        if (points.Count == 0) { Status = "Добавь точки"; return; }

        if (Mode != WorkbenchMode.Strategies && actor.Arrived && !pathBlocked)
        {
            if (waitUntil <= 0) { actor.Celebrate(); waitUntil = Time.time + settings.PointWait; }
            if (Time.time >= waitUntil) { targetIndex = (targetIndex + 1) % points.Count; waitUntil = 0; nextPlan = 0; }
        }
        if (Time.time >= nextPlan && waitUntil <= 0) Plan();
    }

    private void UpdateRouteOrigins()
    {
        for (int i = 0; i < routeLines.Length; i++)
        {
            var line = routeLines[i];
            if (line.gameObject.activeSelf && line.positionCount > 0) line.SetPosition(0, actor.transform.position + Vector3.up * (0.1f + i * 0.025f));
        }
    }

    private void UpdateDeath()
    {
        if (!actor.Dead) return;
        if (diedAt < 0) diedAt = Time.unscaledTime;
        Status = "Агент погиб";
        if (Time.unscaledTime - diedAt < settings.DeathEffectDuration) return;
        actor.ResetAt(new Vector3(-11, 0, -7), settings.MaxHealth);
        diedAt = -1;
        Paused = true; Time.timeScale = 0;
        if (points.Count > 0) Plan(); else ClearLines();
        Status = "Продолжить после возврата";
        nextPlan = 0;
    }

    private void Plan()
    {
        if (points.Count == 0 || !actor.Agent.isOnNavMesh) return;
        targetIndex = Mathf.Clamp(targetIndex, 0, points.Count - 1);
        Vector3[] threats = ThreatPositions();
        var threatDirections = new List<Vector3>();
        if (threats.Length > 0) foreach (var enemy in enemies) if (enemy.gameObject.activeSelf) threatDirections.Add(enemy.transform.forward);
        if (Automatic) Strategy = Mode == WorkbenchMode.Strategies ? ChooseStrategy(threats) : RouteStrategy.Shortest;
        Vector3[] path = routes.Find(actor.transform.position, points[targetIndex].transform.position, Strategy, threats, threatDirections.ToArray());
        risk = routes.Risky;
        bool blocked = path.Length == 0;
        if (blocked && !pathBlocked) actor.Blocked();
        pathBlocked = blocked;
        if (blocked) actor.Halt();
        else if (!actor.Arrived || Vector3.Distance(actor.transform.position, points[targetIndex].transform.position) > settings.ArrivalDistance + 0.2f) actor.Follow(path);
        else if (Mode == WorkbenchMode.Strategies && Current.reached != targetIndex) { actor.Celebrate(); Current.reached = targetIndex; }
        Status = blocked ? "Нет пути" : risk ? "Маршрут с риском" : "Маршрут готов";
        for (int i = 0; i < points.Count; i++) points[i].Show(i, i == targetIndex, blocked && i == targetIndex);
        for (int i = 0; i < routeLines.Length; i++)
        {
            bool visible = blocked ? i == (int)Strategy : CompareRoutes && Mode == WorkbenchMode.Strategies || i == (int)Strategy;
            routeLines[i].gameObject.SetActive(visible);
            if (!visible) continue;
            Vector3[] corners = i == (int)Strategy ? path : routes.Find(actor.transform.position, points[targetIndex].transform.position, (RouteStrategy)i, threats, threatDirections.ToArray());
            if (blocked)
            {
                var partial = new NavMeshPath();
                NavMesh.CalculatePath(actor.transform.position, points[targetIndex].transform.position, NavMesh.AllAreas, partial);
                corners = partial.corners;
            }
            routeLines[i].startColor = routeLines[i].endColor = blocked ? new Color(1, 0.2f, 0.2f) : Color.white;
            routeLines[i].positionCount = corners.Length;
            for (int j = 0; j < corners.Length; j++) routeLines[i].SetPosition(j, corners[j] + Vector3.up * (0.1f + i * 0.025f));
        }
        nextPlan = Time.time + settings.ReplanInterval;
        if (ShowCover) RefreshCover(threats);
    }

    private RouteStrategy ChooseStrategy(Vector3[] threats)
    {
        if (actor.Health <= settings.LowHealth) return RouteStrategy.Hidden;
        foreach (Vector3 enemy in threats) if (Vector3.Distance(enemy, actor.transform.position) <= settings.SightRange * 1.35f) return RouteStrategy.Safe;

        return RouteStrategy.Shortest;
    }

    private Vector3[] ThreatPositions()
    {
        if (Mode != WorkbenchMode.Strategies || situation == 0) return Array.Empty<Vector3>();
        var positions = new List<Vector3>();
        foreach (var enemy in enemies) if (enemy.gameObject.activeSelf) positions.Add(enemy.transform.position);

        return positions.ToArray();
    }

    public void SetMode(int value)
    {
        if (!begun || value < 0 || value >= modes.Length || (int)Mode == value) return;
        SaveState(); Mode = (WorkbenchMode)value; targetIndex = 0; dragging = -1; waitUntil = 0;
        Tool = EditTool.Select; Automatic = true; Strategy = RouteStrategy.Shortest;
        LoadState();
    }

    public void SetTool(int value) { EndDrag(); Tool = (EditTool)Mathf.Clamp(value, 0, 3); }
    public void SetStrategy(int value)
    {
        Automatic = value < 0;
        if (!Automatic) Strategy = (RouteStrategy)Mathf.Clamp(value, 0, 2);
        nextPlan = 0;
        if (Paused && !Rebuilding) Plan();
    }

    public void ToggleCompare()
    {
        CompareRoutes = !CompareRoutes;
        nextPlan = 0;
        if (Paused && !Rebuilding) Plan();
    }

    public void ToggleNavMesh()
    {
        ShowNavMesh = !ShowNavMesh;
        navMeshView.gameObject.SetActive(ShowNavMesh);
        if (ShowNavMesh) RefreshNavMesh();
    }

    public void ToggleCover()
    {
        ShowCover = !ShowCover;
        coverView.gameObject.SetActive(ShowCover);
        if (ShowCover && !Rebuilding) RefreshCover(ThreatPositions());
    }
    public void TogglePause() { Paused = !Paused; Time.timeScale = Paused ? 0 : SimulationSpeed; }
    public void SetSpeed(float value) { SimulationSpeed = Mathf.Clamp(value, 0.5f, 2); if (!Paused) Time.timeScale = SimulationSpeed; }

    public void SetSituation(int value)
    {
        situation = Mathf.Clamp(value, 0, 2);
        actor.SetHealth(situation == 2 ? settings.LowHealth : settings.MaxHealth);
        foreach (var enemy in enemies) enemy.ThreatEnabled = situation != 0;
        Automatic = true; nextPlan = 0;
        if (Paused && !Rebuilding) Plan();
    }

    public void ReturnToStart()
    {
        if (Rebuilding) { Status = "Подожди обновления NavMesh"; return; }
        actor.ResetAt(new Vector3(-11, 0, -7), settings.MaxHealth);
        Current.reached = -1; waitUntil = 0; nextPlan = 0; diedAt = -1;
        if (Paused) Plan();
    }

    public void FocusAgent()
    {
        var target = actor.transform.position;
        sceneCamera.transform.parent.position = new Vector3(target.x + 4, 24, target.z - 16);
    }

    public void ResetMode()
    {
        Current.history.Add(Capture());
        ResetState(Current); targetIndex = 0; waitUntil = 0; resetEnemies = true;
        LoadState();
    }

    public void Undo()
    {
        if (Current.history.Count == 0) return;
        Snapshot saved = Current.history[^1];
        Current.history.RemoveAt(Current.history.Count - 1);
        Current.obstacles = new(saved.obstacles); Current.points = new(saved.points);
        LoadObjects(); RequestRebuild();
    }

    private void LoadState()
    {
        restoreActor = true;
        LoadObjects();
        actor.Halt();
        foreach (var enemy in enemies) { enemy.gameObject.SetActive(Mode == WorkbenchMode.Strategies); enemy.ThreatEnabled = situation != 0; }
        RequestRebuild();
    }

    private void SaveState() { Current.actorPosition = actor.transform.position; Current.health = actor.Health; Current.hasActor = true; }

    private void LoadObjects()
    {
        foreach (var item in obstacles) ReleaseItem(item);
        foreach (var item in points) ReleaseItem(item);
        obstacles.Clear(); points.Clear();
        foreach (var entry in Current.obstacles)
        {
            var item = Pooling.Instantiate(obstaclePrefabs[entry.kind], entry.position, Quaternion.Euler(0, entry.rotation, 0), objectsRoot).GetComponent<WorkbenchItem>();
            item.Kind = entry.kind; item.IsPoint = false; item.Index = obstacles.Count; obstacles.Add(item);
        }
        foreach (Vector3 point in Current.points)
        {
            var item = Pooling.Instantiate(pointPrefab, point, Quaternion.identity, objectsRoot).GetComponent<WorkbenchItem>();
            item.IsPoint = true; item.Show(points.Count, points.Count == targetIndex, false); points.Add(item);
        }
        targetIndex = Mathf.Clamp(targetIndex, 0, Mathf.Max(0, points.Count - 1));
        Current.reached = -1;
    }

    private void RequestRebuild() { dirty = true; actor.Halt(); pathBlocked = false; waitUntil = 0; }

    private void BeginRebuild()
    {
        dirty = false;
        if (surface.navMeshData == null) { surface.BuildNavMesh(); FinishRebuild(); return; }
        Physics.SyncTransforms();
        rebuild = surface.UpdateNavMesh(surface.navMeshData);
        Status = "Обновление NavMesh";
    }

    private void FinishRebuild()
    {
        rebuild = null;
        if (queuedRebuild || dirty) { queuedRebuild = false; dirty = true; return; }
        routes.Rebuild();
        if (restoreActor || !actor.Agent.isOnNavMesh)
        {
            Vector3 spawn = Current.hasActor ? Current.actorPosition : new Vector3(-11, 0, -7);
            actor.ResetAt(spawn, Current.hasActor ? Current.health : settings.MaxHealth);
            restoreActor = false;
        }
        if (resetEnemies) { foreach (var enemy in enemies) if (enemy.gameObject.activeSelf) enemy.ResetPatrol(); resetEnemies = false; }
        RefreshNavMesh(); nextPlan = 0;
        if (points.Count > 0) Plan(); else { actor.Halt(); ClearLines(); Status = "Добавь точки"; }
    }

    private void HandleInput()
    {
        var mouse = Mouse.current; var keyboard = Keyboard.current;
        if (mouse == null) return;
        if (HelpOpen)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetHelpOpen(false);
            foreach (var preview in previews) preview.gameObject.SetActive(false);
            return;
        }
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
            if (keyboard.rKey.wasPressedThisFrame) rotation = (rotation + 90) % 360;
            if (keyboard.zKey.wasPressedThisFrame && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)) Undo();
            if (keyboard.escapeKey.wasPressedThisFrame) SetTool(0);
        }
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Ray ray = sceneCamera.ScreenPointToRay(mouse.position.ReadValue());
        var plane = new Plane(Vector3.up, Vector3.zero);
        float distance = 0;
        pointerValid = !overUi && plane.Raycast(ray, out distance);
        pointerPoint = pointerValid ? ray.GetPoint(distance) : Vector3.zero;
        pointerValid &= Mathf.Abs(pointerPoint.x) < settings.ArenaSize.x / 2 - 0.7f && Mathf.Abs(pointerPoint.z) < settings.ArenaSize.y / 2 - 0.7f;
        foreach (var preview in previews) preview.gameObject.SetActive(false);
        if (pointerValid && (Tool == EditTool.Block || Tool == EditTool.Wall))
        {
            int kind = Tool == EditTool.Block ? 0 : 1;
            var ghost = previews[kind]; ghost.gameObject.SetActive(true);
            ghost.position = Snap(pointerPoint) + Vector3.up; ghost.rotation = Quaternion.Euler(0, rotation, 0);
        }
        if (overUi || !pointerValid) { if (mouse.leftButton.wasReleasedThisFrame) EndDrag(); return; }
        if (mouse.rightButton.wasPressedThisFrame) { DeleteAt(ray); return; }
        if (Tool == EditTool.Select)
        {
            if (mouse.leftButton.wasPressedThisFrame && Physics.Raycast(ray, out var hit, 100, ~0))
            {
                var item = hit.collider.GetComponentInParent<WorkbenchItem>();
                if (item != null && item.IsPoint)
                {
                    targetIndex = item.Index; Current.reached = -1; waitUntil = 0; nextPlan = 0;
                    dragging = item.Index; beforeDrag = Capture();
                    if (Paused && !Rebuilding) Plan();
                }
                else if (item != null && !item.IsPoint) Status = "Препятствие: ПКМ удалить";
            }
            if (dragging >= 0 && dragging < points.Count && mouse.leftButton.isPressed && FreePoint(pointerPoint))
            {
                points[dragging].transform.position = pointerPoint; Current.points[dragging] = pointerPoint; nextPlan = 0;
                if (Paused && !Rebuilding) Plan();
            }
            if (mouse.leftButton.wasReleasedThisFrame) EndDrag();
            return;
        }
        if (!mouse.leftButton.wasPressedThisFrame) return;
        if (Tool == EditTool.Point) AddPoint(pointerPoint);
        else PlaceObstacle(pointerPoint, Tool == EditTool.Block ? 0 : 1);
    }

    private void EndDrag()
    {
        if (dragging < 0) return;
        if (beforeDrag != null) Current.history.Add(beforeDrag);
        dragging = -1; beforeDrag = null; nextPlan = 0;
        if (Paused && !Rebuilding) Plan();
    }

    public bool AddPoint(Vector3 position)
    {
        if (Rebuilding || Current.points.Count >= 16 || !FreePoint(position)) { Status = "Точка: выбери свободный пол (до 16)"; return false; }
        Current.history.Add(Capture()); Current.points.Add(position);
        var item = Pooling.Instantiate(pointPrefab, position, Quaternion.identity, objectsRoot).GetComponent<WorkbenchItem>();
        item.IsPoint = true; item.Show(points.Count, points.Count == targetIndex, false); points.Add(item);
        Current.reached = -1; nextPlan = 0; waitUntil = 0;
        if (Paused) Plan();
        Audio.Play(settings.ClickSound, 0.4f);

        return true;
    }

    public bool PlaceObstacle(Vector3 position, int kind)
    {
        if (kind < 0 || kind >= obstaclePrefabs.Length || Current.obstacles.Count >= 48) { Status = "Препятствие: лимит 48"; return false; }
        position = Snap(position);
        Vector3 half = kind == 0 ? new Vector3(0.64f, 1, 0.64f) : new Vector3(0.64f, 1, 2.1f);
        Quaternion turn = Quaternion.Euler(0, rotation, 0);
        if (Mathf.Abs(position.x) + (rotation % 180 == 0 ? half.x : half.z) >= settings.ArenaSize.x / 2 - 0.4f || Mathf.Abs(position.z) + (rotation % 180 == 0 ? half.z : half.x) >= settings.ArenaSize.y / 2 - 0.4f) return RejectPlacement();
        if (Physics.CheckBox(position + Vector3.up, half, turn, obstacleMask, QueryTriggerInteraction.Ignore)) return RejectPlacement();
        if (Overlaps(position, half, turn, actor.transform.position, 0.7f)) return RejectPlacement();
        foreach (var enemy in enemies) if (enemy.gameObject.activeSelf && Overlaps(position, half, turn, enemy.transform.position, 0.7f)) return RejectPlacement();
        foreach (var point in Current.points) if (Overlaps(position, half, turn, point, 0.5f)) return RejectPlacement();
        if (Overlaps(position, half, turn, new Vector3(-11, 0, -7), 0.8f)) return RejectPlacement();
        Current.history.Add(Capture());
        Current.obstacles.Add(new ObstacleState(position, rotation, kind));
        var item = Pooling.Instantiate(obstaclePrefabs[kind], position, turn, objectsRoot).GetComponent<WorkbenchItem>();
        item.Kind = kind; item.Index = obstacles.Count; item.IsPoint = false; obstacles.Add(item); item.Drop(settings.PlaceSound);
        if (rebuild != null) queuedRebuild = true;
        RequestRebuild();

        return true;
    }

    private bool RejectPlacement()
    {
        Status = "Нет места: объект, точка или край";
        Audio.Play(settings.ClickSound, 0.15f, 0.65f);

        return false;
    }

    private static bool Overlaps(Vector3 center, Vector3 half, Quaternion rotation, Vector3 position, float radius)
    {
        Vector3 local = Quaternion.Inverse(rotation) * (position - center);

        return Mathf.Abs(local.x) < half.x + radius && Mathf.Abs(local.z) < half.z + radius;
    }
    private static Vector3 Snap(Vector3 value) => new(Mathf.Round(value.x), 0, Mathf.Round(value.z));
    private bool FreePoint(Vector3 point) => NavMesh.SamplePosition(point, out var hit, 0.35f, NavMesh.AllAreas) && Vector3.Distance(hit.position, point) < 0.4f && !Physics.CheckSphere(point + Vector3.up * 0.5f, 0.35f, obstacleMask);

    private void DeleteAt(Ray ray)
    {
        if (!Physics.Raycast(ray, out var hit, 100)) return;
        var item = hit.collider.GetComponentInParent<WorkbenchItem>();
        if (item == null) return;
        Current.history.Add(Capture());
        if (item.IsPoint) { Current.points.RemoveAt(item.Index); points.Remove(item); for (int i = 0; i < points.Count; i++) points[i].Index = i; }
        else { Current.obstacles.RemoveAt(item.Index); obstacles.Remove(item); for (int i = 0; i < obstacles.Count; i++) obstacles[i].Index = i; }
        WorkbenchEffects.Emit(item.transform.position, new Color(0.65f, 0.75f, 0.8f), 22); WorkbenchEffects.Shake(0.1f);
        Audio.Play(settings.RemoveSound, 0.45f); ReleaseItem(item);
        targetIndex = Mathf.Clamp(targetIndex, 0, Mathf.Max(0, points.Count - 1));
        Current.reached = -1;
        if (rebuild != null) queuedRebuild = true;
        RequestRebuild();
    }

    private void ReleaseItem(WorkbenchItem item)
    {
        Pooling.Destroy(item.gameObject);
        if (item.gameObject.activeSelf)
        {
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }
    }

    private void HandleCamera()
    {
        if (HelpOpen) return;
        var mouse = Mouse.current; var keyboard = Keyboard.current;
        if (mouse == null) return;
        Vector3 pan = Vector3.zero;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) pan.z += 1;
            if (keyboard.sKey.isPressed) pan.z -= 1;
            if (keyboard.aKey.isPressed) pan.x -= 1;
            if (keyboard.dKey.isPressed) pan.x += 1;
        }
        pan *= 15 * Time.unscaledDeltaTime;
        if (mouse.middleButton.isPressed) { Vector2 delta = mouse.delta.ReadValue(); pan += new Vector3(-delta.x, 0, -delta.y) * 0.028f; }
        Vector3 position = sceneCamera.transform.parent.position + pan;
        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) position += sceneCamera.transform.forward * Mathf.Clamp(mouse.scroll.ReadValue().y * 2.4f, -4.8f, 4.8f);
        position.x = Mathf.Clamp(position.x, -20, 20); position.y = Mathf.Clamp(position.y, 13, 33); position.z = Mathf.Clamp(position.z, -35, 8);
        sceneCamera.transform.parent.position = position;
    }

    private void RefreshNavMesh()
    {
        var data = NavMesh.CalculateTriangulation();
        var mesh = new Mesh { name = "NavigationOverlay" };
        var vertices = data.vertices;
        for (int i = 0; i < vertices.Length; i++) vertices[i].y += 0.045f;
        mesh.vertices = vertices; mesh.triangles = data.indices; mesh.RecalculateNormals();
        if (navMeshView.sharedMesh != null) Destroy(navMeshView.sharedMesh);
        navMeshView.sharedMesh = mesh; navMeshView.gameObject.SetActive(ShowNavMesh);
    }

    private void RefreshCover(Vector3[] threats)
    {
        var vertices = new List<Vector3>(); var indices = new List<int>();
        for (float x = -13; x < 13; x += 1.5f)
        for (float z = -10; z < 10; z += 1.5f)
        {
            var p = new Vector3(x, 0.07f, z);
            if (!NavMesh.SamplePosition(p, out _, 0.2f, NavMesh.AllAreas)) continue;
            bool covered = threats.Length > 0;
            foreach (var threat in threats) if (!Physics.Linecast(p + Vector3.up * 0.8f, threat + Vector3.up * 0.8f, obstacleMask)) { covered = false; break; }
            if (!covered) continue;
            int i = vertices.Count;
            vertices.Add(p + new Vector3(-0.6f, 0, -0.6f)); vertices.Add(p + new Vector3(-0.6f, 0, 0.6f)); vertices.Add(p + new Vector3(0.6f, 0, 0.6f)); vertices.Add(p + new Vector3(0.6f, 0, -0.6f));
            indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        var mesh = new Mesh { name = "CoverOverlay" }; mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0);
        if (coverView.sharedMesh != null) Destroy(coverView.sharedMesh);
        coverView.sharedMesh = mesh;
    }

    private void ClearLines() { foreach (var line in routeLines) line.positionCount = 0; }
    private Snapshot Capture() => new(Current.points, Current.obstacles);
    private void ResetState(ModeState state)
    {
        state.points = new() { new Vector3(-11, 0, 6), new Vector3(11, 0, 6), new Vector3(11, 0, -7), new Vector3(-11, 0, -7) };
        if (Array.IndexOf(modes, state) == (int)WorkbenchMode.Strategies) state.points = new() { new Vector3(11, 0, -7), new Vector3(11, 0, 6), new Vector3(-11, 0, 6) };
        state.obstacles = new(); state.hasActor = false; state.health = settings.MaxHealth; state.reached = -1;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1;
        if (navMeshView != null && navMeshView.sharedMesh != null) Destroy(navMeshView.sharedMesh);
        if (coverView != null && coverView.sharedMesh != null) Destroy(coverView.sharedMesh);
    }

    [Serializable]
    private class ModeState
    {
        public List<Vector3> points = new();
        public List<ObstacleState> obstacles = new();
        public List<Snapshot> history = new();
        public Vector3 actorPosition;
        public float health;
        public bool hasActor;
        public int reached = -1;
    }

    [Serializable]
    private class Snapshot
    {
        public List<Vector3> points;
        public List<ObstacleState> obstacles;
        public Snapshot(List<Vector3> markers, List<ObstacleState> blocks) { points = new(markers); obstacles = new(blocks); }
    }

    [Serializable]
    private struct ObstacleState
    {
        public Vector3 position;
        public int rotation;
        public int kind;
        public ObstacleState(Vector3 point, int angle, int shape) { position = point; rotation = angle; kind = shape; }
    }
}
