namespace Example.Client.Models;

public static class Amounts
{
    public static decimal? OrNull(Func<decimal?> compute)
    {
        try
        {
            return compute();
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
