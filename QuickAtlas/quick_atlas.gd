@tool
extends EditorPlugin

var dock : QuickAtlasEditorWindow

func _clear():
    dock.set_edit_target(null)

func _enter_tree() -> void:
    var dockScene = load("res://addons/QuickAtlas/QuickAtlas.tscn")
    dock = dockScene.instantiate()
    add_control_to_dock(EditorPlugin.DOCK_SLOT_LEFT_UL, dock)

func _exit_tree() -> void:
    remove_control_from_docks(dock)
    dock.free()
    dock = null

func _handles(object: Object) -> bool:
    return object is Texture2D

func _edit(object: Object) -> void:
    dock.init(get_editor_interface(), get_undo_redo())
    dock.set_edit_target(object as Texture2D)
    dock.grab_focus()
