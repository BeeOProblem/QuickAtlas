## Used to track in-process modifications to an AtlasTexture resource before
## committing them to the filesystem and provides helper pseudo-control handles
## to support drag and drop resizing
@tool
extends Node
class_name QuickAtlasTextureEdits

const NOTHING_CLICKED = -666

var handles : Array[Rect2]

# NOTE: None of these update actual resource files until save_resource is called
var edited_path : String
var actual_texture : AtlasTexture
var original_region : Rect2
var filter_clip : bool:
    get:
        return actual_texture.filter_clip
    set(value):
        actual_texture.filter_clip = value

var region : Rect2:
    get:
       return actual_texture.region
    set(value):
        if value != actual_texture.region:
            actual_texture.region = value
            recalculate_handles()
            print("new region ", actual_texture.region)
var margin : Rect2:
    get:
        return actual_texture.margin
    set(value):
        if value != actual_texture.margin:
            actual_texture.margin = value

var grid_settings : QuickAtlasGridSettings

func _init(grid_settings : QuickAtlasGridSettings, texture : Texture2D, initial_path : String = "", initial_region : Rect2 = Rect2(0,0,0,0)):
    self.grid_settings = grid_settings
    if texture is AtlasTexture and initial_path == "":
        # constructing from existing Atlastexture
        actual_texture = texture
        edited_path = texture.resource_path
    else:
        # constructing unsaved new AtlasTexture
        edited_path = initial_path
        initial_region.position = snap_position_to_grid(initial_region.position)
        original_region = initial_region
        actual_texture = AtlasTexture.new()
        actual_texture.atlas = texture
        actual_texture.region = initial_region
    recalculate_handles()

func save_resource_file() -> Error:
    var error = OK
    var save_flags = 0
    if edited_path != actual_texture.resource_path and \
       actual_texture.resource_path != null and actual_texture.resource_path != "":
        print("Rename/Move AtlasTexture ", actual_texture.resource_path, " to ", edited_path)
        error = DirAccess.rename_absolute(ProjectSettings.globalize_path(actual_texture.resource_path), ProjectSettings.globalize_path(edited_path))
        if error != OK: return error
        save_flags = ResourceSaver.FLAG_CHANGE_PATH | ResourceSaver.FLAG_REPLACE_SUBRESOURCE_PATHS
    original_region = actual_texture.region
    error = ResourceSaver.save(actual_texture, edited_path, save_flags)
    if error == OK: print("Saved AtlasTexture ", actual_texture.resource_path)
    return error


func get_click_if_any(position : Vector2, zoom_scale : float) -> int:
    var clicked_handle = NOTHING_CLICKED
    if region.has_point(position / zoom_scale):
        print("Mouse pressed on texture ", edited_path)
        clicked_handle = -1

    var i = 0
    for handle in handles:
        # we use a handle that's artifically enlarged to determine if the mouse is on it
        # so that, no matter what the zoom level is the clickable area is the same size
        var scaled_handle = Rect2(handle.position + handle.size * 0.5, handle.size / zoom_scale)
        scaled_handle.position -= scaled_handle.size * 0.5
        if scaled_handle.has_point(position / zoom_scale):
            print("Mouse pressed on handle ", i, " of ", edited_path)
            clicked_handle = i
            break
        i += 1
    return clicked_handle

func move_region(offset : Vector2, position_on_atlas : Vector2):
    var distance = position_on_atlas - (region.position + offset)

    # clamp movement such that the texture cannot be moved outside of its source atlas
    var atlas_size = actual_texture.atlas.get_size()
    if region.position.x < -distance.x: distance.x = -region.position.x
    if region.position.y < -distance.y: distance.y = -region.position.y
    if distance.x + region.end.x >= atlas_size.x: distance.x = atlas_size.x - region.end.x
    if distance.y + region.end.y >= atlas_size.y: distance.y = atlas_size.y - region.end.y

    # force movement to keep corner of region aligned with the grid
    var new_position = region.position + distance
    new_position = snap_position_to_grid(new_position)

    var new_region = Rect2(region)
    new_region.position = new_position
    region = new_region
    recalculate_handles()

# Move the specified handle to the specified position. Moves/resizes the texture so it follows
# the handle.
#
# This can change when the drag operation changes the handle's relative location
# (e.g. when dragging the right side across the left this will change from 4 to 3)
#
# 0 - top left
# 1 - top middle
# 2 - top right
# 3 - middle (vertically) left
# 4 - middle (vertically) right
# 5 - bottom left
# 6 - bottom middle
# 7 - bottom right
func move_handle_to(handle : int, position : Vector2) -> int:
    # TODO: add dummy handle index to allow swapping selected handle with MAAAATH!!! instead of big ifs
    var move = Vector2.ZERO
    var grow = Vector2.ZERO

    # force position to nearest grid coordinate if snap is on or nearest pixel if not
    position = snap_position_to_grid(position)
    if handle == 0 or handle == 3 or handle == 5:
        # dragging the left around
        var distance = position.x - region.position.x
        move.x = distance
        grow.x = -distance
        if(distance > region.size.x):
            # moving left side past the right side
            move.x = region.size.x
            grow.x = distance - region.size.x

            # swap selected handle to the right side to allow smooth drag
            if (handle == 0): handle = 2
            if (handle == 3): handle = 4
            if (handle == 5): handle = 7
    elif handle == 2 or handle == 4 or handle == 7:
        # dragging the right around
        var distance = position.x - (region.position.x + region.size.x)
        grow.x = distance
        if (distance < -region.size.x):
            # moving the right side past the left side
            move.x = distance - (-region.size.x)
            grow.x = -distance - region.size.x * 2

            # swap selected handle to the left side to allow smooth drag
            if (handle == 2): handle = 0
            if (handle == 4): handle = 3
            if (handle == 7): handle = 5

    if handle == 0 or handle == 1 or handle == 2:
        # dragging the top around
        var distance = position.y - region.position.y
        move.y = distance
        grow.y = -distance
        if (distance > region.size.y):
            # moving top past the bottom
            move.y = region.size.y
            grow.y = distance - region.size.y

            # swap selected handle to the left side to allow smooth drag
            # 0 -> 5 swaps from top to bottom +1 for each increment to the right
            handle += 5
    elif handle == 5 or handle == 6 or handle == 7:
        # dragging the bottom around
        var distance = position.y - (region.position.y + region.size.y)
        grow.y = distance
        if (distance < -region.size.y):
            # moving bottom past top
            move.y = distance - (-region.size.y)
            grow.y = -distance - region.size.y * 2

            # swap selected handle to the top side to allow smooth drag
            # 5 -> 0 swaps from bottom to top +1 for each increment to the right
            handle -= 5

    var new_region = Rect2(region)
    new_region.position += move
    new_region.size += grow
    region = new_region
    recalculate_handles()
    return handle

func snap_position_to_grid(position : Vector2) -> Vector2:
    var grid_snap = Vector2.ONE
    if (grid_settings.snap_to_grid):
        grid_snap = Vector2(grid_settings.grid_size_x, grid_settings.grid_size_y)

    print("snap ", position, " to (", floor(position.x / grid_snap.x) * grid_snap.x, ", ", floor(position.y / grid_snap.y) * grid_snap.y, ")")
    # gross hack, floor will cause undesired movement if position is already grid aligned
    position.x = floor(position.x / grid_snap.x) * grid_snap.x
    position.y = floor(position.y / grid_snap.y) * grid_snap.y
    return position

# Update the position of the handles based off of the size/position of
# the tracked AtlasTexture region.
func recalculate_handles():
    var left = region.position.x
    var right = left + region.size.x
    var midX= (left + right) / 2
    var top = region.position.y
    var bottom = top + region.size.y
    var midY= (top + bottom) / 2

    handles = [
        Rect2(left - QuickAtlasPreviewControls.HANDLE_OFFSET, top - QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET),
        Rect2(midX - QuickAtlasPreviewControls.HANDLE_OFFSET, top - QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET),
        Rect2(right - QuickAtlasPreviewControls.HANDLE_OFFSET, top - QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET),
        Rect2(left - QuickAtlasPreviewControls.HANDLE_OFFSET, midY - QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET),
        Rect2(right - QuickAtlasPreviewControls.HANDLE_OFFSET, midY - QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET),
        Rect2(left - QuickAtlasPreviewControls.HANDLE_OFFSET, bottom - QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET),
        Rect2(midX - QuickAtlasPreviewControls.HANDLE_OFFSET, bottom - QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET),
        Rect2(right - QuickAtlasPreviewControls.HANDLE_OFFSET, bottom - QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET, QuickAtlasPreviewControls.HANDLE_OFFSET)
    ]
