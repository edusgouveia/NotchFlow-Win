using NotchFlow.Core.Models;
using NotchFlow.Core.Notifications;
using NotchFlow.Core.Settings;
using Xunit;

namespace NotchFlow.Core.Tests;

public class NotificationCoordinatorTests : IDisposable
{
    private static readonly DateTimeOffset Base = new(2026, 8, 5, 14, 0, 0, TimeSpan.Zero);

    private readonly string _settingsPath = Path.Combine(
        Path.GetTempPath(), $"notchflow-test-{Guid.NewGuid():N}.json");

    /// <summary>Serviço controlável, para exercitar o coordenador sem a Central de Ações.</summary>
    private sealed class FakeService : INotificationService
    {
        public List<NotificationItem> Items { get; set; } = [];

        public NotificationAccess Access { get; set; } = NotificationAccess.Allowed;

        public event EventHandler? NotificationsChanged;

        public Task<NotificationAccess> RequestAccessAsync() => Task.FromResult(Access);

        public Task<IReadOnlyList<NotificationItem>> ReadAsync()
            => Task.FromResult<IReadOnlyList<NotificationItem>>(Items.ToList());

        public void Raise() => NotificationsChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose() { }
    }

    private static NotificationItem Item(uint id, int minutesAgo = 0, string title = "Fulano") => new()
    {
        Id = id,
        AppId = "MSTeams_8wekyb3d8bbwe!MSTeams",
        Source = "Teams",
        Accent = 0xFF6264A7,
        Title = title,
        Body = "mensagem",
        ReceivedAt = Base.AddMinutes(-minutesAgo)
    };

    private (NotificationCoordinator Coordinator, FakeService Service) Build()
    {
        var settings = new AppSettings(_settingsPath) { NotificationsEnabled = true };
        var service = new FakeService();
        // Despacho síncrono: nos testes não há thread de interface.
        var coordinator = new NotificationCoordinator(service, settings, action => action());
        return (coordinator, service);
    }

    [Fact]
    public async Task APrimeiraLeituraNaoAvisaDoQueJaEstavaNaCentral()
    {
        var (coordinator, service) = Build();
        var avisos = 0;
        coordinator.NotificationArrived += (_, _) => avisos++;

        service.Items = [Item(1), Item(2), Item(3)];
        await coordinator.StartAsync();

        // Abrir o NotchFlow com mensagens acumuladas não pode disparar um aviso por mensagem.
        Assert.Equal(0, avisos);
        Assert.Equal(3, coordinator.Count);

        coordinator.Dispose();
    }

    [Fact]
    public async Task UmaNotificacaoInediteDisparaOAviso()
    {
        var (coordinator, service) = Build();
        service.Items = [Item(1)];
        await coordinator.StartAsync();

        NotificationItem? recebida = null;
        coordinator.NotificationArrived += (_, item) => recebida = item;

        service.Items = [Item(2, title: "Ciclano"), Item(1)];
        await coordinator.RefreshAsync();

        Assert.NotNull(recebida);
        Assert.Equal("Ciclano", recebida.Title);

        coordinator.Dispose();
    }

    [Fact]
    public async Task UmaRajadaDisparaUmAvisoSo()
    {
        var (coordinator, service) = Build();
        service.Items = [Item(1)];
        await coordinator.StartAsync();

        var avisos = 0;
        coordinator.NotificationArrived += (_, _) => avisos++;

        // Três novas de uma vez não podem virar uma fila de aberturas da ilha.
        service.Items = [Item(4), Item(3), Item(2), Item(1)];
        await coordinator.RefreshAsync();

        Assert.Equal(1, avisos);

        coordinator.Dispose();
    }

    [Fact]
    public async Task ReleituraDaMesmaListaNaoAvisaDeNovo()
    {
        var (coordinator, service) = Build();
        service.Items = [Item(1)];
        await coordinator.StartAsync();

        var avisos = 0;
        coordinator.NotificationArrived += (_, _) => avisos++;

        await coordinator.RefreshAsync();
        await coordinator.RefreshAsync();

        Assert.Equal(0, avisos);

        coordinator.Dispose();
    }

    [Fact]
    public async Task UmaNotificacaoLidaESquecidaEPodeAvisarSeVoltar()
    {
        var (coordinator, service) = Build();
        service.Items = [Item(1)];
        await coordinator.StartAsync();

        var avisos = 0;
        coordinator.NotificationArrived += (_, _) => avisos++;

        // O usuário leu no Teams e o Teams removeu o toast.
        service.Items = [];
        await coordinator.RefreshAsync();
        Assert.Equal(0, coordinator.Count);
        Assert.Equal(0, avisos);

        // O mesmo id voltando conta como novo, senão nunca mais avisaria.
        service.Items = [Item(1)];
        await coordinator.RefreshAsync();
        Assert.Equal(1, avisos);

        coordinator.Dispose();
    }

    [Fact]
    public async Task OPainelListaAsMaisRecentesEContaOResto()
    {
        var (coordinator, service) = Build();
        service.Items =
        [
            Item(5, minutesAgo: 0),
            Item(4, minutesAgo: 1),
            Item(3, minutesAgo: 2),
            Item(2, minutesAgo: 3),
            Item(1, minutesAgo: 4)
        ];
        await coordinator.StartAsync();

        Assert.Equal(5, coordinator.Count);
        Assert.Equal(3, coordinator.Visible.Count);
        Assert.Equal(2, coordinator.Overflow);
        Assert.Equal(5u, coordinator.Visible[0].Id);

        coordinator.Dispose();
    }

    [Fact]
    public async Task SemNotificacoesNaoHaIndicadorNemColuna()
    {
        var (coordinator, service) = Build();
        service.Items = [];
        await coordinator.StartAsync();

        Assert.False(coordinator.HasNotifications);
        Assert.Equal(0, coordinator.Count);
        Assert.Empty(coordinator.Visible);

        coordinator.Dispose();
    }

    [Fact]
    public async Task ComOAcessoNegadoOCoordenadorFicaVazioSemQuebrar()
    {
        var (coordinator, service) = Build();
        service.Access = NotificationAccess.Denied;
        service.Items = [Item(1)];

        await coordinator.StartAsync();

        Assert.False(coordinator.HasNotifications);
        Assert.Equal(NotificationAccess.Denied, coordinator.Access);

        coordinator.Dispose();
    }

    [Fact]
    public async Task ComORecursoDesligadoNadaELido()
    {
        var settings = new AppSettings(_settingsPath) { NotificationsEnabled = false };
        var service = new FakeService { Items = [Item(1)] };
        var coordinator = new NotificationCoordinator(service, settings, action => action());

        await coordinator.StartAsync();

        Assert.False(coordinator.HasNotifications);

        coordinator.Dispose();
    }

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }
}
