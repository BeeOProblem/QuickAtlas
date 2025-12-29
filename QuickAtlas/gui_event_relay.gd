@tool
extends MarginContainer

@export
var relay_target : Control

func _gui_input(event: InputEvent) -> void:
    # mouse events need position modified since _GuiInput assumes the position
    # is relative to the control receiving the event and will behave weird if
    # that assumption is broken by the position being relative to some other control
    if event is InputEventMouse:
        var offset = relay_target.global_position - global_position
        event.position -= offset
    relay_target._gui_input(event)
