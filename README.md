# QuickAtlas - Faster AtlasTexture creation for Godot 4

Version 0.3

Use this plugin to create AtlasTexture resources from sprite sheets with a simple mouse-based UI. This
plugin is loosely inspired by Unity 3D's sprite editor.

## How to use
 1) Copy the QuickAtlas directory to (your project)/addons
 2) Open your project in Godot
 3) Build your project
 4) Under the Project menu click Project Settings
 5) Go to the Plugins tab and enable QuickAtlas
 6) Move/resize the QuickAtlas dock to your liking
 7) In the Filesystem dock double-click the texture you want to create textures from
 8) Click and drag the mouse to select the region for the AtlasTexture you want to create
 9) Give the AtlasTexture resource a name
 10) Repeat steps 4 and 5 for all the sprites you want to create

## Caveats
This has been built with and for Godot 4. I'm not planning on providing support for Godot 3 unless there's
a lot of demand for it.

This is a very early version of the tool and is missing some quality of life features. It also has bugs.
I have only tested this with my personal projects which are all C# based. I do not know how this will work
in a project based in GDScript but you're welcome to try it out.
