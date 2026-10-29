using System.Globalization;
using System.Windows;

namespace DotNetCad.Demo.Views;

public partial class TextEntryDialog : Window
{
    public string AnnotationText => ContentInput.Text;
    public double AnnotationHeight { get; private set; }

    public TextEntryDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => ContentInput.Focus();
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AnnotationText) || AnnotationText.Any(char.IsControl))
        {
            ValidationMessage.Text = "Digite um texto em uma única linha.";
            ContentInput.Focus();
            return;
        }
        if (!double.TryParse(HeightInput.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture,
                out var height) || !double.IsFinite(height) || height < .05)
        {
            ValidationMessage.Text = "Informe uma altura válida, a partir de 0,05 m.";
            HeightInput.Focus();
            return;
        }
        AnnotationHeight = height;
        DialogResult = true;
    }
}
