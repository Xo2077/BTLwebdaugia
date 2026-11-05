namespace BTLwebdaugia.Services;

public static class MoneyRules
{
    public const decimal MaxStoredAmount = 999999999999999999m;
    public const decimal MaxTopUpAmount = 1000000m;

    public static bool CanCredit(decimal balance, decimal amount)
    {
        return balance >= 0 && amount > 0 &&
            amount <= MaxStoredAmount && balance <= MaxStoredAmount - amount;
    }
}
