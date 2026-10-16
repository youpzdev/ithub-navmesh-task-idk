using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class WorkbenchHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshWorkbench workbench;
    [SerializeField] private WorkbenchButton[] modes;
    [SerializeField] private WorkbenchButton[] tools;
    [SerializeField] private WorkbenchButton[] strategies;
    [SerializeField] private WorkbenchButton[] situations;
    [SerializeField] private WorkbenchButton[] speeds;
    [SerializeField] private WorkbenchButton compare;
    [SerializeField] private WorkbenchButton navMesh;
    [SerializeField] private WorkbenchButton cover;
    [SerializeField] private WorkbenchButton pause;
    [SerializeField] private WorkbenchButton undo;
    [SerializeField] private WorkbenchButton reset;
    [SerializeField] private WorkbenchButton returnToStart;
    [SerializeField] private WorkbenchButton focus;
    [SerializeField] private WorkbenchButton help;
    [SerializeField] private WorkbenchButton closeHelp;
    [SerializeField] private GameObject strategyGroup;
    [SerializeField] private GameObject helpWindow;
    [SerializeField] private TMP_Text currentStrategy;
    [SerializeField] private TMP_Text health;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_Text pointCount;
    [SerializeField] private CanvasGroup modeContent;
    private WorkbenchMode lastMode = (WorkbenchMode)(-1);
    private readonly List<HudButtonBinding> bindings = new();
    private readonly string[] strategyNames = { "Кратчайший", "Безопасный", "Скрытный" };

    public void Configure(NavMeshWorkbench bench, WorkbenchButton[] modeButtons, WorkbenchButton[] toolButtons,
        WorkbenchButton[] strategyButtons, WorkbenchButton[] situationButtons, WorkbenchButton[] speedButtons,
        WorkbenchButton[] actions, GameObject strategiesRoot, GameObject helpRoot, TMP_Text[] texts, CanvasGroup content)
    {
        workbench = bench;
        modes = modeButtons; tools = toolButtons; strategies = strategyButtons; situations = situationButtons; speeds = speedButtons;
        compare = actions[0]; navMesh = actions[1]; cover = actions[2]; pause = actions[3]; undo = actions[4]; reset = actions[5];
        returnToStart = actions[6]; focus = actions[7]; help = actions[8]; closeHelp = actions[9];
        strategyGroup = strategiesRoot; helpWindow = helpRoot;
        currentStrategy = texts[0]; health = texts[1]; status = texts[2]; pointCount = texts[3]; modeContent = content;
    }

    private void OnEnable()
    {
        workbench.SetHelpOpen(helpWindow.activeSelf);
        lastMode = (WorkbenchMode)(-1);
        for (int i = 0; i < modes.Length; i++) { int index = i; Bind(modes[i], () => workbench.SetMode(index)); }
        for (int i = 0; i < tools.Length; i++) { int index = i; Bind(tools[i], () => workbench.SetTool(index)); }
        for (int i = 0; i < strategies.Length; i++) { int index = i - 1; Bind(strategies[i], () => workbench.SetStrategy(index)); }
        for (int i = 0; i < situations.Length; i++) { int index = i; Bind(situations[i], () => workbench.SetSituation(index)); }
        float[] values = { 0.5f, 1f, 2f };
        for (int i = 0; i < speeds.Length; i++) { float speed = values[i]; Bind(speeds[i], () => workbench.SetSpeed(speed)); }
        Bind(compare, workbench.ToggleCompare);
        Bind(navMesh, workbench.ToggleNavMesh);
        Bind(cover, workbench.ToggleCover);
        Bind(pause, workbench.TogglePause);
        Bind(undo, workbench.Undo);
        Bind(reset, workbench.ResetMode);
        Bind(returnToStart, workbench.ReturnToStart);
        Bind(focus, workbench.FocusAgent);
        Bind(help, () => ShowHelp(true));
        Bind(closeHelp, () => ShowHelp(false));
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (helpWindow.activeSelf != workbench.HelpOpen) ShowHelp(workbench.HelpOpen);
        bool strategyMode = workbench.Mode == WorkbenchMode.Strategies;
        if (lastMode != workbench.Mode)
        {
            lastMode = workbench.Mode;
            strategyGroup.SetActive(strategyMode);
            tools[1].transform.parent.gameObject.SetActive(workbench.Mode != WorkbenchMode.Patrol);

            tools[3].gameObject.SetActive(workbench.Mode != WorkbenchMode.Obstacles);
            modeContent.DOKill();
            modeContent.alpha = 0.35f;
            modeContent.DOFade(1f, 0.18f).SetUpdate(true);
        }
        for (int i = 0; i < modes.Length; i++) modes[i].SetSelected((int)workbench.Mode == i);
        for (int i = 0; i < tools.Length; i++) tools[i].SetSelected((int)workbench.Tool == i);
        for (int i = 0; i < strategies.Length; i++) strategies[i].SetSelected(i == 0 ? workbench.Automatic : !workbench.Automatic && (int)workbench.Strategy == i - 1);
        for (int i = 0; i < speeds.Length; i++) speeds[i].SetSelected(Mathf.Approximately(workbench.SimulationSpeed, i == 0 ? 0.5f : i == 1 ? 1f : 2f));
        for (int i = 0; i < situations.Length; i++) situations[i].SetSelected(workbench.Situation == i);
        compare.SetSelected(workbench.CompareRoutes);
        navMesh.SetSelected(workbench.ShowNavMesh);
        cover.SetSelected(workbench.ShowCover);
        pause.SetSelected(workbench.Paused);
        pause.SetText(workbench.Paused ? "Продолжить" : "Пауза");
        undo.interactable = workbench.UndoCount > 0;
        string strategy = strategyNames[(int)workbench.Strategy];
        SetText(currentStrategy, (workbench.Automatic ? "Авто / " : "") + strategy + (workbench.Risky ? " !" : ""));
        SetText(health, Mathf.CeilToInt(workbench.Health) + " HP");
        SetText(status, workbench.Status);
        SetText(pointCount, "Точек: " + workbench.PointCount);
    }

    public void ShowHelp(bool visible)
    {
        workbench.SetHelpOpen(visible);
        helpWindow.SetActive(visible);
        if (!visible) return;
        var content = helpWindow.transform.GetChild(0);
        content.DOKill();
        content.localScale = Vector3.one * 0.96f;
        content.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private static void SetText(TMP_Text target, string value) { if (target.text != value) target.text = value; }

    private void Bind(WorkbenchButton button, UnityAction action)
    {
        button.onClick.AddListener(action);
        bindings.Add(new HudButtonBinding(button, action));
    }

    private void OnDisable()
    {
        foreach (var binding in bindings) binding.button.onClick.RemoveListener(binding.action);
        bindings.Clear();
        modeContent.DOKill();
        modeContent.alpha = 1;
        helpWindow.transform.GetChild(0).DOKill();
        helpWindow.transform.GetChild(0).localScale = Vector3.one;
    }
}

internal class HudButtonBinding
{
    public readonly WorkbenchButton button;
    public readonly UnityAction action;
    public HudButtonBinding(WorkbenchButton button, UnityAction action) { this.button = button; this.action = action; }
}
