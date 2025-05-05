﻿using Godot;
using System.IO;

#if TOOLS
/// <summary>
/// Used to track in-process modifications to an AtlasTexture resource before
/// committing them to the filesystem and provides helper pseudo-control handles
/// to support drag and drop resizing
/// </summary>
public class AtlasTextureEdits
{
    // TODO: consider changing AtlasTextureEdits and Handles to nodes for easier(?) mouse handling
    public Rect2[] Handles;

    private AtlasTexture actualTexture;
    private string editedPath;

    public AtlasTexture ActualTexture
    {
        get
        {
            return actualTexture;
        }

        set
        {
            actualTexture = value;
            OriginalRegion = actualTexture.Region;
            RecalculateHandles();
        }
    }

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
            return ActualTexture.FilterClip;
        }

        set
        {
            ActualTexture.FilterClip = value;
        }
    }

    /// <summary>
    /// Gets the most recently saved AtlasTexture Region
    /// </summary>
    public Rect2 OriginalRegion
    {
        get; private set;
    }

    /// <summary>
    /// Does not update the actual resource file until SaveResourceFile is called
    /// </summary>
    public Rect2 Region
    {
        get
        {
            return ActualTexture.Region;
        }
        set
        {
            if (value != ActualTexture.Region)
            {
                ActualTexture.Region = value;
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
            return ActualTexture.Margin;
        }

        set
        {
            ActualTexture.Margin = value;
        }
    }

    private QuickAtlasGridSettings gridSettings;

    /// <summary>
    /// Used when creating an entirely new AtlasTexture resource. This will not
    /// actually exist on the filesystem until SaveResourceFile is called
    /// </summary>
    /// <param name="initialPath"></param>
    /// <param name="initialRegion"></param>
    /// <param name="baseTexture"></param>
    public AtlasTextureEdits(QuickAtlasGridSettings gridSettings, string initialPath, Rect2 initialRegion, Texture2D baseTexture)
    {
        this.gridSettings = gridSettings;

        OriginalRegion = initialRegion;
        ActualTexture = new AtlasTexture();
        ActualTexture.Atlas = baseTexture;
        ActualTexture.Region = initialRegion;
        editedPath = initialPath;
        RecalculateHandles();
    }

    /// <summary>
    /// Used when tracking changes to an already existing AtlasTexture resource
    /// </summary>
    /// <param name="texture"></param>
    public AtlasTextureEdits(QuickAtlasGridSettings gridSettings, AtlasTexture texture)
    {
        this.gridSettings = gridSettings;

        ActualTexture = texture;
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
        ResourceSaver.SaverFlags saveFlags = ResourceSaver.SaverFlags.None;
        if (editedPath != ActualTexture.ResourcePath && !string.IsNullOrEmpty(ActualTexture.ResourcePath))
        {
            GD.Print("Rename/Move AtlasTexture " + ActualTexture.ResourcePath + " to " + editedPath);
            Directory.Move(ProjectSettings.GlobalizePath(ActualTexture.ResourcePath), ProjectSettings.GlobalizePath(editedPath));
            saveFlags = ResourceSaver.SaverFlags.ChangePath | ResourceSaver.SaverFlags.ReplaceSubresourcePaths;
        }

        OriginalRegion = ActualTexture.Region;
        ResourceSaver.Save(ActualTexture, editedPath, saveFlags);
        GD.Print("Saved AtlasTexture " + ActualTexture.ResourcePath);
    }

    /// <summary>
    /// Check if the mouse position is on either the tracked AtlasTexture's region or one
    /// of the eight drag/drop handles for resizing the texture.
    /// </summary>
    /// <param name="mousePosition"></param>
    /// <param name="clickedHandle"></param>
    /// <returns></returns>
    public bool GetClickIfAny(Vector2 mousePosition, float zoomScaleValue, ref int clickedHandle)
    {
        bool clicked = false;
        if (Region.HasPoint(mousePosition / zoomScaleValue))
        {
            GD.Print(string.Format("Mouse pressed on texture {0}", ResourcePath));
            clicked = true;
            clickedHandle = -1;
        }

        int i = 0;
        foreach (Rect2 handle in Handles)
        {
            // we use a handle that's artifically enlarged to determine if the mouse is on it
            // so that, no matter what the zoom level is the clickable area is the same size
            Rect2 scaledHandle = new Rect2(handle.Position + handle.Size * 0.5f, handle.Size / zoomScaleValue);
            scaledHandle.Position -= scaledHandle.Size * 0.5f;

            if (scaledHandle.HasPoint(mousePosition / zoomScaleValue))
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
    public void MoveRegion(Vector2 offset, Vector2 positionOnAtlas)
    {
        Vector2 distance = positionOnAtlas - (Region.Position + offset);

        // clamp movement such that the texture cannot be moved outside of its source atlas 
        Vector2 atlasSize = actualTexture.Atlas.GetSize();
        if (Region.Position.X < -distance.X) distance.X = -Region.Position.X;
        if (Region.Position.Y < -distance.Y) distance.Y = -Region.Position.Y;
        if (distance.X + Region.End.X >= atlasSize.X) distance.X = atlasSize.X - Region.End.X;
        if (distance.Y + Region.End.Y >= atlasSize.Y) distance.Y = atlasSize.Y - Region.End.Y;

        // force movement to keep corner of region aligned with the grid
        Vector2 newPosition = Region.Position + distance;
        SnapPositionToGrid(ref newPosition);

        Rect2 newRegion = Region;
        newRegion.Position = newPosition;
        Region = newRegion;
        RecalculateHandles();
    }

    /// <summary>
    /// Move the specified handle to the specified position. Moves/resizes the texture so it follows
    /// the handle.
    /// </summary>
    /// <param name="handle">
    /// This can change when the drag operation changes the handle's relative location
    /// (e.g. when dragging the right side across the left this will change from 4 to 3)
    /// 
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
    public void MoveHandleTo(ref int handle, Vector2 position)
    {
        // TODO: add dummy handle index to allow swapping selected handle with MAAAATH!!! instead of big ifs
        Vector2 move = Vector2.Zero;
        Vector2 grow = Vector2.Zero;

        // force position to nearest grid coordinate if snap is on or nearest pixel if not
        SnapPositionToGrid(ref position);

        if (handle == 0 || handle == 3 || handle == 5)
        {
            // dragging the left around
            float distance = position.X - Region.Position.X;
            move.X = distance;
            grow.X = -distance;
            if(distance > Region.Size.X)
            {
                // moving left side past the right side
                move.X = Region.Size.X;
                grow.X = distance - Region.Size.X;

                // swap selected handle to the right side to allow smooth drag
                if (handle == 0) handle = 2;
                if (handle == 3) handle = 4;
                if (handle == 5) handle = 7;
            }
        }
        else if (handle == 2 || handle == 4 || handle == 7)
        {
            // dragging the right around
            float distance = position.X - (Region.Position.X + Region.Size.X);
            grow.X = distance;
            if (distance < -Region.Size.X)
            {
                // moving the right side past the left side
                move.X = distance - (-Region.Size.X);
                grow.X = -distance - Region.Size.X * 2;

                // swap selected handle to the left side to allow smooth drag
                if (handle == 2) handle = 0;
                if (handle == 4) handle = 3;
                if (handle == 7) handle = 5;
            }
        }

        if (handle == 0 || handle == 1 || handle == 2)
        {
            // dragging the top around
            float distance = position.Y - Region.Position.Y;
            move.Y = distance;
            grow.Y = -distance;

            if (distance > Region.Size.Y)
            {
                // moving top past the bottom
                move.Y = Region.Size.Y;
                grow.Y = distance - Region.Size.Y;

                // swap selected handle to the left side to allow smooth drag
                // 0 -> 5 swaps from top to bottom +1 for each increment to the right
                handle += 5;
            }
        }
        else if(handle == 5 || handle == 6 || handle == 7)
        {
            // dragging the bottom around
            float distance = position.Y - (Region.Position.Y + Region.Size.Y);
            grow.Y = distance;
            if (distance < -Region.Size.Y)
            {
                // moving bottom past top
                move.Y = distance - (-Region.Size.Y);
                grow.Y = -distance - Region.Size.Y * 2;

                // swap selected handle to the top side to allow smooth drag
                // 5 -> 0 swaps from bottom to top +1 for each increment to the right
                handle -= 5;
            }
        }

        Rect2 newRegion = Region;
        newRegion.Position += move;
        newRegion.Size += grow;
        Region = newRegion;
        RecalculateHandles();
    }

    private void SnapPositionToGrid(ref Vector2 position)
    {
        Vector2 gridSnap = Vector2.One;
        if (gridSettings.SnapToGrid)
        {
            gridSnap = new Vector2(gridSettings.GridSizeX, gridSettings.GridSizeY);
        }

        // gross hack, floor will cause undesired movement if position is already grid aligned
        position.X = Mathf.Floor(position.X / gridSnap.X) * gridSnap.X;
        position.Y = Mathf.Floor(position.Y / gridSnap.Y) * gridSnap.Y;
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
#endif

