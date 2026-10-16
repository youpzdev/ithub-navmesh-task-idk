using UnityEngine;
using TMPro;

public class WorkbenchThreatView : MonoBehaviour
{
    [SerializeField] private WorkbenchEnemy enemy;
    [SerializeField] private WorkbenchSettings settings;
    [SerializeField] private MeshFilter view;
    [SerializeField] private LayerMask obstacles;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private LineRenderer edge;

    [SerializeField] private Mesh mesh;
    private readonly Vector3[] vertices = new Vector3[66];
    private readonly int[] triangles = new int[64 * 3];

    private Renderer viewRenderer;
    private MaterialPropertyBlock color;

    public void Configure(WorkbenchEnemy guard, WorkbenchSettings data, MeshFilter filter, LayerMask mask, TMP_Text label, Camera camera, LineRenderer boundary)
    {
        enemy = guard;
        settings = data;
        view = filter;
        obstacles = mask;
        stateLabel = label;
        sceneCamera = camera;
        edge = boundary;
    }

    private void OnEnable()
    {
        ReleaseMesh();
        color = new MaterialPropertyBlock();
        viewRenderer = view.GetComponent<Renderer>();
        mesh = new Mesh { name = "EnemyVisibility" }; view.sharedMesh = mesh;
        for (int i = 0; i < 64; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
    }

    private void LateUpdate()
    {
        view.gameObject.SetActive(enemy.ThreatEnabled);
        stateLabel.transform.rotation = sceneCamera.transform.rotation;
        string state = enemy.State == EnemyAwareness.Detected ? "Обнаружен" : enemy.State == EnemyAwareness.Searching ? "Поиск" : "Патруль";
        if (stateLabel.text != state) stateLabel.text = state;
        Color tint = enemy.State == EnemyAwareness.Detected ? new Color(0.85f, 0.1f, 0.08f) : enemy.State == EnemyAwareness.Searching ? new Color(0.75f, 0.4f, 0.04f) : new Color(0.08f, 0.36f, 0.45f);
        stateLabel.color = tint;
        color.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 0.16f));
        color.SetColor("_Color", new Color(tint.r, tint.g, tint.b, 0.16f));
        viewRenderer.SetPropertyBlock(color);
        color.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 0.85f));
        color.SetColor("_Color", new Color(tint.r, tint.g, tint.b, 0.85f));
        edge.SetPropertyBlock(color);
        edge.gameObject.SetActive(enemy.ThreatEnabled);
        if (!enemy.ThreatEnabled) return;
        vertices[0] = new Vector3(0, 0.09f, 0);
        for (int i = 0; i <= 64; i++)
        {
            float degrees = i <= 32 ? -settings.SightAngle * 0.5f + settings.SightAngle * Mathf.Max(0, i - 1) / 31 : settings.SightAngle * 0.5f + (360 - settings.SightAngle) * (i - 33) / 31;
            float angle = degrees * Mathf.Deg2Rad;
            Vector3 direction = transform.TransformDirection(new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)));
            float range = i >= 1 && i <= 32 ? settings.SightRange : settings.ContactRange;
            float length = Physics.Raycast(transform.position + Vector3.up * 0.8f, direction, out var hit, range, obstacles) ? hit.distance : range;
            vertices[i + 1] = transform.InverseTransformDirection(direction * length) + Vector3.up * 0.09f;
        }
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds();
        edge.positionCount = 67; edge.SetPosition(0, vertices[0]);
        for (int i = 1; i < vertices.Length; i++) edge.SetPosition(i, vertices[i]);
        edge.SetPosition(66, vertices[0]); edge.startColor = edge.endColor = tint;
    }

    private void OnDisable()
    {
        ReleaseMesh();
        color = null;
    }

    private void ReleaseMesh()
    {
        if (mesh == null) return;
        if (view && view.sharedMesh == mesh) view.sharedMesh = null;
        Destroy(mesh);
        mesh = null;
    }
}
