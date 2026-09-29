using DotNetCad.Core.Entities;
using DotNetCad.Demo.Mvvm;

namespace DotNetCad.Demo.ViewModels;

public sealed class LayerItemViewModel : ObservableObject
{
    private readonly CadLayer _layer;
    private readonly Action _onVisibilityChanged;
    private readonly Action<string> _onActivate;
    private bool _isActive;

    public string Name => _layer.Name;

    public string ColorHex
    {
        get => _layer.ColorHex;
        set
        {
            if (_layer.ColorHex != value)
            {
                _layer.ColorHex = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsVisible
    {
        get => _layer.IsVisible;
        set
        {
            if (_layer.IsVisible != value)
            {
                _layer.IsVisible = value;
                OnPropertyChanged();
                _onVisibilityChanged();
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (value) _onActivate(Name);
        }
    }

    public void SetActive(bool value) => SetProperty(ref _isActive, value, nameof(IsActive));

    public bool IsLocked
    {
        get => _layer.IsLocked;
        set
        {
            if (_layer.IsLocked != value)
            {
                _layer.IsLocked = value;
                OnPropertyChanged();
            }
        }
    }

    public LayerItemViewModel(CadLayer layer, Action onVisibilityChanged, Action<string> onActivate)
    {
        _layer = layer;
        _onVisibilityChanged = onVisibilityChanged;
        _onActivate = onActivate;
    }
}
