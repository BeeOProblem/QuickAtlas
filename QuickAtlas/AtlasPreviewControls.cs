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
	public const float HandleSize = 5;
    public const float HandleOffset = HandleSize / 2;

    [Export]
    QuickAtlasEditorWindow window;
    
    [Export]
    ScrollContainer scrollArea;

    /// true indicates the current drag operation is for creating a new AtlasTexture
    private bool addingNewTexture;

    private AtlasTextureEdits clickedTexture;
    private int clickedHandle;

    // this is supplied by the Init function and populated by QuickAtlasEditorWindow
    // DO NOT ADD OR REMOVE ITEMS!
    private List<AtlasTextureEdits> textures;

    public void SetAtlasSource(List<AtlasTextureEdits> textures) 
	{
        this.textures = textures;
		QueueRedraw();
	}

	public override void _Ready()
	{
        textures = new List<AtlasTextureEdits>();
    }

    public override void _Draw()
	{
		if (textures == null) return;
		foreach(AtlasTextureEdits texture in textures)
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
                window.GrabFocus();
                AcceptEvent();
                QueueRedraw();

                if (mouseEvent.Pressed)
                {
                    addingNewTexture = false;
                    clickedTexture = null;
                    foreach (AtlasTextureEdits texture in textures)
                    {
                        if (texture.GetClickIfAny(mouseEvent.Position, ref clickedHandle))
                        { 
                            clickedTexture = texture;
                            break;
                        }
                    }

                    if(clickedTexture == null)
                    {
                        clickedTexture = window.StartNewTexture(mouseEvent.Position);

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
                        if(addingNewTexture)
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
        }

        if(inputEvent is InputEventMouseMotion)
        {
            InputEventMouseMotion motionEvent = (InputEventMouseMotion)inputEvent;
            if(motionEvent.ButtonMask == MouseButtonMask.Left)
            {
                if(clickedTexture != null)
                {
                    if (clickedHandle < 0)
                    {
                        clickedTexture.MoveRegion(motionEvent.Relative);
                    }
                    else
                    {
                        clickedTexture.MoveHandleTo(clickedHandle, motionEvent.Position);
                    }

                    GetViewport().SetInputAsHandled();
                    QueueRedraw();
                }
            }
            else if(motionEvent.ButtonMask == MouseButtonMask.Middle)
            {
                scrollArea.ScrollHorizontal -= (int)motionEvent.Relative.X;
                scrollArea.ScrollVertical -= (int)motionEvent.Relative.Y;

                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void DrawAtlasTextureControls(AtlasTextureEdits texture)
    {
        if (texture == clickedTexture)
        {
            DrawRect(texture.Region, Colors.Red, false);
        }
        else if (texture == window.SelectedTexture)
        {
            DrawRect(texture.Region, Colors.Green, false);
        }
        else
        {
            DrawRect(texture.Region, Colors.White, false);
        }

        int i =  0;
        foreach (Rect2 handle in texture.Handles)
        {
            if (i == clickedHandle && texture == clickedTexture)
            {
                DrawRect(handle, Colors.DeepPink, true);
            }
            else
            {
                DrawRect(handle, Colors.White, true);
            }
        }
    }
}
