namespace Credify.Models;

public record TaxBook(long GrossCredits, long InitialCredits, double TaxPercentage)
{
    public long Tax => Convert.ToInt64(Math.Round(GrossCredits * TaxPercentage));
    public long TaxedCredits => GrossCredits - Tax;
    public long NetChange => TaxedCredits - InitialCredits;
} 
