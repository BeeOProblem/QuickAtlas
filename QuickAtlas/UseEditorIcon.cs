using Godot;
using System;

#if TOOLS
[Tool]
public partial class UseEditorIcon : Button
{
    [Export]
    string EditorIconName;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Icon = GetThemeIcon(EditorIconName, "EditorIcons");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
#endif