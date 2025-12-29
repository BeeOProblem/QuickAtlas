@tool
extends Node
class_name QuickAtlasGridSettings

@export
var size_x : SpinBox
@export
var size_y : SpinBox
@export
var snap : CheckButton
@export
var square_grid : CheckBox

var grid_size_x : int
var grid_size_y : int
var snap_to_grid : bool

var last_updated_size_x : bool

func _ready() -> void:
    # make the checked and unchecked value look like a chain being linked/unlinked
    square_grid.add_theme_icon_override("checked", square_grid.get_theme_icon("Instance", "EditorIcons"));
    square_grid.add_theme_icon_override("unchecked", square_grid.get_theme_icon("Unlinked", "EditorIcons"));

func _snap_toggled(on : bool) -> void:
    snap_to_grid = on

func _square_grid_toggled(on : bool) -> void:
    if on:
        # assume the last dimension to change is the one the user
        # wants to actually use for sizing things
        if last_updated_size_x:
            grid_size_y = grid_size_x
            size_y.set_value_no_signal(grid_size_y)
        else:
            grid_size_x = grid_size_y
            size_x.set_value_no_signal(grid_size_x)

func _changed_grid_x(value : float):
    last_updated_size_x = true
    grid_size_x = value
    if square_grid.pressed:
        grid_size_y = value
        size_y.set_value_no_signal(grid_size_y)

func _changed_grid_y(value : float):
    last_updated_size_x = false
    grid_size_y = value
    if square_grid.pressed:
        grid_size_x = value
        size_x.set_value_no_signal(grid_size_x)
