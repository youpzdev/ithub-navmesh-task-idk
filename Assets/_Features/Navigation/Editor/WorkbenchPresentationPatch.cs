using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class WorkbenchPresentationPatch
{
    private const string Root = "Assets/_Features/Navigation";
    private const string BoldPath = Root + "/Fonts/Roboto Bold SDF.asset";
    private const string PopupPath = Root + "/Prefabs/DamagePopup.prefab";

    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Останови Play Mode перед сохранением оформления сцены");
        var heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Unbounded SDF.asset");
        if (!heading) throw new InvalidOperationException("Не найден Unbounded SDF");
        var body = BoldFont();
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя .,:;!?+-/()[]<>=%#";
        heading.TryAddCharacters(chars); body.TryAddCharacters(chars);
        var texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var text in texts) ApplyFont(text, heading, body);
        foreach (string path in new[] { Root + "/Prefabs/WorkbenchButton.prefab", Root + "/Prefabs/Point.prefab" })
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
            var prefab = PrefabUtility.LoadPrefabContents(path);
            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true)) ApplyFont(text, heading, body);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        var popup = Popup(body);
        foreach (var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (particles.name != "StepDust" && particles.name != "GuardDust") continue;
            var main = particles.main; main.startColor = Color.white;
            var fade = particles.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            fade.enabled = true; fade.color = gradient;
            EditorUtility.SetDirty(particles);
            EditorSceneManager.MarkSceneDirty(particles.gameObject.scene);
        }
        foreach (var actor in UnityEngine.Object.FindObjectsByType<WorkbenchActor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(actor, "Damage feedback references");
            var renderers = actor.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.name == "Capsule" || renderer.name == "LeftFoot" || renderer.name == "RightFoot").ToArray();
            actor.ConfigureDamage(popup, Camera.main, renderers);
            EditorUtility.SetDirty(actor);
            EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
        }
        EditorUtility.SetDirty(heading); EditorUtility.SetDirty(body);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        return "Оформление сохранено: " + texts.Length + " текстов, Roboto " + body.faceInfo.styleName + ", ссылки на DamagePopup назначены";
    }

    public static bool IsHeading(string text) => text == "NAVMESH" || text == "Управление" || text == "РЕЖИМ" || text == "ИНСТРУМЕНТЫ" || text == "СТРАТЕГИЯ" || text == "СИТУАЦИЯ" || text == "СЛОИ";

    private static TMP_FontAsset BoldFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
        if (font) return font;
        const string sourcePath = Root + "/Fonts/Roboto-Bold.ttf";
        if (!AssetDatabase.LoadAssetAtPath<Font>(sourcePath)) throw new InvalidOperationException("Не найден исходный шрифт " + sourcePath);
        AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        font = TMP_FontAsset.CreateFontAsset(source, 64, 7, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (!font.faceInfo.styleName.Contains("Bold")) throw new InvalidOperationException("Roboto-Bold.ttf: начертание " + font.faceInfo.styleName + ", ожидался Bold");
        font.name = "Roboto Bold SDF";
        AssetDatabase.CreateAsset(font, BoldPath);
        AssetDatabase.AddObjectToAsset(font.material, font);
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);

        return font;
    }

    private static void ApplyFont(TMP_Text text, TMP_FontAsset heading, TMP_FontAsset body)
    {
        Undo.RecordObject(text, "Text typography");
        text.font = IsHeading(text.text) ? heading : body;
        text.fontStyle = FontStyles.Normal;
        text.fontWeight = FontWeight.Regular;
        EditorUtility.SetDirty(text);
        if (text.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
    }

    private static WorkbenchDamageView Popup(TMP_FontAsset font)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
        var root = prefab ? PrefabUtility.LoadPrefabContents(PopupPath) : new GameObject("DamagePopup", typeof(RectTransform), typeof(TextMeshPro), typeof(WorkbenchDamageView));
        var text = root.GetComponent<TextMeshPro>();
        text.font = font; text.fontSize = 8; text.fontStyle = FontStyles.Normal; text.fontWeight = FontWeight.Regular;
        text.text = "-18"; text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(3.5f, 1.2f);
        text.color = new Color(1, 0.025f, 0.045f); text.textWrappingMode = TextWrappingModes.NoWrap;
        const string materialPath = Root + "/Materials/DamageText.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(font.material) { name = "DamageText" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.12f, 0.005f, 0.01f));
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
        text.fontSharedMaterial = material;
        root.GetComponent<WorkbenchDamageView>().Configure(text);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
        if (prefab) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root);
        EditorUtility.SetDirty(material);

        return saved.GetComponent<WorkbenchDamageView>();
    }
}
