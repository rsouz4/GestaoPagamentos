using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Devedores;

public class Devedor : Entity<Guid>
{
    public string Nome { get; private set; } = string.Empty;
    public string? Telefone { get; private set; }
    public string? Email { get; private set; }
    public string? Observacao { get; private set; }

    private Devedor() { }

    public static Devedor Criar(string nome, string? telefone, string? email, string? observacao)
    {
        var devedor = new Devedor { Id = Guid.CreateVersion7() };
        devedor.Atualizar(nome, telefone, email, observacao);
        return devedor;
    }

    public void Atualizar(string nome, string? telefone, string? email, string? observacao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do devedor é obrigatório.");

        Nome = nome.Trim();
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }
}
