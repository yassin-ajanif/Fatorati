using System.Data;
using System.Diagnostics;
using System.Globalization;
using GestionCommerciale.Shared.Database;
using Microsoft.Data.Sqlite;

namespace GestionCommerciale.Shared.Services;

public class PerformanceTestService
{
    private static readonly Random Rng = Random.Shared;
    private readonly string _cs;

    public PerformanceTestService()
    {
        _cs = DatabasePath.GetConnectionString();
    }

    public async Task<string> RunAsync(IProgress<string> progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync(ct);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA synchronous=OFF; PRAGMA journal_mode=WAL; PRAGMA cache_size=-500000; PRAGMA temp_store=MEMORY;";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var max = await GetMaxIdsAsync(conn, ct);
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        progress.Report("Création de 10 000 produits...");
        await InsertProductsAsync(conn, max.ProdId, now, ct);

        progress.Report("Création de 1 000 clients...");
        await InsertClientsAsync(conn, max.TiersId, now, ct);

        progress.Report("Création de 100 000 bons de réception...");
        await InsertBrHeadersAsync(conn, max, now, ct);
        await InsertBrLinesAsync(conn, max, ct);

        progress.Report("Création de 1 000 000 factures...");
        await InsertFactureHeadersAsync(conn, max, now, ct);
        await InsertFactureLinesAsync(conn, max, ct);

        sw.Stop();
        var e = sw.Elapsed;
        return $"Terminé en {e.Hours}h {e.Minutes}m {e.Seconds}s ({e.TotalSeconds:F1}s)";
    }

    private static async Task<(long ProdId, long TiersId, long FactId, long FactLigneId, long BRId, long BRLigneId)>
        GetMaxIdsAsync(SqliteConnection conn, CancellationToken ct)
    {
        async Task<long> Max(string table)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT IFNULL(MAX(Id),0) FROM \"{table}\"";
            var r = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(r);
        }

        return (
            await Max("Produits"),
            await Max("Tiers"),
            await Max("Factures"),
            await Max("FactureLignes"),
            await Max("BonsReception"),
            await Max("BonReceptionLignes")
        );
    }

    private static async Task InsertProductsAsync(SqliteConnection conn, long startId, string now, CancellationToken ct)
    {
        const int total = 10_000, batch = 500;
        var designs = new[] { "Ordinateur portable", "Souris sans fil", "Clavier mécanique", "Écran 24\"", "Disque dur SSD", "Carte mémoire", "Imprimante", "Scanner", "Webcam HD", "Casque audio", "Enceinte Bluetooth", "Hub USB", "Câble HDMI", "Adaptateur secteur", "Batterie externe", "Sacoche ordinateur", "Tapis de souris", "Support téléphone", "Ventilateur USB", "Lampe LED" };

        for (var i = 0; i < total; i += batch)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("INSERT INTO Produits (Id,CreatedAt,UpdatedAt,Reference,CodeBarre,Designation,Unite,PrixAchatHT,PrixVenteHT,TauxTVA,StockActuel,StockMinimum,Actif) VALUES ");
            var end = Math.Min(i + batch, total);
            for (var j = i; j < end; j++)
            {
                var id = startId + 1 + j;
                var desig = $"{designs[j % designs.Length]} #{j}";
                var pa = Rng.Next(500, 500_000) / 100m;
                var pv = pa + Rng.Next(200, 300_000) / 100m;
                var tva = Rng.NextDouble() < 0.7 ? 20m : Rng.NextDouble() < 0.5 ? 14m : 10m;
                if (j > i) sb.Append(',');
                sb.Append(CultureInfo.InvariantCulture, $"({id},'{now}','{now}','PROD-{j:D5}',NULL,'{Escape(desig)}','U',{pa:F2},{pv:F2},{tva:F1},{Rng.Next(0,501)},{Rng.Next(0,51)},1)");
            }
            await ExecAsync(conn, sb.ToString(), ct);
        }
    }

    private static async Task InsertClientsAsync(SqliteConnection conn, long startId, string now, CancellationToken ct)
    {
        const int batch = 500;
        for (var i = 0; i < 1000; i += batch)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("INSERT INTO Tiers (Id,CreatedAt,UpdatedAt,Type,Nom,ICE,Adresse,Ville,Telephone,Email,ConditionsPaiement,Actif) VALUES ");
            var end = Math.Min(i + batch, 1000);
            for (var j = i; j < end; j++)
            {
                var id = startId + 1 + j;
                if (j > i) sb.Append(',');
                sb.Append(CultureInfo.InvariantCulture, $"({id},'{now}','{now}',0,'Client test {j}','','Casablanca','Casablanca','','','',1)");
            }
            await ExecAsync(conn, sb.ToString(), ct);
        }
    }

    private static async Task InsertBrHeadersAsync(SqliteConnection conn,
        (long ProdId, long TiersId, long FactId, long FactLigneId, long BRId, long BRLigneId) max,
        string now, CancellationToken ct)
    {
        const int total = 100_000, batch = 500;
        var year = DateTime.Now.Year;
        var startBr = max.BRId + 1;
        var fourStart = max.TiersId + 1;

        for (var i = 0; i < total; i += batch)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("INSERT INTO BonsReception (Id,CreatedAt,UpdatedAt,Numero,BonCommandeId,FournisseurId,Date,Note) VALUES ");
            var end = Math.Min(i + batch, total);
            for (var j = i; j < end; j++)
            {
                var id = startBr + j;
                var fourId = fourStart + Rng.Next(0, 1000);
                var daysAgo = Rng.Next(0, 365);
                var date = DateTime.Today.AddDays(-daysAgo).ToString("yyyy-MM-dd");
                if (j > i) sb.Append(',');
                sb.Append(CultureInfo.InvariantCulture, $"({id},'{now}','{now}','BR-{year}-{j:D7}',NULL,{fourId},'{date}','')");
            }
            await ExecAsync(conn, sb.ToString(), ct);
        }
    }

    private static async Task InsertBrLinesAsync(SqliteConnection conn,
        (long ProdId, long TiersId, long FactId, long FactLigneId, long BRId, long BRLigneId) max,
        CancellationToken ct)
    {
        const int totalBr = 100_000, batch = 1000;
        var startBr = max.BRId + 1;
        var startLigne = max.BRLigneId + 1;
        var prodStart = max.ProdId + 1;
        var ligneIdx = 0;
        var year = DateTime.Now.Year;

        for (var i = 0; i < totalBr; i++)
        {
            var brId = startBr + i;
            var linesPerBr = Rng.Next(1, 11);
            for (var li = 0; li < linesPerBr; li++)
            {
                if (ligneIdx % batch == 0 && ligneIdx > 0)
                {
                    // previous batch already flushed; start new batch
                }
                ligneIdx++;
            }
        }

        // Use streaming: for each batch of 1000 lines, generate and insert
        ligneIdx = 0;
        System.Text.StringBuilder? sb = null;

        for (var i = 0; i < totalBr; i++)
        {
            var brId = startBr + i;
            var linesPerBr = Rng.Next(1, 11);
            for (var li = 0; li < linesPerBr; li++)
            {
                if (ligneIdx % batch == 0)
                {
                    if (sb != null) { await ExecAsync(conn, sb.ToString(), ct); }
                    sb = new System.Text.StringBuilder();
                    sb.Append("INSERT INTO BonReceptionLignes (Id,CreatedAt,UpdatedAt,BRId,ProduitId,Designation,QuantiteRecue,PrixUnitaireHT,TauxTVA) VALUES ");
                }

                var id = startLigne + ligneIdx;
                var prodId = prodStart + Rng.Next(0, 10_000);
                var qty = Rng.Next(1, 101);
                var pu = Rng.Next(500, 200_000) / 100m;
                var tva = Rng.NextDouble() < 0.7 ? 20m : 10m;
                var desig = $"Produit {prodId}";
                var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                if (ligneIdx % batch > 0) sb.Append(',');
                sb.Append(CultureInfo.InvariantCulture, $"({id},'{now}','{now}',{brId},{prodId},'{Escape(desig)}',{qty},{pu:F2},{tva:F1})");
                ligneIdx++;
            }
        }

        if (sb != null) await ExecAsync(conn, sb.ToString(), ct);
    }

    private static async Task InsertFactureHeadersAsync(SqliteConnection conn,
        (long ProdId, long TiersId, long FactId, long FactLigneId, long BRId, long BRLigneId) max,
        string now, CancellationToken ct)
    {
        const int total = 1_000_000, batch = 500;
        var year = DateTime.Now.Year;
        var startFact = max.FactId + 1;
        var clientStart = max.TiersId + 1;

        for (var i = 0; i < total; i += batch)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("INSERT INTO Factures (Id,CreatedAt,UpdatedAt,Numero,ClientId,Date,DateEcheance,EstPayee,RemiseGlobale,Note) VALUES ");
            var end = Math.Min(i + batch, total);
            for (var j = i; j < end; j++)
            {
                var id = startFact + j;
                var clientId = clientStart + Rng.Next(0, 1000);
                var daysAgo = Rng.Next(0, 365);
                var date = DateTime.Today.AddDays(-daysAgo);
                var echeance = date.AddDays(Rng.Next(15, 61));
                var estPayee = Rng.NextDouble() < 0.6 ? 1 : 0;
                var remiseGlobale = Rng.NextDouble() < 0.3 ? Rng.Next(0, 1001) / 100m : 0m;
                if (j > i) sb.Append(',');
                sb.Append(CultureInfo.InvariantCulture, $"({id},'{now}','{now}','FAC-{year}-{j:D7}',{clientId},'{date:yyyy-MM-dd}','{echeance:yyyy-MM-dd}',{estPayee},{remiseGlobale:F2},'')");
            }
            await ExecAsync(conn, sb.ToString(), ct);
        }
    }

    private static async Task InsertFactureLinesAsync(SqliteConnection conn,
        (long ProdId, long TiersId, long FactId, long FactLigneId, long BRId, long BRLigneId) max,
        CancellationToken ct)
    {
        const int totalFact = 1_000_000, batch = 1000;
        var startFact = max.FactId + 1;
        var startLigne = max.FactLigneId + 1;
        var prodStart = max.ProdId + 1;
        var ligneIdx = 0;
        System.Text.StringBuilder? sb = null;

        for (var i = 0; i < totalFact; i++)
        {
            var factId = startFact + i;
            var linesPerFact = Rng.Next(1, 6);
            for (var li = 0; li < linesPerFact; li++)
            {
                if (ligneIdx % batch == 0)
                {
                    if (sb != null) { await ExecAsync(conn, sb.ToString(), ct); }
                    sb = new System.Text.StringBuilder();
                    sb.Append("INSERT INTO FactureLignes (Id,CreatedAt,UpdatedAt,FactureId,ProduitId,Designation,Quantite,PrixUnitaireHT,Remise,TauxTVA,Conditionnement) VALUES ");
                }

                var id = startLigne + ligneIdx;
                var prodId = prodStart + Rng.Next(0, 10_000);
                var qty = Rng.Next(1, 11);
                var pu = Rng.Next(1000, 500_000) / 100m;
                var remise = Rng.NextDouble() < 0.3 ? Rng.Next(0, 2001) / 100m : 0m;
                var tva = Rng.NextDouble() < 0.7 ? 20m : 10m;
                var desig = $"Produit {prodId}";
                var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                if (ligneIdx % batch > 0) sb.Append(',');
                sb.Append(CultureInfo.InvariantCulture, $"({id},'{now}','{now}',{factId},{prodId},'{Escape(desig)}',{qty},{pu:F2},{remise:F2},{tva:F1},'U')");
                ligneIdx++;
            }
        }

        if (sb != null) await ExecAsync(conn, sb.ToString(), ct);
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var tx = conn.BeginTransaction();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static string Escape(string s) => s.Replace("'", "''");
}
