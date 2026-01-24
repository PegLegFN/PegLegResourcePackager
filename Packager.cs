using Godot;
using Godot.Collections;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Godot.HttpRequest;

public partial class Packager : Control
{
    [Export(PropertyHint.GlobalFile, "Godot_*_console.exe")]
    string godotExePath;
    [Export]
    Label latestVersionLabel;
    [Export]
    TabBar newVersionType;
    [Export]
    LineEdit versionInput;
    [Export]
    Control customVersionContent;
    [Export]
    Control newVersionContent;
    [Export]
    Label newVersionLabel;
    [Export]
    OptionButton platformSelector;
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

    VersionData latestVer;

    public override void _Ready()
    {
        var existingVersions = DirAccess.GetDirectoriesAt(VersionData.BaseExportFolder);
        latestVer = default;
        foreach (var vText in existingVersions)
        {
            if (VersionData.TryParse(vText, out var vData) && vData > latestVer && vData.major < 500)
                latestVer = vData;
        }
        latestVersionLabel.Text = latestVer.ToString();
        versionInput.TextChanged += CheckInput;
        newVersionType.TabChanged += SetNewVersionType;
        SetNewVersionType(newVersionType.CurrentTab);
    }

    VersionData newVer;
    void SetNewVersionType(long tab)
    {
        newVer = tab switch
        {
            0 => new(latestVer.major + 1),
            1 => new(latestVer.major, latestVer.minor + 1),
            _ => new(latestVer.major, latestVer.minor, latestVer.patch + 1),
        };
        bool custom = tab > 2;
        newVersionContent.Visible = !custom;
        customVersionContent.Visible = custom;
        if (custom)
            CheckInput(versionInput.Text);
        newVersionLabel.Text = newVer.ToString();
    }

    void CheckInput(string input)
    {
        if (!VersionData.TryParse(input, out var version, out string error))
        {
            badFormatWarning.Visible = true;
            badFormatWarning.TooltipText = error;
            newVer = latestVer with { patch = latestVer.patch + 1 };
            return;
        }
        newVer = version;
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
            if (platformSelector.Selected == 0)
            {
                for (int i = 2; i < platformSelector.ItemCount; i++)
                {
                    await ExportForPlatform(newVer, platformSelector.GetItemText(i));
                }
            }
            else
            {
                await ExportForPlatform(newVer, platformSelector.GetItemText(platformSelector.Selected));
            }
        }
        finally
        {
            isExporting = false;
            loadingIcon.Visible = false;
            interactableArea.Visible = true;
        }
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
            VersionData version = new(690, 0, 0);
            await ExportForPlatform(new(690, 0, 0), "Windows", true);

            string packageRoot = ProjectSettings.GlobalizePath("res://Builds/Packages");
            var exportPath = $"{packageRoot}/{version}/PegLegResources-{version}.pck";
            DirAccess.RenameAbsolute(exportPath, quickExportTargetFolder+"/PegLegResourcePacks/ExtraPatch.pck");
        }
        finally
        {
            isExporting = false;
            loadingIcon.Visible = false;
            interactableArea.Visible = true;
        }
    }

    public async Task ExportForPlatform(VersionData version, string platform, bool force = false)
    {
        GD.Print($"exporting {version} for {platform}");
        var majorBasis = version.MajorBasis;
        var minorBasis = version.MinorBasis;
        GD.Print("major basis: " + majorBasis ?? "none");
        GD.Print("minor basis: " + minorBasis ?? "none");

        string packageRoot = ProjectSettings.GlobalizePath("res://Builds/Packages");

        if (!DirAccess.DirExistsAbsolute(version.ExportFolder))
            DirAccess.MakeDirAbsolute(version.ExportFolder);

        string majorPath = majorBasis?.PackagePath(platform);
        string minorPath = minorBasis?.PackagePath(platform);
        string exportPath = version.PackagePath(platform);

        if (majorPath is not null && !FileAccess.FileExists(majorPath))
        {
            latestError.Text = $"Basis package {majorBasis} does not exist for the {platform} platform\n{majorPath}";
            return;
        }
        if (minorPath is not null && !FileAccess.FileExists(minorPath))
        {
            latestError.Text = $"Basis package {minorPath} does not exist for the {platform} platform\n{minorPath}";
            return;
        }
        if (FileAccess.FileExists(exportPath) && !Input.IsKeyPressed(Key.Shift) && !force)
        {
            latestError.Text = "Package exists, hold Shift to force replace";
            return;
        }

        var patches = $"{majorPath}{(minorPath is null ? "" : $", {minorPath}")}";
        var patchNames = $"{majorBasis}{(minorBasis is null ? "" : $", {minorBasis}")}";
        if (majorPath is null)
            GD.Print("Exporting full pack...");
        else
            GD.Print($"Exporting as patch... ({patchNames})");

        GD.Print(exportPath);
        int result = -100;
        Godot.Collections.Array output = [];
        await Task.Run(() =>
        {
            if (majorPath is null)
                result = OS.Execute(godotExePath, ["--headless", "--export-pack", platform, exportPath], output);
            else
                result = OS.Execute(godotExePath, ["--headless", "--export-patch", platform, exportPath, "--patches", patches], output);
        });

        //GD.Print(string.Join("\n", output));

        GD.Print("Export complete");
        if (!force)
            OS.ShellOpen(version.ExportFolder);

        importer?.RefreshVersions();
    }
}


public partial record struct VersionData(int major, int minor = 0, int patch = 0) : IComparable<VersionData>
{
    [GeneratedRegex(@"^v(\d+)\.(\d+)\.(\d+)$")]
    private static partial Regex VersionRegex();

    static readonly Dictionary<string, string> PlatformMarkers = new()
    {
        ["Desktop"] = "",
        ["Mobile"] = "-m",
    };

    public static string BaseExportFolder => ProjectSettings.GlobalizePath("res://Builds/Packages");
    public static bool TryParse(string input, out VersionData version) => TryParse(input, out version, out _);
    public static bool TryParse(string input, out VersionData version, out string error)
    {
        version = default;
        error = null;

        var match = VersionRegex().Match(input);
        if (!match.Success)
        {
            error = "Failed to parse version";
            return false;
        }

        var groups = match.Groups;
        var newVersion = version;

        newVersion.major = int.Parse(groups[1].Value);
        newVersion.minor = int.Parse(groups[2].Value);
        newVersion.patch = int.Parse(groups[3].Value);

        if (newVersion.major.ToString() != groups[1].Value)
        {
            error = $"Incorect number format in Major version number ({newVersion.major} != {groups[1]})";
            return false;
        }
        if (newVersion.minor.ToString() != groups[2].Value)
        {
            error = $"Incorect number format in Minor version number ({newVersion.minor} != {groups[2]})";
            return false;
        }
        if (newVersion.patch.ToString() != groups[3].Value)
        {
            error = $"Incorect number format in Patch version number ({newVersion.patch} != {groups[3]})";
            return false;
        }

        version = newVersion;
        return true;
    }

    public static bool operator >(VersionData left, VersionData right) => left.CompareTo(right) > 0;
    public static bool operator <(VersionData left, VersionData right) => left.CompareTo(right) < 0;
    public static bool operator >=(VersionData left, VersionData right) => left.CompareTo(right) >= 0;
    public static bool operator <=(VersionData left, VersionData right) => left.CompareTo(right) <= 0;

    public int CompareTo(VersionData other)
    {
        if (major != other.major)
            return major.CompareTo(other.major);
        if (minor != other.minor)
            return minor.CompareTo(other.minor);
        return patch.CompareTo(other.patch);
    }

    public VersionData? MajorBasis => minor > 0 || patch > 0 ? new(major, 0, 0) : null;
    public VersionData? MinorBasis => minor > 0 && patch > 0 ? new(major, minor, 0) : null;

    public string ExportFolder => $"{BaseExportFolder}/{this}";
    public string PackagePath(string platform) => $"{ExportFolder}/PegLegResources{PlatformMarkers[platform]}-{this}.pck";

    public override string ToString() => $"v{major}.{minor}.{patch}";
}
