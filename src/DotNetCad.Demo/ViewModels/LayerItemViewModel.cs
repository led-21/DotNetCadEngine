using System.Windows.Input;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Demo.Mvvm;
namespace DotNetCad.Demo.ViewModels;
public sealed class LayerItemViewModel : ObservableObject
{
    private readonly CadLayer _layer;
    private readonly Func<string,string?,LayerDeleteMode,CadCommandResult> _operate;
    private bool _isActive;
    public string Name => _layer.Name;
    private readonly Func<int> _count;
    public int EntityCount => _count();
    public void RefreshCounts() { OnPropertyChanged(nameof(EntityCount));OnPropertyChanged(nameof(Details)); }
    public string Details => $"{EntityCount} entidade(s) · {(IsLocked ? "bloqueada" : "livre")} · {(IsActive ? "ativa" : "")}";
    public string ColorHex { get=>_layer.ColorHex; set { _operate("COLOR",value,LayerDeleteMode.Cancel); OnPropertyChanged(); } }
    public bool IsVisible { get=>_layer.IsVisible; set { if(value!=IsVisible)_operate(value?"SHOW":"HIDE",null,LayerDeleteMode.Cancel); OnPropertyChanged(); } }
    public bool IsLocked { get=>_layer.IsLocked; set { if(value!=IsLocked)_operate(value?"LOCK":"UNLOCK",null,LayerDeleteMode.Cancel); OnPropertyChanged(); } }
    public bool IsActive { get=>_isActive; set { if(value && !_isActive)_operate("ACTIVE",null,LayerDeleteMode.Cancel); } }
    public void SetActive(bool value)=>SetProperty(ref _isActive,value,nameof(IsActive));
    public ICommand ActivateCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand TransferCommand { get; }
    public ICommand LockCommand { get; }
    public LayerItemViewModel(CadLayer layer,Func<int> count,Func<string,string?,LayerDeleteMode,CadCommandResult> operate,Action rename,Action delete)
    {
        _layer=layer;_count=count;_operate=operate;
        ActivateCommand=new RelayCommand(()=>operate("ACTIVE",null,LayerDeleteMode.Cancel));
        RenameCommand=new RelayCommand(rename,()=>layer.Name!="0" && !layer.IsProtected && !layer.IsLocked);
        DeleteCommand=new RelayCommand(delete,()=>layer.Name!="0" && !layer.IsProtected && !layer.IsLocked);
        TransferCommand=new RelayCommand(()=>operate("TRANSFER",null,LayerDeleteMode.Cancel));
        LockCommand=new RelayCommand(()=>IsLocked=!IsLocked);
    }
}
