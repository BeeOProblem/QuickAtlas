using Godot;
using System.IO;
using System.Collections.Generic;

[Tool]
public partial class QuickAtlasEditorWindow : Control
{
    [Export]
    Control NoTargetOverlay;

    [Export]
    TextureRect TexturePreviewArea;

    [Export]
    AtlasPreviewControls PreviewControls;
    [Export]
    ScrollContainer PreviewContainer;

    [Export]
    FileDialog FileDialog;
    [Export]
    ConfirmationDialog ConfirmationDialog;
    [Export]
    AcceptDialog ErrorDialog;

    [Export]
    TextureRect SubTexturePreviewArea;
    [Export]
    LineEdit ResourceName;

    [Export]
    SpinBox RegionX;
    [Export]
    SpinBox RegionY;
    [Export]
    SpinBox RegionW;
    [Export]
    SpinBox RegionH;

    [Export]
    SpinBox MarginX;
    [Export]
    SpinBox MarginY;
    [Export]
    SpinBox MarginW;
    [Export]
    SpinBox MarginH;

    [Export]
    CheckBox FilterClip;

    private EditorInterface editorInterface;
    private EditorUndoRedoManager undoRedo;
    private Dictionary<string, List<string>> textureAtlasRefs;

    private Texture2D currentBaseTexture;
    private string basePath;

    private int newTextureCounter = 0;
    private AtlasTextureEdits selectedAtlasTexture;
    private List<AtlasTextureEdits> textureEdits;

    public AtlasTextureEdits SelectedTexture
    {
        get
        {
            return selectedAtlasTexture;
        }

        set
        {
            if (selectedAtlasTexture == value) return;
            selectedAtlasTexture = value;
            UpdateControlValues();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        InputEventKey key = @event as InputEventKey;
        if (currentBaseTexture != null && SelectedTexture != null)
        {
            if (key != null && key.Keycode == Key.Delete)
            {
                AcceptEvent();
                if (!key.Pressed)
                {
                    if (SelectedTexture != null)
                    {
                        _OnDeletePressed();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Initialize references to objects that are normally only accessible to EditorPlugin
    /// </summary>
    /// <param name="editorInterface"></param>
    /// <param name="undoRedo"></param>
    public void Init(EditorInterface editorInterface, EditorUndoRedoManager undoRedo)
    {
        if (this.editorInterface != null) return;

        this.editorInterface = editorInterface;
        this.undoRedo = undoRedo;

        RebuildResourceDictionary();
        editorInterface.GetResourceFilesystem().FilesystemChanged += _FilesystemChanged;
        textureEdits = new List<AtlasTextureEdits>();
    }

    /// <summary>
    /// DO NOT CALL THIS DIRECTLY! Use SetEditTarget instead
    /// <para />
    /// Loads the specified texture for use in creating AtlasTexture regions using it
    /// as a source. If any exist already then this will create AtlasTextureEdits instances
    /// to allow them to be modified if the user desires.
    /// <para />
    /// Called by Godot's undo/redo history to change the current source texture for
    /// AtlasTextures to be edited. This needs to be in the undo history so that other
    /// historic actions on AtlasTexture are done in the appropriate context.
    /// </summary>
    /// <param name="target"></param>
    public void DoSetEditTargetAction(string target)
    {
        if (target == "null")
        {
            // TODO: calling with target of null in middle of setting valid edit target
            NoTargetOverlay.Visible = true;

            // set all controls to a default blank state when nothing is selected
            currentBaseTexture = null;
            SubTexturePreviewArea.Texture = null;
            TexturePreviewArea.Texture = null;
            PreviewControls.SetAtlasSource(null, null);

            ResourceName.Text = string.Empty;
            RegionX.SetValueNoSignal(0);
            RegionY.SetValueNoSignal(0);
            RegionW.SetValueNoSignal(0);
            RegionH.SetValueNoSignal(0);
            MarginX.SetValueNoSignal(0);
            MarginY.SetValueNoSignal(0);
            MarginW.SetValueNoSignal(0);
            MarginH.SetValueNoSignal(0);
            FilterClip.SetPressedNoSignal(false);
        }
        else
        {
            // grab atlastexture paths from texture path
            Texture2D targetTexture = ResourceLoader.Load<Texture2D>(target);
            currentBaseTexture = targetTexture;
            TexturePreviewArea.Texture = targetTexture;

            textureEdits.Clear();

            if (currentBaseTexture != null)
            {
                string atlasSource = currentBaseTexture.ResourcePath;

                GD.Print("Getting atlas textures from " + atlasSource);
                List<string> textureNames = textureAtlasRefs[atlasSource];
                foreach (string textureName in textureNames)
                {
                    GD.Print("\t" + textureName);
                    textureEdits.Add(new AtlasTextureEdits(ResourceLoader.Load<AtlasTexture>(textureName)));
                }
            }

            PreviewControls.SetAtlasSource(targetTexture, textureEdits);
            basePath = targetTexture.ResourcePath.Substr(0, targetTexture.ResourcePath.LastIndexOf('/'));
            RegionX.MaxValue = currentBaseTexture.GetWidth();
            RegionY.MaxValue = currentBaseTexture.GetHeight();
            RegionW.MaxValue = currentBaseTexture.GetWidth();
            RegionH.MaxValue = currentBaseTexture.GetHeight();
            NoTargetOverlay.Visible = false;
        }
    }

    /// <summary>
    /// Called by the plugin entry point to start modifying AtlasTextures associated with
    /// the given texture. If this is called with an AtlasTexture as the target it will
    /// instead use the AtlasTexture's source texture as a target since this is not built
    /// to allow AtlasTexture instances to be sourced from other AtlasTexture instances
    /// </summary>
    /// <param name="target"></param>
    public void SetEditTarget(Texture2D target)
    {
        AtlasTexture targetAsAtlas = target as AtlasTexture;
        if (targetAsAtlas != null)
        {
            target = targetAsAtlas.Atlas;
        }

        if (target != currentBaseTexture)
        {
            undoRedo.CreateAction("QuickAtlas - Change source atlas", UndoRedo.MergeMode.Ends, this);
            undoRedo.AddDoMethod(this, "DoSetEditTargetAction", target != null ? target.ResourcePath : "null");
            undoRedo.AddUndoMethod(this, "DoSetEditTargetAction", currentBaseTexture != null ? currentBaseTexture.ResourcePath : "null");
            undoRedo.CommitAction();
        }

        if (targetAsAtlas != null)
        { 
            for (int i = 0; i < textureEdits.Count; i++)
            {
                if (textureEdits[i].actualTexture.ResourcePath == targetAsAtlas.ResourcePath)
                {
                    SelectedTexture = textureEdits[i];
                    PreviewControls.CallDeferred("CenterView");
                    return;
                }
            }
        
            GD.Print("Unable to find edit info for " + targetAsAtlas.ResourcePath);
        }
    }

    /// <summary>
    /// First step in creating a new AtlasTexture. Creates the internal tracking object
    /// for the AtlasTexture with its region starting at the given position but does not
    /// commit it to the filesystem. This texture will start with a size of 0x0 and MUST be
    /// resized before it can be saved.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public AtlasTextureEdits StartNewTexture(Vector2 position)
    {
        newTextureCounter++;

        string newTextureName = string.Format("{0}/new_atlas_texture_{1}.tres", basePath, newTextureCounter);
        AtlasTextureEdits newTexture = new AtlasTextureEdits(newTextureName, new Rect2(position, Vector2.One), currentBaseTexture);
        textureEdits.Add(newTexture);
        return newTexture;
    }

    /// <summary>
    /// Final step in creating a new AtlasTexture. Commits it to Godot's undo history and saves the
    /// texture to the filesystem and ensures all internal tracking is up to date.
    /// </summary>
    /// <param name="editedTexture"></param>
    public void DoAddNewTextureAction(AtlasTextureEdits editedTexture)
    {
        undoRedo.CreateAction("QuickAtlas - Create AtlasTexture Region");
        undoRedo.AddDoMethod(this, "AddTextureRegion", editedTexture.ResourcePath, editedTexture.Region);
        undoRedo.AddUndoMethod(this, "DeleteTextureRegion", editedTexture.ResourcePath);
        undoRedo.CommitAction();
    }

    /// <summary>
    /// Changes the size of the currently selected texture and commits the action to Godot's
    /// undo history.
    /// </summary>
    /// <param name="newRegion"></param>
    public void DoChangeRegionAction(Rect2 newRegion)
    {
        undoRedo.CreateAction("QuickAtlas - Region");
        undoRedo.AddDoMethod(this, "ChangeRegion", selectedAtlasTexture.ResourcePath, newRegion);
        undoRedo.AddUndoMethod(this, "ChangeRegion", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.OriginalRegion);
        undoRedo.CommitAction();
    }

    /// <summary>
    /// DO NOT CALL DIRECTLY! This is called by the Godot editor to commit a new AtlasTexture
    /// to the filesystem. Updates internal Texure -> AtlasTexture[] tracking data as well.
    /// </summary>
    /// <param name="newResourcePath"></param>
    /// <param name="newRegion"></param>
    /// <exception cref="System.Exception"></exception>
    public void AddTextureRegion(string newResourcePath, Rect2 newRegion)
    {
        GD.Print("(Re)create AtlasTexture ", newResourcePath, newRegion);
        AtlasTextureEdits newTexture = null;
        for (int i = 0; i < textureEdits.Count; i++)
        {
            if (textureEdits[i].ResourcePath == newResourcePath)
            {
                if (SelectedTexture != textureEdits[i])
                {
                    throw new System.Exception("Creating AtlasTexture that already exists via undo/redo!");
                }
                else
                {
                    GD.Print("Committing new texture created from drag+drop");
                    newTexture = textureEdits[i];
                }
            }
        }

        if (newTexture == null)
        {
            newTexture = new AtlasTextureEdits(newResourcePath, newRegion, currentBaseTexture);
            textureEdits.Add(newTexture);
        }

        try
        {
            newTexture.SaveResourceFile();

            textureAtlasRefs[currentBaseTexture.ResourcePath].Add(newTexture.ResourcePath);
            selectedAtlasTexture = newTexture;
            UpdateControlValues();
            editorInterface.GetResourceFilesystem().Scan();
        }
        catch (IOException error)
        {
            textureEdits.Remove(newTexture);
            ErrorDialog.DialogText = error.Message;
            ErrorDialog.Show();
        }
    }

    /// <summary>
    /// DO NOT CALL DIRECTLY! This is called by the Godot editor to commit renaming or moving
    /// an AtlasTexture resource in the filesystem. Updates internal Texure -> AtlasTexture[]
    /// tracking data as well.
    /// </summary>
    /// <param name="oldResourcePath"></param>
    /// <param name="newResourcePath"></param>
    public void ChangeTextureResourcePath(string oldResourcePath, string newResourcePath)
    {
        if (oldResourcePath == newResourcePath)
        {
            GD.Print("Changing ", oldResourcePath, " to same name");
            return;
        }

        GD.Print("Change path from ", oldResourcePath, " to ", newResourcePath);
        for (int i = 0; i < textureEdits.Count; i++)
        {
            if (textureEdits[i].ResourcePath == oldResourcePath)
            {
                textureEdits[i].ResourcePath = newResourcePath;

                try
                {
                    textureEdits[i].SaveResourceFile();

                    UpdateControlValues();
                    editorInterface.GetResourceFilesystem().Scan();

                    textureAtlasRefs[currentBaseTexture.ResourcePath].Remove(oldResourcePath);
                    textureAtlasRefs[currentBaseTexture.ResourcePath].Add(newResourcePath);
                }
                catch (IOException error)
                {
                    ErrorDialog.DialogText = error.Message;
                    ErrorDialog.Show();

                    textureEdits[i].ResourcePath = oldResourcePath;
                }

                return;
            }
        }

        GD.Print("Cannot find AtlasTexture, probably associated with different parent than selected ", oldResourcePath);
    }

    /// <summary>
    /// DO NOT CALL DIRECTLY! This is called by the Godot editor to commit deletion of an AtlasTexture
    /// resource from the filesystem. Updates internal Texure -> AtlasTexture[] tracking data as well.
    /// </summary>
    /// <param name="targetResourcePath"></param>
    public void DeleteTextureRegion(string targetResourcePath)
    {
        GD.Print("Delete AtlasTexture ", targetResourcePath);
        for (int i = 0; i < textureEdits.Count; i++)
        {
            if (textureEdits[i].ResourcePath == targetResourcePath)
            {
                File.Delete(ProjectSettings.GlobalizePath(selectedAtlasTexture.ResourcePath));
                textureAtlasRefs[currentBaseTexture.ResourcePath].Remove(selectedAtlasTexture.ResourcePath);
                textureEdits.Remove(selectedAtlasTexture);
                selectedAtlasTexture = null;
                UpdateControlValues();
                editorInterface.GetResourceFilesystem().Scan();
                return;
            }
        }

        GD.Print("Deleting AtlasTexture that was already deleted");
    }

    /// <summary>
    /// DO NOT CALL DIRECTLY! This is called by the Godot editor to commit changes to the
    /// specified AtlasTexture's region. Used for both resize and move operations. Saves
    /// changes to the filesystem.
    /// <para />
    /// Note that this is not used in creation since the create action is expected to be
    /// done after the user is done providing a size. This keeps undo history cleaner and
    /// prevents Undo from producing a useless 0x0 texture.
    /// </summary>
    /// <param name="resourcePath"></param>
    /// <param name="newRegion"></param>
    public void ChangeRegion(string resourcePath, Rect2 newRegion)
    {
        GD.Print("Change Region ", resourcePath);
        for (int i = 0; i < textureEdits.Count; i++)
        {
            if (textureEdits[i].ResourcePath == resourcePath)
            {
                textureEdits[i].Region = newRegion;
                UpdateControlValues();
                textureEdits[i].SaveResourceFile();
            }
        }
    }

    /// <summary>
    /// DO NOT CALL DIRECTLY! This is called by the Godot editor to commit changes to the
    /// specified AtlasTexture's margin. Saves changes to the filesystem.
    /// </summary>
    /// <param name="resourcePath"></param>
    /// <param name="newMargin"></param>
    public void ChangeMargin(string resourcePath, Rect2 newMargin)
    {
        GD.Print("Change Margin ", resourcePath);
        for (int i = 0; i < textureEdits.Count; i++)
        {
            if (textureEdits[i].ResourcePath == resourcePath)
            {
                textureEdits[i].Margin = newMargin;
                UpdateControlValues();
                textureEdits[i].SaveResourceFile();
            }
        }
    }

    /// <summary>
    /// DO NOT CALL DIRECTLY! This is called by the Godot editor to commit changes to the
    /// filter_clip property of an AtlasTexture. Saves changes to the filesystem.
    /// </summary>
    /// <param name="resourcePath"></param>
    /// <param name="value"></param>
    public void SetFilterClip(string resourcePath, bool value)
    {
        GD.Print("Change FilterClip ", resourcePath);
        for (int i = 0; i < textureEdits.Count; i++)
        {
            if (textureEdits[i].ResourcePath == resourcePath)
            {
                textureEdits[i].FilterClip = value;
                UpdateControlValues();
                textureEdits[i].SaveResourceFile();
            }
        }
    }

    /// <summary>
    /// Event handler. Called when Godot detects a change to the filesystem and is used
    /// to update internal tracking of associations between Texture2D resources and
    /// AtlasTexture resources.
    /// </summary>
    private void _FilesystemChanged()
    {
        GD.Print("File system state changed. Rebuilding AtlasTexture dictionary");
        RebuildResourceDictionary();
        UpdateControlValues();
    }

    /// <summary>
    /// Signal handler for the Rename... button.
    /// </summary>
    private void _OnRenamePressed()
    {
        FileDialog.CurrentPath = selectedAtlasTexture.ResourcePath;
        FileDialog.Show();
    }

    /// <summary>
    /// Signal handler. Rename the selected texture to the selected file. File dialog is opened
    /// when Rename... is clicked.
    /// </summary>
    /// <param name="path"></param>
    private void _OnFileDialogSelected(string path)
    {
        DoRenameAction(path);
    }

    /// <summary>
    /// Signal handler for the Delete button. Action requires confirmation.
    /// </summary>
    private void _OnDeletePressed()
    {
        string name = SelectedTexture.ResourcePath.Substring(SelectedTexture.ResourcePath.LastIndexOf("/") + 1);
        ConfirmationDialog.DialogText = "Delete AtlasTexture \"" + name + "\"?";
        ConfirmationDialog.Show();
    }

    /// <summary>
    /// Signal handler for the Delete button's confirmation dialog.
    /// </summary>
    private void _OnConfirmed()
    {
        undoRedo.CreateAction("QuickAtlas - Delete AtlasTexture Region", UndoRedo.MergeMode.Disable, this);
        undoRedo.AddDoMethod(this, "DeleteTextureRegion", selectedAtlasTexture.ResourcePath);
        undoRedo.AddUndoMethod(this, "AddTextureRegion", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.Region);
        undoRedo.CommitAction(true);
    }

    /// <summary>
    /// Signal handler for Delete button's confirmation dialog.
    /// </summary>
    private void _OnCancelled()
    {
        GD.Print("AtlasTexture delete cancelled");
    }

    /// <summary>
    /// Signal handler for the user pressing RETURN on the AtlasTexture's resource
    /// path/name text box. Immediately tries to rename the resource.
    /// </summary>
    /// <param name="path"></param>
    private void _OnResourcePathChangeSubmit(string path)
    {
        DoRenameAction(path);
    }

    /// <summary>
    /// Signal handler for the Filter Clip checkbox.
    /// </summary>
    /// <param name="value"></param>
    private void _OnFilterClipCheckboxToggled(bool value)
    {
        undoRedo.CreateAction("QuickAtlas - Filter Clip");
        undoRedo.AddDoMethod(this, "SetFilterClip", selectedAtlasTexture.ResourcePath, value);
        undoRedo.AddUndoMethod(this, "SetFilterClip", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.FilterClip);
        undoRedo.CommitAction();
    }

    /// <summary>
    /// Signal handler for one of the Region text boxes
    /// </summary>
    /// <param name="value"></param>
    private void _OnChangedRegionX(double value)
    {
        Rect2 newRegion = selectedAtlasTexture.Region;
        newRegion.Position = new Vector2((float)value, newRegion.Position.Y);
        DoChangeRegionAction(newRegion);
    }

    /// <summary>
    /// Signal handler for one of the Region text boxes
    /// </summary>
    /// <param name="value"></param>
    private void _OnChangedRegionY(double value)
    {
        Rect2 newRegion = selectedAtlasTexture.Region;
        newRegion.Position = new Vector2(newRegion.Position.X, (float)value);
        DoChangeRegionAction(newRegion);
    }

    /// <summary>
    /// Signal handler for one of the Region text boxes
    /// </summary>
    /// <param name="value"></param>
    private void _OnChangedRegionW(double value)
    {
        Rect2 newRegion = selectedAtlasTexture.Region;
        newRegion.Size = new Vector2((float)value, newRegion.Size.Y);
        DoChangeRegionAction(newRegion);
    }

    /// <summary>
    /// Signal handler for one of the Region text boxes
    /// </summary>
    /// <param name="value"></param>
    private void _OnChangedRegionH(double value)
    {
        Rect2 newRegion = selectedAtlasTexture.Region;
        newRegion.Size = new Vector2(newRegion.Size.X, (float)value);
        DoChangeRegionAction(newRegion);
    }

    /// <summary>
    /// Signal handler for one of the Margin text boxes
    /// </summary>
    /// <param name="value"></param>
    private void _OnChangedMarginX(double value)
    {
        Rect2 newMargin = selectedAtlasTexture.Margin;
        newMargin.Position = new Vector2((float)value, newMargin.Position.Y);
        DoChangeMarginAction(newMargin);
    }

    /// <summary>
    /// Signal handler for one of the Margin text boxes
    /// </summary>
    /// <param name="value"></param>
    private void _OnChangedMarginY(double value)
    {
        Rect2 newMargin = selectedAtlasTexture.Margin;
        newMargin.Position = new Vector2(newMargin.Position.X, (float)value);
        DoChangeMarginAction(newMargin);
    }

    /// <summary>
    /// Signal handler for one of the Margin text boxes
    /// </summary>
    /// <param name="value"></param>
    private void _OnChangedMarginW(double value)
    {
        Rect2 newMargin = selectedAtlasTexture.Margin;
        newMargin.Size = new Vector2((float)value, newMargin.Position.Y);
        DoChangeMarginAction(newMargin);
    }

    /// <summary>
    /// Signal handler for one of the Margin text boxes
    /// </summary>
    /// <param name="value"></param>
    private void _OnChangedMarginH(double value)
    {
        Rect2 newMargin = selectedAtlasTexture.Margin;
        newMargin.Size = new Vector2(newMargin.Size.X, (float)value);
        DoChangeMarginAction(newMargin);
    }

    /// <summary>
    /// Commits changes to the selected AtlasTexture's margin to the filesystem and
    /// Godot's undo history.
    /// </summary>
    /// <param name="newMargin"></param>
    private void DoChangeMarginAction(Rect2 newMargin)
    {
        undoRedo.CreateAction("QuickAtlas - Margin");
        undoRedo.AddDoMethod(this, "ChangeMargin", selectedAtlasTexture.ResourcePath, newMargin);
        undoRedo.AddUndoMethod(this, "ChangeMargin", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.Margin);
        undoRedo.CommitAction();
    }

    /// <summary>
    /// Commits a new name for the selected AtlasTexture to the filesystem and
    /// Godot's undo history
    /// </summary>
    /// <param name="newPath"></param>
    private void DoRenameAction(string newPath)
    {
        if (!newPath.EndsWith(".tres"))
        {
            GD.Print("Specified resource path without .tres extension. Adding.");
            newPath += ".tres";
        }

        string oldPath = selectedAtlasTexture.ResourcePath;
        undoRedo.CreateAction("QuickAtlas - Change resource path", UndoRedo.MergeMode.Disable, this);
        undoRedo.AddDoMethod(this, "ChangeTextureResourcePath", oldPath, newPath);
        undoRedo.AddUndoMethod(this, "ChangeTextureResourcePath", newPath, oldPath);
        undoRedo.CommitAction();
    }

    /// <summary>
    /// Walks the project's directory tree and detects all existing Texture2D resource.
    /// 
    /// From that this builds a dictionary mapping base Texture resources to any AtlasTextures that
    /// reference them as a source. This is used for creating AtlasTextureEdits objects when
    /// the user selects a Texture2D in the Filesystem dock of the Godot editor.
    /// </summary>
    private void RebuildResourceDictionary()
    {
        List<string> allResources = new List<string>();

        // walk project tree
        var fs = editorInterface.GetResourceFilesystem();
        WalkProjectResources(fs.GetFilesystem(), allResources);

        // track which AtlasTexture resources are associated with what Texture2D resources
        // provide empty reference lists for source textures that do not have associated AtlasTexture
        textureAtlasRefs = new Dictionary<string, List<string>>();
        foreach (string resourcePath in allResources)
        {
            try 
            { 
                Resource test = ResourceLoader.Load(resourcePath);
                AtlasTexture texture = test as AtlasTexture;
                if (texture != null)
                {
                    if (!textureAtlasRefs.ContainsKey(texture.Atlas.ResourcePath))
                    {
                        textureAtlasRefs[texture.Atlas.ResourcePath] = new List<string>();
                    }

                    textureAtlasRefs[texture.Atlas.ResourcePath].Add(resourcePath);
                }
                else
                {
                    // TODO: this, technically, is any resource
                    if (!textureAtlasRefs.ContainsKey(resourcePath))
                    {
                        textureAtlasRefs[resourcePath] = new List<string>();
                    }
                }
            }
            catch(System.Exception e)
            {
                GD.Print("Failed to scan ", resourcePath, " ", e.GetType().ToString(), " ", e.Message);
            }
        }
    }

    /// <summary>
    /// Recursively walk the directory tree. Any resources found are added to the output list.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="output"></param>
    private void WalkProjectResources(EditorFileSystemDirectory directory, List<string> output)
    {
        var fileCount = directory.GetFileCount();
        for (int i = 0; i < fileCount; i++)
        {
            output.Add(directory.GetFilePath(i));
        }

        var directoryCount = directory.GetSubdirCount();
        for (int i = 0; i < directoryCount; i++)
        {
            WalkProjectResources(directory.GetSubdir(i), output);
        }
    }

    /// <summary>
    /// Updates control values with current properties of the selected AtlasTexture.
    /// Use when the selection is changed, an action is undone or when a property
    /// is modified via an action done on AtlasPreviewControls.
    /// </summary>
    /// <note>
    /// Only ever use Set(thing)NoSignal in this. Only to be used in response to 
    /// an external change and not to commit modifications.
    /// </note>
    private void UpdateControlValues()
    {
        PreviewControls.QueueRedraw();
        SubTexturePreviewArea.Texture = selectedAtlasTexture?.actualTexture;
        if (selectedAtlasTexture != null)
        {
            RegionX.SetValueNoSignal(selectedAtlasTexture.Region.Position.X);
            RegionY.SetValueNoSignal(selectedAtlasTexture.Region.Position.Y);
            RegionW.SetValueNoSignal(selectedAtlasTexture.Region.Size.X);
            RegionH.SetValueNoSignal(selectedAtlasTexture.Region.Size.Y);

            MarginX.SetValueNoSignal(selectedAtlasTexture.Margin.Position.X);
            MarginY.SetValueNoSignal(selectedAtlasTexture.Margin.Position.Y);
            MarginW.SetValueNoSignal(selectedAtlasTexture.Margin.Size.X);
            MarginH.SetValueNoSignal(selectedAtlasTexture.Margin.Size.Y);

            FilterClip.SetPressedNoSignal(selectedAtlasTexture.FilterClip);

            ResourceName.Text = selectedAtlasTexture.ResourcePath;
            RegionW.MaxValue = currentBaseTexture.GetWidth() - selectedAtlasTexture.Region.Position.X;
            RegionH.MaxValue = currentBaseTexture.GetHeight() - selectedAtlasTexture.Region.Position.Y;
        }
    }
}
