using System.Text.Json.Serialization;

namespace Destock.App.Models;

public sealed class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "user";
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = "";
    public string? LastLoginAt { get; set; }

    [JsonIgnore]
    public bool IsAdmin => string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string FriendlyRole => IsAdmin ? "Administrateur" : "Utilisateur";

    [JsonIgnore]
    public string StatusLabel => IsActive ? "Actif" : "Désactivé";

    [JsonIgnore]
    public string LastLoginLabel => string.IsNullOrWhiteSpace(LastLoginAt)
        ? "Jamais connecté"
        : $"Dernière connexion : {Fmt.DateTimeHuman(LastLoginAt)}";
}

public sealed class CategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#10B981";
    public long ProductCount { get; set; }

    [JsonIgnore]
    public string ProductCountLabel => ProductCount == 1 ? "1 produit" : $"{ProductCount} produits";
}

public sealed class WarehouseDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsDefault { get; set; }
    public long ProductCount { get; set; }
    public double TotalQuantity { get; set; }
    public long StockValueMinor { get; set; }

    [JsonIgnore]
    public string Summary => $"{ProductCount} produit(s) · {Fmt.Quantity(TotalQuantity)} unité(s)";

    [JsonIgnore]
    public string AddressLabel => string.IsNullOrWhiteSpace(Address) ? "Aucune adresse renseignée" : Address;

    [JsonIgnore]
    public string PhoneLabel => string.IsNullOrWhiteSpace(Phone) ? "" : Phone;

    [JsonIgnore]
    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
}

public class ProductDto
{
    public long Id { get; set; }
    public string Sku { get; set; } = "";
    public string? Barcode { get; set; }
    public string Name { get; set; } = "";
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string Unit { get; set; } = "unité";
    public long PurchasePriceMinor { get; set; }
    public long SalePriceMinor { get; set; }
    public double MinStock { get; set; }
    public bool IsActive { get; set; } = true;
    public double TotalQuantity { get; set; }
    public long StockValueMinor { get; set; }
    public bool IsLowStock { get; set; }

    [JsonIgnore]
    public string SearchSubtitle => string.IsNullOrWhiteSpace(Barcode) ? Sku : $"{Sku} · {Barcode}";

    [JsonIgnore]
    public string StockLabel => $"{Fmt.Quantity(TotalQuantity)} {Unit}";
}

public sealed class ProductSummaryDto : ProductDto
{
}

public sealed class ProductDetailDto : ProductDto
{
    public string Description { get; set; } = "";
    public List<StockEntryDto> Stocks { get; set; } = [];
}

public sealed class StockEntryDto
{
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public double Quantity { get; set; }
}

public sealed class MovementDto
{
    public long Id { get; set; }
    public string Type { get; set; } = "in";
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public double Quantity { get; set; }
    public long UnitPriceMinor { get; set; }
    public long TotalMinor { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public long? UserId { get; set; }
    public string UserName { get; set; } = "";
    public string CreatedAt { get; set; } = "";

    [JsonIgnore]
    public bool IsIn => Type == "in";

    [JsonIgnore]
    public string QuantityLabel
    {
        get
        {
            var amount = Fmt.Quantity(Math.Abs(Quantity));
            return Type switch
            {
                "in" => $"+{amount}",
                "out" => $"−{amount}",
                "adjust" => Quantity < 0 ? $"−{amount}" : $"+{amount}",
                _ => amount
            };
        }
    }

    [JsonIgnore]
    public string DetailsLabel
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Reference))
            {
                parts.Add(Reference);
            }
            parts.Add(Fmt.DateTimeHuman(CreatedAt));
            return string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}

public sealed class AlertDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public string Unit { get; set; } = "unité";
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public double Quantity { get; set; }
    public double MinStock { get; set; }
    public double Deficit { get; set; }

    [JsonIgnore]
    public string ThresholdLabel => $"Seuil : {Fmt.Quantity(MinStock)} {Unit}";

    [JsonIgnore]
    public string DeficitLabel => Deficit <= 0.0001
        ? "Seuil atteint"
        : $"Manque {Fmt.Quantity(Deficit)} {Unit}";
}

public sealed class MonthFlowDto
{
    public string Month { get; set; } = "";
    public string Label { get; set; } = "";
    public double StockIn { get; set; }
    public double StockOut { get; set; }
    public long InValueMinor { get; set; }
    public long OutValueMinor { get; set; }
}

public sealed class TopProductDto
{
    public long ProductId { get; set; }
    public string Name { get; set; } = "";
    public double Quantity { get; set; }
    public long ValueMinor { get; set; }
}

public sealed class DashboardDto
{
    public long ProductCount { get; set; }
    public long CategoryCount { get; set; }
    public long WarehouseCount { get; set; }
    public long LowStockCount { get; set; }
    public double TotalQuantity { get; set; }
    public long StockValueMinor { get; set; }
    public long MovementsToday { get; set; }
    public double StockInMonth { get; set; }
    public double StockOutMonth { get; set; }
    public List<MonthFlowDto> MonthlyFlows { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
    public List<MovementDto> RecentMovements { get; set; } = new();
    public List<AlertDto> Alerts { get; set; } = new();
}

public sealed class DashboardSummaryDto
{
    public long ProductCount { get; set; }
    public long CategoryCount { get; set; }
    public long WarehouseCount { get; set; }
    public long LowStockCount { get; set; }
    public double TotalQuantity { get; set; }
    public long StockValueMinor { get; set; }
    public long MovementsToday { get; set; }
    public double StockInMonth { get; set; }
    public double StockOutMonth { get; set; }
    public List<MonthFlowDto> MonthlyFlows { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
    public List<MovementDto> RecentMovements { get; set; } = new();
    public List<AlertDto> Alerts { get; set; } = new();
}

public sealed class ReportDto
{
    public string Filename { get; set; } = "";
    public string Content { get; set; } = "";
}

public sealed class AppInfoDto
{
    public string Version { get; set; } = "";
    public string DbPath { get; set; } = "";
    public long ProductCount { get; set; }
    public long MovementCount { get; set; }
    public long UserCount { get; set; }
    public long WarehouseCount { get; set; }
}

public sealed class MessageDto
{
    public string Message { get; set; } = "";
}

public sealed class IdDto
{
    public long Id { get; set; }
}

public sealed class LookupOption
{
    public long? Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#94A3B8";
    public override string ToString() => Name;
}

public sealed class CurrencyOption
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}

public sealed class ThemeOption
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}
