using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class WorkbenchHudBuilder
{
    private const string ButtonPath = "Assets/_Features/Navigation/Prefabs/WorkbenchButton.prefab";
    private const float PanelWidth = 320f;
    private const float Gap = 6f;
    private const float RowHeight = 32f;
    private const float BodySize = 15f;
    private static readonly Color Panel = new(0.075f, 0.088f, 0.11f);
    private static readonly Color TextColor = new(0.94f, 0.96f, 0.98f);
    private static readonly Color Muted = new(0.57f, 0.64f, 0.71f);
    private static TMP_FontAsset headingFont;
    private static TMP_FontAsset bodyFont;
    private static GameObject buttonPrefab;
    private static AudioClip clickSound;

    public static void Build(NavMeshWorkbench bench, TMP_FontAsset heading, TMP_FontAsset body)
    {
        headingFont = heading;
        bodyFont = body;
        clickSound = bench.Settings.ClickSound;
        CreateButtonPrefab();
        if (!Object.FindFirstObjectByType<EventSystem>())
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(events, "Navigation UI");
        }
        var canvasObject = new GameObject("Workbench UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(WorkbenchHud));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        var root = canvasObject.GetComponent<RectTransform>();
        var panel = Box("Control panel", root, Panel);
        Anchor(panel, new Vector2(1, 0), Vector2.one, new Vector2(-PanelWidth, 0), Vector2.zero);
        Label("NAVMESH", panel, true, 25, 20, 20, 280, 40);
        Label("УЧЕБНЫЙ ПОЛИГОН", panel, false, 11, 20, 60, 280, 22).color = Muted;
        var viewport = Box("Viewport", panel, Color.clear);
        Anchor(viewport, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-20, -104));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Node("Controls", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
        var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 8;
        vertical.childControlHeight = true; vertical.childForceExpandHeight = false;
        vertical.childControlWidth = true; vertical.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 26;
        var modeSection = Section(content, "Режим");
        var modes = Buttons(modeSection, new[] { "01   Патруль", "02   Препятствия", "03   Стратегии" });
        foreach (var mode in modes)
        {
            mode.GetComponent<LayoutElement>().minHeight = 30;
            mode.GetComponent<LayoutElement>().preferredHeight = 30;
        }
        var modeContent = Section(content, null);
        var fade = modeContent.gameObject.AddComponent<CanvasGroup>();
        var toolSection = Section(modeContent, "Инструменты");
        var pointTools = Row(toolSection);
        var select = Button(pointTools, "Выбрать");
        var point = Button(pointTools, "Точка");
        var obstacleTools = Row(toolSection);
        var block = Button(obstacleTools, "Блок");
        var wall = Button(obstacleTools, "Стена");
        var tools = new[] { select, block, wall, point };
        var pointCount = FlowLabel(toolSection, "Точек: 0", 12, Muted);
        var strategySection = Section(modeContent, "Стратегия");
        var strategyValue = FlowLabel(strategySection, "Авто / Кратчайший", 16, TextColor);
        var strategies = Buttons(strategySection, new[] { "Автоматически", "Кратчайший", "Безопасный", "Скрытный" }, true);
        FlowLabel(strategySection, "СИТУАЦИЯ", 11, Muted, 16);
        var situationRow = Row(strategySection);
        situationRow.GetComponent<LayoutElement>().preferredHeight = 38;
        var situations = Buttons(situationRow, new[] { "Спокойно", "Опасность", "Мало\nздоровья" });
        foreach (var situation in situations)
        {
            situation.GetComponentInChildren<TMP_Text>().fontSize = 12;
            situation.GetComponent<LayoutElement>().preferredHeight = 38;
        }
        var compare = Button(strategySection, "Сравнить три маршрута");
        FlowLabel(strategySection, "<color=#55B7FF>Сплошная</color>   кратчайший\n<color=#FFB34D>Пунктир</color>   безопасный\n<color=#63DB9D>Штрихи</color>   скрытный", 12, Muted, 54);
        var layers = Section(content, "Слои");
        var layerRow = Row(layers);
        var navMesh = Button(layerRow, "NavMesh");
        var cover = Button(layerRow, "Укрытия");
        var healthSection = Section(content, null);
        var health = FlowLabel(healthSection, "100 HP", 18, TextColor);
        var edits = Row(healthSection);
        var undo = Button(edits, "Отменить");
        var reset = Button(edits, "Сброс режима");
        var returnToStart = Button(healthSection, "На старт");
        var utility = Row(healthSection);
        var focus = Button(utility, "К агенту");
        var help = Button(utility, "Справка");
        var bar = Box("Time bar", root, Panel);
        Anchor(bar, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(-PanelWidth, 82));
        var pause = Button(bar, "Пауза");
        Place(pause.GetComponent<RectTransform>(), 20, 21, 142, 40);
        var speeds = new WorkbenchButton[3];
        string[] speedNames = { "0.5x", "1x", "2x" };
        for (int i = 0; i < speeds.Length; i++)
        {
            speeds[i] = Button(bar, speedNames[i]);
            Place(speeds[i].GetComponent<RectTransform>(), 178 + i * 62, 21, 54, 40);
        }
        var status = Label("Патруль", bar, false, 14, 382, 18, 380, 46);
        status.color = Muted;
        var helpOverlay = Box("Help overlay", root, new Color(0.02f, 0.03f, 0.04f, 0.72f));
        Anchor(helpOverlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var helpPanel = Box("Help", helpOverlay, Panel);
        helpPanel.anchorMin = helpPanel.anchorMax = new Vector2(0.5f, 0.5f);
        helpPanel.pivot = new Vector2(0.5f, 0.5f); helpPanel.sizeDelta = new Vector2(540, 510);
        Label("Управление", helpPanel, true, 24, 28, 24, 484, 42);
        Label("WASD / средняя кнопка мыши\nПеремещение камеры\n\nКолесо мыши   Приближение\nЛевая кнопка   Поставить / выбрать\nПеретаскивание   Переместить точку\nПравая кнопка   Удалить объект\nR   Повернуть препятствие\nCtrl+Z   Отменить действие\nПробел   Пауза / продолжить", helpPanel, false, 17, 28, 92, 484, 318);
        var closeHelp = Button(helpPanel, "Понятно");
        Place(closeHelp.GetComponent<RectTransform>(), 28, 438, 484, 44);
        helpOverlay.gameObject.SetActive(false);
        canvasObject.GetComponent<WorkbenchHud>().Configure(bench, modes, tools, strategies, situations, speeds,
            new[] { compare, navMesh, cover, pause, undo, reset, returnToStart, focus, help, closeHelp },
            strategySection.gameObject, helpOverlay.gameObject, new[] { strategyValue, health, status, pointCount }, fade);
        Undo.RegisterCreatedObjectUndo(canvasObject, "Navigation UI");
        EditorUtility.SetDirty(canvasObject);
    }

    private static void CreateButtonPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Features/Navigation/Prefabs")) AssetDatabase.CreateFolder("Assets/_Features/Navigation", "Prefabs");
        var temp = new GameObject("WorkbenchButton", typeof(RectTransform), typeof(Image), typeof(WorkbenchButton), typeof(LayoutElement));
        var image = temp.GetComponent<Image>();
        image.color = new Color(0.19f, 0.21f, 0.25f);
        var button = temp.GetComponent<WorkbenchButton>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        var text = Label("Кнопка", temp.transform, false, BodySize, 10, 0, 240, RowHeight);
        Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(9, 0), new Vector2(-9, 0));
        text.alignment = TextAlignmentOptions.Center;
        button.Configure(text);
        button.ConfigureAudio(clickSound);
        var layout = temp.GetComponent<LayoutElement>();
        layout.preferredHeight = RowHeight; layout.minHeight = RowHeight; layout.flexibleWidth = 1;
        buttonPrefab = PrefabUtility.SaveAsPrefabAsset(temp, ButtonPath);
        Object.DestroyImmediate(temp);
    }

    private static WorkbenchButton[] Buttons(Transform parent, string[] labels, bool paired = false)
    {
        var result = new WorkbenchButton[labels.Length];
        Transform row = parent;
        for (int i = 0; i < result.Length; i++)
        {
            if (paired && i % 2 == 0) row = Row(parent);
            result[i] = Button(row, labels[i]);
        }

        return result;
    }

    private static WorkbenchButton Button(Transform parent, string label)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, parent);
        instance.name = label;
        var button = instance.GetComponent<WorkbenchButton>();
        button.SetText(label);

        return button;
    }

    private static RectTransform Section(Transform parent, string title)
    {
        var node = Node(title ?? "Group", parent);
        var layout = node.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = Gap;
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        layout.childControlWidth = true; layout.childForceExpandWidth = true;
        if (!string.IsNullOrEmpty(title)) FlowLabel(node, title.ToUpperInvariant(), 11, Muted, 20);

        return node;
    }

    private static RectTransform Row(Transform parent)
    {
        var node = Node("Row", parent);
        var layout = node.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = Gap; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        node.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;

        return node;
    }

    private static TMP_Text FlowLabel(Transform parent, string value, float size, Color color, float height = 26)
    {
        var text = Label(value, parent, false, size, 0, 0, 280, height);
        text.color = color;
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

        return text;
    }

    private static TMP_Text Label(string value, Transform parent, bool heading, float size, float x, float y, float width, float height)
    {
        var rect = Node("Label", parent);
        Place(rect, x, y, width, height);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        heading |= WorkbenchPresentationPatch.IsHeading(value);
        text.font = heading ? headingFont : bodyFont;
        text.text = value; text.fontSize = size; text.color = TextColor;
        text.fontStyle = FontStyles.Normal;
        text.fontWeight = FontWeight.Regular;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        return text;
    }

    private static RectTransform Box(string name, Transform parent, Color color)
    {
        var node = Node(name, parent);
        node.gameObject.AddComponent<Image>().color = color;

        return node;
    }

    private static RectTransform Node(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform));
        node.transform.SetParent(parent, false);

        return node.GetComponent<RectTransform>();
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
}
