using Godot;
using System.IO;
using System.Collections.Generic;
using static Godot.RenderingServer;

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
	FileDialog FileDialog;
	[Export]
	ConfirmationDialog ConfirmationDialog;
	[Export]
	AcceptDialog ErrorDialog;

	[Export]
	TextureRect SubTexturePreviewArea;
	[Export]
	SpinBox RegionX;
	[Export]
	SpinBox RegionY;
	[Export]
	SpinBox RegionW;
	[Export]
	SpinBox RegionH;
	[Export]
	LineEdit ResourceName;

	private EditorInterface editorInterface;
	private EditorUndoRedoManager undoRedo;
	private Dictionary<string, List<string>> textureAtlasRefs;

	private Texture2D currentBaseTexture;
	private string basePath;

	private bool updatingControlValues = false;
	private int newTextureCounter = 0;
	private AtlasTextureEdits selectedAtlasTexture;
	private List<AtlasTextureEdits> textureEdits;

	public string BasePath
	{
		get
		{
			return basePath;
		}
	}

	public bool HasTarget
	{
		get
		{
			return currentBaseTexture != null;
		}
	}

	public AtlasTextureEdits SelectedTexture
	{
		get
		{
			return selectedAtlasTexture;
		}

		set
		{
			if (selectedAtlasTexture == value) return;
			if (selectedAtlasTexture != null)
			{
				SaveChangesAndUpdateHistory(selectedAtlasTexture);
			}

			selectedAtlasTexture = value;
			UpdateControlValues();
		}
	}

	public void Init(EditorInterface editorInterface, EditorUndoRedoManager undoRedo)
	{
		if (this.editorInterface != null) return;

		this.editorInterface = editorInterface;
		this.undoRedo = undoRedo;

		RebuildResourceDictionary();
		editorInterface.GetResourceFilesystem().FilesystemChanged += _FilesystemChanged;
		textureEdits = new List<AtlasTextureEdits>();
	}

	public void SetEditTargetByName(string target)
	{
		if (target == "null")
		{
			SetEditTargetInternal(null);
		}
		else
		{
			SetEditTargetInternal(ResourceLoader.Load<Texture2D>(target));
		}
	}

	public void SetEditTarget(Texture2D target)
	{
		undoRedo.CreateAction("QuickAtlas - Change source atlas", UndoRedo.MergeMode.Ends, this);
		undoRedo.AddDoMethod(this, "SetEditTargetByName", target != null ? target.ResourcePath : "null");
		undoRedo.AddUndoMethod(this, "SetEditTargetByName", currentBaseTexture != null ? currentBaseTexture.ResourcePath : "null");
		undoRedo.CommitAction(false);

		SetEditTargetInternal(target);
	}

	// this one is for adding a new texture in response to a mouse click
	public AtlasTextureEdits AddNewTexture(Vector2 position)
	{
		newTextureCounter++;

		string newTextureName = string.Format("{0}/new_atlas_texture_{1}.tres", basePath, newTextureCounter);
		AtlasTextureEdits newTexture = new AtlasTextureEdits(newTextureName, new Rect2(position, Vector2.One), currentBaseTexture);
		textureEdits.Add(newTexture);
		return newTexture;
	}

	public void SaveChangesAndUpdateHistory(AtlasTextureEdits editedTexture)
	{
		bool anythingChanged = false;
		string oldPath = null, newPath = null;

		// set up undo history
		// TODO: move changing of stuff into Do/Undo methods to eliminate duplications
		if (editedTexture.IsNew)
		{
			newPath = editedTexture.ResourcePath;
			anythingChanged = true;
			undoRedo.CreateAction("QuickAtlas - Create AtlasTexture", UndoRedo.MergeMode.Disable, this);
			undoRedo.AddDoMethod(this, "AddTextureRegion", editedTexture.ResourcePath, editedTexture.Region);
			undoRedo.AddUndoMethod(this, "DeleteTextureRegion", editedTexture.ResourcePath);
			undoRedo.CommitAction(false);
		}
		else
		{
			if (editedTexture.RegionChanged)
			{
				anythingChanged = true;
				undoRedo.CreateAction("QuickAtlas - Change AtlasTexture region", UndoRedo.MergeMode.Disable, this);
				undoRedo.AddDoMethod(this, "ChangeTextureRegion", editedTexture.ResourcePath, editedTexture.Region);
				undoRedo.AddUndoMethod(this, "ChangeTextureRegion", editedTexture.ResourcePath, editedTexture.UneditedRegion);
				undoRedo.CommitAction(false);
			}

			if (editedTexture.ResourcePathChanged)
			{
				newPath = editedTexture.ResourcePath;
				oldPath = editedTexture.UneditedResourcePath;
				anythingChanged = true;

				undoRedo.CreateAction("QuickAtlas - Change AtlasTexture resource path", UndoRedo.MergeMode.Disable, this);
				undoRedo.AddDoMethod(this, "ChangeTextureResourcePath", editedTexture.ResourcePath, editedTexture.ResourcePath);
				undoRedo.AddUndoMethod(this, "ChangeTextureResourcePath", editedTexture.ResourcePath, editedTexture.UneditedResourcePath);
				undoRedo.CommitAction(false);
			}
		}

		// save changes
		if (anythingChanged)
		{
			editedTexture.SaveResourceFile();

			// update internal references only after we know the file saved successfully
			if(oldPath!= null)
			{
				textureAtlasRefs[currentBaseTexture.ResourcePath].Remove(oldPath);
			}

			if (newPath != null)
			{
				textureAtlasRefs[currentBaseTexture.ResourcePath].Add(newPath);
				editorInterface.GetResourceFilesystem().Scan();
			}

			UpdateControlValues();
		}
	}

	// this one is for adding a new texture to undo a delete
	public void AddTextureRegion(string newResourcePath, Rect2 newRegion)
	{
		GD.Print("(Re)create AtlasTexture ", newResourcePath, newRegion);
		for (int i = 0; i < textureEdits.Count; i++)
		{
			if (textureEdits[i].ResourcePath == newResourcePath)
			{
				throw new System.Exception("Creating AtlasTexture that already exists via undo/redo!");
			}
		}

		AtlasTextureEdits newTexture = new AtlasTextureEdits(newResourcePath, newRegion, currentBaseTexture);
		try
		{
			newTexture.SaveResourceFile();

			textureEdits.Add(newTexture);
			textureAtlasRefs[currentBaseTexture.ResourcePath].Add(newTexture.ResourcePath);
			selectedAtlasTexture = newTexture;
			UpdateControlValues();
		}
		catch (System.IO.IOException error)
		{
			ErrorDialog.DialogText = error.Message;
			ErrorDialog.Show();
		}

		PreviewControls.QueueRedraw();
	}

	public void DeleteTextureRegion(string newResourcePath)
	{
		GD.Print("Delete AtlasTexture ", newResourcePath);
		for (int i = 0; i < textureEdits.Count; i++)
		{
			if (textureEdits[i].ResourcePath == newResourcePath)
			{
				File.Delete(ProjectSettings.GlobalizePath(selectedAtlasTexture.ResourcePath));
				textureAtlasRefs[currentBaseTexture.ResourcePath].Remove(selectedAtlasTexture.ResourcePath);
				textureEdits.Remove(selectedAtlasTexture);
				selectedAtlasTexture = null;
				UpdateControlValues();
				editorInterface.GetResourceFilesystem().Scan();
				PreviewControls.QueueRedraw();
				return;
			}
		}

		GD.Print("Deleting AtlasTexture that was already deleted");
	}

	public void ChangeTextureRegion(string resourcePath, Rect2 newRegion)
	{
		// this code is awful!
		// there's multiple places that do this operation and no good lookup for AtlasTexture here since it's in the preview controls
		// moving the lookup here probably would make sense but I don't feel like doing that yet
		GD.Print("Do/undo change region for ", resourcePath);
		for (int i = 0; i < textureEdits.Count; i++)
		{
			if (textureEdits[i].ResourcePath == resourcePath)
			{
				GD.Print(selectedAtlasTexture == textureEdits[i] ? "undo on selected" : "undo on other");
				GD.Print("  from ", textureEdits[i].Region, " to ", newRegion);
				textureEdits[i].Region = newRegion;
				textureEdits[i].SaveResourceFile();
				UpdateControlValues();
				PreviewControls.QueueRedraw();
				return;
			}
		}

		GD.Print("Cannot find AtlasTexture, probably associated with different parent than selected ", resourcePath);
	}

	public void ChangeTextureResourcePath(string resourcePath, string newResourcePath)
	{
		if (resourcePath != newResourcePath)
		{
			GD.Print("Do/undo change path for ", resourcePath);
			for (int i = 0; i < textureEdits.Count; i++)
			{
				if (textureEdits[i].ResourcePath == resourcePath)
				{
					GD.Print(selectedAtlasTexture == textureEdits[i] ? "undo on selected" : "undo on other");
					GD.Print("  from ", resourcePath, " to ", newResourcePath);
					textureEdits[i].ResourcePath = newResourcePath;

					try
					{
						textureEdits[i].SaveResourceFile();
						
						UpdateControlValues();
						PreviewControls.QueueRedraw();
						editorInterface.GetResourceFilesystem().Scan();
					}
					catch (System.IO.IOException error)
					{
						ErrorDialog.DialogText = error.Message;
						ErrorDialog.Show();

						textureEdits[i].ResourcePath = textureEdits[i].UneditedResourcePath;
					}

					return;
				}
			}
		}

		GD.Print("Cannot find AtlasTexture, probably associated with different parent than selected ", resourcePath);
	}

	private void _FilesystemChanged()
	{
		// TODO: ignore when triggered by own save actions
		GD.Print("File system state changed. Rebuilding AtlasTexture dictionary");
		foreach (AtlasTextureEdits edits in textureEdits)
		{
			edits.ResourcePath = edits.UneditedResourcePath;
		}

		RebuildResourceDictionary();
		UpdateControlValues();
	}

	private void _OnRenamePressed()
	{
		FileDialog.CurrentPath = selectedAtlasTexture.ResourcePath;
		FileDialog.Show();
	}

	private void _OnFileDialogSelected(string path)
	{
		RenameSelectedAtlasTexture(path);
	}

	private void _OnDeletePressed()
	{
		ConfirmationDialog.DialogText = "Are you sure?";
		ConfirmationDialog.Show();
	}

	private void _OnConfirmed()
	{
		undoRedo.CreateAction("QuickAtlas - Delete AtlasTexture", UndoRedo.MergeMode.Disable, this);
		undoRedo.AddDoMethod(this, "DeleteTextureRegion", selectedAtlasTexture.ResourcePath);
		undoRedo.AddUndoMethod(this, "AddTextureRegion", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.Region);
		undoRedo.CommitAction(true);
	}

	private void _OnCancelled()
	{
		GD.Print("AtlasTexture delete cancelled");
	}

	private void _OnResourcePathChanged(string path)
	{
		selectedAtlasTexture.ResourcePath = path;
	}

	private void _OnResourcePathChangeSubmit(string path)
	{
		RenameSelectedAtlasTexture(path);
	}

	private void _OnChangedX(double value)
	{
		if (updatingControlValues) return;

		selectedAtlasTexture.Position = new Vector2((float)value, selectedAtlasTexture.Position.Y);
		PreviewControls.QueueRedraw();
		SaveChangesAndUpdateHistory(selectedAtlasTexture);
	}

	private void _OnChangedY(double value)
	{
		if (updatingControlValues) return;
		
		selectedAtlasTexture.Position = new Vector2(selectedAtlasTexture.Position.X, (float)value);
		PreviewControls.QueueRedraw();
		SaveChangesAndUpdateHistory(selectedAtlasTexture);
	}

	private void _OnChangedW(double value)
	{
		if (updatingControlValues) return;
		
		selectedAtlasTexture.Size = new Vector2((float)value, selectedAtlasTexture.Size.Y);
		PreviewControls.QueueRedraw();
		SaveChangesAndUpdateHistory(selectedAtlasTexture);
	}

	private void _OnChangedH(double value)
	{
		if (updatingControlValues) return;
		
		selectedAtlasTexture.Size = new Vector2(selectedAtlasTexture.Size.X, (float)value);
		PreviewControls.QueueRedraw();
		SaveChangesAndUpdateHistory(selectedAtlasTexture);
	}

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
			if (resourcePath.EndsWith(".tres"))
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
        }
    }

	private void WalkProjectResources(EditorFileSystemDirectory directory, List<string> output)
	{
		var c = directory.GetFileCount();
		for (int i = 0; i < c; i++)
		{
			output.Add(directory.GetFilePath(i));
		}

		var d = directory.GetSubdirCount();
		for (int i = 0; i < d; i++)
		{
			WalkProjectResources(directory.GetSubdir(i), output);
		}
	}

	private void SetEditTargetInternal(Texture2D target)
	{
		// grab atlastexture paths from texture path
		currentBaseTexture = target;
		TexturePreviewArea.Texture = target;

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

		PreviewControls.SetAtlasSource(textureEdits);
		if (target != null)
		{
			basePath = target.ResourcePath.Substr(0, target.ResourcePath.LastIndexOf('/'));
			RegionX.MaxValue = currentBaseTexture.GetWidth();
			RegionY.MaxValue = currentBaseTexture.GetHeight();
			RegionW.MaxValue = currentBaseTexture.GetWidth();
			RegionH.MaxValue = currentBaseTexture.GetHeight();
			NoTargetOverlay.Visible = false;
		}
		else
		{
			NoTargetOverlay.Visible = true;
		}
	}

	private void RenameSelectedAtlasTexture(string path)
	{
		if (!path.EndsWith(".tres"))
		{
			GD.Print("Specified resource path without .tres extension. Adding.");
			path += ".tres";
		}

		ResourceName.Text = path;
		selectedAtlasTexture.ResourcePath = path;

		try
		{
			SaveChangesAndUpdateHistory(selectedAtlasTexture);
		}
		catch (System.IO.IOException error)
		{
			ErrorDialog.DialogText = error.Message;
			ErrorDialog.Show();

			selectedAtlasTexture.ResourcePath = selectedAtlasTexture.UneditedResourcePath;
			ResourceName.Text = selectedAtlasTexture.UneditedResourcePath;
		}
	}

	private void UpdateControlValues()
	{
		SubTexturePreviewArea.Texture = selectedAtlasTexture?.actualTexture;
		if (selectedAtlasTexture != null)
		{
			updatingControlValues = true;
			RegionX.Value = selectedAtlasTexture.Region.Position.X;
			RegionY.Value = selectedAtlasTexture.Region.Position.Y;
			RegionW.Value = selectedAtlasTexture.Region.Size.X;
			RegionH.Value = selectedAtlasTexture.Region.Size.Y;

			ResourceName.Text = selectedAtlasTexture.ResourcePath;
			RegionW.MaxValue = currentBaseTexture.GetWidth() - selectedAtlasTexture.Region.Position.X;
			RegionH.MaxValue = currentBaseTexture.GetHeight() - selectedAtlasTexture.Region.Position.Y;
			updatingControlValues = false;
		}
	}
}



