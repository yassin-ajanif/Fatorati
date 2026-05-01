using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GestionCommerciale.Shared.Database;
using GestionCommerciale.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Modules.Livraison;

internal static class BonLivraisonDeleteReferencedMessage
{
    /// <summary>Returns a localized error body if the BL is referenced by factures; otherwise null.</summary>
    public static async Task<string?> BuildIfBlockedAsync(
        AppDbContext db,
        int bonLivraisonId,
        ILocaleService locale,
        CancellationToken cancellationToken = default)
    {
        var factNums = await db.Factures.AsNoTracking()
            .Where(f => f.BLId == bonLivraisonId)
            .OrderBy(f => f.Numero)
            .Select(f => f.Numero)
            .ToListAsync(cancellationToken);

        if (factNums.Count == 0)
            return null;

        return string.Join(Environment.NewLine,
            locale.T("BL_ErrDeleteReferencedIntro"),
            locale.Tf("BL_ErrRefFact", string.Join(", ", factNums)));
    }
}
