using Godot;
using System.Threading.Tasks;

public partial class Packager : Control
{
    [Export(PropertyHint.GlobalFile, "Godot_*_console.exe")]
    string godotExePath;
    [Export]
    LineEdit versionInput;
    [Export]
    Label latestError;
    [Export]
    TestImporter importer;
    [Export]
    Control loadingIcon;
    [Export]
    Control interactableArea;

    RegEx versionRegex= new();
    public override void _Ready()
    {
        versionRegex.Compile("^v(\\d+)\\.(\\d+)\\.(\\d+)([a-z_]*)$");
    }

    bool isExporting = false;
    public async void ExportPackage()
	{
        if (isExporting)
            return;
        try
        {
            isExporting = true;
            loadingIcon.Visible = true;
            interactableArea.Visible = false;

            if (!FileAccess.FileExists(godotExePath))
            {
                latestError.Text = "Godot executable not provided";
                return;
            }
            var versionText = versionInput.Text;
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
                {
                    latestError.Text = $"Incorect number format in Major version number ({major} != {groups[1]})";
                    return;
                }
                if (minor.ToString() != groups[2])
                {
                    latestError.Text = $"Incorect number format in Minor version number ({minor} != {groups[2]})";
                    return;
                }
                if (patch.ToString() != groups[3])
                {
                    latestError.Text = $"Incorect number format in Patch version number ({patch} != {groups[3]})";
                    return;
                }
                if (!string.IsNullOrWhiteSpace(groups[4]))
                    prereleaseMarker = groups[4];
            }
            else
            {
                latestError.Text = "Failed to parse version label";
                return;
            }
            //todo: export both Win64 and Android
            await ExportForPlatform("Win64", major, minor, patch, prereleaseMarker);
        }
        finally
        {
            isExporting = false;
            loadingIcon.Visible = false;
            interactableArea.Visible = true;
        }
    }

    public async Task ExportForPlatform(string platform, int major, int minor, int patch, string prereleaseMarker=null)
    {
        string exportingVersion = $"v{major}.{minor}.{patch}{prereleaseMarker}";
        GD.Print("exporting: "+exportingVersion);
        string majorBasis = null;
        if (patch > 0 || minor > 0)
            majorBasis = $"v{major}.0.0";
        string minorBasis = null;
        if (patch > 0 && minor > 0)
            minorBasis = $"v{major}.{minor}.0";
        GD.Print("major basis: " + majorBasis);
        GD.Print("minor basis: " + minorBasis);

        string packageRoot = ProjectSettings.GlobalizePath("res://Builds/Packages");

        var exportPath = packageRoot;
        exportPath += $"/PLR-{exportingVersion}";
        if (!DirAccess.DirExistsAbsolute(exportPath))
            DirAccess.MakeDirAbsolute(exportPath);
        exportPath += $"/PegLegResources-{exportingVersion}-{platform}.pck";

        string majorPath = null;
        if(majorBasis is not null)
            majorPath = packageRoot + $"/PLR-{majorBasis}/PegLegResources-{majorBasis}-{platform}.pck";
        string minorPath = null;
        if (minorBasis is not null)
            minorPath = packageRoot + $"/PLR-{minorBasis}/PegLegResources-{minorBasis}-{platform}.pck";

        if (majorPath is not null && !FileAccess.FileExists(majorPath))
        {
            latestError.Text = $"Basis package {majorBasis} does not exist\n{majorPath}";
            return;
        }
        if (minorPath is not null && !FileAccess.FileExists(minorPath))
        {
            latestError.Text = $"Basis package {minorPath} does not exist\n{minorPath}";
            return;
        }
        if (FileAccess.FileExists(exportPath) && !Input.IsKeyPressed(Key.Shift))
        {
            latestError.Text = "Package exists, hold Shift to force replace";
            return;
        }

        var patches = $"{majorPath}{(minorPath is null ? "" : $",{minorPath}")}";
        if (majorPath is null)
            GD.Print("Exporting full pack...");
        else
            GD.Print($"Exporting as patch... ({patches})");

        await Task.Run(() =>
        {
            if (majorPath is null)
                OS.Execute(godotExePath, ["--headless", "--export-pack", "Package", exportPath]);
            else
                OS.Execute(godotExePath, ["--headless", "--export-patch", "Package", exportPath, "--patches", patches]);
        });

        GD.Print("Export complete");

        importer?.RefreshVersions();
    }
}
