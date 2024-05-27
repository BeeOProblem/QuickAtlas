## Version 0.3
Fixes another critical bug #28 that prevents QuickAtlas from opening unless a specific resource `res://Player/PlayerFire2.tres`

Also includes the following bug fixes and improvements:

- #23 Allow user to zoom in and out in UI
- #16 Fix undo history not populating correctly
- #26 Make sure zoom is centered when using scrollwheel
- #15 Center selected texture in window
- #29 Add conflict handling to autoname algorithm
- #25 Fix excessive margin in preview
- #24 Make sure texture handles are clickable at all zoom levels
- #22 Clear UI when deleting selected AtlasTexture
- #20 Clear UI when user is editing something that isn't a texture
- #19 Improved handling of external filesystem changes
- #17 Fix errors on renaming textures

## Version 0.2
Fixes a critical bug #21 that would prevent QuickAtlas from working with any texture that doesn't already have at least on AtlasTexture using it.

Also includes the following bug fixes and improvements:

- #1 Fixed janky behavior on dragging a AtlasTexture region across itself (or creating a region dragging in any direction besides down and right)
- #3 Selecting an existing AtlasTexture in the Filesystem dock now opens its source and selects it in QuickAtlas
- #6 DELETE key will now delete the selected AtlasTexture
- #7 Combined AtlasTexture create and initial sizing action into one in History
- #9 Added UI for modifying all AtlasTexture properties
- #11 Fixed sizing of AtlasTexture preview

Internal code improvements:

- #8 Removed code duplication between Undo/Redo and "normal" AtlasTexture change operations
- #14 Fully documented the code


## Version 0.1
Initial release.