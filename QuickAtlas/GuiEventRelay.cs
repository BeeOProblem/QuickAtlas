using Godot;
using System;

[Tool]
public partial class GuiEventRelay : MarginContainer
{
    [Export]
    Control RelayTarget;

    public override void _GuiInput(InputEvent inputEvent)
    {
        RelayTarget._GuiInput(inputEvent);
    }
}
