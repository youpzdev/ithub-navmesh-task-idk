using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public static class WorkbenchPresentationChecks
{
    [Serializable]
    public class Report { public int passed; public List<string> failures = new(); }

    public static string CheckFonts()
    {
        var report = new Report();
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool heading = IsHeading(text.text);
            string family = text.font ? text.font.faceInfo.familyName : "null";
            string style = text.font ? text.font.faceInfo.styleName : "null";
            Expect(report, heading ? family.Contains("Unbounded") : family.Contains("Roboto") && style.Contains("Bold"),
                text.name + " '" + text.text + "': font=" + family + "/" + style + ", ожидался " + (heading ? "Unbounded" : "Roboto/Bold"));
            string visible = Regex.Replace(text.text, "<[^>]*>", "").Replace("\n", "").Replace("\r", "");
            if (text.font) Expect(report, text.font.HasCharacters(visible, out uint[] missing, false, false), text.name + ": отсутствуют символы " + string.Join(",", missing ?? Array.Empty<uint>()));
        }

        return JsonUtility.ToJson(report);
    }

    public static string CheckDamage()
    {
        var report = new Report();
        var bench = UnityEngine.Object.FindFirstObjectByType<NavMeshWorkbench>();
        if (!Application.isPlaying || !bench || bench.Rebuilding || bench.Actor.Dead) return "Стенд не готов к проверке урона";
        var actor = bench.Actor;
        var prefab = (WorkbenchDamageView)typeof(WorkbenchActor).GetField("damagePrefab", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(actor);
        if (!prefab) return "Примени WorkbenchPresentationPatch.Apply до проверки урона";
        var position = actor.transform.position;
        float health = actor.Health;
        actor.ResetAt(position, 7);
        actor.SetPaused(false);
        actor.TakeHit(position + Vector3.right);
        Expect(report, actor.Health == 0 && actor.LastDamage == 7, "Добивающий удар: Health=" + actor.Health + ", LastDamage=" + actor.LastDamage + ", ожидалось 0 и 7");
        var popups = UnityEngine.Object.FindObjectsByType<WorkbenchDamageView>(FindObjectsSortMode.None);
        Expect(report, popups.Length == 1 && popups[0].Label.text == "-7", "Попап должен показывать фактически снятые 7 HP, активных попапов=" + popups.Length);
        var targets = (Renderer[])typeof(WorkbenchActor).GetField("flashTargets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(actor);
        foreach (var target in targets)
        {
            var block = new MaterialPropertyBlock(); target.GetPropertyBlock(block);
            Expect(report, block.GetFloat("_HitFlash") == 1, target.name + ": _HitFlash=" + block.GetFloat("_HitFlash") + ", ожидалось 1");
        }
        actor.ResetAt(position, bench.Settings.MaxHealth);
        Expect(report, actor.FlashStrength == 0 && UnityEngine.Object.FindObjectsByType<WorkbenchDamageView>(FindObjectsSortMode.None).Length == 0,
            "Возврат на старт должен убрать вспышку и активные попапы");
        actor.SetPaused(false);
        actor.TakeHit(position + Vector3.right);
        actor.TakeHit(position + Vector3.left);
        popups = UnityEngine.Object.FindObjectsByType<WorkbenchDamageView>(FindObjectsSortMode.None);
        Expect(report, popups.Length == 2, "Два удара должны показать два попапа, получено " + popups.Length);
        actor.ResetAt(position, health);
        actor.SetPaused(bench.Paused);
        Expect(report, UnityEngine.Object.FindObjectsByType<WorkbenchDamageView>(FindObjectsSortMode.None).Length == 0, "После сброса не должно оставаться активных попапов");

        return JsonUtility.ToJson(report);
    }
    public static bool IsHeading(string text) => WorkbenchPresentationPatch.IsHeading(text);
    private static void Expect(Report report, bool value, string failure) { if (value) report.passed++; else report.failures.Add(failure); }
}
