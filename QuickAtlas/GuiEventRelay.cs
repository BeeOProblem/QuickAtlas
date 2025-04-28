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
        // mouse events need position modified since _GuiInput assumes the position
        // is relative to the control receiving the event and will behave weird if
        // that assumption is broken by the position being relative to some other control
        if (inputEvent is InputEventMouse mouse) {
            var offset = RelayTarget.GlobalPosition - GlobalPosition;
            mouse.Position -= offset;
        }

        RelayTarget._GuiInput(inputEvent);
    }
}
#endif
