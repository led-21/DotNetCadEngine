using System.Text;
using System.Windows;
using DotNetCad.Core.Editor;
using DotNetCad.Demo.ViewModels;
using DotNetCad.Infrastructure.MutuaConecta;
using DotNetCad.Infrastructure.Native;
using Xunit;

namespace DotNetCad.Demo.Tests;

public class RemoteRevisionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OpenRevision_PreservesLocalDrawingOnCancellationOrInvalidFile(bool cancel, bool invalid)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var main = new MainViewModel();
                var local = new CadDocument { Title = "local.cad" };
                main.ReplaceDocument(local, null, true);
                main.UnsavedChangesPrompt = () => cancel ? MessageBoxResult.Cancel : MessageBoxResult.No;
                var api = new RevisionClient(invalid);
                var store = new MutuaCredentialStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MutuaRemoteTest_" + Guid.NewGuid().ToString("N")));
                var panel = new MutuaConectaViewModel(main, api, store);
                Assert.True(panel.ConnectAndLoginAsync().GetAwaiter().GetResult());
                panel.SelectedProject = panel.Projects.Single();
                panel.LoadSelectedProjectAsync().GetAwaiter().GetResult();
                Assert.Single(panel.Revisions);
                var opened = panel.DownloadAndOpenRevisionAsync().GetAwaiter().GetResult();
                Assert.Equal(!cancel && !invalid, opened);
                if (cancel || invalid) Assert.Same(local, main.Document);
                else { Assert.Equal("remote.cad", main.Document.Title); Assert.True(main.HasUnsavedChanges); }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw new AggregateException(failure);
    }

    private sealed class RevisionClient(bool invalid) : IMutuaConectaApiClient
    {
        private readonly Guid projectId = Guid.NewGuid();
        public string BaseUrl => "http://localhost:5180";
        public string? SessionToken { get; set; }
        public void SetBaseUrl(string url) { }
        public Task<MutuaConnectionStatus> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(new MutuaConnectionStatus(true));
        public Task<MutuaAuthResult> LoginAsync(string email, string password, CancellationToken ct = default) { SessionToken = "test"; return Task.FromResult(new MutuaAuthResult(true, "test")); }
        public Task<IReadOnlyList<MutuaProjectSummaryDto>> GetMyProjectsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MutuaProjectSummaryDto>>([new(projectId,"P","Project","", "", "", "", Guid.NewGuid(),"",0,0,1,DateTimeOffset.UtcNow)]);
        public Task<MutuaProjectDetailDto?> GetProjectDetailsAsync(Guid id, CancellationToken ct = default) => Task.FromResult<MutuaProjectDetailDto?>(new(projectId,"P","Project","","","",true,"",null,Guid.NewGuid(),Guid.NewGuid(),"",[],[],[],[new(Guid.NewGuid(),null,Guid.NewGuid(),"","Remote","remote.cad","CadNative",2,"",10,null,"ProjectTeam",DateTimeOffset.UtcNow)]));
        public Task<byte[]> DownloadNativeDocumentAsync(Guid id, MutuaDocumentSummaryDto revision, CancellationToken ct = default) => Task.FromResult(Encoding.UTF8.GetBytes(invalid ? "broken" : CadDrawingSerializer.Serialize(new CadDocument())));
        public Task<IReadOnlyList<MutuaProfessionalMatchDto>> GetRecommendedSpecialistsAsync(Guid p, Guid r, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MutuaProfessionalMatchDto>>([]);
        public Task<MutuaInvitationDto?> SendCollaborationInvitationAsync(Guid p, MutuaCreateInvitationRequest r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MutuaDocumentSyncResult> SyncCadDocumentMetadataAsync(Guid p, MutuaCadDocumentSyncRequest r, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
