using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Tests.Servicos;

public sealed class DividaServiceTests : IDisposable
{
    private readonly Ambiente _a = new();
    public void Dispose() => _a.Dispose();

    [Fact]
    public async Task Criar_DevedorInexistente_Falha()
    {
        var r = await _a.Dividas.CriarAsync(new CriarDividaCommand(
            Guid.NewGuid(), "x", 100m, _a.Hoje, TipoDivida.Avulsa, 1, null));

        Assert.False(r.Sucesso);
        Assert.Equal("Devedor não encontrado.", r.Erro);
    }

    [Fact]
    public async Task Criar_ValorZero_Falha()
    {
        var devedor = await _a.CriarDevedorAsync();

        var r = await _a.Dividas.CriarAsync(new CriarDividaCommand(
            devedor, "x", 0m, _a.Hoje, TipoDivida.Avulsa, 1, null));

        Assert.False(r.Sucesso);
        Assert.Equal("O valor emprestado deve ser maior que zero.", r.Erro);
        Assert.Empty(await _a.Dividas.ListarAsync());
    }

    [Fact]
    public async Task Criar_Parcelada_GeraParcelasMensaisQueSomamOValorTotal()
    {
        var devedor = await _a.CriarDevedorAsync();

        var id = await _a.CriarDividaAsync(devedor, 1000m, diasAteVencimento: 10, TipoDivida.Parcelada, parcelas: 3);

        var d = await _a.ObterDividaAsync(id);
        Assert.Equal(1300m, d.ValorTotal);
        Assert.Equal(0.30m, d.TaxaJuros);
        Assert.Equal([433.33m, 433.33m, 433.34m], d.Parcelas.Select(p => p.ValorPrevisto));
        Assert.Equal([1, 2, 3], d.Parcelas.Select(p => p.Numero));
        var primeiro = _a.Hoje.AddDays(10);
        Assert.Equal(
            [primeiro, primeiro.AddMonths(1), primeiro.AddMonths(2)],
            d.Parcelas.Select(p => p.DataVencimento));
    }

    [Fact]
    public async Task Criar_Avulsa_IgnoraQuantidadeEGeraUmaParcela()
    {
        var devedor = await _a.CriarDevedorAsync();

        var id = await _a.CriarDividaAsync(devedor, 1000m, tipo: TipoDivida.Avulsa, parcelas: 5);

        var d = await _a.ObterDividaAsync(id);
        Assert.Single(d.Parcelas);
        Assert.Equal(1300m, d.Parcelas[0].ValorPrevisto);
    }

    [Fact]
    public async Task Obter_TrazDadosDoDevedor()
    {
        var devedor = await _a.CriarDevedorAsync("Maria", "11987654321");
        var id = await _a.CriarDividaAsync(devedor, descricao: "Moto");

        var d = await _a.ObterDividaAsync(id);

        Assert.Equal("Maria", d.DevedorNome);
        Assert.Equal("11987654321", d.DevedorTelefone);
        Assert.Equal("Moto", d.Descricao);
        Assert.Equal(StatusDivida.Ativa, d.Status);
        Assert.Equal(1300m, d.ValorRestante);
        Assert.Equal(0m, d.ValorPago);
    }

    [Fact]
    public async Task Obter_Inexistente_Falha()
    {
        var r = await _a.Dividas.ObterAsync(Guid.NewGuid());

        Assert.False(r.Sucesso);
        Assert.Equal("Dívida não encontrada.", r.Erro);
    }

    [Fact]
    public async Task Listar_OrdenaPeloPrimeiroVencimentoDecrescente()
    {
        var devedor = await _a.CriarDevedorAsync();
        var cedo = await _a.CriarDividaAsync(devedor, diasAteVencimento: 5);
        var tarde = await _a.CriarDividaAsync(devedor, diasAteVencimento: 20);

        var lista = await _a.Dividas.ListarAsync();

        Assert.Equal([tarde, cedo], lista.Select(d => d.Id));
        Assert.All(lista, d => Assert.Equal("Maria", d.DevedorNome));
    }

    [Fact]
    public async Task Listar_FiltraPorStatusEPorDevedor()
    {
        var maria = await _a.CriarDevedorAsync("Maria");
        var joao = await _a.CriarDevedorAsync("Joao", "11911112222");
        var ativa = await _a.CriarDividaAsync(maria);
        var cancelada = await _a.CriarDividaAsync(maria);
        var doJoao = await _a.CriarDividaAsync(joao);
        Assert.True((await _a.Dividas.CancelarAsync(cancelada)).Sucesso);

        var soCanceladas = await _a.Dividas.ListarAsync(StatusDivida.Cancelada);
        var soMaria = await _a.Dividas.ListarAsync(devedorId: maria);
        var ativasDoJoao = await _a.Dividas.ListarAsync(StatusDivida.Ativa, joao);

        Assert.Equal([cancelada], soCanceladas.Select(d => d.Id));
        Assert.Equal(2, soMaria.Count);
        Assert.Equal([doJoao], ativasDoJoao.Select(d => d.Id));
        Assert.Contains(ativa, soMaria.Select(d => d.Id));
    }

    [Fact]
    public async Task Listar_CalculaResumoDaDivida()
    {
        var devedor = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(devedor, 1000m, diasAteVencimento: -2);

        var resumo = Assert.Single(await _a.Dividas.ListarAsync());

        Assert.Equal(1300m, resumo.ValorTotal);
        Assert.Equal(1, resumo.ParcelasAtrasadas);
        Assert.Equal(1690m, resumo.ValorRestante);
        Assert.Equal(_a.Hoje.AddDays(-2), resumo.ProximoVencimento);
    }

    [Fact]
    public async Task Atualizar_AlteraDescricaoEObservacao()
    {
        var devedor = await _a.CriarDevedorAsync();
        var id = await _a.CriarDividaAsync(devedor, descricao: "Antiga");

        var r = await _a.Dividas.AtualizarAsync(new AtualizarDividaCommand(id, "Nova", "obs"));

        Assert.True(r.Sucesso);
        var d = await _a.ObterDividaAsync(id);
        Assert.Equal("Nova", d.Descricao);
        Assert.Equal("obs", d.Observacao);
    }

    [Fact]
    public async Task Atualizar_DescricaoVazia_Falha()
    {
        var devedor = await _a.CriarDevedorAsync();
        var id = await _a.CriarDividaAsync(devedor);

        var r = await _a.Dividas.AtualizarAsync(new AtualizarDividaCommand(id, " ", null));

        Assert.False(r.Sucesso);
        Assert.Equal("A descrição da dívida é obrigatória.", r.Erro);
    }

    [Fact]
    public async Task Atualizar_Inexistente_Falha()
    {
        var r = await _a.Dividas.AtualizarAsync(new AtualizarDividaCommand(Guid.NewGuid(), "x", null));

        Assert.False(r.Sucesso);
        Assert.Equal("Dívida não encontrada.", r.Erro);
    }

    [Fact]
    public async Task AlterarTaxaJuros_RecalculaTotalEParcelasSemPagamento()
    {
        var devedor = await _a.CriarDevedorAsync();
        var id = await _a.CriarDividaAsync(devedor, 1000m);

        var r = await _a.Dividas.AlterarTaxaJurosAsync(id, 0.5m);

        Assert.True(r.Sucesso);
        var d = await _a.ObterDividaAsync(id);
        Assert.Equal(0.5m, d.TaxaJuros);
        Assert.Equal(1500m, d.ValorTotal);
        Assert.Equal(1500m, d.Parcelas[0].ValorPrevisto);
    }

    [Fact]
    public async Task AlterarTaxaJuros_Negativa_Falha()
    {
        var devedor = await _a.CriarDevedorAsync();
        var id = await _a.CriarDividaAsync(devedor);

        var r = await _a.Dividas.AlterarTaxaJurosAsync(id, -0.1m);

        Assert.False(r.Sucesso);
        Assert.Equal("A taxa de juros não pode ser negativa.", r.Erro);
    }

    [Fact]
    public async Task Cancelar_MarcaCanceladaEDescartaNotificacoesPendentes()
    {
        var devedor = await _a.CriarDevedorAsync();
        var id = await _a.CriarDividaAsync(devedor, diasAteVencimento: 2);
        Assert.Equal(1, await _a.Notificacoes.GerarAsync());
        Assert.Equal(1, await _a.Notificacoes.ContarPendentesAsync());

        var r = await _a.Dividas.CancelarAsync(id);

        Assert.True(r.Sucesso);
        Assert.Equal(StatusDivida.Cancelada, (await _a.ObterDividaAsync(id)).Status);
        Assert.Equal(0, await _a.Notificacoes.ContarPendentesAsync());
        Assert.Single(await _a.Notificacoes.ListarAsync(StatusNotificacao.Descartada));
    }

    [Fact]
    public async Task Cancelar_Inexistente_Falha()
    {
        var r = await _a.Dividas.CancelarAsync(Guid.NewGuid());

        Assert.False(r.Sucesso);
        Assert.Equal("Dívida não encontrada.", r.Erro);
    }
}
