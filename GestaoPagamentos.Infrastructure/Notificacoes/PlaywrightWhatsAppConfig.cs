namespace GestaoPagamentos.Infrastructure.Notificacoes;

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
