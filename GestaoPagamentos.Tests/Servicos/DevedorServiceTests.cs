using GestaoPagamentos.Application.Devedores;

namespace GestaoPagamentos.Tests.Servicos;

public sealed class DevedorServiceTests : IDisposable
{
    private readonly Ambiente _a = new();
    public void Dispose() => _a.Dispose();

    [Fact]
    public async Task Listar_RetornaOrdenadoPorNome()
    {
        await _a.CriarDevedorAsync("Zeca");
        await _a.CriarDevedorAsync("Ana");

        var lista = await _a.Devedores.ListarAsync();

        Assert.Equal(["Ana", "Zeca"], lista.Select(d => d.Nome));
    }

    [Fact]
    public async Task Listar_FiltraPorParteDoNomeSemDiferenciarMaiusculas()
    {
        await _a.CriarDevedorAsync("Maria");
        await _a.CriarDevedorAsync("Carlos");

        var lista = await _a.Devedores.ListarAsync("ARI");

        Assert.Equal(["Maria"], lista.Select(d => d.Nome));
    }

    [Fact]
    public async Task Salvar_Novo_GuardaTodosOsCampos()
    {
        var r = await _a.Devedores.SalvarAsync(
            new SalvarDevedorCommand(null, "  Joao  ", " 11999990000 ", "j@x.com", "obs"));

        Assert.True(r.Sucesso);
        Assert.Equal("Joao", r.Valor!.Nome);
        Assert.Equal("11999990000", r.Valor.Telefone);
        var salvo = Assert.Single(await _a.Devedores.ListarAsync());
        Assert.Equal(r.Valor.Id, salvo.Id);
        Assert.Equal("j@x.com", salvo.Email);
        Assert.Equal("obs", salvo.Observacao);
    }

    [Fact]
    public async Task Salvar_Existente_Atualiza()
    {
        var id = await _a.CriarDevedorAsync("Maria");

        var r = await _a.Devedores.SalvarAsync(new SalvarDevedorCommand(id, "Maria Silva", null, null, null));

        Assert.True(r.Sucesso);
        var salvo = Assert.Single(await _a.Devedores.ListarAsync());
        Assert.Equal("Maria Silva", salvo.Nome);
        Assert.Null(salvo.Telefone);
    }

    [Fact]
    public async Task Salvar_IdInexistente_Falha()
    {
        var r = await _a.Devedores.SalvarAsync(new SalvarDevedorCommand(Guid.NewGuid(), "X", null, null, null));

        Assert.False(r.Sucesso);
        Assert.Equal("Devedor não encontrado.", r.Erro);
    }

    [Fact]
    public async Task Salvar_NomeVazio_Falha()
    {
        var r = await _a.Devedores.SalvarAsync(new SalvarDevedorCommand(null, "   ", null, null, null));

        Assert.False(r.Sucesso);
        Assert.Equal("O nome do devedor é obrigatório.", r.Erro);
        Assert.Empty(await _a.Devedores.ListarAsync());
    }

    [Fact]
    public async Task Excluir_SemDividas_Remove()
    {
        var id = await _a.CriarDevedorAsync();

        var r = await _a.Devedores.ExcluirAsync(id);

        Assert.True(r.Sucesso);
        Assert.Empty(await _a.Devedores.ListarAsync());
    }

    [Fact]
    public async Task Excluir_ComDividaAtiva_Falha()
    {
        var id = await _a.CriarDevedorAsync();
        await _a.CriarDividaAsync(id);

        var r = await _a.Devedores.ExcluirAsync(id);

        Assert.False(r.Sucesso);
        Assert.Equal("Não é possível excluir um devedor com dívidas ativas.", r.Erro);
        Assert.Single(await _a.Devedores.ListarAsync());
    }

    [Fact]
    public async Task Excluir_ComDividaCancelada_RemoveDevedorEDivida()
    {
        var id = await _a.CriarDevedorAsync();
        var dividaId = await _a.CriarDividaAsync(id);
        Assert.True((await _a.Dividas.CancelarAsync(dividaId)).Sucesso);

        var r = await _a.Devedores.ExcluirAsync(id);

        Assert.True(r.Sucesso);
        Assert.Empty(await _a.Devedores.ListarAsync());
        Assert.False((await _a.Dividas.ObterAsync(dividaId)).Sucesso);
    }

    [Fact]
    public async Task Excluir_Inexistente_Falha()
    {
        var r = await _a.Devedores.ExcluirAsync(Guid.NewGuid());

        Assert.False(r.Sucesso);
        Assert.Equal("Devedor não encontrado.", r.Erro);
    }
}
