using System.IO;
using System.Text.Json;
namespace DotNetCad.Demo;
public sealed record CadLayoutPreferences(double ConsoleHeight=180,bool ConsoleExpanded=true,double InspectorWidth=180,bool InspectorExpanded=true,bool IsConsoleVisible=true)
{
    public static string UserPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DotNetCadEngine","layout.json");
    public static CadLayoutPreferences Load(string? path=null)
    {
        try { var p=JsonSerializer.Deserialize<CadLayoutPreferences>(File.ReadAllText(path??UserPath));
            return p is not null && double.IsFinite(p.ConsoleHeight) && double.IsFinite(p.InspectorWidth)
                ? p with {ConsoleHeight=Math.Clamp(p.ConsoleHeight,64,600),InspectorWidth=Math.Clamp(p.InspectorWidth,130,600)}:new(); }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException){return new();}
    }
    public void Save(string? path=null)
    {
        path??=UserPath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);
    }
}
