using GestaoPagamentos.Application.Pagamentos;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Tests.Servicos;

public sealed class PagamentoServiceTests : IDisposable
{
    private readonly Ambiente _a = new();
    public void Dispose() => _a.Dispose();

    private async Task<(Guid DividaId, Guid ParcelaId)> NovaDividaAsync(int dias = 10, decimal valor = 1000m)
    {
        var devedor = await _a.CriarDevedorAsync();
        var dividaId = await _a.CriarDividaAsync(devedor, valor, dias);
        var parcelaId = (await _a.ObterDividaAsync(dividaId)).Parcelas[0].Id;
        return (dividaId, parcelaId);
    }

    [Fact]
    public async Task Registrar_Parcial_MantemParcelaEDividaAbertas()
    {
        var (dividaId, parcelaId) = await NovaDividaAsync();

        var r = await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 300m, _a.Hoje, "pix"));

        Assert.True(r.Sucesso);
        Assert.False(r.Valor!.ParcelaQuitada);
        Assert.False(r.Valor.DividaQuitada);
        Assert.Equal(dividaId, r.Valor.DividaId);
        Assert.Equal("Maria", r.Valor.DevedorNome);
        Assert.Equal(1, r.Valor.NumeroParcela);
        var d = await _a.ObterDividaAsync(dividaId);
        Assert.Equal(300m, d.ValorPago);
        Assert.Equal(1000m, d.ValorRestante);
        Assert.Equal(StatusParcela.PagaParcial, d.Parcelas[0].Status);
        var pagamento = Assert.Single(d.Parcelas[0].Pagamentos);
        Assert.Equal("pix", pagamento.Observacao);
    }

    [Fact]
    public async Task Registrar_Total_QuitaParcelaEDivida()
    {
        var (dividaId, parcelaId) = await NovaDividaAsync();

        var r = await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 1300m, _a.Hoje, null));

        Assert.True(r.Sucesso);
        Assert.True(r.Valor!.ParcelaQuitada);
        Assert.True(r.Valor.DividaQuitada);
        var d = await _a.ObterDividaAsync(dividaId);
        Assert.Equal(StatusDivida.Quitada, d.Status);
        Assert.Equal(StatusParcela.Paga, d.Parcelas[0].Status);
    }

    [Fact]
    public async Task Registrar_CriaAvisoDePagamentoRecebidoJaEnviado()
    {
        var (_, parcelaId) = await NovaDividaAsync();

        await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 300m, _a.Hoje, null));

        var aviso = Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Enviada, TipoNotificacao.PagamentoRecebido));
        Assert.Contains("300,00", aviso.Mensagem);
        Assert.Contains("recebido de Maria", aviso.Mensagem);
        Assert.Contains("parcela 1", aviso.Mensagem);
        Assert.Equal(_a.Relogio.Agora, aviso.DataEnvio);
    }

    [Fact]
    public async Task Registrar_QuitandoParcela_DescartaNotificacoesPendentes()
    {
        var (_, parcelaId) = await NovaDividaAsync(dias: 1);
        Assert.Equal(1, await _a.Notificacoes.GerarAsync());

        await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 1300m, _a.Hoje, null));

        Assert.Equal(0, await _a.Notificacoes.ContarPendentesAsync());
        Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Descartada, TipoNotificacao.Lembrete));
    }

    [Fact]
    public async Task Registrar_PagamentoParcial_NaoDescartaNotificacoesPendentes()
    {
        var (_, parcelaId) = await NovaDividaAsync(dias: 1);
        await _a.Notificacoes.GerarAsync();

        await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 100m, _a.Hoje, null));

        Assert.Equal(1, await _a.Notificacoes.ContarPendentesAsync());
    }

    [Fact]
    public async Task Registrar_ExcedendoORestante_FalhaESemGravar()
    {
        var (dividaId, parcelaId) = await NovaDividaAsync();

        var r = await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 2000m, _a.Hoje, null));

        Assert.False(r.Sucesso);
        Assert.Equal("O pagamento excede o valor restante da parcela (incluindo a multa).", r.Erro);
        Assert.Equal(0m, (await _a.ObterDividaAsync(dividaId)).ValorPago);
        Assert.Empty(await _a.Notificacoes.ListarAsync(tipo: TipoNotificacao.PagamentoRecebido));
    }

    [Fact]
    public async Task Registrar_ValorZero_Falha()
    {
        var (_, parcelaId) = await NovaDividaAsync();

        var r = await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 0m, _a.Hoje, null));

        Assert.False(r.Sucesso);
        Assert.Equal("O valor do pagamento deve ser maior que zero.", r.Erro);
    }

    [Fact]
    public async Task Registrar_ParcelaInexistente_Falha()
    {
        var r = await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(Guid.NewGuid(), 10m, _a.Hoje, null));

        Assert.False(r.Sucesso);
        Assert.Equal("Parcela não encontrada.", r.Erro);
    }

    [Fact]
    public async Task Registrar_DividaCancelada_Falha()
    {
        var (dividaId, parcelaId) = await NovaDividaAsync();
        await _a.Dividas.CancelarAsync(dividaId);

        var r = await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 10m, _a.Hoje, null));

        Assert.False(r.Sucesso);
        Assert.Equal("Dívida cancelada não aceita pagamentos.", r.Erro);
    }

    [Fact]
    public async Task Excluir_RemovePagamentoEReabreDividaQuitada()
    {
        var (dividaId, parcelaId) = await NovaDividaAsync();
        await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcelaId, 1300m, _a.Hoje, null));
        var d = await _a.ObterDividaAsync(dividaId);
        Assert.Equal(StatusDivida.Quitada, d.Status);

        var r = await _a.Pagamentos.ExcluirAsync(d.Parcelas[0].Pagamentos[0].Id);

        Assert.True(r.Sucesso);
        var depois = await _a.ObterDividaAsync(dividaId);
        Assert.Equal(StatusDivida.Ativa, depois.Status);
        Assert.Equal(0m, depois.ValorPago);
        Assert.Empty(depois.Parcelas[0].Pagamentos);
    }

    [Fact]
    public async Task Excluir_Inexistente_Falha()
    {
        var r = await _a.Pagamentos.ExcluirAsync(Guid.NewGuid());

        Assert.False(r.Sucesso);
        Assert.Equal("Pagamento não encontrado.", r.Erro);
    }

    [Fact]
    public async Task Renovar_PagaOsJurosECriaNovaParcelaNoMesSeguinte()
    {
        var (dividaId, parcelaId) = await NovaDividaAsync(dias: 5);
        var antes = await _a.ObterDividaAsync(dividaId);

        var r = await _a.Pagamentos.RenovarComJurosAsync(new RenovarParcelaComJurosCommand(parcelaId, _a.Hoje));

        Assert.True(r.Sucesso);
        Assert.Equal(300m, r.Valor!.Valor);
        Assert.False(r.Valor.ParcelaQuitada);
        Assert.False(r.Valor.DividaQuitada);
        var d = await _a.ObterDividaAsync(dividaId);
        Assert.Equal(2, d.Parcelas.Count);
        Assert.Equal(300m, d.Parcelas[0].ValorPago);
        Assert.Equal("Pagamento de juros - Renovação", d.Parcelas[0].Pagamentos[0].Observacao);
        Assert.Equal(2, d.Parcelas[1].Numero);
        Assert.Equal(1300m, d.Parcelas[1].ValorPrevisto);
        Assert.Equal(antes.Parcelas[0].DataVencimento.AddMonths(1), d.Parcelas[1].DataVencimento);
        Assert.Equal(StatusDivida.Ativa, d.Status);
    }

    [Fact]
    public async Task Renovar_CriaAvisoDeJurosCobrados()
    {
        var (_, parcelaId) = await NovaDividaAsync(dias: 5);

        await _a.Pagamentos.RenovarComJurosAsync(new RenovarParcelaComJurosCommand(parcelaId, _a.Hoje));

        var aviso = Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Enviada, TipoNotificacao.PagamentoRecebido));
        Assert.Contains("Juros de", aviso.Mensagem);
        Assert.Contains("300,00", aviso.Mensagem);
        Assert.Contains("Dívida renovada", aviso.Mensagem);
    }

    [Fact]
    public async Task Renovar_ParcelaInexistente_Falha()
    {
        var r = await _a.Pagamentos.RenovarComJurosAsync(new RenovarParcelaComJurosCommand(Guid.NewGuid(), _a.Hoje));

        Assert.False(r.Sucesso);
        Assert.Equal("Parcela não encontrada.", r.Erro);
    }
}
