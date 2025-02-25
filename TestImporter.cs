using Godot;

public partial class TestImporter : Node
{
	[Export]
	string test;
    [Export]
    string test2;
    [Export]
    string testAsset;
    [Export]
	TextureRect testResult;

	public void Import()
	{
		string packPath = GetPckPath(test);
        string packPath2 = GetPckPath(test2);

        GD.Print(ProjectSettings.LoadResourcePack(packPath) + $" ({test})");
        GD.Print(ProjectSettings.LoadResourcePack(packPath2) + $" ({test2})");
        testResult.Texture = ResourceLoader.Load<Texture2D>(testAsset, "Texture2D");
	}

	static string GetPckPath(string pckName)
    {
        if (OS.HasFeature("editor"))
        {
           return "res://Builds/Packages/" + pckName;
        }
        else
        {
            return string.Join("/", OS.GetExecutablePath().Split('/')[..^2]) + "/Packages/" + pckName;
        }
    }
}
