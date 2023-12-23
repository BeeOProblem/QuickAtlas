using Godot;
using System.Collections.Generic;

/// <summary>
/// Main script for the scrollable preview of the current source texture being
/// used for creating/editing AtlasTexture resources. Draws boxes with handles
/// for each existing texture to allow modification by drag+drop. Also is responsible
/// for creating new regions when dragging in an area not occupied by an AtlasTexture
/// 
/// The QuickAtlasEditorWindow object is responsible for the actual work of
/// creating and maintining the list of AtlasTextureEdits tracking objects as
/// well as committing any changes to the filesystem. This only handles user input.
/// </summary>
[Tool]
public partial class AtlasPreviewControls : Control
{
	public const float HandleSize = 7;
    public const float HandleOffset = HandleSize / 2;

    [Export]
    QuickAtlasEditorWindow window;
    
    [Export]
    ScrollContainer ScrollArea;
    [Export]
    TextureRect TexturePreviewArea;
    [Export]
    MarginContainer PreviewAreaMargin;
    [Export]
    Label ZoomLevelLabel;

    [Export]
    int MaxZoomPercent = 400;

    [Export]
    int MinZoomPercent = 10;

    [Export]
    int ZoomPercentIncrement = 10;

    private int zoomPercentage = 100;
    private float zoomScaleValue = 1;

    // true indicates the current drag operation is for creating a new AtlasTexture
    private bool addingNewTexture;

    private AtlasTextureEdits clickedTexture;
    private int clickedHandle;

    private int firstHandle;
    private Vector2 dragStart;

    // this is supplied by the Init function and populated by QuickAtlasEditorWindow
    // DO NOT ADD OR REMOVE ITEMS!
    private List<AtlasTextureEdits> textures;
    private List<AtlasTextureEdits> emptyTextureList = new List<AtlasTextureEdits>();

    public void SetAtlasSource(Texture2D sourceTexture, List<AtlasTextureEdits> textures) 
	{
        // if we aren't editing anything make sure we don't draw any AtlasTexture regions
        if (textures == null) textures = emptyTextureList;
        if (ReferenceEquals(textures, this.textures)) return;

        this.textures = textures;

        zoomScaleValue = 1;
        zoomPercentage = 100;

        // make sure margins and such are set up correctly
        // even though margins only need to be set when resizing and when _Ready
        // _Ready is called before our size is actually set properly
        _OnScrollAreaSizeChanged();

        QueueRedraw();
	}

	public override void _Ready()
	{
        textures = new List<AtlasTextureEdits>();
    }

    public override void _Draw()
	{
		if (textures == null) return;
        DrawRect(new Rect2(0, 0, TexturePreviewArea.Size), Colors.Aqua, false);

        foreach (AtlasTextureEdits texture in textures)
		{
            DrawAtlasTextureControls(texture);
		}
	}

    public override void _GuiInput(InputEvent inputEvent)
    {
		if(inputEvent is InputEventMouseButton)
		{
            InputEventMouseButton mouseEvent = (InputEventMouseButton)inputEvent;
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                StartOrEndDragOperation(mouseEvent);
            }
            else if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.WheelDown)
            {
                ChangeZoom(-ZoomPercentIncrement);
            }
            else if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.WheelUp)
            {
                ChangeZoom(ZoomPercentIncrement);
            }
        }

        if (inputEvent is InputEventMouseMotion)
        {
            InputEventMouseMotion motionEvent = (InputEventMouseMotion)inputEvent;
            if(motionEvent.ButtonMask == MouseButtonMask.Left)
            {
                if(clickedTexture != null)
                {
                    if (clickedHandle < 0)
                    {
                        clickedTexture.MoveRegion(motionEvent.Relative / zoomScaleValue);
                    }
                    else
                    {
                        clickedTexture.MoveHandleTo(ref clickedHandle, motionEvent.Position / zoomScaleValue);
                    }

                    AcceptEvent();
                    QueueRedraw();
                }
            }
            else
            {
                if (motionEvent.ButtonMask == MouseButtonMask.Middle)
                {
                    ScrollArea.ScrollHorizontal -= (int)(motionEvent.Relative.X);
                    ScrollArea.ScrollVertical -= (int)(motionEvent.Relative.Y);
                    AcceptEvent();
                }
            }
        }
    }

    private void _OnScrollAreaSizeChanged()
    {
        Vector2 halfSize = ScrollArea.Size * 0.5f;
        PreviewAreaMargin.AddThemeConstantOverride("margin_left", (int)halfSize.X);
        PreviewAreaMargin.AddThemeConstantOverride("margin_right", (int)halfSize.X);
        PreviewAreaMargin.AddThemeConstantOverride("margin_top", (int)halfSize.Y);
        PreviewAreaMargin.AddThemeConstantOverride("margin_bottom", (int)halfSize.Y);

        if (TexturePreviewArea.Texture != null)
        {
            PreviewAreaMargin.CustomMinimumSize = TexturePreviewArea.Texture.GetSize() * zoomScaleValue + ScrollArea.Size;
        }
    }

    private void _OnZoomInPressed()
    {
        ChangeZoom(ZoomPercentIncrement);
    }

    private void _OnZoomOutPressed()
    {
        ChangeZoom(-ZoomPercentIncrement);
    }

    /// <summary>
    /// Center the selected AtlasTexture region in the preview area
    /// </summary>
    private void CenterView()
    {
        Vector2 previewCenter = ScrollArea.Size * 0.5f;
        Vector2 halfSize = window.SelectedTexture.Region.Size * 0.5f * zoomScaleValue;
        Vector2 scrollPos = window.SelectedTexture.Region.Position * zoomScaleValue - (previewCenter - halfSize);
        scrollPos.X += PreviewAreaMargin.GetThemeConstant("margin_left");
        scrollPos.Y += PreviewAreaMargin.GetThemeConstant("margin_top");
        ScrollArea.SetDeferred("scroll_horizontal", (int)scrollPos.X);
        ScrollArea.SetDeferred("scroll_vertical", (int)scrollPos.Y);
    }

    private void StartOrEndDragOperation(InputEventMouseButton mouseEvent)
    {
        window.GrabFocus();
        AcceptEvent();
        QueueRedraw();

        if (mouseEvent.Pressed)
        {
            addingNewTexture = false;
            clickedTexture = null;
            foreach (AtlasTextureEdits texture in textures)
            {
                if (texture.GetClickIfAny(mouseEvent.Position, zoomScaleValue, ref clickedHandle))
                {
                    clickedTexture = texture;
                    break;
                }
            }

            if (clickedTexture == null)
            {
                clickedTexture = window.StartNewTexture(mouseEvent.Position / zoomScaleValue);

                // bottom right, arbitrary but should give decent ability to size and fit workflow
                // NOTE: gets janky if the user drags in any direction besides down and right
                clickedHandle = 7;
                addingNewTexture = true;
            }

            window.SelectedTexture = clickedTexture;
        }
        else
        {
            if (clickedTexture != null)
            {
                if (addingNewTexture)
                {
                    window.DoAddNewTextureAction(clickedTexture);
                }
                else
                {
                    window.DoChangeRegionAction(clickedTexture.Region);
                }

                // click is no longer in progress
                clickedTexture = null;
                clickedHandle = -1;
            }
        }
    }

    private void ChangeZoom(int increment)
    {

        AcceptEvent();
        QueueRedraw();

        float zoomScaleValueOld = zoomScaleValue;
        zoomPercentage += increment;
        zoomPercentage = Mathf.Clamp(zoomPercentage, MinZoomPercent, MaxZoomPercent);
        zoomScaleValue = zoomPercentage / 100.0f;

        // since the margin and center are both equal to half the ScrollArea size
        // compensating for both causes the values to cancel so the ScrollArea.Scroll... is the center
        Vector2 scrollPosCenter = new Vector2(ScrollArea.ScrollHorizontal, ScrollArea.ScrollVertical);
        Vector2 scrollPosScaledOld = scrollPosCenter / zoomScaleValueOld;
        Vector2 scrollPosNew = scrollPosScaledOld * zoomScaleValue;

        PreviewAreaMargin.CustomMinimumSize = TexturePreviewArea.Texture.GetSize() * zoomScaleValue + ScrollArea.Size;
        ScrollArea.SetDeferred("scroll_horizontal", (int)scrollPosNew.X);
        ScrollArea.SetDeferred("scroll_vertical", (int)scrollPosNew.Y);

        ZoomLevelLabel.Text = zoomScaleValue.ToString("P0");
    }

    private void DrawAtlasTextureControls(AtlasTextureEdits texture)
    {
        Rect2 scaledTexture = texture.Region;
        scaledTexture.Size *= zoomScaleValue;
        scaledTexture.Position *= zoomScaleValue;
        if (texture == clickedTexture)
        {
            DrawRect(scaledTexture, Colors.Red, false);
        }
        else if (texture == window.SelectedTexture)
        {
            DrawRect(scaledTexture, Colors.Green, false);
        }
        else
        {
            DrawRect(scaledTexture, Colors.White, false);
        }

        int i =  0;
        foreach (Rect2 handle in texture.Handles)
        {
            Rect2 scaledHandle = handle;
            // we want to keep the handles the same size but position
            // is offset so the center of the handle is at the position of the rectangle part it's meant to move
            // so we need to remove the offset before scaling then put it back
            scaledHandle.Position += scaledHandle.Size * 0.5f;
            scaledHandle.Position *= zoomScaleValue;
            scaledHandle.Position -= scaledHandle.Size * 0.5f;
            if (i == clickedHandle && texture == clickedTexture)
            {
                DrawRect(scaledHandle, Colors.Yellow, true);
            }
            else
            {
                DrawRect(scaledHandle, Colors.White, true);
            }

            i++;
        }
    }
}
