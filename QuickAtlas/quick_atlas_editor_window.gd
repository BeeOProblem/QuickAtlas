@tool
extends Control
class_name QuickAtlasEditorWindow

@export
var no_target_overlay : Control
@export
var texture_preview_area : TextureRect

@export
var grid_settings : QuickAtlasGridSettings
@export
var preview_controls : QuickAtlasPreviewControls
@export
var preview_container : ScrollContainer
@export
var file_dialog : FileDialog
@export
var confirmation_dialog : ConfirmationDialog
@export
var error_dialog : AcceptDialog

@export
var sub_texture_preview_area : TextureRect
@export
var resource_name : LineEdit
@export
var region_x : SpinBox
@export
var region_y : SpinBox
@export
var region_w : SpinBox
@export
var region_h : SpinBox

@export
var margin_x : SpinBox
@export
var margin_y : SpinBox
@export
var margin_w : SpinBox
@export
var margin_h : SpinBox

@export
var filter_clip : CheckBox

var editor_interface : EditorInterface
var undo_redo : EditorUndoRedoManager
var texture_atlas_refs : Dictionary[String, Array]
var current_base_texture : Texture2D
var base_path : String
var new_texture_counter : int
var selected_atlas_texture : QuickAtlasTextureEdits
var selected_texture : QuickAtlasTextureEdits :
    get:
        return selected_atlas_texture
    set(value):
        if selected_atlas_texture == value: return
        selected_atlas_texture = value
        update_control_values()
        hack_for_godot_bug_selection_changed = true

var texture_edits : Array[QuickAtlasTextureEdits]

# Fix for #34
# Godot will fire a bogus value changed event for the focused control reverting
# an unsignaled value change. This is indicate the signal should be ignored.
var hack_for_godot_bug_selection_changed : bool

func _gui_input(event: InputEvent) -> void:
    if current_base_texture == null: return
    if selected_texture == null: return

    if event is InputEventKey:
        if event.keycode == KEY_DELETE:
            accept_event()
            if not event.pressed:
                if selected_texture != null:
                    _on_delete_pressed()

func _process(delta: float) -> void:
    if hack_for_godot_bug_selection_changed:
        hack_for_godot_bug_selection_changed = false

func init(editor_interface : EditorInterface, undo_redo : EditorUndoRedoManager) -> void:
    if self.editor_interface != null: return
    self.editor_interface = editor_interface
    self.undo_redo = undo_redo

    rebuild_resource_dictionary()
    # TODO: this triggers on files being saved in addition to
    # fs changes I actually care about like files being added/deleted/renamed externally
    # need a solution to detect add/delete/rename files and ignore modifications
    #editor_interface.get_resource_filesystem().filesystem_changed.connect(_filesystem_changed)
    texture_edits = []

func set_edit_target(target : Texture2D):
    var target_atlas = null
    if target is AtlasTexture:
        target_atlas = target
        target = target.atlas

    if target != current_base_texture:
        undo_redo.create_action("QuickAtlas - Change source atlas", UndoRedo.MERGE_ENDS, self)
        if target != null: undo_redo.add_do_method(self, "do_set_edit_target_action", target.resource_path)
        else: undo_redo.add_do_method(self, "do_set_edit_target_action", "null")
        if current_base_texture != null: undo_redo.add_undo_method(self, "do_set_edit_target_action", current_base_texture.resource_path)
        else: undo_redo.add_undo_method(self, "do_set_edit_target_action", "null")
        undo_redo.commit_action()

    if target_atlas != null:
        for edit in texture_edits:
            if edit.actual_texture.resource_path == target_atlas.resource_path:
                self.selected_texture = edit
                preview_controls.call_deferred("center_view")

## DO NOT CALL THIS DIRECTLY! Use SetEditTarget instead
##
## Loads the specified texture for use in creating AtlasTexture regions using it
## as a source. If any exist already then this will create AtlasTextureEdits instances
## to allow them to be modified if the user desires.
##
## Called by Godot's undo/redo history to change the current source texture for
## AtlasTextures to be edited. This needs to be in the undo history so that other
## historic actions on AtlasTexture are done in the appropriate context.
func do_set_edit_target_action(target : String):
    if target == "null":
        # TODO: calling with target of null in middle of setting valid edit target
        no_target_overlay.visible = true

        # set all controls to a default blank state when nothing is selected
        current_base_texture = null
        sub_texture_preview_area.texture = null
        texture_preview_area.texture = null
        preview_controls.set_atlas_source(null, [])

        resource_name.text = ""
        region_x.set_value_no_signal(0)
        region_y.set_value_no_signal(0)
        region_w.set_value_no_signal(0)
        region_h.set_value_no_signal(0)
        margin_x.set_value_no_signal(0)
        margin_y.set_value_no_signal(0)
        margin_w.set_value_no_signal(0)
        margin_h.set_value_no_signal(0)
        filter_clip.set_pressed_no_signal(false)
    else:
        # grab atlastexture paths from texture path
        var target_texture = load(target)
        current_base_texture = target_texture
        texture_preview_area.texture = target_texture

        texture_edits.clear()
        if current_base_texture != null:
            var atlas_source = current_base_texture.resource_path

            print("Getting atlas textures from " + atlas_source)
            print(texture_atlas_refs)
            var texture_names = texture_atlas_refs.get(atlas_source)
            for texture_name in texture_names:
                print("\t" + texture_name)
                texture_edits.append(QuickAtlasTextureEdits.new(grid_settings, load(texture_name)))

        preview_controls.set_atlas_source(target_texture, texture_edits)
        base_path = target_texture.resource_path.rsplit('/', true, 1)[0]
        region_x.max_value = current_base_texture.get_width()
        region_y.max_value = current_base_texture.get_height()
        region_w.max_value = current_base_texture.get_width()
        region_h.max_value = current_base_texture.get_height()
        no_target_overlay.visible = false

## First step in creating a new AtlasTexture. Creates the internal tracking object
## for the AtlasTexture with its region starting at the given position but does not
## commit it to the filesystem. This texture will start with a size of 0x0 and MUST be
## resized before it can be saved.
func start_new_texture(position : Vector2) -> QuickAtlasTextureEdits:
    var new_texture_name : String
    while true:
        new_texture_counter += 1
        new_texture_name = "{0}/new_atlas_texture{1}.tres".format([base_path, new_texture_counter])
        if !ResourceLoader.exists(new_texture_name): break
    print("start new texture ", base_path, " ", new_texture_counter, " ", current_base_texture.resource_path)

    var new_texture = QuickAtlasTextureEdits.new(grid_settings, current_base_texture, new_texture_name, Rect2(position, Vector2(grid_settings.grid_size_x, grid_settings.grid_size_y)))
    texture_edits.append(new_texture)
    return new_texture

func do_add_new_texture_action(edited_texture : QuickAtlasTextureEdits):
    undo_redo.create_action("QuickAtlas - Create AtlasTexture Region")
    undo_redo.add_do_method(self, "add_texture_region", edited_texture.edited_path, edited_texture.region)
    undo_redo.add_undo_method(self, "delete_texture_region", edited_texture.edited_path)
    undo_redo.commit_action()

# DO NOT CALL DIRECTLY!
func add_texture_region(new_resource_path : String, new_region : Rect2):
    print("(Re)create AtlasTexture ", new_resource_path, " ", new_region)
    var new_texture = null
    for edits in texture_edits:
        if edits.edited_path == new_resource_path:
            if selected_texture != edits:
                printerr("Creating AtlasTexture that already exists via undo/redo!")
                return
            else:
                print("Committing new texture created from drag+drop")
                new_texture = edits
    if new_texture == null:
        new_texture = QuickAtlasTextureEdits.new(grid_settings, current_base_texture, new_resource_path, new_region)
        texture_edits.append(new_texture)

    var error = new_texture.save_resource_file()
    if error == OK:
        texture_atlas_refs[current_base_texture.resource_path].append(new_texture.edited_path)
        selected_atlas_texture = new_texture
        update_control_values()
        editor_interface.get_resource_filesystem().scan()
    else:
        texture_edits.remove_at(texture_edits.find(new_texture))
        error_dialog.dialog_text = error_string(error)
        error_dialog.show()

func do_change_region_action(new_region : Rect2):
    undo_redo.create_action("QuickAtlas - Region")
    undo_redo.add_do_method(self, "change_region", selected_texture.edited_path, new_region)
    undo_redo.add_undo_method(self, "change_region", selected_texture.edited_path, selected_texture.original_region)
    undo_redo.commit_action()

# DO NOT CALL DIRECTLY!
func change_region(resource_path : String, new_region : Rect2):
    print("Change Region ", resource_path)
    for edit in texture_edits:
        if edit.edited_path == resource_path:
            edit.region = new_region
            update_control_values()
            var error = edit.save_resource_file()
            if error != OK:
                error_dialog.dialog_text = error_string(error)
                error_dialog.show()

func do_change_margin_action(new_margin : Rect2):
    undo_redo.create_action("QuickAtlas - Margin")
    undo_redo.add_do_method(self, "change_margin", selected_atlas_texture.edited_path, new_margin)
    undo_redo.add_undo_method(self, "change_margin", selected_atlas_texture.edited_path, selected_atlas_texture.margin)
    undo_redo.commit_action()

# DO NOT CALL DIRECTLY!
func change_margin(resource_path : String, new_margin : Rect2):
    print("Change Margin ", resource_path)
    for edit in texture_edits:
        if edit.edited_path == resource_path:
            edit.margin = new_margin
            update_control_values()
            edit.save_resource_file()

# DO NOT CALL DIRECTLY!
func set_filter_clip(resource_path : String, value : bool):
    print("Set filter clip ", resource_path)
    for edit in texture_edits:
        if edit.edited_path == resource_path:
            edit.filter_clip = value
            update_control_values()
            var error = edit.save_resource_file()
            if error != OK:
                error_dialog.dialog_text = error_string(error)
                error_dialog.show()

func do_rename_action(new_path : String):
    if !new_path.ends_with(".tres"):
        print("Specified resource without .tres extension. Adding.")
        new_path == ".tres"

    var old_path = selected_texture.edited_path
    undo_redo.CreateAction("QuickAtlas - Change resource path", UndoRedo.MergeMode.MERGE_DISABLE, self)
    undo_redo.add_do_method(self, "change_texture_resource_path", old_path, new_path)
    undo_redo.add_undo_method(self, "change_texture_resource_path", new_path, old_path)
    undo_redo.commit_action()

# DO NOT CALL DIRECTLY!
func change_texture_resource_path(old_resource_path : String, new_resource_path : String):
    if old_resource_path == new_resource_path:
        print("Changing ", old_resource_path, " to same name")
        return

    print("Change path from ", old_resource_path, " to ", new_resource_path)
    for edit in texture_edits:
        if edit.edited_path == old_resource_path:
            edit.edited_path = new_resource_path
            var error : Error = edit.save_resource_file()
            if error != OK:
                error_dialog.dialog_text = error_string(error)
                error_dialog.show()
                edit.edited_path = old_resource_path

            update_control_values()
            editor_interface.get_resource_filesystem().scan()

            texture_atlas_refs[current_base_texture.resource_path].erase(old_resource_path)
            texture_atlas_refs[current_base_texture.resource_path].append(new_resource_path)
            return
    print("Cannot find AtlasTexture, probably associated with different parent than selected ", old_resource_path)

# DO NOT CALL DIRECTLY!
func delete_texture_region(target_resource_path : String):
    print("Delete AtlasTexture ", target_resource_path)
    for edit in texture_edits:
        if edit.edited_path == target_resource_path:
            DirAccess.remove_absolute(ProjectSettings.globalize_path(edit.edited_path))
            texture_atlas_refs[current_base_texture.resource_path].erase(edit.edited_path)
            if edit == selected_atlas_texture:
                selected_atlas_texture = null

            texture_edits.remove_at(texture_edits.find(selected_atlas_texture))
            update_control_values()
            editor_interface.get_resource_filesystem().scan()
            return
    print("Deleting AtlasTexture that was already deleted")

func _filesystem_changed():
    print("Filesystem state changed. Rebuilding AtlasTexture dictionary.")
    rebuild_resource_dictionary()
    update_control_values()

func _on_rename_pressed():
    file_dialog.current_path = selected_atlas_texture.resource_path
    file_dialog.show()

func _on_file_dialog_selected(path : String):
    do_rename_action(path)

func _on_resource_path_change_submit(path : String):
    do_rename_action(path)

func _on_delete_pressed():
    var name = selected_texture.edited_path.rsplit('/', true, 1)[1]
    confirmation_dialog.dialog_text = "Delete AtlasTexture \"" + name + "\"?"
    confirmation_dialog.show()

func _on_confirmed():
    # BUG: undoing a delete action should restore margin and clip as well!
    undo_redo.create_action("QuickAtlas - Delete AtlasTexture Region", UndoRedo.MERGE_DISABLE, self)
    undo_redo.add_do_method(self, "delete_texture_region", selected_atlas_texture.edited_path)
    undo_redo.add_undo_method(self, "add_texture_region", selected_atlas_texture.edited_path, selected_atlas_texture.region)
    undo_redo.commit_action()

func _on_cancelled():
    print("AtlasTexture delete cancelled")

func _on_filter_clip_checkbox_toggled(value : bool):
    undo_redo.create_action("QuickAtlas - Filter Clip")
    undo_redo.add_do_method(self, "set_filter_clip", selected_atlas_texture.edited_path, value)
    undo_redo.add_undo_method(self, "set_filter_clip", selected_atlas_texture.edited_path, selected_atlas_texture.filter_clip)
    undo_redo.commit_action()

func _on_changed_region_x(value : float):
    if hack_for_godot_bug_selection_changed: return
    var new_region = Rect2(selected_atlas_texture.region)
    new_region.position = Vector2(value, new_region.position.y)
    do_change_region_action(new_region)

func _on_changed_region_y(value : float):
    if hack_for_godot_bug_selection_changed: return
    var new_region = Rect2(selected_atlas_texture.region)
    new_region.position = Vector2(new_region.position.x, value)
    do_change_region_action(new_region)

func _on_changed_region_w(value : float):
    if hack_for_godot_bug_selection_changed: return
    var new_region = Rect2(selected_atlas_texture.region)
    new_region.size = Vector2(value, new_region.size.y)
    do_change_region_action(new_region)

func _on_changed_region_h(value : float):
    if hack_for_godot_bug_selection_changed: return
    var new_region = Rect2(selected_atlas_texture.region)
    new_region.size = Vector2(new_region.size.x, value)
    do_change_region_action(new_region)

func _on_changed_margin_x(value : float):
    if hack_for_godot_bug_selection_changed: return
    var new_margin = Rect2(selected_atlas_texture.region)
    new_margin.position = Vector2(value, new_margin.position.y)
    do_change_margin_action(new_margin)

func _on_changed_margin_y(value : float):
    if hack_for_godot_bug_selection_changed: return
    var new_margin = Rect2(selected_atlas_texture.region)
    new_margin.position = Vector2(new_margin.position.x, value)
    do_change_margin_action(new_margin)

func _on_changed_margin_w(value : float):
    if hack_for_godot_bug_selection_changed: return
    var new_margin = Rect2(selected_atlas_texture.region)
    new_margin.size = Vector2(value, new_margin.size.y)
    do_change_margin_action(new_margin)

func _on_changed_margin_h(value : float):
    if hack_for_godot_bug_selection_changed: return
    var new_margin = Rect2(selected_atlas_texture.region)
    new_margin.size = Vector2(new_margin.size.x, value)
    do_change_margin_action(new_margin)

func rebuild_resource_dictionary():
    # walk project to find all existing resources
    var all_resources : Array[String] = []
    var fs = editor_interface.get_resource_filesystem()
    walk_project_resources(fs.get_filesystem(), all_resources)

    # track which AtlasTexture resources are associated with what Texture2D resources
    # provide empty reference lists for source textures that do not have associated AtlasTexture
    texture_atlas_refs = {}
    for resource_path in all_resources:
        var test = load(resource_path)
        if test is AtlasTexture:
            if test.atlas.resource_path not in texture_atlas_refs:
                texture_atlas_refs[test.atlas.resource_path] = []
            texture_atlas_refs[test.atlas.resource_path].append(resource_path)
        elif test is Texture2D:
            texture_atlas_refs[resource_path] = []

    # since Godot doesn't tell us what specifically changed reload everything
    # and update any of our tracking that needs it
    if current_base_texture != null:
        preview_controls.queue_redraw()

        # BUG: This doesn't actually detect the base texture being deleted/renamed properly
        #      since something in Godot makes the stale paths work automagically Github #27
        # our base texture was renamed or deleted, clear everything
        if current_base_texture.resource_path not in texture_atlas_refs:
            print("Selected Atlas source lost. Clearing selection")
            editor_interface.get_selection().clear()
            return

        print("Re-scanning atlas textures from ", current_base_texture.resource_path)
        var texture_names = texture_atlas_refs[current_base_texture.resource_path]
        for texture_name in texture_names:
            var already_exists = false
            var new_actual_texture = load(texture_name)
            for edit in texture_edits:
                if edit.actual_texture.resource_path == new_actual_texture.resource_path:
                    # TODO: make actual_texture a property and update stuff
                    print("\tUpdated ", texture_name)
                    edit.actual_texture = new_actual_texture
                    already_exists = true

            if !already_exists:
                print("\tAdded ", texture_name)
                texture_edits.append(QuickAtlasTextureEdits.new(grid_settings, new_actual_texture))

        # remove anything that amy have been deleted or renames
        var i = 0
        while i < len(texture_edits):
            var edits = texture_edits[i]
            if edits.actual_texture.resource_path not in texture_names:
                # selected texture was deleted or renamed, deselect
                if selected_texture == edits:
                    selected_texture = null
                print("\tREMOVED ", edits.actual_texture.resource_path)
                texture_edits.remove_at(i)
            else:
                i += 1

## Recursively walk the directory tree. Any resources found are added to output
func walk_project_resources(directory : EditorFileSystemDirectory, output : Array[String]):
    var file_count = directory.get_file_count()
    for i in range(file_count):
        output.append(directory.get_file_path(i))

    var directory_count = directory.get_subdir_count()
    for i in range(directory_count):
        walk_project_resources(directory.get_subdir(i), output)

## Updates control values with current proerties of the selected AtlasTexture
## Use when the selection is changed, an action is undone or when a property
## is modified via an action done on AtlasPreviewControls.
##
## IMPORTANT NOTE!
## Only ever use Set(thing)NoSignal in this. Only to be used in response to
## an external change and not to commit modifications.
func update_control_values():
    preview_controls.queue_redraw()
    if selected_texture != null:
        sub_texture_preview_area.texture = selected_texture.actual_texture

        region_x.set_value_no_signal(selected_texture.region.position.x)
        region_y.set_value_no_signal(selected_texture.region.position.y)
        region_w.set_value_no_signal(selected_texture.region.size.x)
        region_h.set_value_no_signal(selected_texture.region.size.y)

        margin_x.set_value_no_signal(selected_texture.margin.position.x)
        margin_y.set_value_no_signal(selected_texture.margin.position.y)
        margin_w.set_value_no_signal(selected_texture.margin.size.x)
        margin_h.set_value_no_signal(selected_texture.margin.size.y)

        filter_clip.set_pressed_no_signal(selected_texture.filter_clip)

        resource_name.text = selected_texture.edited_path
        region_w.max_value = current_base_texture.get_width() - selected_texture.region.position.x
        region_h.max_value = current_base_texture.get_height() - selected_texture.region.position.y
    else:
        sub_texture_preview_area.texture = null

        region_x.set_value_no_signal(0)
        region_y.set_value_no_signal(0)
        region_w.set_value_no_signal(0)
        region_h.set_value_no_signal(0)

        margin_x.set_value_no_signal(0)
        margin_y.set_value_no_signal(0)
        margin_w.set_value_no_signal(0)
        margin_h.set_value_no_signal(0)

        filter_clip.set_pressed_no_signal(false)

        resource_name.text = ""
