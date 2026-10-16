using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class WorkbenchGlowPatch
{
    private const string Root = "Assets/_Features/Navigation";
    public const string ProfilePath = Root + "/Data/WorkbenchGlow.asset";

    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Останови Play Mode перед настройкой свечения сцены");
        if (!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Не удалось сохранить открытые сцены перед настройкой свечения");
        var pipeline = UniversalRenderPipeline.asset;
        if (!pipeline) throw new InvalidOperationException("Активный Render Pipeline не URP");
        var camera = Camera.main;
        if (!camera) throw new InvalidOperationException("Не найдена камера MainCamera");
        if (!pipeline.supportsHDR) { Undo.RecordObject(pipeline, "HDR rendering"); pipeline.supportsHDR = true; EditorUtility.SetDirty(pipeline); }
        Undo.RecordObject(camera, "Camera glow");
        camera.allowHDR = true;
        var data = camera.GetUniversalAdditionalCameraData();
        Undo.RecordObject(data, "Camera post processing");
        data.renderPostProcessing = true;
        data.volumeLayerMask |= 1;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "WorkbenchGlow"; AssetDatabase.CreateAsset(profile, ProfilePath); }
        var bloom = Override<Bloom>(profile);
        bloom.intensity.Override(0.4f); bloom.threshold.Override(1.1f); bloom.scatter.Override(0.65f);
        bloom.highQualityFiltering.Override(true);
        var tonemapping = Override<Tonemapping>(profile); tonemapping.mode.Override(TonemappingMode.ACES);
        var grading = Override<ColorAdjustments>(profile); grading.contrast.Override(3); grading.saturation.Override(3);
        var vignette = Override<Vignette>(profile); vignette.intensity.Override(0.08f); vignette.smoothness.Override(0.4f);
        var volume = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(item => item.sharedProfile == profile);
        if (!volume)
        {
            var go = new GameObject("WorkbenchGlow"); Undo.RegisterCreatedObjectUndo(go, "Workbench glow volume");
            volume = go.AddComponent<Volume>();
        }
        Undo.RecordObject(volume, "Workbench glow volume");
        volume.gameObject.SetActive(true);
        volume.gameObject.layer = 0; volume.enabled = true; volume.isGlobal = true; volume.priority = 10; volume.weight = 1; volume.sharedProfile = profile;
        var poi = GlowMaterial("POIGlow", 1.7f);
        var indicator = GlowMaterial("IndicatorGlow", 2.1f);
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!Owned(renderer.sharedMaterial)) continue;
            if (renderer.name == "Ring" && renderer.GetComponentInParent<WorkbenchItem>()) { Undo.RecordObject(renderer, "POI glow"); renderer.sharedMaterial = poi; EditorUtility.SetDirty(renderer); }
            if (renderer.name == "EdgeLight") { Undo.RecordObject(renderer, "Indicator glow"); renderer.sharedMaterial = indicator; EditorUtility.SetDirty(renderer); }
        }
        const string pointPath = Root + "/Prefabs/Point.prefab";
        var point = PrefabUtility.LoadPrefabContents(pointPath);
        var ring = point.GetComponentsInChildren<Renderer>(true).First(item => item.name == "Ring");
        ring.sharedMaterial = poi;
        PrefabUtility.SaveAsPrefabAsset(point, pointPath); PrefabUtility.UnloadPrefabContents(point);
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        var colors = new[] { bench.Settings.AgentColor, bench.Settings.SafeColor, bench.Settings.HiddenColor };
        for (int i = 0; i < colors.Length; i++)
        {
            var route = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Route" + i + ".mat");
            if (!Owned(route)) throw new InvalidOperationException("Нет материала Route" + i + " в Navigation");
            Color hdr = colors[i].linear * 1.7f; hdr.a = colors[i].a;
            route.SetColor("_BaseColor", hdr); EditorUtility.SetDirty(route);
        }
        EditorUtility.SetDirty(profile); EditorUtility.SetDirty(camera); EditorUtility.SetDirty(data); EditorUtility.SetDirty(volume);
        foreach (var component in profile.components) EditorUtility.SetDirty(component);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Не удалось сохранить сцену со свечением");

        return "Свечение сохранено: HDR, Bloom 0.4, ACES, POI, маршруты и индикаторы; UI Overlay";
    }

    private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var component))
        {
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        component.active = true;

        return component;
    }

    private static Material GlowMaterial(string name, float strength)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Teal.mat");
            material = new Material(source) { name = name }; AssetDatabase.CreateAsset(material, path);
        }
        material.SetFloat("_GlowStrength", strength); EditorUtility.SetDirty(material);

        return material;
    }

    private static bool Owned(Material material) => material && AssetDatabase.GetAssetPath(material).StartsWith(Root + "/Materials/", StringComparison.Ordinal);
}
