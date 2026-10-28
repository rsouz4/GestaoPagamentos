using GestaoPagamentos.Application.Notificacoes;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GestaoPagamentos.Infrastructure.Notificacoes;

public sealed class PlaywrightWhatsAppService : IWhatsAppAutomationService, IAsyncDisposable
{
    private readonly ILogger<PlaywrightWhatsAppService> _logger;
    private readonly PlaywrightWhatsAppConfig _config;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _isAuthenticated;
    private bool _primeiraSessao;

    public PlaywrightWhatsAppService(
        ILogger<PlaywrightWhatsAppService> logger,
        PlaywrightWhatsAppConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<bool> VerificarAutenticacaoAsync(CancellationToken ct = default)
    {
        try
        {
            if (_page is null)
                await InicializarAsync(ct);

            // Se a página já carregou o WhatsApp Web, devemos estar autenticados
            if (_isAuthenticated)
                return true;

            // Espera o WhatsApp Web carregar
            _logger.LogInformation("Navegando para WhatsApp Web...");
            await _page!.GotoAsync("https://web.whatsapp.com", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = (int)_config.TimeoutCarregamentoPagina.TotalMilliseconds
            });

            _logger.LogDebug("WhatsApp Web carregado. Verificando autenticação...");

            // Seletores que só existem com o usuário logado (o canvas do QR Code não é confiável)
            var seletoresAutenticacao = new[]
            {
                "div[data-testid='chat-list']",
                "div[data-testid='pane-side']",
                "#pane-side",
                "div[role='grid'][aria-label]",
                "div[aria-label='Chats']",
                "div[aria-label='Lista de conversas']"
            };

            bool autenticado = false;
            string seletorEncontrado = string.Empty;
            bool qrCodeLogado = false;

            _logger.LogInformation("Aguardando autenticação (máx {Timeout})...", _config.TimeoutAutenticacao);

            var limite = DateTime.UtcNow + _config.TimeoutAutenticacao;
            while (!autenticado && DateTime.UtcNow < limite)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var seletor in seletoresAutenticacao)
                {
                    if (await _page.QuerySelectorAsync(seletor) is not null)
                    {
                        autenticado = true;
                        seletorEncontrado = seletor;
                        break;
                    }
                }

                if (autenticado)
                    break;

                if (!qrCodeLogado && await _page.QuerySelectorAsync("canvas") is not null)
                {
                    qrCodeLogado = true;
                    _logger.LogInformation("QR Code visível. Aguardando escanear...");
                }

                await Task.Delay(1000, ct);
            }

            if (autenticado)
                await Task.Delay(2000, ct);

            // Se nenhum seletor funcionou, verifica por URL e tira screenshot para debug
            if (!autenticado)
            {
                var url = _page.Url;
                _logger.LogWarning("Nenhum seletor de chat encontrado. URL atual: {Url}", url);

                // Tira screenshot para debug
                try
                {
                    var debugPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "GestaoPagamentos",
                        "Debug"
                    );
                    Directory.CreateDirectory(debugPath);

                    var screenshotPath = Path.Combine(debugPath, $"whatsapp_debug_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
                    await _page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });

                    _logger.LogInformation("Screenshot salvo em: {ScreenshotPath}", screenshotPath);
                }
                catch (Exception screenshotEx)
                {
                    _logger.LogWarning(screenshotEx, "Erro ao tirar screenshot para debug");
                }
                }

            if (autenticado)
            {
                _isAuthenticated = true;
                _logger.LogInformation("Autenticação no WhatsApp Web verificada com sucesso (seletor: {Seletor})", seletorEncontrado);

                // Salva a sessão para reutilização futura
                await SalvarSessaoAsync();

                if (_primeiraSessao)
                {
                    _logger.LogInformation("Primeira autenticação concluída. Fechando navegador visível e reabrindo em modo headless");
                    await FecharAsync(ct);
                    await InicializarAsync(ct);
                    _isAuthenticated = true;
                }

                return true;
            }
            else
            {
                _logger.LogWarning("Não autenticado no WhatsApp Web. Pode ser necessário escanear QR Code manualmente.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar autenticação no WhatsApp Web");
            return false;
        }
    }

    public async Task<bool> EnviarMensagemAsync(string telefone, string mensagem, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(telefone) || string.IsNullOrWhiteSpace(mensagem))
            {
                _logger.LogWarning("Telefone ou mensagem vazia");
                return false;
            }

            if (_page is null)
                await InicializarAsync(ct);

            _logger.LogInformation("Iniciando envio de mensagem para {Telefone}", telefone);

            // Verifica autenticação
            var autenticado = await VerificarAutenticacaoAsync(ct);
            if (!autenticado)
            {
                _logger.LogError("Não autenticado no WhatsApp Web");
                return false;
            }

            // Constrói link WhatsApp
            var linkWhatsApp = $"https://web.whatsapp.com/send?phone={telefone}";

            _logger.LogDebug("Acessando link WhatsApp: {Link}", linkWhatsApp);
            await _page!.GotoAsync(linkWhatsApp, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = (int)_config.TimeoutCarregamentoPagina.TotalMilliseconds
            });

            // Aguarda campo de mensagem
            await _page.WaitForSelectorAsync("div[contenteditable='true'][role='textbox']", new PageWaitForSelectorOptions
            {
                Timeout = (int)_config.TimeoutCampoDeMensagem.TotalMilliseconds
            });

            // Delay realista antes de digitar
            await Task.Delay(_config.DelayAntesDeDigitar, ct);

            // Digita a mensagem
            var caixaMensagem = await _page.QuerySelectorAsync("div[contenteditable='true'][role='textbox']");
            if (caixaMensagem is null)
            {
                _logger.LogError("Caixa de mensagem não encontrada");
                return false;
            }

            // Limpa qualquer texto anterior e digita
            await caixaMensagem.ClickAsync(new ElementHandleClickOptions { Delay = 100 });
            await caixaMensagem.FillAsync(string.Empty);
#pragma warning disable CS0612
            await caixaMensagem.TypeAsync(mensagem, new ElementHandleTypeOptions { Delay = _config.DelayEntreCaracteres });
#pragma warning restore CS0612

            // Delay antes de enviar
            await Task.Delay(_config.DelayAntesDeEnviar, ct);

            // Procura e clica no botão de envio
            var botaoEnvio = await _page.QuerySelectorAsync("button[aria-label*='Enviar']");
            //if (botaoEnvio is not null)
            //{
            //    await botaoEnvio.ClickAsync(new ElementHandleClickOptions { Delay = 100 });
            //}
            //else
            //{
            // Alternativa: usar Enter
            //}

            await _page.Keyboard.PressAsync("Enter");

            _logger.LogInformation("Mensagem enviada com sucesso para {Telefone}", telefone);
            return true;
        }
        catch (PlaywrightException ex)
        {
            _logger.LogError(ex, "Erro Playwright ao enviar mensagem para {Telefone}", telefone);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Envio de mensagem cancelado");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao enviar mensagem para {Telefone}", telefone);
            return false;
        }
    }

    private const string MarcadorAutenticado = "autenticado.flag";

    private static string ObterCaminhoSessao() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GestaoPagamentos",
        "WhatsAppProfile");

    private async Task SalvarSessaoAsync()
    {
        try
        {
            if (_context is null)
                return;

            var sessionPath = ObterCaminhoSessao();
            Directory.CreateDirectory(sessionPath);

            var marcador = Path.Combine(sessionPath, MarcadorAutenticado);
            await File.WriteAllTextAsync(marcador, DateTime.UtcNow.ToString("O"));

            _logger.LogInformation("Sessão marcada como autenticada. Perfil em: {SessionPath}", sessionPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao salvar sessão, mas isso não interrompe a execução");
        }
    }

    public async Task FecharAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fechando serviço Playwright WhatsApp");

            if (_page is not null)
            {
                await _page.CloseAsync();
                _page = null;
            }

            if (_context is not null)
            {
                await _context.CloseAsync();
                _context = null;
            }

            if (_browser is not null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }

            _isAuthenticated = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fechar serviço Playwright");
        }
    }

    private async Task InicializarAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Inicializando Playwright com suporte a persistência de sessão");

            var playwright = await Playwright.CreateAsync();

            var sessionPath = ObterCaminhoSessao();
            Directory.CreateDirectory(sessionPath);

            _logger.LogInformation("Perfil da sessão: {SessionPath}", sessionPath);

            var semSessaoSalva = !File.Exists(Path.Combine(sessionPath, MarcadorAutenticado));
            _primeiraSessao = semSessaoSalva;
            var headless = _config.ModoHeadless && !semSessaoSalva;

            if (semSessaoSalva)
                _logger.LogInformation("Sem sessão salva: abrindo navegador visível para escanear o QR Code");

            // Perfil persistente: mantém IndexedDB/localStorage/cookies, necessários para o login do WhatsApp Web
            _context = await playwright.Chromium.LaunchPersistentContextAsync(sessionPath, new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = headless,
                Args = _config.ArgsAdicionalBrowser?.ToList(),
                UserAgent = _config.UserAgent
            });

            _page = _context.Pages.FirstOrDefault() ?? await _context.NewPageAsync();

            _logger.LogInformation("Playwright inicializado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar Playwright");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await FecharAsync();
    }
}

public sealed class PlaywrightWhatsAppConfig
{
    public bool ModoHeadless { get; set; } = true;
    public TimeSpan TimeoutCarregamentoPagina { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan TimeoutAutenticacao { get; set; } = TimeSpan.FromSeconds(120);
    public TimeSpan TimeoutCampoDeMensagem { get; set; } = TimeSpan.FromSeconds(15);
    public int DelayAntesDeDigitar { get; set; } = 800;
    public int DelayEntreCaracteres { get; set; } = 50;
    public int DelayAntesDeEnviar { get; set; } = 1500;
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";
    public IEnumerable<string>? ArgsAdicionalBrowser { get; set; }
}
