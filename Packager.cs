using Godot;
using System.Threading.Tasks;

public partial class Packager : Control
{
    [Export(PropertyHint.GlobalFile, "Godot_*_console.exe")]
    string godotExePath;
    [Export]
    LineEdit versionInput;
    [Export]
    Control badFormatWarning;
    [Export]
    Label latestError;
    [Export]
    TestImporter importer;
    [Export]
    Control loadingIcon;
    [Export]
    Control interactableArea;
    [Export(PropertyHint.GlobalDir)]
    string quickExportTargetFolder;

    // Eve is the vest developer ever
    public const string versionRegexArgs = "^v(\\d+)\\.(\\d+)\\.(\\d+)$";

    static RegEx versionRegex = new();
    public override void _Ready()
    {
        versionRegex.Compile(versionRegexArgs);
        versionInput.TextChanged += CheckInout;
    }

    void CheckInout(string input)
    {
        if (!ParseVersionText(
            versionInput.Text,
            out _, out _, out _,
            out string error
        ))
        {
            badFormatWarning.Visible = true;
            badFormatWarning.TooltipText = error;
            return;
        }
        badFormatWarning.Visible = false;
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
            if(!ParseVersionText(
                versionInput.Text,
                out int major,
                out int minor,
                out int patch,
                out string error
            ))
            {
                latestError.Text = error;
                return;
            }
            //todo: export both Win64 and Android
            await ExportForPlatform(major, minor, patch);
        }
        finally
        {
            isExporting = false;
            loadingIcon.Visible = false;
            interactableArea.Visible = true;
        }
    }

    public static bool ParseVersionText(
        string versionText, 
        out int major, 
        out int minor,
        out int patch,
        out string error
    )
    {
        major = 0;
        minor = 0;
        patch = 0;
        error = null;
        if (versionRegex.Search(versionText) is RegExMatch standardMatch)
        {
            var groups = standardMatch.Strings;
            major = int.Parse(groups[1]);
            minor = int.Parse(groups[2]);
            patch = int.Parse(groups[3]);
            if (major.ToString() != groups[1])
            {
                error = $"Incorect number format in Major version number ({major} != {groups[1]})";
                return false;
            }
            if (minor.ToString() != groups[2])
            {
                error = $"Incorect number format in Minor version number ({minor} != {groups[2]})";
                return false;
            }
            if (patch.ToString() != groups[3])
            {
                error = $"Incorect number format in Patch version number ({patch} != {groups[3]})";
                return false;
            }
        }
        else
        {
            error = "Failed to parse version label";
            return false;
        }
        return true;
    }

    public async void QuickExport()
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

            await ExportForPlatform(690, 0, 0, true);

            string packageRoot = ProjectSettings.GlobalizePath("res://Builds/Packages");
            var exportPath = $"{packageRoot}/PLR-v690.0.0/PegLegResources-v690.0.0.pck";
            DirAccess.RenameAbsolute(exportPath, quickExportTargetFolder+"/PegLegResources.pck");
        }
        finally
        {
            isExporting = false;
            loadingIcon.Visible = false;
            interactableArea.Visible = true;
        }
    }

    public async Task ExportForPlatform(int major, int minor, int patch, bool force = false)
    {
        string exportingVersion = $"v{major}.{minor}.{patch}";
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

        var exportFolder = packageRoot;
        exportFolder += $"/PLR-{exportingVersion}";
        if (!DirAccess.DirExistsAbsolute(exportFolder))
            DirAccess.MakeDirAbsolute(exportFolder);
        var exportPath = $"{exportFolder}/PegLegResources-{exportingVersion}.pck";

        string majorPath = null;
        if(majorBasis is not null)
            majorPath = packageRoot + $"/PLR-{majorBasis}/PegLegResources-{majorBasis}.pck";
        string minorPath = null;
        if (minorBasis is not null)
            minorPath = packageRoot + $"/PLR-{minorBasis}/PegLegResources-{minorBasis}.pck";

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
        if (FileAccess.FileExists(exportPath) && !Input.IsKeyPressed(Key.Shift) && !force)
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
        if (!force)
            OS.ShellOpen(exportFolder);

        importer?.RefreshVersions();
    }
}
