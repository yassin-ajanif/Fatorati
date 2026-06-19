using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Shared.Services;

public sealed class DocumentNumberService : IDocumentNumberService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public DocumentNumberService(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    public Task<string> NextDevisAsync(CancellationToken cancellationToken = default) =>
        NextFromDbAsync(async db =>
        {
            var list = await db.Devis.AsNoTracking().Select(d => d.Numero).ToListAsync(cancellationToken);
            return list;
        }, "DEV", cancellationToken);

    public Task<string> NextBLAsync(CancellationToken cancellationToken = default) =>
        NextFromDbAsync(async db =>
        {
            var list = await db.BonsLivraison.AsNoTracking().Select(d => d.Numero).ToListAsync(cancellationToken);
            return list;
        }, "BL", cancellationToken);

    public Task<string> NextBRAsync(CancellationToken cancellationToken = default) =>
        NextFromDbAsync(async db =>
        {
            var list = await db.BonsReception.AsNoTracking().Select(d => d.Numero).ToListAsync(cancellationToken);
            return list;
        }, "BR", cancellationToken);

    public Task<string> NextBCAsync(CancellationToken cancellationToken = default) =>
        NextFromDbAsync(async db =>
        {
            var list = await db.BonsCommande.AsNoTracking().Select(d => d.Numero).ToListAsync(cancellationToken);
            return list;
        }, "BC", cancellationToken);

    public Task<string> NextFactureAsync(CancellationToken cancellationToken = default) =>
        NextFromDbAsync(async db =>
        {
            var list = await db.Factures.AsNoTracking().Select(d => d.Numero).ToListAsync(cancellationToken);
            return list;
        }, "FAC", cancellationToken);

    public Task<string> NextFactureFournisseurAsync(CancellationToken cancellationToken = default) =>
        NextFromDbAsync(async db =>
        {
            var list = await db.FacturesFournisseurs.AsNoTracking().Select(d => d.Numero).ToListAsync(cancellationToken);
            return list;
        }, "FAF", cancellationToken);

    public Task<string> NextAvoirAsync(CancellationToken cancellationToken = default) =>
        NextFromDbAsync(async db =>
        {
            var list = await db.Avoirs.AsNoTracking().Select(d => d.Numero).ToListAsync(cancellationToken);
            return list;
        }, "AVO", cancellationToken);

    public Task<string> NextAvoirFournisseurAsync(CancellationToken cancellationToken = default) =>
        NextFromDbAsync(async db =>
        {
            var list = await db.AvoirsFournisseurs.AsNoTracking().Select(d => d.Numero).ToListAsync(cancellationToken);
            return list;
        }, "AVF", cancellationToken);

    private async Task<string> NextFromDbAsync(Func<AppDbContext, Task<List<string>>> loadNumeros, string prefix, CancellationToken cancellationToken)
    {
        var year = DateTime.Now.Year;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var numeros = await loadNumeros(db);
        var last = 0;
        var prefixYear = $"{prefix}-{year}-";
        foreach (var n in numeros)
        {
            if (string.IsNullOrEmpty(n) || !n.StartsWith(prefixYear, StringComparison.Ordinal)) continue;
            var tail = n[prefixYear.Length..];
            if (int.TryParse(tail, out var num) && num > last) last = num;
        }

        return NumberingHelper.Generate(prefix, last, year);
    }
}
