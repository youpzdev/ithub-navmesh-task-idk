using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class WorkbenchGlowChecks
{
    private const string Root = "Assets/_Features/Navigation";
    [Serializable]
    public class Report { public int passed; public List<string> failures = new(); }

    public static string Check()
    {
        var report = new Report();
        var camera = Camera.main;
        var pipeline = UniversalRenderPipeline.asset;
        Expect(report, pipeline && pipeline.supportsHDR, "URP должен быть активен с HDR");
        Expect(report, camera && camera.allowHDR, "Камера должна разрешать HDR");
        if (!camera) return JsonUtility.ToJson(report);
        var data = camera.GetUniversalAdditionalCameraData();
        Expect(report, data.renderPostProcessing, "На камере выключен post processing");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(WorkbenchGlowPatch.ProfilePath);
        var volume = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(item => item.sharedProfile == profile);
        Expect(report, profile && volume && volume.isActiveAndEnabled && volume.isGlobal && volume.weight == 1 && volume.priority == 10,
            "Нужен активный глобальный WorkbenchGlow с приоритетом 10 и весом 1");
        if (profile)
        {
            bool found = profile.TryGet<Bloom>(out var bloom);
            Expect(report, found && bloom.active && bloom.intensity.overrideState && Mathf.Approximately(bloom.intensity.value, 0.4f)
                && bloom.threshold.overrideState && Mathf.Approximately(bloom.threshold.value, 1.1f), "Bloom должен быть включен: intensity=0.4, threshold=1.1 с override");
            Expect(report, profile.TryGet<Tonemapping>(out var tone) && tone.active && tone.mode.overrideState && tone.mode.value == TonemappingMode.ACES,
                "Tonemapping должен использовать ACES");
        }
        if (volume) Expect(report, (data.volumeLayerMask.value & (1 << volume.gameObject.layer)) != 0, "Слой WorkbenchGlow не входит в volumeLayerMask камеры");
        var canvas = UnityEngine.Object.FindFirstObjectByType<WorkbenchHud>().GetComponent<Canvas>();
        Expect(report, canvas.renderMode == RenderMode.ScreenSpaceOverlay, "UI должен остаться ScreenSpaceOverlay вне Bloom");
        var popup = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/DamagePopup.prefab").GetComponent<WorkbenchDamageView>();
        var color = popup.Label.color;
        Expect(report, color.r >= 0.8f && color.g <= 0.15f && color.b <= 0.15f, "Цвет попапа должен быть красным, получен " + color);
        foreach (string name in new[] { "Floor", "Agent", "Guard", "Obstacles", "Markings", "Graphite", "Teal" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
            Expect(report, material && (!material.HasProperty("_GlowStrength") || material.GetFloat("_GlowStrength") == 0), name + ": исходный материал не должен получить свечение");
        }
        foreach (string name in new[] { "POIGlow", "IndicatorGlow" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
            Expect(report, material && material.HasProperty("_GlowStrength") && material.GetFloat("_GlowStrength") > 1,
                name + ": нужен отдельный материал со свечением");
        }
        for (int i = 0; i < 3; i++)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Route" + i + ".mat");
            Expect(report, material && material.GetColor("_BaseColor").maxColorComponent > 1 && material.GetTexture("_BaseMap"),
                "Route" + i + ": нужен HDR-цвет с исходным рисунком линии");
        }
        foreach (var light in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(item => item.name == "EdgeLight"))
            Expect(report, light.sharedMaterial.name.StartsWith("IndicatorGlow", StringComparison.Ordinal), light.name + ": не назначен IndicatorGlow");

        return JsonUtility.ToJson(report);
    }

    private static void Expect(Report report, bool value, string failure) { if (value) report.passed++; else report.failures.Add(failure); }
}
