using GestionCommerciale.Modules.Devis.Models;
using GestionCommerciale.Modules.Facturation.Models;
using GestionCommerciale.Modules.Livraison.Models;
using GestionCommerciale.Modules.Commande.Models;
using GestionCommerciale.Modules.Reception.Models;
using GestionCommerciale.Modules.Stock.Models;
using GestionCommerciale.Modules.Tiers.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionCommerciale.Shared.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tiers> Tiers => Set<Tiers>();
    public DbSet<Categorie> Categories => Set<Categorie>();
    public DbSet<Produit> Produits => Set<Produit>();
    public DbSet<MouvementStock> MouvementsStock => Set<MouvementStock>();
    public DbSet<Devis> Devis => Set<Devis>();
    public DbSet<DevisLigne> DevisLignes => Set<DevisLigne>();
    public DbSet<BonLivraison> BonsLivraison => Set<BonLivraison>();
    public DbSet<BonLivraisonLigne> BonLivraisonLignes => Set<BonLivraisonLigne>();
    public DbSet<BonCommande> BonsCommande => Set<BonCommande>();
    public DbSet<BonCommandeLigne> BonCommandeLignes => Set<BonCommandeLigne>();
    public DbSet<BonReception> BonsReception => Set<BonReception>();
    public DbSet<BonReceptionLigne> BonReceptionLignes => Set<BonReceptionLigne>();
    public DbSet<Facture> Factures => Set<Facture>();
    public DbSet<FactureLigne> FactureLignes => Set<FactureLigne>();
    public DbSet<Paiement> Paiements => Set<Paiement>();
    public DbSet<Avoir> Avoirs => Set<Avoir>();
    public DbSet<AvoirLigne> AvoirLignes => Set<AvoirLigne>();
    public DbSet<AppSettingsRow> AppSettings => Set<AppSettingsRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tiers>(e =>
        {
            e.ToTable("Tiers");
            e.Property(t => t.Type).HasConversion<int>();
        });

        modelBuilder.Entity<Produit>(e =>
        {
            e.HasOne(p => p.Categorie).WithMany().HasForeignKey(p => p.CategorieId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(p => p.Reference).IsUnique();
        });

        modelBuilder.Entity<MouvementStock>(e =>
        {
            e.Property(m => m.Type).HasConversion<int>();
            e.HasOne(m => m.Produit).WithMany().HasForeignKey(m => m.ProduitId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Devis>(e =>
        {
            e.HasMany(d => d.Lignes).WithOne(l => l.Devis).HasForeignKey(l => l.DevisId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BonLivraison>(e =>
        {
            e.HasMany(b => b.Lignes).WithOne(l => l.BonLivraison).HasForeignKey(l => l.BLId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BonCommande>(e =>
        {
            e.HasMany(b => b.Lignes).WithOne(l => l.BonCommande).HasForeignKey(l => l.BonCommandeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BonReception>(e =>
        {
            e.HasOne(b => b.BonCommande).WithMany().HasForeignKey(b => b.BonCommandeId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(b => b.Lignes).WithOne(l => l.BonReception).HasForeignKey(l => l.BRId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Facture>(e =>
        {
            e.HasMany(f => f.Lignes).WithOne(l => l.Facture).HasForeignKey(l => l.FactureId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(f => f.Paiements).WithOne(p => p.Facture).HasForeignKey(p => p.FactureId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Paiement>(e =>
        {
            e.Property(p => p.Mode).HasConversion<int>();
        });

        modelBuilder.Entity<Avoir>(e =>
        {
            e.HasOne(a => a.Facture).WithMany().HasForeignKey(a => a.FactureId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(a => a.Lignes).WithOne(l => l.Avoir).HasForeignKey(l => l.AvoirId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSettingsRow>(e =>
        {
            e.HasKey(x => x.Id);
        });
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTimestamps()
    {
        var utc = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<GestionCommerciale.Shared.Models.BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utc;
                entry.Entity.UpdatedAt = utc;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utc;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AppSettingsRow>())
        {
            // no BaseEntity timestamps
        }
    }
}
