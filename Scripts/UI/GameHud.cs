using System;
using System.Collections.Generic;
using Godot;

namespace WorldForge.UI;

/// <summary>
/// Compact two-row in-game HUD. Long tool lists live in native MenuButton
/// popups instead of widening the viewport or covering the island.
/// The HUD only emits actions; world, simulation and saves remain elsewhere.
/// </summary>
public partial class GameHud : PanelContainer
{
    private static readonly string[] ToolNames =
    {
        "Land", "Ocean", "Sand", "Forest", "Mountain", "Person", "Meteor", "Gather"
    };
    private static readonly int[][] ToolGroups =
    {
        new[] { 0, 1, 2, 4 }, // Terrain
        new[] { 3, 7 },       // Nature
        new[] { 5 },          // Life
        new[] { 6 }           // Powers
    };
    private static readonly string[] GroupNames =
    {
        "TERRAIN", "NATURE", "LIFE", "POWERS"
    };

    private readonly List<MenuButton> _groups=new();
    private readonly MenuButton _speedButton;
    private readonly MenuButton _detailsButton;
    private readonly Label _toolLabel;
    private readonly Label _radiusLabel;
    private readonly Label _status;
    private readonly Label _timing;
    private readonly Label _fps;
    private int _selectedTool=5;
    private int _population,_woodAvailable,_foodAvailable,_workers;
    private int _woodHarvested,_foodHarvested,_seed;
    private long _tick;
    private string _shelterStatus="Off";
    private int _lastFps;
    private double _lastWorstFrame;
    private bool _hasFps;

    public event Action<int>? ToolSelected;
    public event Action<int>? RadiusChangeRequested;
    public event Action<int>? SpeedRequested;
    public event Action? NewWorldRequested;
    public event Action? MenuRequested;

    private static readonly Color Background=new("#142330F2");
    private static readonly Color Surface=new("#29404F");
    private static readonly Color Hover=new("#3B586A");
    private static readonly Color Active=new("#2E9184");
    private static readonly Color Foreground=new("#F7F4E9");
    private static readonly Color Muted=new("#B7CDD3");

    // An actual no-argument constructor is needed by the old 2D scene and Godot.
    public GameHud():this(false) {}

    public GameHud(bool showGameMenu)
    {
        Name="GameHud";
        MouseFilter=MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel",Style(Background,12));

        var padding=new MarginContainer();
        padding.AddThemeConstantOverride("margin_left",12);
        padding.AddThemeConstantOverride("margin_right",12);
        padding.AddThemeConstantOverride("margin_top",7);
        padding.AddThemeConstantOverride("margin_bottom",7);
        AddChild(padding);

        var rows=new VBoxContainer();
        rows.AddThemeConstantOverride("separation",4);
        padding.AddChild(rows);

        var top=new HBoxContainer();
        top.AddThemeConstantOverride("separation",6);
        rows.AddChild(top);

        var title=MakeLabel("WORLD FORGE",18,Foreground);
        title.CustomMinimumSize=new Vector2(139,40);
        top.AddChild(title);

        if(showGameMenu)
        {
            Button menu=MakeButton("MENU",()=>MenuRequested?.Invoke(),65,39);
            menu.TooltipText="Pause, save, load, help, settings and quit (Esc).";
            top.AddChild(menu);
        }
        else
        {
            Button newer=MakeButton("NEW",()=>NewWorldRequested?.Invoke(),59,39);
            newer.TooltipText="Create a new empty world.";
            top.AddChild(newer);
        }

        for(int group=0;group<ToolGroups.Length;group++)
        {
            int category=group;
            var button=new MenuButton
            {
                Text=GroupNames[group]+"  ▾",
                CustomMinimumSize=new Vector2(group==0?94:group==1?93:group==2?73:90,39),
                FocusMode=FocusModeEnum.None,
                MouseFilter=MouseFilterEnum.Stop,
                Flat=false
            };
            StyleButton(button,Surface);
            button.TooltipText="Choose a "+GroupNames[group].ToLowerInvariant()+" tool.";
            PopupMenu popup=button.GetPopup();
            popup.AddThemeFontSizeOverride("font_size",16);
            foreach(int index in ToolGroups[category])
            {
                // The original 2D scene has no Gather brush.
                if(!showGameMenu && index==7)continue;
                popup.AddItem(ToolNames[index],index);
            }
            popup.IdPressed+=id=>
            {
                int index=checked((int)id);
                SetSelectedTool(index);
                ToolSelected?.Invoke(index);
            };
            top.AddChild(button);
            _groups.Add(button);
        }

        _toolLabel=MakeLabel("PERSON",15,Foreground);
        _toolLabel.CustomMinimumSize=new Vector2(91,39);
        _toolLabel.ClipText=true;
        _toolLabel.HorizontalAlignment=HorizontalAlignment.Center;
        _toolLabel.TooltipText="Currently selected tool; choose a different one from the categories.";
        top.AddChild(_toolLabel);

        var minus=MakeButton("−",()=>RadiusChangeRequested?.Invoke(-1),29,39);
        minus.TooltipText="Decrease brush radius.";
        top.AddChild(minus);

        _radiusLabel=MakeLabel("R 2",15,Muted);
        _radiusLabel.HorizontalAlignment=HorizontalAlignment.Center;
        _radiusLabel.CustomMinimumSize=new Vector2(42,39);
        top.AddChild(_radiusLabel);

        var plus=MakeButton("+",()=>RadiusChangeRequested?.Invoke(1),29,39);
        plus.TooltipText="Increase brush radius.";
        top.AddChild(plus);

        var spacer=new Control
        {
            SizeFlagsHorizontal=SizeFlags.ExpandFill,
            MouseFilter=MouseFilterEnum.Ignore
        };
        top.AddChild(spacer);

        _speedButton=new MenuButton
        {
            Text="1x  ▾",
            CustomMinimumSize=new Vector2(76,39),
            FocusMode=FocusModeEnum.None,
            MouseFilter=MouseFilterEnum.Stop,
            Flat=false
        };
        StyleButton(_speedButton,Surface);
        _speedButton.TooltipText="Simulation speed (Space toggles pause).";
        PopupMenu speedOptions=_speedButton.GetPopup();
        speedOptions.AddThemeFontSizeOverride("font_size",16);
        speedOptions.AddItem("Pause",0);
        speedOptions.AddItem("1x",1);
        speedOptions.AddItem("2x",2);
        speedOptions.AddItem("4x",4);
        speedOptions.IdPressed+=id=>SpeedRequested?.Invoke(checked((int)id));
        top.AddChild(_speedButton);

        _fps=MakeLabel("FPS --",14,Muted);
        _fps.HorizontalAlignment=HorizontalAlignment.Center;
        _fps.CustomMinimumSize=new Vector2(64,39);
        _fps.TooltipText="Measured frame rate over the last second.";
        top.AddChild(_fps);

        var separator=new HSeparator();
        rows.AddChild(separator);

        var bottom=new HBoxContainer();
        bottom.AddThemeConstantOverride("separation",8);
        rows.AddChild(bottom);

        _status=MakeLabel("0 people  ·  Place citizens using LIFE > Person",14,Muted);
        _status.SizeFlagsHorizontal=SizeFlags.ExpandFill;
        _status.ClipText=true;
        _status.CustomMinimumSize=new Vector2(0,23);
        bottom.AddChild(_status);

        _timing=MakeLabel("Tick 0",13,Muted);
        _timing.CustomMinimumSize=new Vector2(158,23);
        _timing.HorizontalAlignment=HorizontalAlignment.Right;
        bottom.AddChild(_timing);

        _detailsButton=new MenuButton
        {
            Text="DETAILS  ▾",
            CustomMinimumSize=new Vector2(100,27),
            FocusMode=FocusModeEnum.None,
            MouseFilter=MouseFilterEnum.Stop,
            Flat=false
        };
        StyleButton(_detailsButton,Surface);
        _detailsButton.AddThemeFontSizeOverride("font_size",12);
        _detailsButton.TooltipText="Full world, housing and performance figures.";
        PopupMenu details=_detailsButton.GetPopup();
        details.AddThemeFontSizeOverride("font_size",15);
        for(int i=0;i<8;i++)
        {
            details.AddItem("Loading...",i);
            details.SetItemDisabled(i,true);
        }
        bottom.AddChild(_detailsButton);

        SetSelectedTool(5);
        RefreshSummary();
    }

    public void SetSelectedTool(int index)
    {
        if(index<0 || index>=ToolNames.Length)return;
        _selectedTool=index;
        SetTextIfChanged(_toolLabel,ToolNames[index].ToUpperInvariant());
        for(int group=0;group<_groups.Count;group++)
        {
            bool active=Array.IndexOf(ToolGroups[group],_selectedTool)>=0;
            StyleButton(_groups[group],active?Active:Surface);
        }
    }

    public void UpdateState(string toolName,int radius,int speed,int seed,int population,long tick)
    {
        _population=population;
        _tick=tick;
        _seed=seed;
        SetTextIfChanged(_toolLabel,
            toolName.StartsWith("Sculpt",StringComparison.OrdinalIgnoreCase)
                ? "SCULPT" : toolName.ToUpperInvariant());
        _toolLabel.TooltipText=toolName;
        SetTextIfChanged(_radiusLabel,$"R {radius}");
        SetTextIfChanged(_speedButton,speed==0?"PAUSE ▾":$"{speed}x  ▾");
        RefreshSummary();
    }

    public void UpdateResourceStock(int wood,int food,int workers=0,
        int woodHarvested=0,int foodHarvested=0)
    {
        if(wood<0 || food<0 || workers<0 || woodHarvested<wood ||
            foodHarvested<food)
            throw new ArgumentOutOfRangeException(nameof(wood));
        _woodAvailable=wood;
        _foodAvailable=food;
        _workers=workers;
        _woodHarvested=woodHarvested;
        _foodHarvested=foodHarvested;
        RefreshSummary();
    }

    public void UpdateShelterStatus(string status)
    {
        _shelterStatus=status;
        RefreshSummary();
    }

    /// <summary>Actual one-second frame summary, not a GPU profiler.</summary>
    public void UpdatePerformance(int fps,double worstFrameMilliseconds)
    {
        _hasFps=true;
        _lastFps=fps;
        _lastWorstFrame=worstFrameMilliseconds;
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        string housing=_shelterStatus.Split(';')[0].Trim();
        string overview=_population==0
            ? "0 people  ·  Choose LIFE > Person to place your first citizens"
            : $"People {_population:N0}  ·  {housing}  ·  Wood {_woodAvailable:N0}" +
              $"  ·  Food {_foodAvailable:N0}  ·  Working {_workers:N0}";
        SetTextIfChanged(_status,overview);
        SetTextIfChanged(_fps,_hasFps?$"{_lastFps} FPS":"FPS --");
        SetTextIfChanged(_timing,_hasFps
            ? $"Tick {_tick:N0}  ·  {_lastWorstFrame:F1} ms"
            : $"Tick {_tick:N0}");
        _status.TooltipText=$"{overview}\n{_shelterStatus}";
        PopupMenu details=_detailsButton.GetPopup();
        string[] lines=
        {
            $"Seed: {_seed} | Tick: {_tick:N0}",
            $"Population: {_population:N0} | Working: {_workers:N0}",
            $"Housing: {_shelterStatus}",
            $"Wood available: {_woodAvailable:N0}",
            $"Wood harvested (historical): {_woodHarvested:N0}",
            $"Food available: {_foodAvailable:N0}",
            $"Food harvested (historical): {_foodHarvested:N0}",
            _hasFps?$"FPS: {_lastFps} | Worst frame: {_lastWorstFrame:F1} ms":"FPS: measuring..."
        };
        for(int i=0;i<lines.Length;i++)
            if(details.GetItemText(i)!=lines[i])
                details.SetItemText(i,lines[i]);
    }

    private static void SetTextIfChanged(Label label,string text)
    {
        if(label.Text!=text)label.Text=text;
    }

    private static void SetTextIfChanged(MenuButton button,string text)
    {
        if(button.Text!=text)button.Text=text;
    }

    private static Label MakeLabel(string text,int size,Color color)
    {
        var label=new Label { Text=text,VerticalAlignment=VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size",size);
        label.AddThemeColorOverride("font_color",color);
        label.MouseFilter=MouseFilterEnum.Ignore;
        return label;
    }

    private static Button MakeButton(string text,Action action,float width,float height)
    {
        var button=new Button
        {
            Text=text,CustomMinimumSize=new Vector2(width,height),
            FocusMode=FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop
        };
        StyleButton(button,Surface);
        button.Pressed+=action;
        return button;
    }

    private static void StyleButton(Button button,Color baseColor)
    {
        button.AddThemeFontSizeOverride("font_size",14);
        button.AddThemeColorOverride("font_color",Foreground);
        button.AddThemeColorOverride("font_hover_color",Foreground);
        button.AddThemeColorOverride("font_pressed_color",Foreground);
        button.AddThemeStyleboxOverride("normal",Style(baseColor,7));
        button.AddThemeStyleboxOverride("hover",Style(Hover,7));
        button.AddThemeStyleboxOverride("pressed",Style(Active,7));
        button.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());
    }

    private static StyleBoxFlat Style(Color color,int radius)=>new()
    {
        BgColor=color,CornerRadiusTopLeft=radius,
        CornerRadiusTopRight=radius,CornerRadiusBottomLeft=radius,
        CornerRadiusBottomRight=radius
    };
}
