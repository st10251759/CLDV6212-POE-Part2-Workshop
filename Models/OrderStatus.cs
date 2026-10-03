namespace PageTurn.Functions.Models;

public static class OrderStatus
{
    public const string Received = "Received";
    public const string Preparing = "Preparing";   // being picked and packed
    public const string Ready = "Ready";           // ready for collection
    public const string Collected = "Collected";
}