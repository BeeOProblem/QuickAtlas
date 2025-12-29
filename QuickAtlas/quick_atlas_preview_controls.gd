@tool
extends Control
class_name QuickAtlasPreviewControls

const HANDLE_SIZE : int = 7
const HANDLE_OFFSET : int = HANDLE_SIZE / 2

@export
var window : QuickAtlasEditorWindow
@export
var scroll_area : ScrollContainer
@export
var texture_preview_area : TextureRect
@export
var preview_area_margin : MarginContainer
@export
var zoom_level_label : Label

@export
var max_zoom_percent : int = 400
@export
var min_zoom_percent : int = 10
@export
var zoom_percent_increment : int = 10

var zoom_percentage : int = 100
var zoom_scale_value : float = 1

# true indicates the current drag operation is for creating a new AtlasTexture
var adding_new_texture : bool

var clicked_texture : QuickAtlasTextureEdits
var clicked_handle : int
var clicked_point_on_texture : Vector2

var first_handle : int
var drag_start : Vector2

# this is supplied by the init function and populated by QuickAtlasEditorWindow
# DO NOT ADD OR REMOVE ITEMS HERE!
var textures : Array[QuickAtlasTextureEdits]
var emprty_texture_list : Array[QuickAtlasTextureEdits]

func _ready() -> void:
    textures = []

func _draw() -> void:
    draw_rect(Rect2(Vector2.ZERO, texture_preview_area.size), Color.AQUA, false)
    for texture in textures:
        draw_atlas_texture_controls(texture);

func _gui_input(event) -> void:
    if event is InputEventMouseButton:
        if event.button_index == MOUSE_BUTTON_LEFT:
            start_or_end_drag(event)
        elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
            change_zoom(-zoom_percent_increment)
        elif event.button_index == MOUSE_BUTTON_WHEEL_UP:
            change_zoom(zoom_percent_increment)
    elif event is InputEventMouseMotion:
        if event.button_mask == MOUSE_BUTTON_MASK_LEFT:
            if clicked_texture != null:
                accept_event()
                if clicked_handle == -1:
                    # do not drag if off sprite sheet otherwise if the mouse
                    # goes offscreen then back the sprite will move really far from the mouse
                    if event.position.x < 0 or event.position.y < 0: return
                    if event.position.x >= texture_preview_area.size.x: return
                    if event.position.y >= texture_preview_area.size.y: return

                    # TODO: allow mouse to return to its original click position before moving if drag got clamped
                    clicked_texture.move_region(clicked_point_on_texture, event.position / zoom_scale_value)
                else:
                    # clamp mouse position to valid area for texture
                    var mouse_pos = event.position
                    if (mouse_pos.x < 0): mouse_pos.x = 0
                    if (mouse_pos.y < 0): mouse_pos.y = 0
                    if (mouse_pos.x >= texture_preview_area.size.x): mouse_pos.x = texture_preview_area.size.x - 1
                    if (mouse_pos.y >= texture_preview_area.size.y): mouse_pos.y = texture_preview_area.size.y - 1

                    clicked_handle = clicked_texture.move_handle_to(clicked_handle, mouse_pos / zoom_scale_value)
                queue_redraw()
        elif event.button_mask == MOUSE_BUTTON_MASK_MIDDLE:
            scroll_area.scroll_horizontal -= (int)(event.relative.x)
            scroll_area.scroll_vertical -= (int)(event.relative.y)
            accept_event()

func _scroll_area_size_changed():
    var halfSize = scroll_area.size * 0.5
    preview_area_margin.add_theme_constant_override("margin_left", halfSize.x)
    preview_area_margin.add_theme_constant_override("margin_right", halfSize.x)
    preview_area_margin.add_theme_constant_override("margin_top", halfSize.y)
    preview_area_margin.add_theme_constant_override("margin_bottom", halfSize.y)

    if texture_preview_area.texture != null:
        preview_area_margin.custom_minimum_size = texture_preview_area.texture.get_size() * zoom_scale_value + scroll_area.size;

func _zoom_in_pressed():
    change_zoom(zoom_percent_increment)

func _zoom_out_pressed():
    change_zoom(-zoom_percent_increment)

func set_atlas_source(source : Texture2D, textures : Array[QuickAtlasTextureEdits]):
    # if we aren't editing anything make sure we don't draw any AtlasTexture regions
    if textures == null: textures = []
    if textures == self.textures: return

    self.textures = textures;
    zoom_scale_value = 1
    zoom_percentage = 100

    # make sure margins and such are set up correctly
    # even though margins only need to be set when resizing and when _Ready
    # _ready is called before our size is actually set properly
    _scroll_area_size_changed()
    queue_redraw()

func center_view():
    var center = scroll_area.size * 0.5;
    var half_size = window.selected_texture.region.size * 0.5 * zoom_scale_value;
    var scroll_pos = window.selected_texture.region.position * zoom_scale_value - (center - half_size);
    scroll_pos.x += preview_area_margin.GetThemeConstant("margin_left");
    scroll_pos.y += preview_area_margin.GetThemeConstant("margin_top");
    scroll_area.SetDeferred("scroll_horizontal", scroll_pos.x);
    scroll_area.SetDeferred("scroll_vertical", scroll_pos.y);

func start_or_end_drag(event):
    window.grab_focus();
    accept_event();
    queue_redraw();

    if event.pressed:
        # do not start any operations if click is outside the texture area
        if event.position.x < 0 || event.position.y < 0: return
        if event.position.x >= texture_preview_area.size.x: return
        if event.position.y >= texture_preview_area.size.y: return

        adding_new_texture = false
        clicked_texture = null
        for texture in textures:
            clicked_handle = texture.get_click_if_any(event.position, zoom_scale_value)
            if clicked_handle != -666:
                clicked_point_on_texture = event.position - texture.region.position * zoom_scale_value;
                clicked_texture = texture;
                break

        if clicked_texture == null:
            clicked_texture = window.start_new_texture(event.position / zoom_scale_value);

            # bottom right, arbitrary but should give decent ability to size and fit workflow
            clicked_handle = 7
            adding_new_texture = true
        window.selected_texture = clicked_texture;
    else:
        # clamp mouse position to valid area for texture
        var mouse_pos = event.position;
        if mouse_pos.x < 0: mouse_pos.x = 0
        if mouse_pos.y < 0: mouse_pos.y = 0
        if mouse_pos.x >= texture_preview_area.size.x: mouse_pos.x = texture_preview_area.size.x - 1;
        if mouse_pos.y >= texture_preview_area.size.y: mouse_pos.y = texture_preview_area.size.y - 1;

        if clicked_texture != null:
            if adding_new_texture:
                window.do_add_new_texture_action(clicked_texture);
            else:
                window.do_change_region_action(clicked_texture.region);

            # click is no longer in progress
            clicked_texture = null;
            clicked_handle = -666;

func change_zoom(increment: int):
    accept_event()
    queue_redraw()

    var zoom_scale_value_old = zoom_scale_value
    zoom_percentage += increment
    zoom_percentage = clamp(zoom_percentage, min_zoom_percent, max_zoom_percent)
    zoom_scale_value = zoom_percentage / 100.0

    # since the margin and center are both equal to half the ScrollArea size
    # compensating for both causes the values to cancel so the ScrollArea.Scroll... is the center
    var scroll_pos_center = Vector2(scroll_area.scroll_horizontal, scroll_area.scroll_vertical)
    var scroll_pos_scaled_old = scroll_pos_center / zoom_scale_value_old
    var scroll_pos_new = scroll_pos_scaled_old * zoom_scale_value

    preview_area_margin.custom_minimum_size = texture_preview_area.texture.get_size() * zoom_scale_value + scroll_area.size
    scroll_area.set_deferred("scroll_horizontal", scroll_pos_new.x)
    scroll_area.set_deferred("scroll_vertical", scroll_pos_new.y)
    zoom_level_label.text = "%d%%" % zoom_percentage

func draw_atlas_texture_controls(texture_edits: QuickAtlasTextureEdits):
        var scaled_texture = texture_edits.region
        scaled_texture.size *= zoom_scale_value;
        scaled_texture.position *= zoom_scale_value;
        if texture_edits == clicked_texture:
            draw_rect(scaled_texture, Color.RED, false);
        elif texture_edits == window.selected_texture:
            draw_rect(scaled_texture, Color.GREEN, false);
        else:
            draw_rect(scaled_texture, Color.WHITE, false);

        var i = 0
        for handle in texture_edits.handles:
            var scaled_handle = handle;
            # we want to keep the handles the same size but position
            # is offset so the center of the handle is at the position of the rectangle part it's meant to move
            # so we need to remove the offset before scaling then put it back
            scaled_handle.position += scaled_handle.size * 0.5;
            scaled_handle.position *= zoom_scale_value;
            scaled_handle.position -= scaled_handle.size * 0.5;
            if i == clicked_handle and texture_edits == clicked_texture:
                draw_rect(scaled_handle, Color.YELLOW, true);
            else:
                draw_rect(scaled_handle, Color.WHITE, true);
            i += 1
