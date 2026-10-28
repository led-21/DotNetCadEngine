using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DotNetCad.Demo.ViewModels.Properties;

namespace DotNetCad.Demo.Controls;

public partial class PropertyInspectorView : UserControl
{
    public PropertyInspectorView()
    {
        InitializeComponent();
    }

    private void PropBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender is TextBox tb)
            {
                var binding = tb.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (sender is TextBox tb)
            {
                var binding = tb.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateTarget();
            }
            e.Handled = true;
        }
    }

    private void OnBatchLayerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PropertyInspectorViewModel vm && sender is ComboBox cb && cb.SelectedItem is string newLayer)
        {
            vm.ApplyBatchLayer(newLayer);
        }
    }

    private void OnBatchColorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is PropertyInspectorViewModel vm && sender is TextBox tb)
        {
            vm.ApplyBatchColor(tb.Text);
            e.Handled = true;
        }
    }

    private void OnBatchColorComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PropertyInspectorViewModel vm && sender is ComboBox cb && cb.SelectedValue is string hex)
        {
            vm.ApplyBatchColor(hex);
        }
    }

    private void OnBatchColorLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is PropertyInspectorViewModel vm && sender is TextBox tb)
        {
            vm.ApplyBatchColor(tb.Text);
        }
    }
}
