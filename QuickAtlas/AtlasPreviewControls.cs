using Godot;
using System.Collections.Generic;

[Tool]
public partial class AtlasPreviewControls : Control
{
	public const float HandleSize = 5;
    public const float HandleOffset = HandleSize / 2;

    [Export]
    QuickAtlasEditorWindow window;
    
    [Export]
    ScrollContainer scrollArea;

    // this is supplied by the Init function and populated elsewhere
    // DO NOT ADD OR REMOVE ITEMS!
    private List<AtlasTextureEdits> textures;

    // these members can be modified
    private int newTextureCounter;

    private bool addingNewTexture;
    private AtlasTextureEdits clickedTexture;
    private int clickedHandle;

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
                GetViewport().SetInputAsHandled();
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
                        clickedTexture = window.AddNewTexture(mouseEvent.Position);

                        // bottom right, arbitrary but should give decent ability to size and fit workflow
                        clickedHandle = 7;
                        addingNewTexture = true;
                    }

                    window.SelectedTexture = clickedTexture;
                }
                else
                {
                    if (clickedTexture != null)
                    {
                        window.SaveChangesAndUpdateHistory(clickedTexture);

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
