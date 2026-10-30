using System;
using System.Collections.Generic;
using Godot;

namespace WorldForge.UI;

/// <summary>
/// Scalable native-size HUD; owns Godot Controls, but not world state.
/// All world editing stays in GameRoot / WorldSim. The panel consumes GUI input
/// so clicking a tool never paints the terrain behind the UI.
/// </summary>
public partial class GameHud : PanelContainer
{
    private readonly List<Button> _toolButtons = new();
    private Label _readout = null!;
    private Label _selection = null!;
    private Label _speedLabel = null!;

    public event Action<int>? ToolSelected;
    public event Action<int>? RadiusChangeRequested;
    public event Action<int>? SpeedRequested;
    public event Action? NewWorldRequested;

    private static readonly Color Background = new("#142130F2");
    private static readonly Color Surface = new("#283C4D");
    private static readonly Color Hover = new("#3A5264");
    private static readonly Color Active = new("#338F83");
    private static readonly Color Foreground = new("#F6F4E9");
    private static readonly Color Muted = new("#B6C9D4");

    public GameHud()
    {
        Name = "GameHud";
        MouseFilter = Control.MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", Box(Background, 12));

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 16);
        padding.AddThemeConstantOverride("margin_right", 16);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(padding);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 10);
        padding.AddChild(rows);

        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 8);
        rows.AddChild(tools);

        Label heading = MakeLabel("WORLD FORGE", 21, Foreground);
        heading.CustomMinimumSize = new Vector2(158, 43);
        tools.AddChild(heading);

        var terrainNames = new[] { "Land", "Ocean", "Sand", "Forest", "Mountain", "Person", "Meteor" };
        for (int index = 0; index < terrainNames.Length; index++)
        {
            int selected = index;
            Button button = MakeButton(terrainNames[index], () =>
            {
                SetSelectedTool(selected);
                ToolSelected?.Invoke(selected);
            }, 100, 43);
            button.TooltipText = $"Choose {terrainNames[index]} brush";
            tools.AddChild(button);
            _toolButtons.Add(button);
        }
        SetSelectedTool(5);

        var options = new HBoxContainer();
        options.AddThemeConstantOverride("separation", 8);
        rows.AddChild(options);

        _selection = MakeLabel("Brush: Person  |  Radius: 3", 17, Foreground);
        _selection.CustomMinimumSize = new Vector2(245, 43);
        options.AddChild(_selection);

        options.AddChild(MakeButton("Size -", () => RadiusChangeRequested?.Invoke(-1), 96, 43));
        options.AddChild(MakeButton("Size +", () => RadiusChangeRequested?.Invoke(+1), 96, 43));

        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        options.AddChild(spacer);

        _speedLabel = MakeLabel("Time  1x", 17, Muted);
        _speedLabel.CustomMinimumSize = new Vector2(89, 43);
        options.AddChild(_speedLabel);

        options.AddChild(MakeButton("Pause", () => SpeedRequested?.Invoke(0), 90, 43));
        options.AddChild(MakeButton("1x", () => SpeedRequested?.Invoke(1), 57, 43));
        options.AddChild(MakeButton("2x", () => SpeedRequested?.Invoke(2), 57, 43));
        options.AddChild(MakeButton("4x", () => SpeedRequested?.Invoke(4), 57, 43));
        options.AddChild(MakeButton("New World", () => NewWorldRequested?.Invoke(), 121, 43));

        _readout = MakeLabel("", 16, Muted);
        rows.AddChild(_readout);
    }

    public void SetSelectedTool(int index)
    {
        for (int i = 0; i < _toolButtons.Count; i++)
        {
            bool active = i == index;
            _toolButtons[i].AddThemeStyleboxOverride("normal", Box(active ? Active : Surface, 7));
            _toolButtons[i].AddThemeStyleboxOverride("hover", Box(active ? Active : Hover, 7));
            _toolButtons[i].AddThemeColorOverride("font_color", Foreground);
        }
    }

    public void UpdateState(string toolName, int radius, int speed, int seed, int population, long tick)
    {
        _selection.Text = $"Brush: {toolName}  |  Radius: {radius}";
        _speedLabel.Text = $"Time  {speed}x";
        _readout.Text = $"Seed {seed}    People {population}    Tick {tick}     Left-click paint / spawn   |   Right-drag pan   |   Wheel zoom   |   Space pause";
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    private static Button MakeButton(string text, Action clicked, float width, float height)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, height),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        button.AddThemeFontSizeOverride("font_size", 17);
        button.AddThemeColorOverride("font_color", Foreground);
        button.AddThemeColorOverride("font_hover_color", Foreground);
        button.AddThemeColorOverride("font_pressed_color", Foreground);
        button.AddThemeStyleboxOverride("normal", Box(Surface, 7));
        button.AddThemeStyleboxOverride("hover", Box(Hover, 7));
        button.AddThemeStyleboxOverride("pressed", Box(Active, 7));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += clicked;
        return button;
    }

    private static StyleBoxFlat Box(Color color, int radius) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius
    };
}
