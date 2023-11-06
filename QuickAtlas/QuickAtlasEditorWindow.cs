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
		AtlasTexture targetAsAtlas = target as AtlasTexture;
		if (targetAsAtlas != null)
		{
			target = targetAsAtlas.Atlas;
		}

		if (target != currentBaseTexture)
		{
			undoRedo.CreateAction("QuickAtlas - Change source atlas", UndoRedo.MergeMode.Ends, this);
			undoRedo.AddDoMethod(this, "SetEditTargetByName", target != null ? target.ResourcePath : "null");
			undoRedo.AddUndoMethod(this, "SetEditTargetByName", currentBaseTexture != null ? currentBaseTexture.ResourcePath : "null");
			undoRedo.CommitAction(false);

			SetEditTargetInternal(target);
		}

		if (targetAsAtlas != null)
		{ 
			for (int i = 0; i < textureEdits.Count; i++)
			{
				if (textureEdits[i].actualTexture.ResourcePath == targetAsAtlas.ResourcePath)
				{
					SelectedTexture = textureEdits[i];

					// center the selection in the preview area
					Vector2 previewCenter = PreviewContainer.Size * 0.5f;
					Vector2 halfSize = SelectedTexture.Region.Size * 0.5f;
					Vector2 scrollPos = SelectedTexture.Region.Position - (previewCenter - halfSize);
					PreviewContainer.ScrollHorizontal = (int)scrollPos.X;
					PreviewContainer.ScrollVertical = (int)scrollPos.Y;
					return;
				}
			}
		
			GD.Print("Unable to find edit info for " + targetAsAtlas.ResourcePath);
		}
	}

	// this one does initial setup for a new texture but does not commit it to the filesystem
	public AtlasTextureEdits StartNewTexture(Vector2 position)
	{
		newTextureCounter++;

		string newTextureName = string.Format("{0}/new_atlas_texture_{1}.tres", basePath, newTextureCounter);
		AtlasTextureEdits newTexture = new AtlasTextureEdits(newTextureName, new Rect2(position, Vector2.One), currentBaseTexture);
		textureEdits.Add(newTexture);
		return newTexture;
	}

	public void DoAddNewTextureAction(AtlasTextureEdits editedTexture)
	{
		undoRedo.CreateAction("QuickAtlas - Create AtlasTexture Region");
		undoRedo.AddDoMethod(this, "AddTextureRegion", editedTexture.ResourcePath, editedTexture.Region);
		undoRedo.AddUndoMethod(this, "DeleteTextureRegion", editedTexture.ResourcePath);
		undoRedo.CommitAction();
	}

	public void DoChangeRegionAction(Rect2 newRegion)
	{
		undoRedo.CreateAction("QuickAtlas - Region");
		undoRedo.AddDoMethod(this, "ChangeRegion", selectedAtlasTexture.ResourcePath, newRegion);
		undoRedo.AddUndoMethod(this, "ChangeRegion", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.Region);
		undoRedo.CommitAction();
	}

	// this commits a new texture to the filesystem and is called by undo/redo actions
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
				return;
			}
		}

		GD.Print("Deleting AtlasTexture that was already deleted");
	}

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

	private void _FilesystemChanged()
	{
		GD.Print("File system state changed. Rebuilding AtlasTexture dictionary");
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
		DoRenameAction(path);
	}

	private void _OnDeletePressed()
	{
		string name = SelectedTexture.ResourcePath.Substring(SelectedTexture.ResourcePath.LastIndexOf("/") + 1);
		ConfirmationDialog.DialogText = "Delete AtlasTexture \"" + name + "\"?";
		ConfirmationDialog.Show();
	}

	private void _OnConfirmed()
	{
		undoRedo.CreateAction("QuickAtlas - Delete AtlasTexture Region", UndoRedo.MergeMode.Disable, this);
		undoRedo.AddDoMethod(this, "DeleteTextureRegion", selectedAtlasTexture.ResourcePath);
		undoRedo.AddUndoMethod(this, "AddTextureRegion", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.Region);
		undoRedo.CommitAction(true);
	}

	private void _OnCancelled()
	{
		GD.Print("AtlasTexture delete cancelled");
	}

	private void _OnResourcePathChangeSubmit(string path)
	{
		DoRenameAction(path);
	}

	private void _OnFilterClipCheckboxToggled(bool value)
	{
		undoRedo.CreateAction("QuickAtlas - Filter Clip");
		undoRedo.AddDoMethod(this, "SetFilterClip", selectedAtlasTexture.ResourcePath, value);
		undoRedo.AddUndoMethod(this, "SetFilterClip", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.FilterClip);
		undoRedo.CommitAction();
	}

	private void _OnChangedRegionX(double value)
	{
		Rect2 newRegion = selectedAtlasTexture.Region;
		newRegion.Position = new Vector2((float)value, newRegion.Position.Y);
		DoChangeRegionAction(newRegion);
	}

	private void _OnChangedRegionY(double value)
	{
		Rect2 newRegion = selectedAtlasTexture.Region;
		newRegion.Position = new Vector2(newRegion.Position.X, (float)value);
		DoChangeRegionAction(newRegion);
	}

	private void _OnChangedRegionW(double value)
	{
		Rect2 newRegion = selectedAtlasTexture.Region;
		newRegion.Size = new Vector2((float)value, newRegion.Size.Y);
		DoChangeRegionAction(newRegion);
	}

	private void _OnChangedRegionH(double value)
	{
		Rect2 newRegion = selectedAtlasTexture.Region;
		newRegion.Size = new Vector2(newRegion.Size.X, (float)value);
		DoChangeRegionAction(newRegion);
	}

	private void _OnChangedMarginX(double value)
	{
		Rect2 newMargin = selectedAtlasTexture.Margin;
		newMargin.Position = new Vector2((float)value, newMargin.Position.Y);
		DoChangeMarginAction(newMargin);
	}

	private void _OnChangedMarginY(double value)
	{
		Rect2 newMargin = selectedAtlasTexture.Margin;
		newMargin.Position = new Vector2(newMargin.Position.X, (float)value);
		DoChangeMarginAction(newMargin);
	}

	private void _OnChangedMarginW(double value)
	{
		Rect2 newMargin = selectedAtlasTexture.Margin;
		newMargin.Size = new Vector2((float)value, newMargin.Position.Y);
		DoChangeMarginAction(newMargin);
	}

	private void _OnChangedMarginH(double value)
	{
		Rect2 newMargin = selectedAtlasTexture.Margin;
		newMargin.Size = new Vector2(newMargin.Size.X, (float)value);
		DoChangeMarginAction(newMargin);
	}

	private void DoChangeMarginAction(Rect2 newMargin)
	{
		undoRedo.CreateAction("QuickAtlas - Margin");
		undoRedo.AddDoMethod(this, "ChangeMargin", selectedAtlasTexture.ResourcePath, newMargin);
		undoRedo.AddUndoMethod(this, "ChangeMargin", selectedAtlasTexture.ResourcePath, selectedAtlasTexture.Margin);
		undoRedo.CommitAction();
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
