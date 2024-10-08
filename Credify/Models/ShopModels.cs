﻿namespace Credify.Models;

public class ServerShopItem
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public long Cost { get; set; }
    public int MaxPurchaseAmount { get; set; }
    public bool CanBeBought { get; set; }
}

public class ClientShopItem
{
    public int Id { get; set; }
    public int Amount { get; set; }
}

public class ClientShopContext : ClientShopItem
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = null!;
    public DateTimeOffset Bought { get; set; }
}
