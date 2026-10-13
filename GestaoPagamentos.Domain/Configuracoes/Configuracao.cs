using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Configuracoes;

public class Configuracao : Entity<Guid>
{
    public const string TemplateLembretePadrao =
        "Olá, {Nome}! Lembrete: a parcela de {Valor} ({Descricao}) vence em {Vencimento}.";

    public const string TemplateAtrasoPadrao =
        "Olá, {Nome}! A parcela de {Valor} ({Descricao}), com vencimento em {Vencimento}, está em atraso. Pode me informar quando será paga?";

    public int DiasAntecedenciaLembrete { get; private set; } = 3;
    public int IntervaloEnvioLoteMs { get; private set; } = 1500;
    public string TemplateLembrete { get; private set; } = TemplateLembretePadrao;
    public string TemplateAtraso { get; private set; } = TemplateAtrasoPadrao;

    private Configuracao() { }

    public static Configuracao CriarPadrao() => new() { Id = Guid.CreateVersion7() };

    public void Atualizar(int diasAntecedenciaLembrete, int intervaloEnvioLoteMs, string templateLembrete, string templateAtraso)
    {
        if (diasAntecedenciaLembrete < 0)
            throw new DomainException("A antecedência do lembrete não pode ser negativa.");
        if (intervaloEnvioLoteMs < 0)
            throw new DomainException("O intervalo do envio em lote não pode ser negativo.");
        if (string.IsNullOrWhiteSpace(templateLembrete) || string.IsNullOrWhiteSpace(templateAtraso))
            throw new DomainException("Os templates de mensagem são obrigatórios.");

        DiasAntecedenciaLembrete = diasAntecedenciaLembrete;
        IntervaloEnvioLoteMs = intervaloEnvioLoteMs;
        TemplateLembrete = templateLembrete.Trim();
        TemplateAtraso = templateAtraso.Trim();
    }
}
