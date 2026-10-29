using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Demo.Mvvm;
using DotNetCad.Infrastructure.MutuaConecta;
using DotNetCad.Infrastructure.Svg;

namespace DotNetCad.Demo.ViewModels;

public sealed class MutuaConectaViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IMutuaConectaApiClient _apiClient;
    private readonly MutuaCredentialStore _credentialStore;

    private bool _isPanelVisible = true;
    private string _apiUrl = MutuaEndpointValidator.DefaultProductionUrl;
    private string _demoEmail = string.Empty;
    private string _demoPassword = string.Empty;
    private string _connectionStatusText = "Modo Offline (API desconectada)";
    private bool _isConnected;
    private bool _isAuthenticated;
    private string _authenticatedUserName = "Não autenticado";
    private string _authenticatedUserTitle = string.Empty;
    private string _statusMessage = "Pronto para sincronização ou trabalho 100% offline.";
    private bool _isBusy;

    // Linked Project PRJ-2026-001 info
    private Guid _linkedProjectId;
    private string _linkedProjectCode = string.Empty;
    private string _linkedProjectTitle = "Selecione um projeto";
    private Guid _electricalRequirementId;

    // Collaboration draft staged for human confirmation
    private bool _isDraftPendingConfirmation;
    private string _draftRecipientName = string.Empty;
    private Guid? _draftRecipientProfileId;
    private string _draftMessage = string.Empty;
    private decimal _draftHonorarium = 14500.00m;

    public bool IsPanelVisible
    {
        get => _isPanelVisible;
        set => SetProperty(ref _isPanelVisible, value);
    }

    public string ApiUrl
    {
        get => _apiUrl;
        set
        {
            if (SetProperty(ref _apiUrl, value))
            {
                if (MutuaEndpointValidator.TryValidateBaseUrl(value, out var err))
                {
                    _apiClient.SetBaseUrl(value);
                    _credentialStore.SaveConfig(new MutuaConfig
                    {
                        ApiBaseUrl = value,
                        LastLinkedProjectId = _linkedProjectId,
                        LastLinkedProjectCode = _linkedProjectCode,
                        LastUserEmail = _demoEmail
                    });
                }
                else
                {
                    StatusMessage = $"URL inválida: {err}";
                }
            }
        }
    }

    public string DemoEmail
    {
        get => _demoEmail;
        set => SetProperty(ref _demoEmail, value);
    }

    public string DemoPassword
    {
        get => _demoPassword;
        set => SetProperty(ref _demoPassword, value);
    }

    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        private set => SetProperty(ref _connectionStatusText, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set => SetProperty(ref _isAuthenticated, value);
    }

    public string AuthenticatedUserName
    {
        get => _authenticatedUserName;
        private set => SetProperty(ref _authenticatedUserName, value);
    }

    public string AuthenticatedUserTitle
    {
        get => _authenticatedUserTitle;
        private set => SetProperty(ref _authenticatedUserTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string LinkedProjectCode
    {
        get => _linkedProjectCode;
        set => SetProperty(ref _linkedProjectCode, value);
    }

    public string LinkedProjectTitle
    {
        get => _linkedProjectTitle;
        set => SetProperty(ref _linkedProjectTitle, value);
    }

    public Guid LinkedProjectId
    {
        get => _linkedProjectId;
        set => SetProperty(ref _linkedProjectId, value);
    }

    public Guid ElectricalRequirementId
    {
        get => _electricalRequirementId;
        set => SetProperty(ref _electricalRequirementId, value);
    }

    public bool IsDraftPendingConfirmation
    {
        get => _isDraftPendingConfirmation;
        set => SetProperty(ref _isDraftPendingConfirmation, value);
    }

    public string DraftRecipientName
    {
        get => _draftRecipientName;
        set => SetProperty(ref _draftRecipientName, value);
    }

    public Guid? DraftRecipientProfileId
    {
        get => _draftRecipientProfileId;
        set => SetProperty(ref _draftRecipientProfileId, value);
    }

    public string DraftMessage
    {
        get => _draftMessage;
        set => SetProperty(ref _draftMessage, value);
    }

    public decimal DraftHonorarium
    {
        get => _draftHonorarium;
        set => SetProperty(ref _draftHonorarium, value);
    }

    public ObservableCollection<MutuaProjectSummaryDto> Projects { get; } = [];
    public ObservableCollection<MutuaProjectRequirementDto> Requirements { get; } = [];
    private MutuaProjectSummaryDto? _selectedProject;
    public MutuaProjectSummaryDto? SelectedProject
    {
        get => _selectedProject;
        set { if (SetProperty(ref _selectedProject, value)) { LinkedProjectId = Guid.Empty; LinkedProjectCode = string.Empty; LinkedProjectTitle = "Selecione um projeto"; Requirements.Clear(); SelectedRequirement = null; RecommendedSpecialists.Clear(); IsDraftPendingConfirmation = false; } }
    }
    private MutuaProjectRequirementDto? _selectedRequirement;
    public MutuaProjectRequirementDto? SelectedRequirement
    {
        get => _selectedRequirement;
        set { if (SetProperty(ref _selectedRequirement, value)) { ElectricalRequirementId = value?.Id ?? Guid.Empty; RecommendedSpecialists.Clear(); IsDraftPendingConfirmation = false; } }
    }
    private Guid _draftProjectId, _draftRequirementId;
    private readonly Dictionary<string, int> _revisionVersions = new(StringComparer.OrdinalIgnoreCase);
    public ICommand LoadSelectedProjectCommand { get; }
    public async Task LoadSelectedProjectAsync()
    {
        var selected = SelectedProject;
        if (!IsAuthenticated || selected == null) return;
        var details = await _apiClient.GetProjectDetailsAsync(selected.Id);
        if (details == null || SelectedProject?.Id != selected.Id) return;
        LinkedProjectId = details.Id; LinkedProjectCode = details.Code; LinkedProjectTitle = details.Title;
        _revisionVersions.Clear();
        foreach (var document in details.Documents) _revisionVersions[document.FileName] = Math.Max(_revisionVersions.GetValueOrDefault(document.FileName), document.VersionNumber);
        Requirements.Clear();
        foreach (var requirement in details.Requirements) Requirements.Add(requirement);
        SelectedRequirement = Requirements.FirstOrDefault();
    }

    public ObservableCollection<MutuaProfessionalMatchDto> RecommendedSpecialists { get; } = [];

    public ICommand TogglePanelCommand { get; }
    public ICommand ConnectAndLoginCommand { get; }
    public ICommand CheckHealthCommand { get; }
    public ICommand LoadDemoSilosDrawingCommand { get; }
    public ICommand SyncCadDocumentCommand { get; }
    public ICommand FindSpecialistsCommand { get; }
    public ICommand PrepareDraftFromMatchCommand { get; }
    public ICommand ConfirmSendInviteCommand { get; }
    public ICommand CancelDraftCommand { get; }

    public MutuaConectaViewModel(MainViewModel mainViewModel, IMutuaConectaApiClient? apiClient = null, MutuaCredentialStore? credentialStore = null)
    {
        _mainViewModel = mainViewModel;
        _credentialStore = credentialStore ?? new MutuaCredentialStore();
        _apiClient = apiClient ?? new MutuaConectaApiClient(baseUrl: MutuaEndpointValidator.DefaultProductionUrl);

        // Load config from credential store if available
        var config = _credentialStore.LoadConfig();
        if (!string.IsNullOrWhiteSpace(config.ApiBaseUrl) && MutuaEndpointValidator.TryValidateBaseUrl(config.ApiBaseUrl, out _))
        {
            _apiUrl = config.ApiBaseUrl;
            _apiClient.SetBaseUrl(_apiUrl);
        }
        if (config.LastLinkedProjectId.HasValue)
        {
            _linkedProjectId = config.LastLinkedProjectId.Value;
        }
        if (!string.IsNullOrWhiteSpace(config.LastLinkedProjectCode))
        {
            _linkedProjectCode = config.LastLinkedProjectCode;
        }
        if (!string.IsNullOrWhiteSpace(config.LastUserEmail))
        {
            _demoEmail = config.LastUserEmail;
        }

        // Restore token if present in secure DPAPI store
        var savedToken = _credentialStore.LoadSessionToken();
        if (!string.IsNullOrWhiteSpace(savedToken))
        {
            _apiClient.SessionToken = savedToken;
        }

        LoadSelectedProjectCommand = new RelayCommand(async () => { try { await LoadSelectedProjectAsync(); } catch (Exception ex) { StatusMessage = ex.Message; } });
        TogglePanelCommand = new RelayCommand(() => IsPanelVisible = !IsPanelVisible);
        ConnectAndLoginCommand = new RelayCommand(async () => await ConnectAndLoginAsync());
        CheckHealthCommand = new RelayCommand(async () => await CheckHealthAsync());
        LoadDemoSilosDrawingCommand = new RelayCommand(LoadDemoSilosDrawing);
        SyncCadDocumentCommand = new RelayCommand(async () => await SyncCadDocumentAsync());
        FindSpecialistsCommand = new RelayCommand(async () => await FindSpecialistsAsync());
        PrepareDraftFromMatchCommand = new RelayCommand(p =>
        {
            if (p is MutuaProfessionalMatchDto match)
            {
                StageCollaborationDraft(match.ProfileId, match.DisplayName,
                    $"Olá {match.DisplayName}, convidamos você para {SelectedRequirement?.Title} no projeto {LinkedProjectCode}.",
                    SelectedRequirement?.EstimatedHonorariumBrl ?? 0m);
            }
        });
        ConfirmSendInviteCommand = new RelayCommand(async () => await ConfirmSendInviteAsync());
        CancelDraftCommand = new RelayCommand(() =>
        {
            IsDraftPendingConfirmation = false;
            StatusMessage = "Rascunho de convite cancelado pelo usuário.";
            _mainViewModel.AppendConsole("MUTUA: Rascunho de convite cancelado.");
        });
    }

    public async Task<bool> CheckHealthAsync()
    {
        IsBusy = true;
        try
        {
            var health = await _apiClient.CheckHealthAsync();
            IsConnected = health.IsHealthy;
            ConnectionStatusText = health.IsHealthy ? "Conectado à API Mútua Conecta" : "Modo Offline (API indisponível)";
            StatusMessage = health.IsHealthy
                ? $"API pronta. Banco: {health.DatabaseStatus ?? "Conectado"}."
                : (health.ErrorMessage ?? "API offline.");
            return health.IsHealthy;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ConnectAndLoginAsync()
    {
        IsBusy = true;
        StatusMessage = "Autenticando...";
        IsAuthenticated = false; IsDraftPendingConfirmation = false; RecommendedSpecialists.Clear(); Projects.Clear(); Requirements.Clear(); SelectedProject = null; LinkedProjectId = Guid.Empty;
        _apiClient.SessionToken = null;
        try
        {
            var health = await _apiClient.CheckHealthAsync();
            if (!health.IsHealthy)
            {
                IsConnected = false;
                ConnectionStatusText = "Modo Offline (API desligada)";
                StatusMessage = "Não foi possível conectar à API. O CAD continua operando normalmente em modo offline.";
                _mainViewModel.AppendConsole("MUTUA: API offline. CAD operando em modo offline.");
                return false;
            }

            IsConnected = true;
            var auth = await _apiClient.LoginAsync(DemoEmail, DemoPassword);
            if (auth.IsSuccess && auth.AccessToken != null)
            {
                IsAuthenticated = true;
                AuthenticatedUserName = auth.User?.FullName ?? DemoEmail;
                AuthenticatedUserTitle = auth.User?.Title ?? "Engenheiro Autor";
                ConnectionStatusText = $"Online | {AuthenticatedUserName}";
                StatusMessage = $"Autenticado com sucesso como {AuthenticatedUserName}.";

                // Save token securely with Windows DPAPI
                _credentialStore.SaveSessionToken(auth.AccessToken);
                _credentialStore.SaveConfig(new MutuaConfig
                {
                    ApiBaseUrl = ApiUrl,
                    LastLinkedProjectId = LinkedProjectId,
                    LastLinkedProjectCode = LinkedProjectCode,
                    LastUserEmail = DemoEmail
                });

                foreach (var project in await _apiClient.GetMyProjectsAsync()) Projects.Add(project);
                DemoPassword = string.Empty;
                _mainViewModel.AppendConsole($"MUTUA: Autenticado com sucesso como {AuthenticatedUserName}.");
                return true;
            }

            IsAuthenticated = false;
            StatusMessage = auth.ErrorMessage ?? "Falha na autenticação.";
            _mainViewModel.AppendConsole($"MUTUA ERROR: {StatusMessage}");
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro de conexão: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void LoadDemoSilosDrawing()
    {
        var doc = new CadDocument { Title = "UnidadeBeneficiamentoSilos.cad" };

        // Setup layers for multidisciplinary disciplines
        EnsureLayer(doc, "ESTRUTURA", "#38BDF8");
        EnsureLayer(doc, "ELETRICA", "#EAB308");
        EnsureLayer(doc, "SPDA", "#F97316");
        EnsureLayer(doc, "DRENAGEM", "#22C55E");

        doc.Clear();

        // 1. ESTRUTURA: Moega, 2 Silos de 50.000 sacas, túnel de interligação
        doc.AddEntity(new CadRectangle(new Point2D(-5, 10), width: 12, height: 8, rotationDegrees: 0, layer: "ESTRUTURA", colorHex: "#38BDF8"));
        doc.AddEntity(new CadText { Position = new Point2D(1, 14), Text = "MOEGA DE DESCARGA", Height = 1.2, Layer = "ESTRUTURA", ColorHex = "#38BDF8" });

        doc.AddEntity(new CadCircle(new Point2D(25, 14), radius: 7.0, layer: "ESTRUTURA", colorHex: "#38BDF8"));
        doc.AddEntity(new CadText { Position = new Point2D(25, 14), Text = "SILO 01 (50k sacas)", Height = 1.2, Layer = "ESTRUTURA", ColorHex = "#38BDF8" });

        doc.AddEntity(new CadCircle(new Point2D(45, 14), radius: 7.0, layer: "ESTRUTURA", colorHex: "#38BDF8"));
        doc.AddEntity(new CadText { Position = new Point2D(45, 14), Text = "SILO 02 (50k sacas)", Height = 1.2, Layer = "ESTRUTURA", ColorHex = "#38BDF8" });

        doc.AddEntity(new CadLine(new Point2D(7, 14), new Point2D(18, 14), layer: "ESTRUTURA", colorHex: "#38BDF8"));
        doc.AddEntity(new CadLine(new Point2D(32, 14), new Point2D(38, 14), layer: "ESTRUTURA", colorHex: "#38BDF8"));

        doc.AddEntity(new CadDimension
        {
            StartPoint = new Point2D(25, 14),
            EndPoint = new Point2D(45, 14),
            OffsetPoint = new Point2D(35, 23),
            Prefix = "Dist. Silos = ",
            Layer = "ESTRUTURA"
        });

        // 2. ELETRICA: Subestação 300 kVA (34.5 kV) e eletrodutos subterrâneos
        doc.AddEntity(new CadRectangle(new Point2D(60, 10), width: 10, height: 8, rotationDegrees: 0, layer: "ELETRICA", colorHex: "#EAB308"));
        doc.AddEntity(new CadText { Position = new Point2D(65, 14), Text = "SUBESTAÇÃO 300 kVA (34.5 kV)", Height = 1.2, Layer = "ELETRICA", ColorHex = "#EAB308" });
        doc.AddEntity(new CadLine(new Point2D(60, 14), new Point2D(45, 7), layer: "ELETRICA", colorHex: "#EAB308"));
        doc.AddEntity(new CadLine(new Point2D(45, 7), new Point2D(25, 7), layer: "ELETRICA", colorHex: "#EAB308"));
        doc.AddEntity(new CadLine(new Point2D(25, 7), new Point2D(7, 10), layer: "ELETRICA", colorHex: "#EAB308"));

        // 3. SPDA: Anel perimetral de aterramento e proteção contra descargas atmosféricas (NBR 5419)
        doc.AddEntity(new CadLine(new Point2D(-8, 4), new Point2D(74, 4), layer: "SPDA", colorHex: "#F97316"));
        doc.AddEntity(new CadLine(new Point2D(74, 4), new Point2D(74, 24), layer: "SPDA", colorHex: "#F97316"));
        doc.AddEntity(new CadLine(new Point2D(74, 24), new Point2D(-8, 24), layer: "SPDA", colorHex: "#F97316"));
        doc.AddEntity(new CadLine(new Point2D(-8, 24), new Point2D(-8, 4), layer: "SPDA", colorHex: "#F97316"));
        doc.AddEntity(new CadText { Position = new Point2D(33, 25.5), Text = "ANEL DE ATERRAMENTO SPDA (NBR 5419)", Height = 1.1, Layer = "SPDA", ColorHex = "#F97316" });

        // 4. DRENAGEM: Canaleta pluvial
        doc.AddEntity(new CadLine(new Point2D(-10, 0), new Point2D(76, 0), layer: "DRENAGEM", colorHex: "#22C55E"));
        doc.AddEntity(new CadLine(new Point2D(76, 0), new Point2D(76, 2), layer: "DRENAGEM", colorHex: "#22C55E"));
        doc.AddEntity(new CadLine(new Point2D(76, 2), new Point2D(-10, 2), layer: "DRENAGEM", colorHex: "#22C55E"));
        doc.AddEntity(new CadLine(new Point2D(-10, 2), new Point2D(-10, 0), layer: "DRENAGEM", colorHex: "#22C55E"));
        doc.AddEntity(new CadText { Position = new Point2D(33, 1), Text = "CANALETA DE DRENAGEM PLUVIAL", Height = 0.9, Layer = "DRENAGEM", ColorHex = "#22C55E" });

        doc.ActiveLayer = "ESTRUTURA";

        // Refresh UI
        _mainViewModel.Document = doc;
        _mainViewModel.ZoomExtentsHandler?.Invoke();
        StatusMessage = "Planta Demo Silos carregada (UnidadeBeneficiamentoSilos.cad) com 4 disciplinas técnicas.";
        _mainViewModel.AppendConsole("MUTUA: Planta técnica demonstrativa carregada: ESTRUTURA, ELETRICA, SPDA e DRENAGEM.");
    }

    private static void EnsureLayer(CadDocument doc, string name, string colorHex)
    {
        if (doc.GetLayer(name) == null)
        {
            doc.Layers.Add(new CadLayer { Name = name, ColorHex = colorHex });
        }
    }

    private MutuaDocumentSyncResult RejectSync(string error)
    {
        StatusMessage=error;_mainViewModel.AppendConsole("MUTUA ERROR: "+error);return new(false,ErrorMessage:error);
    }
    public async Task<MutuaDocumentSyncResult> SyncCadDocumentAsync()
    {
        if(LinkedProjectId == Guid.Empty)return RejectSync("Selecione e carregue um projeto antes de publicar.");
        if(IsBusy)return RejectSync("Outra operação Mútua está em andamento.");
        if(!IsAuthenticated)return RejectSync("Autentique-se no painel Mútua antes de publicar o documento.");
        if(_mainViewModel.InteractiveOperationActive?.Invoke()==true)return RejectSync("Conclua ou cancele o comando interativo antes de sincronizar.");
        IsBusy = true;
        StatusMessage = "Sincronizando planta CAD e preview SVG com a plataforma web...";
        try
        {
            var req=MutuaCadSyncPayload.Create(_mainViewModel.Document);
            req = req with { ExpectedVersion = _revisionVersions.GetValueOrDefault(req.FileName) };
            var sha256=req.Sha256Hash;
            var result = await _apiClient.SyncCadDocumentMetadataAsync(LinkedProjectId, req);
            if (result.IsSuccess)
            {
                _revisionVersions[req.FileName] = result.VersionNumber ?? req.ExpectedVersion!.Value + 1;
                StatusMessage = $"Documento sincronizado com sucesso (v{result.VersionNumber ?? 1})!";
                _mainViewModel.AppendConsole($"MUTUA OK: Planta CAD sincronizada no projeto {LinkedProjectCode}. SHA-256: {sha256[..8]}...");
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Falha ao sincronizar com a plataforma.";
                _mainViewModel.AppendConsole($"MUTUA ERROR: {StatusMessage}");
            }
            return result;
        }
        catch (Exception ex)
        {
            var res = new MutuaDocumentSyncResult(false, ErrorMessage: ex.Message);
            StatusMessage = $"Erro no sync: {ex.Message}";
            _mainViewModel.AppendConsole($"MUTUA ERROR: {ex.Message}");
            return res;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task<MutuaProjectDetailDto?> GetLinkedProjectDetailsAsync() => _apiClient.GetProjectDetailsAsync(LinkedProjectId);

    public async Task<IReadOnlyList<MutuaProfessionalMatchDto>> FindSpecialistsAsync(Guid? requirementId=null)
    {
        if (LinkedProjectId == Guid.Empty || SelectedRequirement == null) { StatusMessage = "Selecione projeto e requisito."; return []; }
        if (requirementId.HasValue && requirementId != SelectedRequirement.Id) { StatusMessage = "O requisito deve ser selecionado no painel."; return []; }
        var projectId = LinkedProjectId; var selectedId = SelectedRequirement.Id;
        IsBusy = true;
        StatusMessage = "Consultando ranking determinístico de especialistas na API...";
        RecommendedSpecialists.Clear();
        try
        {
            var matches = await _apiClient.GetRecommendedSpecialistsAsync(projectId, selectedId);
            if (LinkedProjectId != projectId || SelectedRequirement?.Id != selectedId) return [];
            foreach (var m in matches)
            {
                RecommendedSpecialists.Add(m);
            }

            if (RecommendedSpecialists.Count > 0)
            {
                var top = RecommendedSpecialists[0];
                StatusMessage = $"Encontrado(a): {top.DisplayName} ({top.Score}% compatível).";
                _mainViewModel.AppendConsole($"MUTUA: {RecommendedSpecialists.Count} especialista(s) recomendados. Destaque: {top.DisplayName} ({top.Score}%).");
            }
            else
            {
                StatusMessage = "Nenhum especialista retornado pela API ou API offline.";
                _mainViewModel.AppendConsole("MUTUA: Nenhum especialista retornado.");
            }

            return matches;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao buscar especialistas: {ex.Message}";
            return [];
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void StageCollaborationDraft(Guid recipientProfileId, string recipientName, string message, decimal honorarium)
    {
        if (LinkedProjectId == Guid.Empty || SelectedRequirement == null || !RecommendedSpecialists.Any(m => m.ProfileId == recipientProfileId))
            throw new InvalidOperationException("Selecione projeto, requisito e profissional recomendado antes de preparar o convite.");
        _draftProjectId = LinkedProjectId; _draftRequirementId = SelectedRequirement.Id;
        DraftRecipientProfileId = recipientProfileId;
        DraftRecipientName = recipientName;
        DraftMessage = message;
        DraftHonorarium = honorarium;
        IsDraftPendingConfirmation = true;
        IsPanelVisible = true;
        StatusMessage = $"Rascunho preparado para {recipientName}. Confirmação do usuário necessária.";
        _mainViewModel.AppendConsole($"MUTUA: Rascunho de convite para {recipientName} preparado no painel. Confirme na UI para enviar.");
    }

    public async Task<bool> ConfirmSendInviteAsync()
    {
        if (IsBusy || !IsAuthenticated || !IsDraftPendingConfirmation || LinkedProjectId != _draftProjectId || SelectedRequirement?.Id != _draftRequirementId || !DraftRecipientProfileId.HasValue)
        {
            StatusMessage = "Nenhum profissional selecionado para o convite.";
            return false;
        }

        IsBusy = true;
        StatusMessage = $"Enviando convite oficial para {DraftRecipientName}...";
        try
        {
            var req = new MutuaCreateInvitationRequest(
                ProjectRequirementId: _draftRequirementId,
                RecipientProfileId: DraftRecipientProfileId.Value,
                Message: DraftMessage,
                ProposedHonorariumBrl: DraftHonorarium
            );

            var invite = await _apiClient.SendCollaborationInvitationAsync(_draftProjectId, req);
            if (invite != null)
            {
                StatusMessage = $"Convite enviado com sucesso para {DraftRecipientName}!";
                _mainViewModel.AppendConsole($"MUTUA OK: Convite enviado para {DraftRecipientName} ({DraftHonorarium:C2}).");
                IsDraftPendingConfirmation = false;
                DraftRecipientProfileId = null;
                DraftRecipientName = string.Empty;
                DraftMessage = string.Empty;
                return true;
            }

            StatusMessage = "Não foi possível enviar o convite (verifique conexão ou permissão).";
            _mainViewModel.AppendConsole("MUTUA ERROR: Falha ao submeter convite.");
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao enviar convite: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
