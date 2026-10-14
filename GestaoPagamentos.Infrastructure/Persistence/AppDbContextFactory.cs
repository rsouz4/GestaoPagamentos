using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GestaoPagamentos.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Data Source=gestao_pagamentos.db";

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        return new AppDbContext(options);
    }
}
