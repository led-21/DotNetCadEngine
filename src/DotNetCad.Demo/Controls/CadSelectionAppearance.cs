using System.Windows.Media;
namespace DotNetCad.Demo.Controls;

/// <summary>Uniform yellow selection accent; the viewport adds a dark outline and thicker stroke.</summary>
public static class CadSelectionAppearance
{
    public static Color Accent { get; } = Color.FromRgb(250,204,21);
    public static Color ColorFor(Color source) => Accent;
}
