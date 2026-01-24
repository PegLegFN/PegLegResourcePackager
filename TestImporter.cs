using Godot;

public partial class TestImporter : Control
{
    [Export]
    OptionButton versionSelector;
    [Export]
    LineEdit testPath;
    [Export]
	TextureRect testResult;
    [Export]
    TextureRect testResult2;
    [Export]
    Texture2D fallback;
    [Export]
    Control viewerRegion;

    public override void _Ready()
    {
        RefreshVersions();
        testPath.TextSubmitted += _ => GetAsset();
    }

    public void RefreshVersions()
    {
        versionSelector.Clear();
        var packageDirectories = DirAccess.GetDirectoriesAt(VersionData.BaseExportFolder);
        foreach (var packageDir in packageDirectories)
        {
            versionSelector.AddItem(packageDir);
        }
    }

    public void SetFromFilePath(string filepath)
    {
        if (!filepath.Contains("/PegLegResources/"))
            return;
        testPath.Text = filepath.Split("/PegLegResources/")[1];
        GetAsset();
    }

    public void Import()
	{
        if (versionSelector.Selected < 0)
            return;
        var versionText = versionSelector.GetItemText(versionSelector.Selected);
        if (!VersionData.TryParse(versionText, out var version, out string error))
        {
            GD.Print("Err: " + error);
            return;
        }

        if(version.MajorBasis is VersionData majorVer)
        {
            GD.Print($"Loading {majorVer}");
            ProjectSettings.LoadResourcePack(majorVer.PackagePath("Windows"));
        }
        if (version.MinorBasis is VersionData minorVer)
        {
            GD.Print($"Loading {minorVer}");
            ProjectSettings.LoadResourcePack(minorVer.PackagePath("Windows"));
        }
        GD.Print($"Loading {version}");
        ProjectSettings.LoadResourcePack(version.PackagePath("Windows"));

        viewerRegion.Visible = true;
        GetAsset();
	}

    public void GetAsset()
    {
        bool exists = ResourceLoader.Exists("res://PegLegResources/" + testPath.Text);
        testResult.Texture = exists ? ResourceLoader.Load<Texture2D>("res://PegLegResources/" + testPath.Text, cacheMode: ResourceLoader.CacheMode.Replace) : fallback;
        testResult2.Texture = testResult.Texture;
    }
}
