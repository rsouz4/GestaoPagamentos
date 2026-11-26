using GestaoPagamentos.Application.Pagamentos;

namespace GestaoPagamentos.Tests.Servicos;

public sealed class RelatorioServiceTests : IDisposable
{
    private readonly Ambiente _a = new();
    public void Dispose() => _a.Dispose();

    private async Task<Guid> ParcelaDeAsync(Guid dividaId) => (await _a.ObterDividaAsync(dividaId)).Parcelas[0].Id;

    [Fact]
    public async Task ObterResumo_SemDados_ZeraTudo()
    {
        var r = await _a.Relatorios.ObterResumoAsync();

        Assert.Equal(0m, r.TotalARreceber);
        Assert.Equal(0m, r.TotalRecebido);
        Assert.Equal(0m, r.TotalEmAtraso);
        Assert.Equal(0, r.DividasAtivas);
        Assert.Equal(0, r.ParcelasPendentes);
        Assert.Equal(0, r.ParcelasPagasParcialmente);
        Assert.Equal(0, r.ParcelasAtrasadas);
    }

    [Fact]
    public async Task ObterResumo_SomaAReceberEAtrasoComMultaEIgnoraCanceladas()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, 1000m, diasAteVencimento: 10);
        await _a.CriarDividaAsync(devedor, 1000m, diasAteVencimento: -2);
        var cancelada = await _a.CriarDividaAsync(devedor, 500m, diasAteVencimento: 5);
        await _a.Dividas.CancelarAsync(cancelada);

        var r = await _a.Relatorios.ObterResumoAsync();

        Assert.Equal(2, r.DividasAtivas);
        Assert.Equal(1, r.ParcelasPendentes);
        Assert.Equal(0, r.ParcelasPagasParcialmente);
        Assert.Equal(1, r.ParcelasAtrasadas);
        Assert.Equal(1690m, r.TotalEmAtraso);
        Assert.Equal(2990m, r.TotalARreceber);
        Assert.Equal(0m, r.TotalRecebido);
    }

    [Fact]
    public async Task ObterResumo_PagamentoParcialMovePendenteParaPagaParcialmente()
    {
        var devedor = await _a.CriarDevedorAsync();
        var aberta = await _a.CriarDividaAsync(devedor, 1000m, diasAteVencimento: 10);
        await _a.CriarDividaAsync(devedor, 1000m, diasAteVencimento: -2);
        var pagamento = await _a.Pagamentos.RegistrarAsync(
            new RegistrarPagamentoCommand(await ParcelaDeAsync(aberta), 300m, _a.Hoje, null));
        Assert.True(pagamento.Sucesso, pagamento.Erro);

        var r = await _a.Relatorios.ObterResumoAsync();

        Assert.Equal(0, r.ParcelasPendentes);
        Assert.Equal(1, r.ParcelasPagasParcialmente);
        Assert.Equal(300m, r.TotalRecebido);
        Assert.Equal(2690m, r.TotalARreceber);
    }

    [Fact]
    public async Task ListarAtrasados_OrdenaPorDiasDeAtrasoEIncluiDadosDoDevedor()
    {
        var maria = await _a.CriarDevedorAsync("Maria", "11987654321");
        var joao = await _a.CriarDevedorAsync("Joao", "11911112222");
        await _a.CriarDividaAsync(maria, 1000m, diasAteVencimento: -2, descricao: "Moto");
        await _a.CriarDividaAsync(joao, 100m, diasAteVencimento: -5, descricao: "Celular");
        await _a.CriarDividaAsync(maria, 1000m, diasAteVencimento: 10, descricao: "Futura");

        var lista = await _a.Relatorios.ListarAtrasadosAsync();

        Assert.Equal([5, 2], lista.Select(x => x.DiasAtraso));
        Assert.Equal("Joao", lista[0].DevedorNome);
        Assert.Equal("11911112222", lista[0].Telefone);
        Assert.Equal("Celular", lista[0].Descricao);
        Assert.Equal(227.50m, lista[0].ValorRestante);
        Assert.Equal(_a.Hoje.AddDays(-5), lista[0].DataVencimento);
        Assert.Equal("Maria", lista[1].DevedorNome);
    }

    [Fact]
    public async Task ListarAtrasados_IgnoraDividasCanceladas()
    {
        var devedor = await _a.CriarDevedorAsync();
        var id = await _a.CriarDividaAsync(devedor, diasAteVencimento: -3);
        await _a.Dividas.CancelarAsync(id);

        Assert.Empty(await _a.Relatorios.ListarAtrasadosAsync());
    }

    [Fact]
    public async Task ListarHistorico_OrdenaPorDataDecrescenteEFiltra()
    {
        var maria = await _a.CriarDevedorAsync("Maria");
        var joao = await _a.CriarDevedorAsync("Joao", "11911112222");
        var divida = await _a.CriarDividaAsync(maria, 1000m, diasAteVencimento: 10, descricao: "Moto");
        var outra = await _a.CriarDividaAsync(joao, 1000m, diasAteVencimento: 10);
        var parcela = await ParcelaDeAsync(divida);
        await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcela, 100m, _a.Hoje.AddDays(-1), "primeiro"));
        await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(parcela, 200m, _a.Hoje, "segundo"));
        await _a.Pagamentos.RegistrarAsync(new RegistrarPagamentoCommand(await ParcelaDeAsync(outra), 50m, _a.Hoje, null));

        var todos = await _a.Relatorios.ListarHistoricoPagamentosAsync();
        var daDivida = await _a.Relatorios.ListarHistoricoPagamentosAsync(dividaId: divida);
        var doJoao = await _a.Relatorios.ListarHistoricoPagamentosAsync(devedorId: joao);

        Assert.Equal(3, todos.Count);
        Assert.Equal(["segundo", "primeiro"], daDivida.Select(h => h.Observacao));
        Assert.Equal([200m, 100m], daDivida.Select(h => h.Valor));
        Assert.All(daDivida, h => Assert.Equal("Maria", h.DevedorNome));
        Assert.All(daDivida, h => Assert.Equal("Moto", h.Descricao));
        Assert.Equal(50m, Assert.Single(doJoao).Valor);
    }
}
