// Passo 4 — gera um script SQL (INSERTs) a partir do SQLite atual, para colar
// no SQL Editor do Supabase depois de rodar db/supabase/01_schema.sql.
//
// Só LÊ o banco (Mode=ReadOnly) e não precisa de credencial do Supabase:
// o resultado é um arquivo .sql que você revisa antes de rodar.
// A saída contém dados reais de devedores: NÃO commitar (db/import/ está no .gitignore).
//
// Uso (a partir da raiz do repositório):
//   dotnet run db/tools/ExportarSqlite.cs -- "<gestao_pagamentos.db>" "<user_id uuid>" "db/import/02_import.sql"
//
// <user_id> é o id do seu usuário em Authentication > Users no painel do Supabase.
#:package Microsoft.Data.Sqlite@10.0.12

using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

if (args.Length < 3)
{
    Console.Error.WriteLine("Uso: dotnet run db/tools/ExportarSqlite.cs -- <arquivo.db> <user_id uuid> <saida.sql>");
    return 1;
}

var dbPath = args[0];
if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"Arquivo não encontrado: {dbPath}");
    return 1;
}
if (!Guid.TryParse(args[1], out var userId))
{
    Console.Error.WriteLine("O user_id precisa ser um uuid válido.");
    return 1;
}
var outPath = args[2];
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

var inv = CultureInfo.InvariantCulture;

// (tabela SQLite, tabela Postgres, colunas) — ordem respeita as chaves estrangeiras.
var tables = new (string Sqlite, string Pg, (string Src, string Dst, Kind Kind)[] Cols)[]
{
    ("devedores", "devedores",
    [
        ("Id", "id", Kind.Guid), ("Nome", "nome", Kind.Text), ("Telefone", "telefone", Kind.TextN),
        ("Email", "email", Kind.TextN), ("Observacao", "observacao", Kind.TextN),
    ]),
    ("dividas", "dividas",
    [
        ("Id", "id", Kind.Guid), ("DevedorId", "devedor_id", Kind.Guid), ("Descricao", "descricao", Kind.Text),
        ("ValorEmprestado", "valor_emprestado", Kind.Dec), ("TaxaJuros", "taxa_juros", Kind.Dec),
        ("ValorTotal", "valor_total", Kind.Dec), ("DataPrimeiroVencimento", "data_primeiro_vencimento", Kind.Date),
        ("DataEmprestimo", "data_emprestimo", Kind.Date), ("Tipo", "tipo", Kind.Int),
        ("QuantidadeParcelas", "quantidade_parcelas", Kind.Int), ("Status", "status", Kind.Int),
        ("Observacao", "observacao", Kind.TextN),
    ]),
    ("parcelas", "parcelas",
    [
        ("Id", "id", Kind.Guid), ("DividaId", "divida_id", Kind.Guid), ("Numero", "numero", Kind.Int),
        ("ValorPrevisto", "valor_previsto", Kind.Dec), ("DataVencimento", "data_vencimento", Kind.Date),
    ]),
    ("pagamentos", "pagamentos",
    [
        ("Id", "id", Kind.Guid), ("ParcelaId", "parcela_id", Kind.Guid), ("Valor", "valor", Kind.Dec),
        ("DataPagamento", "data_pagamento", Kind.Date), ("Observacao", "observacao", Kind.TextN),
    ]),
    ("notificacoes", "notificacoes",
    [
        ("Id", "id", Kind.Guid), ("DevedorId", "devedor_id", Kind.Guid), ("ParcelaId", "parcela_id", Kind.GuidN),
        ("Tipo", "tipo", Kind.Int), ("Mensagem", "mensagem", Kind.Text),
        ("DataGeracao", "data_geracao", Kind.Ts), ("DataEnvio", "data_envio", Kind.TsN), ("Status", "status", Kind.Int),
    ]),
    ("configuracoes", "configuracoes",
    [
        ("Id", "id", Kind.Guid), ("DiasAntecedenciaLembrete", "dias_antecedencia_lembrete", Kind.Int),
        ("IntervaloEnvioLoteMs", "intervalo_envio_lote_ms", Kind.Int),
        ("TemplateLembrete", "template_lembrete", Kind.Text), ("TemplateAtraso", "template_atraso", Kind.Text),
    ]),
};

var sql = new StringBuilder();
sql.AppendLine("-- Gerado por db/tools/ExportarSqlite.cs — contém dados reais, não commitar.");
sql.AppendLine("-- Rodar no SQL Editor do Supabase DEPOIS de db/supabase/01_schema.sql.");
sql.AppendLine("begin;");

// Para a conferência de totais por devedor (calculada em decimal, sem arredondamento de ponto flutuante).
var nomePorDevedor = new Dictionary<Guid, string>();
var dividaDoDevedor = new Dictionary<Guid, Guid>();   // dividaId -> devedorId
var devedorDaParcela = new Dictionary<Guid, Guid>();  // parcelaId -> devedorId
var dividasPorDevedor = new Dictionary<Guid, int>();
var totalPorDevedor = new Dictionary<Guid, decimal>();
var pagoPorDevedor = new Dictionary<Guid, decimal>();
var counts = new List<(string Tabela, int Linhas)>();

using var con = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
con.Open();

foreach (var (src, pg, cols) in tables)
{
    using var cmd = con.CreateCommand();
    cmd.CommandText = $"select {string.Join(", ", cols.Select(c => $"\"{c.Src}\""))} from \"{src}\"";
    using var r = cmd.ExecuteReader();
    var n = 0;
    var colList = "user_id, " + string.Join(", ", cols.Select(c => c.Dst));
    while (r.Read())
    {
        var vals = new List<string> { Q(userId.ToString("D")) };
        string rowId = r.IsDBNull(0) ? "?" : Convert.ToString(r.GetValue(0), inv)!;
        try
        {
            for (var i = 0; i < cols.Length; i++)
                vals.Add(Format(r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i), inv), cols[i].Kind));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Tabela {src}, linha Id={rowId}: {ex.Message}", ex);
        }
        sql.AppendLine($"insert into public.{pg} ({colList}) values ({string.Join(", ", vals)});");
        n++;

        switch (src)
        {
            case "devedores":
                nomePorDevedor[Guid.Parse(rowId)] = Convert.ToString(r.GetValue(1), inv)!;
                break;
            case "dividas":
            {
                var dev = Guid.Parse(Convert.ToString(r.GetValue(1), inv)!);
                dividaDoDevedor[Guid.Parse(rowId)] = dev;
                dividasPorDevedor[dev] = dividasPorDevedor.GetValueOrDefault(dev) + 1;
                totalPorDevedor[dev] = totalPorDevedor.GetValueOrDefault(dev)
                    + decimal.Parse(Convert.ToString(r.GetValue(5), inv)!, inv);
                break;
            }
            case "parcelas":
                devedorDaParcela[Guid.Parse(rowId)] = dividaDoDevedor[Guid.Parse(Convert.ToString(r.GetValue(1), inv)!)];
                break;
            case "pagamentos":
            {
                var dev = devedorDaParcela[Guid.Parse(Convert.ToString(r.GetValue(1), inv)!)];
                pagoPorDevedor[dev] = pagoPorDevedor.GetValueOrDefault(dev)
                    + decimal.Parse(Convert.ToString(r.GetValue(2), inv)!, inv);
                break;
            }
        }
    }
    counts.Add((src, n));
}

sql.AppendLine("commit;");
sql.AppendLine();
sql.AppendLine("-- Conferência: rode e compare com a saída do console da exportação.");
sql.AppendLine("select dv.nome,");
sql.AppendLine("  (select count(*) from public.dividas d where d.devedor_id = dv.id) as dividas,");
sql.AppendLine("  (select coalesce(sum(d.valor_total), 0) from public.dividas d where d.devedor_id = dv.id) as valor_total,");
sql.AppendLine("  (select coalesce(sum(pg.valor), 0) from public.pagamentos pg");
sql.AppendLine("     join public.parcelas pa on pa.id = pg.parcela_id");
sql.AppendLine("     join public.dividas d on d.id = pa.divida_id");
sql.AppendLine("    where d.devedor_id = dv.id) as pago");
sql.AppendLine("from public.devedores dv order by dv.nome;");

File.WriteAllText(outPath, sql.ToString(), new UTF8Encoding(false));

Console.WriteLine($"Arquivo gerado: {Path.GetFullPath(outPath)}\n");
Console.WriteLine("Linhas por tabela:");
foreach (var (t, n) in counts) Console.WriteLine($"  {t,-15} {n}");
Console.WriteLine("\nTotais por devedor (dividas | valor_total | pago):");
foreach (var (id, nome) in nomePorDevedor.OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase))
    Console.WriteLine($"  {nome,-30} {dividasPorDevedor.GetValueOrDefault(id),3} | {totalPorDevedor.GetValueOrDefault(id).ToString("0.00", inv),12} | {pagoPorDevedor.GetValueOrDefault(id).ToString("0.00", inv),12}");
return 0;

static string Q(string s) => "'" + s.Replace("'", "''") + "'";

string Format(string? raw, Kind kind)
{
    if (raw is null)
    {
        if (kind is Kind.TextN or Kind.GuidN or Kind.TsN) return "null";
        throw new FormatException("valor nulo em coluna obrigatória");
    }
    switch (kind)
    {
        case Kind.Guid:
        case Kind.GuidN:
            return Q(Guid.Parse(raw).ToString("D"));
        case Kind.Text:
        case Kind.TextN:
            return Q(raw);
        case Kind.Dec:
            return decimal.Parse(raw, NumberStyles.Number, inv).ToString(inv);
        case Kind.Int:
            return long.Parse(raw, inv).ToString(inv);
        case Kind.Date:
            return Q(DateOnly.Parse(raw, inv).ToString("yyyy-MM-dd", inv));
        case Kind.Ts:
        case Kind.TsN:
            return Q(DateTime.Parse(raw, inv).ToString("yyyy-MM-dd HH:mm:ss.ffffff", inv));
        default:
            throw new ArgumentOutOfRangeException(nameof(kind));
    }
}

enum Kind { Guid, GuidN, Text, TextN, Dec, Int, Date, Ts, TsN }
