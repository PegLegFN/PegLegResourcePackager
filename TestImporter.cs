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
        var packageDirectories = DirAccess.GetDirectoriesAt(GetPckPath());
        foreach (var packageDir in packageDirectories)
        {
            versionSelector.AddItem(packageDir.Split("/")[^1][4..]);
        }
    }

    public void SetFromFilePath(string filepath)
    {
        if (!filepath.Contains("/PegLegResources/"))
            return;
        testPath.Text = filepath.Split("/PegLegResources/")[1];
        GetAsset();
    }

    string GetPackageFromVersion(int major, int minor, int patch)
    {
        var folder = GetPckPath($"PLR-v{major}.{minor}.{patch}");
        return folder+"/"+ DirAccess.GetFilesAt(folder)[0];
    }

    public void Import()
	{
        if (versionSelector.Selected < 0)
            return;
        var versionText = versionSelector.GetItemText(versionSelector.Selected);
        if (!Packager.ParseVersionText(
                versionText,
                out int major,
                out int minor,
                out int patch,
                out string error
            ))
        {
            GD.Print("Err: " + error);
            return;
        }

        var majorpck = GetPackageFromVersion(major, 0, 0);
        GD.Print("Loading "+ majorpck);
        ProjectSettings.LoadResourcePack(majorpck);
        if (minor > 0)
        {
            var minorpck = GetPackageFromVersion(major, minor, 0);
            GD.Print("Loading " + minorpck);
            ProjectSettings.LoadResourcePack(minorpck);
        }
        if (patch > 0)
        {
            var patchpck = GetPackageFromVersion(major, minor, patch);
            GD.Print("Loading " + patchpck);
            ProjectSettings.LoadResourcePack(patchpck);
        }

        viewerRegion.Visible = true;
        GetAsset();
	}

    public void GetAsset()
    {
        bool exists = ResourceLoader.Exists("res://PegLegResources/" + testPath.Text);
        testResult.Texture = exists ? ResourceLoader.Load<Texture2D>("res://PegLegResources/" + testPath.Text, cacheMode: ResourceLoader.CacheMode.Replace) : fallback;
        testResult2.Texture = testResult.Texture;
    }

	static string GetPckPath(string pckName = "")
    {
        if (OS.HasFeature("editor"))
        {
           return "res://Builds/Packages/" + pckName;
        }
        else
        {
            //pegleg will load packages from userdata folder
            return string.Join("/", OS.GetExecutablePath().Split('/')[..^2]) + "/Packages/" + pckName;
        }
    }
}
