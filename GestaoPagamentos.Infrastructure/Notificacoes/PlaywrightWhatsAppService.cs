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

            // Aguarda um tempo para ver se o QR Code desaparece (indicador de autenticação bem-sucedida)
            // O QR Code desaparece após escanear
            var qrcodeSelector = "canvas";
            var tentativasQrCode = 0;
            const int maxTentativasQrCode = 24; // 24 * 5 segundos = 2 minutos de espera
            bool qrcodeEncontrado = false;

            _logger.LogInformation("Aguardando QR Code aparecer ou autenticação...");

            while (tentativasQrCode < maxTentativasQrCode)
            {
                var qrcodePresente = await _page.QuerySelectorAsync(qrcodeSelector) is not null;

                if (qrcodePresente && !qrcodeEncontrado)
                {
                    // QR Code encontrado pela primeira vez
                    qrcodeEncontrado = true;
                    _logger.LogInformation("QR Code encontrado! Aguardando escanear...");
                }

                if (qrcodeEncontrado && !qrcodePresente)
                {
                    // QR Code FOI encontrado e agora desapareceu = autenticado!
                    _logger.LogInformation("QR Code desapareceu - autenticação bem-sucedida!");
                    await Task.Delay(3000); // Aguarda mais um pouco para página carregar chats
                    break;
                }

                if (!qrcodePresente && !qrcodeEncontrado && tentativasQrCode > 2)
                {
                    // Não encontrou QR Code por alguns segundos, pode estar já autenticado
                    _logger.LogInformation("QR Code não encontrado, verificando se já está autenticado...");
                    break;
                }

                tentativasQrCode++;

                if (qrcodeEncontrado)
                {
                    _logger.LogDebug("QR Code visível, aguardando escanear... ({Tentativa}/{Max})", tentativasQrCode, maxTentativasQrCode);
                }
                else
                {
                    _logger.LogDebug("Aguardando QR Code aparecer... ({Tentativa}/{Max})", tentativasQrCode, maxTentativasQrCode);
                }

                await Task.Delay(5000); // Aguarda 5 segundos antes de checar novamente
            }

            // Tenta múltiplos seletores para detectar autenticação
            var seletoresAutenticacao = new[]
            {
                "div[data-testid='chat-list']",           // Seletor padrão
                "div[role='region'][aria-label='Chat list']", // Alternativa
                "div[aria-label='Chats']",                // Em português
                "div[role='main']",                       // Contêiner principal autenticado
                "div[data-testid='drawer']",              // Drawer de chats
                "div[data-testid='pane-side']",           // Painel lateral de chats
                "div[role='navigation']",                 // Navegação (chats)
                "nav",                                     // Elemento nav genérico
                "div._arig"                               // Classe genérica de container
            };

            bool autenticado = false;
            string seletorEncontrado = string.Empty;

            foreach (var seletor in seletoresAutenticacao)
            {
                try
                {
                    _logger.LogDebug("Tentando seletor: {Seletor}", seletor);

                    var elemento = await _page.WaitForSelectorAsync(seletor, new PageWaitForSelectorOptions
                    {
                        Timeout = 5000 // 5 segundos por seletor
                    });

                    if (elemento is not null)
                    {
                        autenticado = true;
                        seletorEncontrado = seletor;
                        _logger.LogInformation("Elemento de autenticação encontrado com seletor: {Seletor}", seletor);
                        break;
                    }
                }
                catch (PlaywrightException ex)
                {
                    // Continua para próximo seletor
                    _logger.LogDebug("Seletor não encontrado: {Seletor} - {Exception}", seletor, ex.Message);
                }
            }

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

                if (url.Contains("web.whatsapp.com") && !url.Contains("/?code=") && !url.Contains("login"))
                {
                    _logger.LogInformation("URL sugere autenticação bem-sucedida, mesmo sem seletor encontrado");
                    autenticado = true;
                }
            }

            if (autenticado)
            {
                _isAuthenticated = true;
                _logger.LogInformation("Autenticação no WhatsApp Web verificada com sucesso (seletor: {Seletor})", seletorEncontrado);

                // Salva a sessão para reutilização futura
                await SalvarSessaoAsync();

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

    private async Task SalvarSessaoAsync()
    {
        try
        {
            if (_context is null)
                return;

            var sessionPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GestaoPagamentos",
                "WhatsAppSession"
            );

            Directory.CreateDirectory(sessionPath);

            var storageStatePath = Path.Combine(sessionPath, "storage_state.json");

            await _context.StorageStateAsync(new BrowserContextStorageStateOptions
            {
                Path = storageStatePath
            });

            _logger.LogInformation("Sessão salva com sucesso em: {StorageStatePath}", storageStatePath);
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

            var sessionPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GestaoPagamentos",
                "WhatsAppSession"
            );

            // Cria diretório se não existir
            Directory.CreateDirectory(sessionPath);

            _logger.LogInformation("Caminho da sessão: {SessionPath}", sessionPath);

            var semSessaoSalva = !File.Exists(Path.Combine(sessionPath, "storage_state.json"));
            var headless = _config.ModoHeadless && !semSessaoSalva;

            if (semSessaoSalva)
                _logger.LogInformation("Sem sessão salva: abrindo navegador visível para escanear o QR Code");

            _browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless,
                Args = _config.ArgsAdicionalBrowser?.ToList()
            });

            var contextOptions = new BrowserNewContextOptions
            {
                UserAgent = _config.UserAgent
            };

            // Se a sessão anterior existe, carrega os cookies/storage
            var storageStatePath = Path.Combine(sessionPath, "storage_state.json");
            if (File.Exists(storageStatePath))
            {
                try
                {
                    contextOptions.StorageStatePath = storageStatePath;
                    _logger.LogInformation("Carregando sessão salva de: {StorageStatePath}", storageStatePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao carregar sessão anterior, será criada uma nova");
                    contextOptions.StorageStatePath = null;
                }
            }
            else
            {
                _logger.LogInformation("Nenhuma sessão anterior encontrada. Uma nova será criada.");
            }

            _context = await _browser.NewContextAsync(contextOptions);
            _page = await _context.NewPageAsync();

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
