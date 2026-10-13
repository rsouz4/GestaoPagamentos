using GestaoPagamentos.Domain.Devedores;

namespace GestaoPagamentos.Application.Devedores;

public sealed record DevedorDto(Guid Id, string Nome, string? Telefone, string? Email, string? Observacao)
{
    public static DevedorDto De(Devedor d) => new(d.Id, d.Nome, d.Telefone, d.Email, d.Observacao);
}

public sealed record SalvarDevedorCommand(Guid? Id, string Nome, string? Telefone, string? Email, string? Observacao);
