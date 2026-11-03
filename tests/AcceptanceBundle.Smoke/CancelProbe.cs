using System;
using Godot;
using WorldForge.VisualLab;

namespace WorldForge.AcceptanceTests;

/// <summary>Controlled headless close/Esc handling; not physical input-dispatch evidence.</summary>
public partial class CancelProbe : Node
{
    private int _frames;
    public override void _Process(double delta)
    {
        if (++_frames != 10) return;
        // The playable scene renames itself in _Ready; use the owned child reference.
        var game = GetChild<VisualLab3D>(0);
        if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--cancel-probe=escape"))
            game._Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
        else
            game.Notification((int)NotificationWMCloseRequest);
    }
}
