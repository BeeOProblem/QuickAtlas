using Godot;
using System;
using System.IO;

/// <summary>
/// Used to track in-process modifications to an AtlasTexture resource before
/// committing them to the filesystem and provides helper pseudo-control handles
/// to support drag and drop resizing
/// </summary>
public class AtlasTextureEdits
{
    // TODO: consider changing AtlasTextureEdits and Handles to nodes for easier(?) mouse handling
    public AtlasTexture actualTexture;
    public Rect2[] Handles;

    private string editedPath;

    /// <summary>
    /// Path of the texture. This does not update the actual resource path
    /// until SaveResourceFile is called.
    /// </summary>
    public string ResourcePath
    {
        get
        {
            return editedPath;
        }

        set
        {
            editedPath = value;
        }
    }

    /// <summary>
    /// Does not update the actual resource file until SaveResourceFile is called
    /// </summary>
    public bool FilterClip
    {
        get
        {
            return actualTexture.FilterClip;
        }

        set
        {
            actualTexture.FilterClip = value;
        }
    }

    /// <summary>
    /// Does not update the actual resource file until SaveResourceFile is called
    /// </summary>
    public Rect2 Region
    {
        get
        {
            return actualTexture.Region;
        }
        set
        {
            if (value != actualTexture.Region)
            {
                actualTexture.Region = value;
                RecalculateHandles();
            }
        }
    }

    /// <summary>
    /// Does not update the actual resource file until SaveResourceFile is called
    /// </summary>
    public Rect2 Margin
    {
        get
        {
            return actualTexture.Margin;
        }

        set
        {
            actualTexture.Margin = value;
        }
    }

    /// <summary>
    /// Used when creating an entirely new AtlasTexture resource. This will not
    /// actually exist on the filesystem until SaveResourceFile is called
    /// </summary>
    /// <param name="initialPath"></param>
    /// <param name="initialRegion"></param>
    /// <param name="baseTexture"></param>
    public AtlasTextureEdits(string initialPath, Rect2 initialRegion, Texture2D baseTexture)
    {
        actualTexture = new AtlasTexture();
        actualTexture.Atlas = baseTexture;
        actualTexture.Region = initialRegion;
        editedPath = initialPath;
        RecalculateHandles();
    }

    /// <summary>
    /// Used when tracking changes to an already existing AtlasTexture resource
    /// </summary>
    /// <param name="texture"></param>
    public AtlasTextureEdits(AtlasTexture texture)
    {
        actualTexture = texture;
        editedPath = texture.ResourcePath;
        RecalculateHandles();
    }

    /// <summary>
    /// Saves changes to this atlas texture to the filesystem. If ResourcePath is changed
    /// then it will also be renamed/moved on the file system in this function. DO NOT CALL
    /// EXCEPT FROM DoXAction FUNCTIONS! This ensures that every change made to a texture is
    /// tracked in Godot's undo/redo history.
    /// </summary>
    public void SaveResourceFile()
    {
        if (editedPath != actualTexture.ResourcePath && !string.IsNullOrEmpty(actualTexture.ResourcePath))
        {
            GD.Print("Rename/Move AtlasTexture " + actualTexture.ResourcePath + " to " + editedPath);
            Directory.Move(ProjectSettings.GlobalizePath(actualTexture.ResourcePath), ProjectSettings.GlobalizePath(editedPath));
        }

        actualTexture.TakeOverPath(editedPath);
        actualTexture.ResourcePath = editedPath;
        ResourceSaver.Save(actualTexture, editedPath);
        GD.Print("Saved AtlasTexture " + actualTexture.ResourcePath);
    }

    /// <summary>
    /// Check if the mouse position is on either the tracked AtlasTexture's region or one
    /// of the eight drag/drop handles for resizing the texture.
    /// </summary>
    /// <param name="mousePosition"></param>
    /// <param name="clickedHandle"></param>
    /// <returns></returns>
    public bool GetClickIfAny(Vector2 mousePosition, ref int clickedHandle)
    {
        bool clicked = false;
        if (Region.HasPoint(mousePosition))
        {
            GD.Print(string.Format("Mouse pressed on texture {0}", ResourcePath));
            clicked = true;
            clickedHandle = -1;
        }

        int i = 0;
        foreach (Rect2 handle in Handles)
        {
            if (handle.HasPoint(mousePosition))
            {
                GD.Print(string.Format("Mouse pressed on handle {0} of {1}", i,ResourcePath));
                clickedHandle = i;
                clicked = true;
                break;
            }

            i++;
        }

        return clicked;
    }

    /// <summary>
    /// Move the entire region the specified amount. No size change. Updates drag handle positions.
    /// </summary>
    /// <param name="distance"></param>
    public void MoveRegion(Vector2 distance)
    {
        Rect2 newRegion = Region;
        newRegion.Position += distance;
        Region = newRegion;
        RecalculateHandles();
    }

    /// <summary>
    /// Move the specified handle to the specified position. Moves/resizes the texture so it follows
    /// the handle.
    /// </summary>
    /// <param name="handle">
    /// 0 - top left
    /// 1 - top middle
    /// 2 - top right
    /// 3 - middle (vertically) left
    /// 4 - middle (vertically) right
    /// 5 - bottom left
    /// 6 - bottom middle
    /// 7 - bottom right
    /// </param>
    /// <param name="position"></param>
    public void MoveHandleTo(int handle, Vector2 position)
    {
        // TODO: disallow handles from being dragged outsize of the base texture's region
        Vector2 move = Vector2.Zero;
        Vector2 grow = Vector2.Zero;
        if (position.X < Region.Position.X)
        {
            grow.X = Region.Position.X - position.X;
            move.X = -grow.X;
        }
        else if (position.X > Region.Position.X + Region.Size.X)
        {
            grow.X = position.X - (Region.Position.X + Region.Size.X);
            move.X = 0;
        }
        else
        {
            if (handle == 0 || handle == 3 || handle == 5)
            {
                // dragging the left side inward (right)
                grow.X = Region.Position.X - position.X;
                move.X = -grow.X;
            }
            else if (handle == 2 || handle == 4 || handle == 7)
            {
                // dragging the right side inward (left)
                grow.X = position.X - (Region.Position.X + Region.Size.X);
                move.X = 0;
            }
        }

        if (position.Y < Region.Position.Y)
        {
            grow.Y = Region.Position.Y - position.Y;
            move.Y = -grow.Y;
        }
        else if (position.Y > Region.Position.Y + Region.Size.Y)
        {
            grow.Y = position.Y - (Region.Position.Y + Region.Size.Y);
            move.Y = 0;
        }
        else
        {
            if (handle == 0 || handle == 1 || handle == 2)
            {
                // dragging the top downward
                grow.Y = Region.Position.Y - position.Y;
                move.Y = -grow.Y;
            }
            else if (handle == 5 || handle == 6 || handle == 7)
            {
                // dragging the bottom upward
                grow.Y = position.Y - (Region.Position.Y + Region.Size.Y);
                move.Y = 0;
            }
        }

        Rect2 newRegion = Region;
        newRegion.Position += move;
        newRegion.Size += grow;
        Region = newRegion;

        RecalculateHandles();
    }

    /// <summary>
    /// Update the position of the handles based off of the size/position of
    /// the tracked AtlasTexture region.
    /// </summary>
    private void RecalculateHandles()
    {
        float left, midX, right;
        float top, midY, bottom;

        left = Region.Position.X;
        top = Region.Position.Y;
        right = left + Region.Size.X;
        bottom = top + Region.Size.Y;
        midX = (left + right) / 2;
        midY = (top + bottom) / 2;

        Handles = new Rect2[8] {
                new Rect2(left - AtlasPreviewControls.HandleOffset, top - AtlasPreviewControls.HandleOffset, AtlasPreviewControls.HandleSize, AtlasPreviewControls.HandleSize),
                new Rect2(midX - AtlasPreviewControls.HandleOffset, top - AtlasPreviewControls.HandleOffset, AtlasPreviewControls.HandleSize, AtlasPreviewControls.HandleSize),
                new Rect2(right - AtlasPreviewControls.HandleOffset, top - AtlasPreviewControls.HandleOffset, AtlasPreviewControls.HandleSize, AtlasPreviewControls.HandleSize),
                new Rect2(left - AtlasPreviewControls.HandleOffset, midY - AtlasPreviewControls.HandleOffset, AtlasPreviewControls.HandleSize, AtlasPreviewControls.HandleSize),
                new Rect2(right - AtlasPreviewControls.HandleOffset, midY - AtlasPreviewControls.HandleOffset, AtlasPreviewControls.HandleSize, AtlasPreviewControls.HandleSize),
                new Rect2(left - AtlasPreviewControls.HandleOffset, bottom - AtlasPreviewControls.HandleOffset, AtlasPreviewControls.HandleSize, AtlasPreviewControls.HandleSize),
                new Rect2(midX - AtlasPreviewControls.HandleOffset, bottom - AtlasPreviewControls.HandleOffset, AtlasPreviewControls.HandleSize, AtlasPreviewControls.HandleSize),
                new Rect2(right - AtlasPreviewControls.HandleOffset, bottom - AtlasPreviewControls.HandleOffset, AtlasPreviewControls.HandleSize, AtlasPreviewControls.HandleSize)
            };
    }
}

