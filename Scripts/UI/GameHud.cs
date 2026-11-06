using System;
using System.Collections.Generic;
using Godot;

namespace WorldForge.UI;

/// <summary>
/// Two floating HUD islands, not a viewport-wide opaque header. Tool choices
/// stay in native dropdowns; the full statistics are built only on demand.
/// HUD events retain the existing V2/legacy brush and simulation contracts.
/// </summary>
public partial class GameHud : PanelContainer
{
    private static readonly string[] ToolNames =
    {
        "Land","Ocean","Sand","Forest","Mountain","Person","Meteor","Gather"
    };
    private static readonly int[][] ToolGroups =
    {
        new[] {0,1,2,4},
        new[] {3,7},
        new[] {5},
        new[] {6}
    };
    private static readonly string[] GroupNames={"TERRAIN","NATURE","LIFE","POWERS"};
    private readonly List<MenuButton> _groups=new();
    private readonly MenuButton _speed;
    private readonly MenuButton _details;
    private readonly Label _currentTool;
    private readonly Label _radius;
    private readonly Label _peopleHomes;
    private readonly Label _supplies;
    private readonly Label _fps;
    private int _selectedTool=5,_population,_seed,_workers;
    private int _woodAvailable,_foodAvailable,_woodHarvested,_foodHarvested;
    private long _tick;
    private string _housing="Off";
    private int _lastFps;
    private double _lastWorstFrame;
    private bool _hasFps;

    public event Action<int>? ToolSelected;
    public event Action<int>? RadiusChangeRequested;
    public event Action<int>? SpeedRequested;
    public event Action? NewWorldRequested;
    public event Action? MenuRequested;

    private static readonly Color Background=new("#142432E9");
    private static readonly Color Surface=new("#294250");
    private static readonly Color Hover=new("#41606B");
    private static readonly Color Active=new("#32958A");
    private static readonly Color Foreground=new("#F8F5EB");
    private static readonly Color Muted=new("#C4D5D9");

    // Godot and the original 2D game expect a true no-parameter constructor.
    public GameHud():this(false) {}

    public GameHud(bool showGameMenu)
    {
        Name="GameHud";
        MouseFilter=MouseFilterEnum.Pass;
        AddThemeStyleboxOverride("panel",new StyleBoxEmpty());

        var fullRow=new HBoxContainer
        {
            MouseFilter=MouseFilterEnum.Ignore
        };
        fullRow.AddThemeConstantOverride("separation",10);
        AddChild(fullRow);

        // Left floating dock: tools, selection, brush radius and time.
        var toolsPanel=new PanelContainer{MouseFilter=MouseFilterEnum.Stop};
        toolsPanel.AddThemeStyleboxOverride("panel",PanelStyle());
        fullRow.AddChild(toolsPanel);
        var toolsMargin=new MarginContainer();
        toolsMargin.AddThemeConstantOverride("margin_left",9);
        toolsMargin.AddThemeConstantOverride("margin_right",9);
        toolsMargin.AddThemeConstantOverride("margin_top",6);
        toolsMargin.AddThemeConstantOverride("margin_bottom",6);
        toolsPanel.AddChild(toolsMargin);
        var bar=new HBoxContainer();
        bar.AddThemeConstantOverride("separation",5);
        toolsMargin.AddChild(bar);

        var title=Label("WORLD FORGE",17,Foreground,115);
        bar.AddChild(title);

        Button main=Button(showGameMenu?"MENU":"NEW",
            ()=>{if(showGameMenu)MenuRequested?.Invoke();else NewWorldRequested?.Invoke();},
            54);
        main.TooltipText=showGameMenu?"Pause, save, load, settings and quit (Esc)":
            "Start a new world.";
        bar.AddChild(main);

        for(int category=0;category<ToolGroups.Length;category++)
        {
            int group=category;
            var groupButton=Dropdown(GroupNames[category]+" ▾",
                category switch {0=>83,1=>77,2=>57,_=>79});
            PopupMenu popup=groupButton.GetPopup();
            popup.AddThemeFontSizeOverride("font_size",15);
            foreach(int index in ToolGroups[group])
            {
                // The older 2D scene does not implement Gather.
                if(!showGameMenu && index==7)continue;
                popup.AddItem(ToolNames[index],index);
            }
            popup.IdPressed+=id=>
            {
                int tool=checked((int)id);
                SetSelectedTool(tool);
                ToolSelected?.Invoke(tool);
            };
            groupButton.TooltipText=$"Choose a {GroupNames[group].ToLowerInvariant()} tool.";
            bar.AddChild(groupButton);
            _groups.Add(groupButton);
        }

        _currentTool=Label("PERSON",13,Foreground,70);
        _currentTool.HorizontalAlignment=HorizontalAlignment.Center;
        _currentTool.ClipText=true;
        _currentTool.TooltipText="Selected brush";
        bar.AddChild(_currentTool);

        var smaller=Button("−",()=>RadiusChangeRequested?.Invoke(-1),26);
        smaller.TooltipText="Decrease brush size";
        bar.AddChild(smaller);
        _radius=Label("R2",14,Muted,28);
        _radius.HorizontalAlignment=HorizontalAlignment.Center;
        bar.AddChild(_radius);
        var larger=Button("+",()=>RadiusChangeRequested?.Invoke(1),26);
        larger.TooltipText="Increase brush size";
        bar.AddChild(larger);

        _speed=Dropdown("1x ▾",62);
        _speed.TooltipText="Set simulation speed; Space pauses";
        PopupMenu speedPopup=_speed.GetPopup();
        speedPopup.AddThemeFontSizeOverride("font_size",15);
        speedPopup.AddItem("Pause",0);
        speedPopup.AddItem("1x",1);
        speedPopup.AddItem("2x",2);
        speedPopup.AddItem("4x",4);
        speedPopup.IdPressed+=id=>SpeedRequested?.Invoke(checked((int)id));
        bar.AddChild(_speed);

        // Transparent flexible gap leaves the world visible between docks.
        var gap=new Control
        {
            MouseFilter=MouseFilterEnum.Ignore,
            SizeFlagsHorizontal=SizeFlags.ExpandFill
        };
        fullRow.AddChild(gap);

        // Right floating dock: live glanceable stats and full details on demand.
        var statsPanel=new PanelContainer{MouseFilter=MouseFilterEnum.Stop};
        statsPanel.AddThemeStyleboxOverride("panel",PanelStyle());
        fullRow.AddChild(statsPanel);
        var statsMargin=new MarginContainer();
        statsMargin.AddThemeConstantOverride("margin_left",9);
        statsMargin.AddThemeConstantOverride("margin_right",9);
        statsMargin.AddThemeConstantOverride("margin_top",6);
        statsMargin.AddThemeConstantOverride("margin_bottom",6);
        statsPanel.AddChild(statsMargin);
        var stats=new HBoxContainer();
        stats.AddThemeConstantOverride("separation",7);
        statsMargin.AddChild(stats);

        _peopleHomes=Label("0 people",13,Foreground,82);
        _peopleHomes.TooltipText="Live population and completed housing";
        stats.AddChild(_peopleHomes);

        _supplies=Label("W0 F0",13,Muted,73);
        _supplies.TooltipText="Available wood / food";
        stats.AddChild(_supplies);

        _fps=Label("FPS --",13,Muted,54);
        _fps.HorizontalAlignment=HorizontalAlignment.Right;
        stats.AddChild(_fps);

        _details=Dropdown("INFO ▾",62);
        _details.TooltipText="Full world, housing and frame-time statistics";
        PopupMenu detailsPopup=_details.GetPopup();
        detailsPopup.AddThemeFontSizeOverride("font_size",15);
        for(int i=0;i<8;i++)
        {
            detailsPopup.AddItem("...",i);
            detailsPopup.SetItemDisabled(i,true);
        }
        // Previously the entire 8-row popup was rewritten multiple times
        // every 250ms, even while closed. Populate only when actually opened.
        detailsPopup.AboutToPopup+=UpdateDetailsPopup;
        stats.AddChild(_details);

        SetSelectedTool(5);
        RefreshGlance();
    }

    public void SetSelectedTool(int index)
    {
        if(index<0 || index>=ToolNames.Length)return;
        _selectedTool=index;
        SetText(_currentTool,ToolNames[index].ToUpperInvariant());
        for(int group=0;group<_groups.Count;group++)
        {
            bool selected=Array.IndexOf(ToolGroups[group],index)>=0;
            StyleButton(_groups[group],selected?Active:Surface);
        }
    }

    public void UpdateState(string toolName,int radius,int speed,int seed,int population,long tick)
    {
        _population=population;
        _seed=seed;
        _tick=tick;
        string displayed=toolName.StartsWith("Sculpt",StringComparison.OrdinalIgnoreCase)
            ?"SCULPT":toolName.ToUpperInvariant();
        SetText(_currentTool,displayed);
        if(_currentTool.TooltipText!=toolName)
            _currentTool.TooltipText=toolName;
        SetText(_radius,$"R{radius}");
        SetText(_speed,speed==0?"PAUSE ▾":$"{speed}x ▾");
        RefreshGlance();
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
        RefreshGlance();
    }

    public void UpdateShelterStatus(string status)
    {
        _housing=status;
        RefreshGlance();
    }

    public void UpdatePerformance(int fps,double worstFrameMilliseconds)
    {
        _hasFps=true;
        _lastFps=fps;
        _lastWorstFrame=worstFrameMilliseconds;
        RefreshGlance();
    }

    private void RefreshGlance()
    {
        int stop=_housing.IndexOf(';');
        string shortHousing=stop>=0?_housing.Substring(0,stop):_housing;
        string population=_population==0?"0 PEOPLE":
            $"{_population:N0} PEOPLE";
        SetText(_peopleHomes,_population==0?population:
            $"{population} · {shortHousing}");
        SetText(_supplies,$"W {_woodAvailable:N0}  F {_foodAvailable:N0}");
        SetText(_fps,_hasFps?$"{_lastFps} FPS":"FPS --");
        string tooltip=$"People: {_population:N0}\nHousing: {_housing}\n"+
            $"Wood: {_woodAvailable:N0}; Food: {_foodAvailable:N0}; "+
            $"Working: {_workers:N0}";
        if(_peopleHomes.TooltipText!=tooltip)
            _peopleHomes.TooltipText=tooltip;
    }

    private void UpdateDetailsPopup()
    {
        string[] lines=
        {
            $"Seed: {_seed} | Tick: {_tick:N0}",
            $"Population: {_population:N0} | Working: {_workers:N0}",
            $"Housing: {_housing}",
            $"Wood available: {_woodAvailable:N0}",
            $"Wood collected, total: {_woodHarvested:N0}",
            $"Food available: {_foodAvailable:N0}",
            $"Food collected, total: {_foodHarvested:N0}",
            _hasFps?$"FPS {_lastFps} | Worst frame {_lastWorstFrame:F1} ms":
                "FPS: measuring"
        };
        PopupMenu popup=_details.GetPopup();
        for(int i=0;i<lines.Length;i++)
            if(popup.GetItemText(i)!=lines[i])
                popup.SetItemText(i,lines[i]);
    }

    private static void SetText(Label label,string value)
    {
        if(label.Text!=value)label.Text=value;
    }

    private static void SetText(MenuButton button,string value)
    {
        if(button.Text!=value)button.Text=value;
    }

    private static Label Label(string text,int size,Color color,float minimumWidth)
    {
        var label=new Label
        {
            Text=text,
            VerticalAlignment=VerticalAlignment.Center,
            CustomMinimumSize=new Vector2(minimumWidth,35),
            MouseFilter=MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size",size);
        label.AddThemeColorOverride("font_color",color);
        return label;
    }

    private static Button Button(string text,Action action,float width)
    {
        var button=new Button
        {
            Text=text,CustomMinimumSize=new Vector2(width,35),
            FocusMode=FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop
        };
        StyleButton(button,Surface);
        button.Pressed+=action;
        return button;
    }

    private static MenuButton Dropdown(string text,float width)
    {
        var button=new MenuButton
        {
            Text=text,CustomMinimumSize=new Vector2(width,35),
            FocusMode=FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop,
            Flat=false
        };
        StyleButton(button,Surface);
        return button;
    }

    private static void StyleButton(Button button,Color background)
    {
        button.AddThemeFontSizeOverride("font_size",13);
        button.AddThemeColorOverride("font_color",Foreground);
        button.AddThemeColorOverride("font_hover_color",Foreground);
        button.AddThemeColorOverride("font_pressed_color",Foreground);
        button.AddThemeStyleboxOverride("normal",Style(background,7));
        button.AddThemeStyleboxOverride("hover",Style(Hover,7));
        button.AddThemeStyleboxOverride("pressed",Style(Active,7));
        button.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());
    }

    private static StyleBoxFlat PanelStyle()=>Style(Background,12);

    private static StyleBoxFlat Style(Color color,int radius)=>new()
    {
        BgColor=color,
        CornerRadiusTopLeft=radius,CornerRadiusTopRight=radius,
        CornerRadiusBottomLeft=radius,CornerRadiusBottomRight=radius
    };
}
