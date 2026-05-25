using System;

namespace GestionCommerciale.Modules.Reporting.ViewModels;

public sealed class ReportSaleByProductRow
{
    public ReportSaleByProductRow(string reference, string designation, string categorie,
        decimal quantite, decimal totalHt, decimal totalTtc, string devise)
    {
        Reference = reference;
        Designation = designation;
        Categorie = categorie;
        Quantite = quantite;
        TotalHt = totalHt;
        TotalTtc = totalTtc;
        Devise = devise;
        LblQty = quantite.ToString("N2");
        LblHt = $"{totalHt:N2} {devise}";
        LblTtc = $"{totalTtc:N2} {devise}";
    }

    public string Reference { get; }
    public string Designation { get; }
    public string Categorie { get; }
    public decimal Quantite { get; }
    public decimal TotalHt { get; }
    public decimal TotalTtc { get; }
    public string Devise { get; }
    public string LblQty { get; }
    public string LblHt { get; }
    public string LblTtc { get; }
}

public sealed class ReportSaleByCustomerRow
{
    public ReportSaleByCustomerRow(string client, string ice, string ville,
        int nbFactures, decimal totalHt, decimal totalTtc, string devise)
    {
        Client = client;
        Ice = ice;
        Ville = ville;
        NbFactures = nbFactures;
        TotalHt = totalHt;
        TotalTtc = totalTtc;
        Devise = devise;
        LblCount = nbFactures.ToString();
        LblHt = $"{totalHt:N2} {devise}";
        LblTtc = $"{totalTtc:N2} {devise}";
    }

    public string Client { get; }
    public string Ice { get; }
    public string Ville { get; }
    public int NbFactures { get; }
    public decimal TotalHt { get; }
    public decimal TotalTtc { get; }
    public string Devise { get; }
    public string LblCount { get; }
    public string LblHt { get; }
    public string LblTtc { get; }
}

public sealed class ReportRefundRow
{
    public ReportRefundRow(string numero, DateTime date, string client,
        string motif, bool retourMarchandise, decimal totalTtc, string devise)
    {
        Numero = numero;
        Date = date;
        Client = client;
        Motif = motif;
        RetourMarchandise = retourMarchandise;
        TotalTtc = totalTtc;
        Devise = devise;
        LblDate = date.ToString("d");
        LblTotal = $"{totalTtc:N2} {devise}";
        LblRetour = retourMarchandise ? "\u2713" : "";
    }

    public string Numero { get; }
    public DateTime Date { get; }
    public string Client { get; }
    public string Motif { get; }
    public bool RetourMarchandise { get; }
    public decimal TotalTtc { get; }
    public string Devise { get; }
    public string LblDate { get; }
    public string LblTotal { get; }
    public string LblRetour { get; }
}

public sealed class ReportDailySaleRow
{
    public ReportDailySaleRow(DateTime date, int nbFactures,
        decimal totalHt, decimal totalTva, decimal totalTtc, string devise)
    {
        Date = date;
        NbFactures = nbFactures;
        TotalHt = totalHt;
        TotalTva = totalTva;
        TotalTtc = totalTtc;
        Devise = devise;
        LblDate = date.ToString("d");
        LblCount = nbFactures.ToString();
        LblHt = $"{totalHt:N2} {devise}";
        LblTva = $"{totalTva:N2} {devise}";
        LblTtc = $"{totalTtc:N2} {devise}";
    }

    public DateTime Date { get; }
    public int NbFactures { get; }
    public decimal TotalHt { get; }
    public decimal TotalTva { get; }
    public decimal TotalTtc { get; }
    public string Devise { get; }
    public string LblDate { get; }
    public string LblCount { get; }
    public string LblHt { get; }
    public string LblTva { get; }
    public string LblTtc { get; }
}

public sealed class ReportStockMovementRow
{
    public ReportStockMovementRow(DateTime date, string produitRef, string produitDesignation,
        string typeMvt, decimal quantite, string origine, decimal stockApres)
    {
        Date = date;
        ProduitRef = produitRef;
        ProduitDesignation = produitDesignation;
        TypeMvt = typeMvt;
        Quantite = quantite;
        Origine = origine;
        StockApres = stockApres;
        LblDate = date.ToString("g");
        LblQty = quantite.ToString("N2");
        LblStockApres = stockApres.ToString("N2");
    }

    public DateTime Date { get; }
    public string ProduitRef { get; }
    public string ProduitDesignation { get; }
    public string TypeMvt { get; }
    public decimal Quantite { get; }
    public string Origine { get; }
    public decimal StockApres { get; }
    public string LblDate { get; }
    public string LblQty { get; }
    public string LblStockApres { get; }
}
