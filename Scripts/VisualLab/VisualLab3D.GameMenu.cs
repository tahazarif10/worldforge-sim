using System;
using System.IO;
using Godot;
using WorldForge.UI;

namespace WorldForge.VisualLab;

/// <summary>
/// Interactive launcher-only menu. The approved raw VisualLab scene and CI
/// still run without a start overlay or changed simulation behavior.
/// </summary>
public partial class VisualLab3D
{
    private bool _menuRequestedOnLaunch;
    private WorldGameMenu? _gameMenu;
    private bool _playSessionStarted;
    // A fresh map with the same seed as a saved one must NEVER overwrite it
    // silently on exit. Only the explicit "replace save" confirmation unlocks it.
    private bool _protectCurrentSave;

    private bool SavedWorldSlotExists()
    {
        try
        {
            string? file=CurrentWorldSaveFile();
            return file is not null && (File.Exists(file) || File.Exists(file+".bak"));
        }
        catch(Exception error)
        {
            GD.PushWarning("Save slot inspection failed: "+error.Message);
            return false;
        }
    }

    private void UpdateGameMenuContext()
    {
        _gameMenu?.SetContext(SavedWorldSlotExists(),_resumeWorldEnabled,
            _world.Seed,_world.Width,_world.Height,_ecoMode,_speed);
    }

    private void SetupGameMenu(CanvasLayer layer)
    {
        _gameMenu=new WorldGameMenu();
        layer.AddChild(_gameMenu);

        _gameMenu.StartNewRequested+=()=>
        {
            // CreateWorld already generated a deterministic EMPTY map in _Ready.
            // Do not load the old save, and never silently overwrite that slot.
            if(_world.People.Count>0 || _world.Tick>0)
                CreateWorld(InitialSeed());
            _playSessionStarted=true;
            _worldResumeBlocked=false;
            _protectCurrentSave=SavedWorldSlotExists();
            _gameMenu.HideMenu();
            RefreshHud();
            GD.Print("NEW WORLD STARTED: 0 citizens. Place the first person with the Person brush.");
        };

        _gameMenu.ContinueRequested+=()=>
        {
            if(TryResumeWorld())
            {
                _playSessionStarted=true;
                _protectCurrentSave=false;
                _gameMenu.HideMenu();
            }
            else
                _gameMenu.ShowFeedback("Could not restore the saved world. Its files were preserved.");
        };

        _gameMenu.ResumeRequested+=()=>_gameMenu.HideMenu();
        _gameMenu.SaveRequested+=RequestMenuSave;
        _gameMenu.ReplaceSaveRequested+=()=>
        {
            _protectCurrentSave=false; // explicit confirmation; never automatic
            bool saved=SaveWorldNow(explicitUserRequest:true);
            if(!saved && SavedWorldSlotExists())_protectCurrentSave=true;
            UpdateGameMenuContext();
            _gameMenu.ShowPause();
            _gameMenu.ShowFeedback(saved?"World saved successfully.":
                "Save failed; check the Godot console. Existing files were not intentionally deleted.");
        };
        _gameMenu.LoadRequested+=()=>
        {
            if(TryResumeWorld())
            {
                _playSessionStarted=true;
                _protectCurrentSave=false;
                _gameMenu.HideMenu();
            }
            else
            {
                UpdateGameMenuContext();
                _gameMenu.ShowPause();
                _gameMenu.ShowFeedback("Cannot load this save. Check Godot console; save files were preserved.");
            }
        };
        _gameMenu.NewWorldRequested+=()=>
        {
            EndPaintStroke();
            // Snapshot the current world if it is owned by this run. Otherwise
            // keep the protected older slot untouched.
            if(_playSessionStarted && _resumeWorldEnabled && !_protectCurrentSave)
                SaveWorldNow();
            int freshSeed=Random.Shared.Next();
            CreateWorld(freshSeed);
            _worldResumeBlocked=false;
            _protectCurrentSave=SavedWorldSlotExists();
            _playSessionStarted=true;
            _gameMenu.HideMenu();
            RefreshHud();
            GD.Print($"NEW EMPTY WORLD: seed {_world.Seed}, citizens {_world.People.Count}.");
        };
        _gameMenu.QuitRequested+=()=>GetTree().Quit();
        _gameMenu.QualityRequested+=eco=>SetQualityTier(eco);
        _gameMenu.SpeedRequested+=speed=>
        {
            _speed=speed;
            RefreshHud();
        };
        _gameMenu.FullscreenRequested+=()=>
        {
            Window window=GetWindow();
            window.Mode=window.Mode==Window.ModeEnum.Fullscreen ||
                window.Mode==Window.ModeEnum.ExclusiveFullscreen
                ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
        };

        UpdateGameMenuContext();
        _gameMenu.ShowStart();
        GD.Print("MAIN MENU: New World starts with ZERO citizens; Continue loads an existing save only on request.");
    }

    private void ShowPauseMenu()
    {
        if(_gameMenu is null)return;
        EndPaintStroke();
        UpdateGameMenuContext();
        _gameMenu.ShowPause();
    }

    private void RequestMenuSave()
    {
        if(!_resumeWorldEnabled)
        {
            _gameMenu?.ShowFeedback("Saving is disabled for this fresh/legacy launch.");
            return;
        }
        if(_protectCurrentSave && SavedWorldSlotExists())
        {
            if(_gameMenu?.Visible!=true)ShowPauseMenu();
            _gameMenu?.AskSaveReplacement();
            return;
        }
        bool saved=SaveWorldNow(explicitUserRequest:true);
        if(_gameMenu?.Visible==true)
        {
            UpdateGameMenuContext();
            _gameMenu.ShowFeedback(saved?"World saved successfully.":
                "Save failed; inspect the Godot log. Your previous save was kept.");
        }
    }

    private void RequestMenuLoad()
    {
        if(_gameMenu is null)
        {
            TryResumeWorld();
            return;
        }
        ShowPauseMenu();
        if(SavedWorldSlotExists())_gameMenu.AskLoadWorld();
        else _gameMenu.ShowFeedback("No save exists for this world seed and map size.");
    }

    private void RequestNewWorld()
    {
        if(_gameMenu is null)
        {
            EndPaintStroke();
            if(_resumeWorldEnabled)SaveWorldNow();
            CreateWorld(Random.Shared.Next());
            RefreshHud();
            return;
        }
        ShowPauseMenu();
        _gameMenu.AskNewWorld();
    }
}
