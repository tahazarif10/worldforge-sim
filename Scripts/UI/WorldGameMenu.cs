using System;
using Godot;

namespace WorldForge.UI;

/// <summary>
/// Launcher-only start/pause menu. Owns presentation, never game state.
/// A new game starts empty; loading an existing save requires a deliberate click.
/// </summary>
public sealed partial class WorldGameMenu : Control
{
    private enum Page { Start, Pause, Help, Settings, NewWorld, ReplaceSave, LoadWorld, Quit }
    private Page _page=Page.Start, _returnPage=Page.Start;
    private readonly VBoxContainer _content;
    private bool _hasSave, _canSave, _eco;
    private int _speed=1, _seed, _width, _height;
    private string _feedback="";

    private static readonly Color Background=new("#101F2AF5");
    private static readonly Color Surface=new("#273B49");
    private static readonly Color Hover=new("#3A5264");
    private static readonly Color Accent=new("#2F8679");
    private static readonly Color Text=new("#F6F5EC");
    private static readonly Color Muted=new("#BACBD3");

    public event Action? StartNewRequested;
    public event Action? ContinueRequested;
    public event Action? ResumeRequested;
    public event Action? SaveRequested;
    public event Action? ReplaceSaveRequested;
    public event Action? LoadRequested;
    public event Action? NewWorldRequested;
    public event Action? QuitRequested;
    public event Action<bool>? QualityRequested;
    public event Action<int>? SpeedRequested;
    public event Action? FullscreenRequested;

    public WorldGameMenu()
    {
        Name="WorldGameMenu";
        Visible=false;
        MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade=new ColorRect
        {
            Color=new Color("#08121CCB"),
            MouseFilter=MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var centering=new CenterContainer { MouseFilter=MouseFilterEnum.Ignore };
        centering.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centering);

        var panel=new PanelContainer
        {
            CustomMinimumSize=new Vector2(500,0),
            MouseFilter=MouseFilterEnum.Stop
        };
        var panelStyle=new StyleBoxFlat
        {
            BgColor=Background,
            CornerRadiusTopLeft=18,
            CornerRadiusTopRight=18,
            CornerRadiusBottomLeft=18,
            CornerRadiusBottomRight=18,
            BorderWidthLeft=1,BorderWidthRight=1,
            BorderWidthTop=1,BorderWidthBottom=1,
            BorderColor=new Color("#536877")
        };
        panel.AddThemeStyleboxOverride("panel",panelStyle);
        centering.AddChild(panel);

        var margin=new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",26);
        margin.AddThemeConstantOverride("margin_right",26);
        margin.AddThemeConstantOverride("margin_top",22);
        margin.AddThemeConstantOverride("margin_bottom",22);
        panel.AddChild(margin);
        _content=new VBoxContainer();
        _content.AddThemeConstantOverride("separation",9);
        margin.AddChild(_content);
    }

    public void SetContext(bool hasSave,bool canSave,int seed,int width,int height,
        bool eco,int speed)
    {
        _hasSave=hasSave;
        _canSave=canSave;
        _seed=seed;
        _width=width;
        _height=height;
        _eco=eco;
        _speed=speed;
    }

    public void ShowStart()
    {
        _page=Page.Start;
        _returnPage=Page.Start;
        _feedback="";
        Visible=true;
        Rebuild();
    }

    public void ShowPause()
    {
        _page=Page.Pause;
        _returnPage=Page.Pause;
        _feedback="";
        Visible=true;
        Rebuild();
    }

    public void AskNewWorld() => Confirm(Page.NewWorld);
    public void AskSaveReplacement() => Confirm(Page.ReplaceSave);
    public void AskLoadWorld() => Confirm(Page.LoadWorld);

    private void Confirm(Page page)
    {
        _returnPage=_page==Page.Start?Page.Start:Page.Pause;
        _page=page;
        Visible=true;
        _feedback="";
        Rebuild();
    }

    public void ShowFeedback(string feedback)
    {
        _feedback=feedback;
        if(Visible)Rebuild();
    }

    public void HideMenu()
    {
        Visible=false;
        _feedback="";
    }

    public void HandleEscape()
    {
        if(!Visible)return;
        if(_page==Page.Start)return;
        if(_page==Page.Pause)
        {
            ResumeRequested?.Invoke();
            return;
        }
        _page=_returnPage;
        _feedback="";
        Rebuild();
    }

    private void Go(Page page)
    {
        _returnPage=_page;
        _page=page;
        _feedback="";
        Rebuild();
    }

    private void Rebuild()
    {
        // Rebuild only on a menu action, never on simulation or rendering ticks.
        foreach(Node old in _content.GetChildren())
        {
            _content.RemoveChild(old);
            old.QueueFree();
        }

        Heading("WORLD FORGE");
        Subtitle(_page==Page.Start?"A living world begins with your first person."
            : _page==Page.Pause?"GAME PAUSED"
            : _page==Page.Help?"HOW TO PLAY"
            : _page==Page.Settings?"SETTINGS"
            : "CONFIRM ACTION");
        Hint($"World {_width} x {_height}    |    Seed {_seed}");

        switch(_page)
        {
            case Page.Start:
                Hint("NEW GAME: 0 CITIZENS. Select Person and click land to create life.");
                Button("START NEW WORLD",()=>StartNewRequested?.Invoke(),primary:true);
                Button(_hasSave?"CONTINUE SAVED WORLD":"CONTINUE (NO SAVE FOUND)",
                    ()=>ContinueRequested?.Invoke(),disabled:!_hasSave);
                Button("SETTINGS",()=>Go(Page.Settings));
                Button("HOW TO PLAY",()=>Go(Page.Help));
                Button("QUIT",()=>Go(Page.Quit));
                if(_hasSave)
                    Hint("The previous world is safe until you explicitly choose to replace its save.");
                break;

            case Page.Pause:
                Button("RESUME",()=>ResumeRequested?.Invoke(),primary:true);
                Button("SAVE WORLD",()=>SaveRequested?.Invoke(),disabled:!_canSave);
                Button("LOAD SAVED WORLD",()=>AskLoadWorld(),disabled:!_hasSave);
                Button("NEW EMPTY WORLD",()=>AskNewWorld());
                Button("SETTINGS",()=>Go(Page.Settings));
                Button("HOW TO PLAY",()=>Go(Page.Help));
                Button("QUIT GAME",()=>Go(Page.Quit));
                break;

            case Page.Settings:
                Hint("Graphics quality affects detail, not your world or citizens.");
                Button(_eco?"QUALITY: ECO":"QUALITY: BALANCED",()=>
                {
                    _eco=!_eco;
                    QualityRequested?.Invoke(_eco);
                    Rebuild();
                },primary:true);
                Hint("Simulation speed");
                var speedRow=new HBoxContainer();
                speedRow.AddThemeConstantOverride("separation",8);
                _content.AddChild(speedRow);
                foreach(int speed in new[]{0,1,2,4})
                {
                    int choice=speed;
                    var button=CreateButton(speed==0?"PAUSE":$"{speed}x",
                        ()=>{_speed=choice;SpeedRequested?.Invoke(choice);Rebuild();},
                        primary:speed==_speed);
                    button.SizeFlagsHorizontal=SizeFlags.ExpandFill;
                    speedRow.AddChild(button);
                }
                Button("TOGGLE FULLSCREEN",()=>FullscreenRequested?.Invoke());
                Button("BACK",Back);
                break;

            case Page.Help:
                Hint("1. Start a new world. There are NO citizens at the beginning.");
                Hint("2. Select PERSON above, then left-click walkable land to place people.");
                Hint("3. With at least two people, citizens gather wood and food, build homes and may grow the population.");
                Hint("4. Change terrain with LAND, OCEAN, SAND, FOREST or MOUNTAIN. METEOR causes destruction.");
                Hint("5. Right mouse: drag camera   |   Wheel: zoom");
                Hint("6. Space: pause   |   1 / 2 / 3: time 1x / 2x / 4x");
                Hint("7. Esc or MENU: pause menu   |   Ctrl+S: save   |   Ctrl+L: load");
                Hint("8. F4: Eco/Balanced   |   F7: sculpt height   |   F8: screenshot");
                Button("BACK",Back,primary:true);
                break;

            case Page.NewWorld:
                Hint("Create a different world with ZERO citizens?");
                Hint("The current session will be saved first when saving is enabled. Existing saves are never deleted by this action.");
                Button("CREATE EMPTY WORLD",()=>NewWorldRequested?.Invoke(),primary:true);
                Button("CANCEL",Back);
                break;

            case Page.ReplaceSave:
                Hint("This new world has the same seed and size as an older saved world.");
                Hint("Replace that saved world? A verified backup is kept when available. This action cannot be undone from the menu.");
                Button("REPLACE OLD SAVE",()=>ReplaceSaveRequested?.Invoke(),primary:true);
                Button("CANCEL",Back);
                break;

            case Page.LoadWorld:
                Hint("Load the previous world and discard UNSAVED changes in the current game?");
                Button("LOAD SAVED WORLD",()=>LoadRequested?.Invoke(),primary:true,disabled:!_hasSave);
                Button("CANCEL",Back);
                break;

            case Page.Quit:
                Hint("Exit the game? Normal exit saves the current session if saving is enabled and the save slot is not protected.");
                Button("QUIT WORLD FORGE",()=>QuitRequested?.Invoke(),primary:true);
                Button("CANCEL",Back);
                break;
        }
        if(!string.IsNullOrWhiteSpace(_feedback))
            Hint(_feedback);
        Subtitle("Original 3D world  |  Local saves on H:  |  Esc to return");
    }

    private void Back()
    {
        _page=_returnPage;
        _feedback="";
        Rebuild();
    }

    private void Heading(string text)
    {
        var label=MakeLabel(text,33,Text);
        label.HorizontalAlignment=HorizontalAlignment.Center;
        _content.AddChild(label);
    }

    private void Subtitle(string text)
    {
        var label=MakeLabel(text,15,Muted);
        label.HorizontalAlignment=HorizontalAlignment.Center;
        _content.AddChild(label);
    }

    private void Hint(string text)
    {
        var label=MakeLabel(text,15,Muted);
        label.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        _content.AddChild(label);
    }

    private void Button(string label,Action action,bool primary=false,bool disabled=false)
    {
        var button=CreateButton(label,action,primary);
        button.Disabled=disabled;
        _content.AddChild(button);
    }

    private static Button CreateButton(string label,Action action,bool primary=false)
    {
        var button=new Button
        {
            Text=label,
            CustomMinimumSize=new Vector2(0,44),
            FocusMode=FocusModeEnum.None,
            MouseFilter=MouseFilterEnum.Stop
        };
        button.AddThemeFontSizeOverride("font_size",17);
        button.AddThemeColorOverride("font_color",Text);
        button.AddThemeColorOverride("font_hover_color",Text);
        button.AddThemeColorOverride("font_pressed_color",Text);
        button.AddThemeColorOverride("font_disabled_color",new Color("#8999A4"));
        button.AddThemeStyleboxOverride("normal",Rounded(primary?Accent:Surface));
        button.AddThemeStyleboxOverride("hover",Rounded(Hover));
        button.AddThemeStyleboxOverride("pressed",Rounded(Accent));
        button.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());
        button.Pressed+=action;
        return button;
    }

    private static Label MakeLabel(string text,int size,Color color)
    {
        var label=new Label{Text=text,MouseFilter=MouseFilterEnum.Ignore};
        label.AddThemeFontSizeOverride("font_size",size);
        label.AddThemeColorOverride("font_color",color);
        return label;
    }

    private static StyleBoxFlat Rounded(Color color)=>new()
    {
        BgColor=color,
        CornerRadiusTopLeft=9,CornerRadiusTopRight=9,
        CornerRadiusBottomLeft=9,CornerRadiusBottomRight=9
    };
}
