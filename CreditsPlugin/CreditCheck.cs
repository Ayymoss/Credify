using SharedLibraryCore.Database.Models;

namespace CreditsPlugin;

public static class CreditCheck
{
    public static bool LessThanZero(int amount) => amount <= 0;

    public static bool AvailableFunds(EFClient client, int amount) => amount > client.GetAdditionalProperty<int>("Credits");
}