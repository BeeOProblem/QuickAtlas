using Godot;
using System;

#if TOOLS
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
#endif
