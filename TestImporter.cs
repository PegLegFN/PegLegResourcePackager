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

    RegEx versionRegex = new();
    public override void _Ready()
    {
        RefreshVersions();
        versionRegex.Compile(Packager.versionRegexArgs);
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

    string GetPackageFromVersion(int major, int minor, int patch, string prereleaseType = null, int prereleaseNumber = 0)
    {
        string prereleaseAddon = "";
        if (prereleaseType is not null)
            prereleaseAddon = $"-{prereleaseType}{(prereleaseNumber < 10 ? "0" : "")}{prereleaseNumber}";
        var folder = GetPckPath($"PLR-v{major}.{minor}.{patch}{prereleaseAddon}");
        return folder+"/"+ DirAccess.GetFilesAt(folder)[0];
    }

    public void Import()
	{
        if (versionSelector.Selected < 0)
            return;
        var versionText = versionSelector.GetItemText(versionSelector.Selected);
        int major = 0;
        int minor = 0;
        int patch = 0;
        string prereleaseMarker = null;
        if (versionRegex.Search(versionText) is RegExMatch standardMatch)
        {
            var groups = standardMatch.Strings;
            major = int.Parse(groups[1]);
            minor = int.Parse(groups[2]);
            patch = int.Parse(groups[3]);
            if (major.ToString() != groups[1])
                return;
            if (minor.ToString() != groups[2])
                return;
            if (patch.ToString() != groups[3])
                return;
            if (!string.IsNullOrWhiteSpace(groups[4]))
                prereleaseMarker = groups[4];
        }
        
        GD.Print("Loading "+ GetPackageFromVersion(major, 0, 0));
        ProjectSettings.LoadResourcePack(GetPackageFromVersion(major, 0, 0));
        if (minor > 0)
        {
            GD.Print("Loading " + GetPackageFromVersion(major, minor, 0));
            ProjectSettings.LoadResourcePack(GetPackageFromVersion(major, minor, 0));
        }
        if (patch > 0)
        {
            GD.Print("Loading " + GetPackageFromVersion(major, minor, patch));
            ProjectSettings.LoadResourcePack(GetPackageFromVersion(major, minor, patch));
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
