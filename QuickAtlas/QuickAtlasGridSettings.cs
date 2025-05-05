using Godot;
using System;

[Tool]
public partial class QuickAtlasGridSettings : Node
{
    [Export]
    SpinBox SizeX;
    [Export]
    SpinBox SizeY;
    [Export]
    CheckButton Snap;
    [Export]
    CheckBox SquareGrid;

    public int GridSizeX
    {
        get; private set;
    }

    public int GridSizeY
    {
        get; private set;
    }

    public bool SnapToGrid
    {
        get
        {
            return Snap.ButtonPressed;
        }
    }

    private bool lastUpdatedSizeX;

    public override void _Ready()
    {
        SquareGrid.AddThemeIconOverride("checked", SquareGrid.GetThemeIcon("Instance", "EditorIcons"));
        SquareGrid.AddThemeIconOverride("unchecked", SquareGrid.GetThemeIcon("Unlinked", "EditorIcons"));
        GridSizeX = (int)SizeX.Value;
        GridSizeY = (int)SizeY.Value;
    }

    private void _SquareGridToggled(bool toggledOn)
    {
        if (toggledOn)
        {
            // assume the last dimension to change is the one the user
            // will want to actually use for sizing things
            if (lastUpdatedSizeX)
            {
                SizeY.SetValueNoSignal(SizeX.Value);
            }
            else
            {
                SizeX.SetValueNoSignal(SizeY.Value);
            }
        }
    }

    private void _ChangedGridX(float value)
    {
        lastUpdatedSizeX = true;
        GridSizeX = (int)value;
        if (SquareGrid.ButtonPressed)
        {
            GridSizeY = GridSizeX;
            SizeY.SetValueNoSignal(value);
        }
    }

    private void _ChangedGridY(float value)
    {
        lastUpdatedSizeX = false;
        GridSizeY = (int)value;
        if (SquareGrid.ButtonPressed)
        {
            GridSizeX = GridSizeY;
            SizeX.SetValueNoSignal(value);
        }
    }
}
