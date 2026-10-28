using System.Collections.ObjectModel;
using DotNetCad.Demo.Mvvm;
namespace DotNetCad.Demo.ViewModels;
public enum ConsoleOrigin { Usuario, MCP, Sistema }
public enum ConsoleSeverity { Informacao, Aviso, Erro }
public sealed record ConsoleMessage(DateTimeOffset Timestamp,ConsoleOrigin Origin,ConsoleSeverity Severity,string Text,bool IsCommand=false)
{
    public string Display => $"{Timestamp:HH:mm:ss} [{Origin}] [{Severity}] {(IsCommand ? "> " : "")}{Text}";
}
/// <summary>Bounded session-only output and independently navigable user command history.</summary>
public sealed class CadConsoleModel : ObservableObject
{
    public const int MessageLimit=1000, CharacterLimit=2_000_000, CommandLimit=128, InputLimit=8192;
    public ObservableCollection<ConsoleMessage> Messages { get; } = [];
    private readonly List<string> _commands=[];
    public IReadOnlyList<string> Commands=>_commands.AsReadOnly();
    private int _index=-1,_characters;
    private string _draft="";
    public void Add(string text,ConsoleOrigin origin=ConsoleOrigin.Sistema,ConsoleSeverity severity=ConsoleSeverity.Informacao,bool command=false)
    {
        if(text.Length>32768)text=text[..32768]+" … [mensagem truncada]";
        Messages.Add(new(DateTimeOffset.Now,origin,severity,text,command));_characters+=text.Length;
        while(Messages.Count>MessageLimit || _characters>CharacterLimit) { _characters-=Messages[0].Text.Length;Messages.RemoveAt(0); }
    }
    public void Remember(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return;
        if(_commands.LastOrDefault()!=text)_commands.Add(text.Length>InputLimit?text[..InputLimit]:text);
        while(_commands.Count>CommandLimit)_commands.RemoveAt(0);
        _index=-1;_draft="";
    }
    public string Previous(string current)
    {
        if(_commands.Count==0)return current;
        if(_index<0){_draft=current;_index=_commands.Count;}
        _index=Math.Max(0,_index-1);return _commands[_index];
    }
    public string Next(string current)
    {
        if(_index<0)return current;
        if(++_index>=_commands.Count){_index=-1;return _draft;}
        return _commands[_index];
    }
    public void ClearOutput(){Messages.Clear();_characters=0;}
    public string Export()=>string.Join(Environment.NewLine,Messages.Select(m=>m.Display));
}
