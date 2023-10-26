#if TOOLS
using Godot;
using System;

[Tool]
public partial class QuickAtlas : EditorPlugin
{
	QuickAtlasEditorWindow dock;

	public override void _Clear ()
	{
		dock.SetEditTarget(null);
	}

    public override void _EnterTree()
	{
		PackedScene dockScene = GD.Load<PackedScene>("res://addons/QuickAtlas/QuickAtlas.tscn");
		dock = dockScene.Instantiate<QuickAtlasEditorWindow>();
		AddControlToDock(DockSlot.LeftUl, dock);
	}

	public override void _ExitTree()
	{
		RemoveControlFromDocks(dock);
		dock.Free();
	}

	public override bool _Handles(GodotObject target)
	{
		return target is Texture2D;
	}
	
	public override void _Edit(GodotObject target)
	{
        dock.Init(GetEditorInterface(), GetUndoRedo());
        dock.SetEditTarget(target as Texture2D);
	}
}
#endif
