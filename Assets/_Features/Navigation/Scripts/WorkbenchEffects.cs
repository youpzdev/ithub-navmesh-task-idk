using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class WorkbenchEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private ParticleSystem burst;
    [SerializeField] private GameObject fragmentPrefab;
    [SerializeField] private WorkbenchSettings settings;

    private static WorkbenchEffects current;

    private readonly List<GameObject> fragments = new();
    private Tween shake;

    private Vector3 shakeOffset;
    private Vector3 cameraPosition;

    public void Configure(Camera camera, ParticleSystem particles, GameObject fragment)
    {
        viewCamera = camera;
        cameraPosition = camera.transform.localPosition;
        burst = particles;
        fragmentPrefab = fragment;
    }

    public void ConfigureSettings(WorkbenchSettings config) { settings = config; }

    private void OnEnable()
    {
        current = this;
        if (viewCamera) cameraPosition = viewCamera.transform.localPosition;
    }

    private void Start()
    {
        if (fragmentPrefab) Pooling.Prewarm(fragmentPrefab, 18);
    }

    private void LateUpdate()
    {
        viewCamera.transform.localPosition = cameraPosition + shakeOffset;
    }

    private void OnDisable()
    {
        shake?.Kill();
        if (viewCamera) viewCamera.transform.localPosition = cameraPosition;
        shakeOffset = Vector3.zero;
        foreach (GameObject fragment in fragments)
        {
            fragment.transform.DOKill();
            fragment.transform.localScale = fragmentPrefab.transform.localScale;
            Pooling.Destroy(fragment);
        }
        fragments.Clear();
        if (burst) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (current == this) current = null;
    }

    public static void Emit(Vector3 point, Color color, int count = 12)
    {
        if (!current) return;
        var emission = new ParticleSystem.EmitParams { position = point, startColor = color, applyShapeToPosition = true };
        current.burst.Emit(emission, count);
    }

    public static void Shake(float strength)
    {
        if (!current) return;
        current.shake?.Kill();
        current.shake = DOVirtual.Float(strength, 0, 0.25f, value =>
            current.shakeOffset = new Vector3(Mathf.Sin(Time.unscaledTime * 71), Mathf.Cos(Time.unscaledTime * 89), 0) * value)
            .SetUpdate(true).OnComplete(() => current.shakeOffset = Vector3.zero);
    }

    public static void Scatter(Vector3 point, Color color)
    {
        if (!current) return;
        Emit(point, color, 30);
        for (int i = 0; i < 6; i++)
        {
            GameObject fragment = Pooling.Instantiate(current.fragmentPrefab, point + Random.insideUnitSphere * 0.25f, Random.rotation);
            current.fragments.Add(fragment);
            var renderer = fragment.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            var rigidbody = fragment.GetComponent<Rigidbody>();
            rigidbody.linearVelocity = Random.insideUnitSphere * 2 + Vector3.up * 3;
            rigidbody.angularVelocity = Random.insideUnitSphere * 9;
            WorkbenchEffects owner = current;
            fragment.transform.DOScale(0, owner.settings.FragmentLifetime).SetEase(Ease.InExpo).OnComplete(() =>
            {
                owner.fragments.Remove(fragment);
                fragment.transform.localScale = owner.fragmentPrefab.transform.localScale;
                Pooling.Destroy(fragment);
            });
        }
    }
}
