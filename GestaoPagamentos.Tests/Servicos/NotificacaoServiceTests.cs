using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Application.Pagamentos;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Tests.Servicos;

public sealed class NotificacaoServiceTests : IDisposable
{
    private readonly Ambiente _a = new();
    public void Dispose() => _a.Dispose();

    [Fact]
    public async Task Gerar_ParcelaVencida_CriaNotificacaoDeAtrasoComMulta()
    {
        var devedor = await _a.CriarDevedorAsync("Maria");
        await _a.CriarDividaAsync(devedor, 1000m, diasAteVencimento: -2, descricao: "Moto");

        var geradas = await _a.Notificacoes.GerarAsync();

        Assert.Equal(1, geradas);
        var n = Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Pendente));
        Assert.Equal(TipoNotificacao.Atraso, n.Tipo);
        Assert.Contains("está em atraso", n.Mensagem);
        Assert.Contains("Olá, Maria!", n.Mensagem);
        Assert.Contains("1.690,00", n.Mensagem);
        Assert.Equal("Moto", n.Descricao);
        Assert.Equal(1, n.NumeroParcela);
        Assert.Equal(1690m, n.Valor);
        Assert.Equal(_a.Hoje.AddDays(-2), n.Vencimento);
        Assert.Equal("11987654321", n.Telefone);
        Assert.Equal(_a.Relogio.Agora, n.DataGeracao);
    }

    [Fact]
    public async Task Gerar_VenceHoje_CriaNotificacaoDeVencimento()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: 0);

        await _a.Notificacoes.GerarAsync();

        var n = Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Pendente));
        Assert.Equal(TipoNotificacao.Vencimento, n.Tipo);
        Assert.Contains("vence em 25/11/2026", n.Mensagem);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 0)]
    public async Task Gerar_Lembrete_SoDentroDaAntecedenciaConfigurada(int dias, int esperadas)
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: dias);

        var geradas = await _a.Notificacoes.GerarAsync();

        Assert.Equal(esperadas, geradas);
        if (esperadas == 1)
            Assert.Equal(TipoNotificacao.Lembrete, Assert.Single(await _a.Notificacoes.ListarAsync()).Tipo);
    }

    [Fact]
    public async Task Gerar_RespeitaAAntecedenciaDaConfiguracao()
    {
        await _a.Configuracoes.SalvarAsync(new ConfiguracaoDto(7, 1500, "L {Nome}", "A {Nome}"));
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: 7);

        Assert.Equal(1, await _a.Notificacoes.GerarAsync());
        Assert.Equal("L Maria", Assert.Single(await _a.Notificacoes.ListarAsync()).Mensagem);
    }

    [Fact]
    public async Task Gerar_ChamadoDuasVezes_NaoDuplicaNotificacoes()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);

        Assert.Equal(1, await _a.Notificacoes.GerarAsync());
        Assert.Equal(0, await _a.Notificacoes.GerarAsync());
        Assert.Single(await _a.Notificacoes.ListarAsync());
    }

    [Fact]
    public async Task Gerar_QuandoALembreteVence_DescartaOLembreteECriaOAtraso()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: 1);
        Assert.Equal(1, await _a.Notificacoes.GerarAsync());

        _a.Relogio.Agora = _a.Relogio.Agora.AddDays(2);
        var geradas = await _a.Notificacoes.GerarAsync();

        Assert.Equal(1, geradas);
        var descartada = Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Descartada));
        Assert.Equal(TipoNotificacao.Lembrete, descartada.Tipo);
        var pendente = Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Pendente));
        Assert.Equal(TipoNotificacao.Atraso, pendente.Tipo);
    }

    [Fact]
    public async Task Gerar_IgnoraDividasCanceladasEQuitadas()
    {
        var devedor = await _a.CriarDevedorAsync();
        var cancelada = await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        var quitada = await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Dividas.CancelarAsync(cancelada);
        var parcela = (await _a.ObterDividaAsync(quitada)).Parcelas[0];
        var pagamento = await _a.Pagamentos.RegistrarAsync(
            new RegistrarPagamentoCommand(parcela.Id, parcela.ValorRestante, _a.Hoje, null));
        Assert.True(pagamento.Sucesso, pagamento.Erro);

        Assert.Equal(0, await _a.Notificacoes.GerarAsync());
    }

    [Fact]
    public async Task ContarPendentes_ConsideraSoAsPendentes()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -3);
        await _a.Notificacoes.GerarAsync();
        Assert.Equal(2, await _a.Notificacoes.ContarPendentesAsync());

        var primeira = (await _a.Notificacoes.ListarAsync()).First();
        await _a.Notificacoes.IgnorarAsync(primeira.Id);

        Assert.Equal(1, await _a.Notificacoes.ContarPendentesAsync());
    }

    [Fact]
    public async Task Listar_SemNotificacoes_RetornaVazio()
    {
        Assert.Empty(await _a.Notificacoes.ListarAsync());
    }

    [Fact]
    public async Task Listar_FiltraPorTipoEStatus_EOrdenaPelaMaisRecente()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        _a.Relogio.Agora = _a.Relogio.Agora.AddHours(1);
        await _a.CriarDividaAsync(devedor, diasAteVencimento: 1);
        await _a.Notificacoes.GerarAsync();

        var todas = await _a.Notificacoes.ListarAsync();
        var soAtraso = await _a.Notificacoes.ListarAsync(tipo: TipoNotificacao.Atraso);
        var enviadas = await _a.Notificacoes.ListarAsync(StatusNotificacao.Enviada);

        Assert.Equal(2, todas.Count);
        Assert.Equal(TipoNotificacao.Lembrete, todas[0].Tipo);
        Assert.Equal(TipoNotificacao.Atraso, Assert.Single(soAtraso).Tipo);
        Assert.Empty(enviadas);
    }

    [Fact]
    public async Task Enviar_AbreOLinkDoWhatsAppEMarcaComoEnviada()
    {
        var devedor = await _a.CriarDevedorAsync("Maria", "(11) 98765-4321");
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var n = Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Pendente));

        var r = await _a.Notificacoes.EnviarAsync(n.Id);

        Assert.True(r.Sucesso);
        var link = Assert.Single(_a.Launcher.Abertos);
        Assert.StartsWith("https://wa.me/5511987654321?text=", link);
        Assert.Contains(Uri.EscapeDataString(n.Mensagem), link);
        var enviada = Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Enviada));
        Assert.Equal(_a.Relogio.Agora, enviada.DataEnvio);
    }

    [Fact]
    public async Task Enviar_ComMensagemEditada_UsaOTextoNovo()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var n = Assert.Single(await _a.Notificacoes.ListarAsync());

        var r = await _a.Notificacoes.EnviarAsync(n.Id, "  Texto novo  ");

        Assert.True(r.Sucesso);
        Assert.EndsWith(Uri.EscapeDataString("Texto novo"), Assert.Single(_a.Launcher.Abertos));
        Assert.Equal("Texto novo", Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Enviada)).Mensagem);
    }

    [Fact]
    public async Task Enviar_TelefoneInvalido_FalhaEMantemPendente()
    {
        var devedor = await _a.CriarDevedorAsync("Maria", "123");
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var n = Assert.Single(await _a.Notificacoes.ListarAsync());

        var r = await _a.Notificacoes.EnviarAsync(n.Id);

        Assert.False(r.Sucesso);
        Assert.Equal("Telefone inválido ou ausente para Maria.", r.Erro);
        Assert.Empty(_a.Launcher.Abertos);
        Assert.Equal(1, await _a.Notificacoes.ContarPendentesAsync());
    }

    [Fact]
    public async Task Enviar_QuandoNaoEstaPendente_Falha()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var n = Assert.Single(await _a.Notificacoes.ListarAsync());
        Assert.True((await _a.Notificacoes.EnviarAsync(n.Id)).Sucesso);

        var r = await _a.Notificacoes.EnviarAsync(n.Id);

        Assert.False(r.Sucesso);
        Assert.Equal("A notificação não está pendente.", r.Erro);
        Assert.Single(_a.Launcher.Abertos);
    }

    [Fact]
    public async Task Enviar_Inexistente_Falha()
    {
        var r = await _a.Notificacoes.EnviarAsync(Guid.NewGuid());

        Assert.False(r.Sucesso);
        Assert.Equal("Notificação não encontrada.", r.Erro);
    }

    [Fact]
    public async Task Ignorar_MarcaComoIgnorada()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var n = Assert.Single(await _a.Notificacoes.ListarAsync());

        var r = await _a.Notificacoes.IgnorarAsync(n.Id);

        Assert.True(r.Sucesso);
        Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Ignorada));
        Assert.Equal(0, await _a.Notificacoes.ContarPendentesAsync());
    }

    [Fact]
    public async Task Ignorar_Inexistente_Falha()
    {
        var r = await _a.Notificacoes.IgnorarAsync(Guid.NewGuid());

        Assert.False(r.Sucesso);
        Assert.Equal("Notificação não encontrada.", r.Erro);
    }

    [Fact]
    public async Task PrepararLote_TrazSoAtrasosPendentes()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.CriarDividaAsync(devedor, diasAteVencimento: 1);
        await _a.Notificacoes.GerarAsync();

        var lote = await _a.Notificacoes.PrepararLoteAtrasadosAsync();

        Assert.Equal(TipoNotificacao.Atraso, Assert.Single(lote).Tipo);
    }

    [Fact]
    public async Task EnviarLote_ComIntervaloZero_EnviaTodosEReportaProgresso()
    {
        await _a.Configuracoes.SalvarAsync(new ConfiguracaoDto(3, 0, "L", "A"));
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -2);
        await _a.Notificacoes.GerarAsync();
        var lote = await _a.Notificacoes.PrepararLoteAtrasadosAsync();
        var progresso = new List<int>();

        var r = await _a.Notificacoes.EnviarLoteAsync(lote, new Progress<GestaoPagamentos.Application.Notificacoes.LoteProgresso>(p => progresso.Add(p.Atual)));
        await Task.Delay(50);

        Assert.Equal(2, r.Enviadas);
        Assert.Equal(0, r.Puladas);
        Assert.False(r.Cancelado);
        Assert.Equal(2, _a.Launcher.Abertos.Count);
        Assert.Equal(0, await _a.Notificacoes.ContarPendentesAsync());
    }

    [Fact]
    public async Task EnviarLote_ContaComoPuladaQuemNaoTemTelefoneValido()
    {
        await _a.Configuracoes.SalvarAsync(new ConfiguracaoDto(3, 0, "L", "A"));
        var bom = await _a.CriarDevedorAsync("Bom", "11987654321");
        var ruim = await _a.CriarDevedorAsync("Ruim", null);
        await _a.CriarDividaAsync(bom, diasAteVencimento: -1);
        await _a.CriarDividaAsync(ruim, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var lote = await _a.Notificacoes.PrepararLoteAtrasadosAsync();

        var r = await _a.Notificacoes.EnviarLoteAsync(lote);

        Assert.Equal(1, r.Enviadas);
        Assert.Equal(1, r.Puladas);
    }

    [Fact]
    public async Task EnviarViaWhatsAppWeb_Sucesso_EnviaTelefoneNormalizadoEMarcaComoEnviada()
    {
        var devedor = await _a.CriarDevedorAsync("Maria", "11987654321");
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var n = Assert.Single(await _a.Notificacoes.ListarAsync());

        var ok = await _a.Notificacoes.EnviarViaWhatsAppWebAsync(n.Id, _a.Automacao);

        Assert.True(ok);
        var enviado = Assert.Single(_a.Automacao.Enviadas);
        Assert.Equal("5511987654321", enviado.Telefone);
        Assert.Equal(n.Mensagem, enviado.Mensagem);
        Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Enviada));
    }

    [Fact]
    public async Task EnviarViaWhatsAppWeb_QuandoAutomacaoFalha_RetornaFalsoEMantemPendente()
    {
        _a.Automacao.Resultado = false;
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var n = Assert.Single(await _a.Notificacoes.ListarAsync());

        var ok = await _a.Notificacoes.EnviarViaWhatsAppWebAsync(n.Id, _a.Automacao);

        Assert.False(ok);
        Assert.Equal(1, await _a.Notificacoes.ContarPendentesAsync());
    }

    [Fact]
    public async Task EnviarViaWhatsAppWeb_TelefoneInvalido_Lanca()
    {
        var devedor = await _a.CriarDevedorAsync("Maria", "123");
        await _a.CriarDividaAsync(devedor, diasAteVencimento: -1);
        await _a.Notificacoes.GerarAsync();
        var n = Assert.Single(await _a.Notificacoes.ListarAsync());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _a.Notificacoes.EnviarViaWhatsAppWebAsync(n.Id, _a.Automacao));

        Assert.Equal("Telefone inválido para o devedor Maria", ex.Message);
        Assert.Empty(_a.Automacao.Enviadas);
    }

    [Fact]
    public async Task EnviarViaWhatsAppWeb_NotificacaoInexistente_Lanca()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _a.Notificacoes.EnviarViaWhatsAppWebAsync(Guid.NewGuid(), _a.Automacao));

        Assert.Equal("Notificação não encontrada.", ex.Message);
    }
}
