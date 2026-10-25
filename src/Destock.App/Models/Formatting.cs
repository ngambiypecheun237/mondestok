using System.Globalization;

namespace Destock.App.Models;

public static class CurrencyStore
{
    private static readonly Dictionary<string, (string Symbol, int Decimals)> Currencies = new()
    {
        ["XOF"] = ("FCFA", 0),
        ["XAF"] = ("FCFA", 0),
        ["EUR"] = ("€", 2),
        ["USD"] = ("$", 2),
        ["MAD"] = ("DH", 2),
        ["GBP"] = ("£", 2),
        ["NGN"] = ("₦", 2),
        ["GHS"] = ("GH₵", 2),
        ["CDF"] = ("FC", 2),
        ["KES"] = ("KSh", 2),
        ["CAD"] = ("$ CA", 2),
    };

    private static string _code = "XOF";

    public static string Code => _code;

    public static int Decimals =>
        Currencies.TryGetValue(_code, out var value) ? value.Decimals : 2;

    public static string Symbol =>
        Currencies.TryGetValue(_code, out var value) ? value.Symbol : _code;

    public static void Configure(string? code)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            _code = code.Trim().ToUpperInvariant();
        }
    }

    public static decimal Factor => Decimals == 0 ? 1m : 100m;

    public static string Format(long minor, bool withSymbol = true)
    {
        var value = minor / Factor;
        var text = value.ToString(Decimals == 0 ? "N0" : "N2", Fmt.Fr);
        return withSymbol && !string.IsNullOrEmpty(Symbol) ? $"{text} {Symbol}" : text;
    }

    public static string FormatShort(long minor)
    {
        var abs = Math.Abs((double)minor / (double)Factor);
        string text;
        if (abs >= 1_000_000_000)
        {
            text = (abs / 1_000_000_000).ToString("0.#", Fmt.Fr) + " Md";
        }
        else if (abs >= 1_000_000)
        {
            text = (abs / 1_000_000).ToString("0.#", Fmt.Fr) + " M";
        }
        else if (abs >= 1_000)
        {
            text = (abs / 1_000).ToString("0.#", Fmt.Fr) + " k";
        }
        else
        {
            return Format(minor);
        }
        return $"{text} {Symbol}".Trim();
    }
}

public static class Fmt
{
    public static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Quantity(double value)
    {
        var text = Math.Abs(value % 1) < 0.0001
            ? value.ToString("N0", Fr)
            : value.ToString("0.##", Fr);
        return text;
    }

    public static string Money(long minor) => CurrencyStore.Format(minor);

    public static string MoneyShort(long minor) => CurrencyStore.FormatShort(minor);

    public static string PriceInput(long minor)
    {
        var value = minor / CurrencyStore.Factor;
        return value.ToString(CurrencyStore.Decimals == 0 ? "0" : "0.00", Fr);
    }

    public static bool TryParseMoney(string? text, out long minor)
    {
        minor = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        var cleaned = text
            .Replace("\u00A0", "")
            .Replace("\u202F", "")
            .Replace(" ", "")
            .Replace("FCFA", "", StringComparison.OrdinalIgnoreCase)
            .Replace("€", "")
            .Replace("$", "");
        if (cleaned.Length == 0)
        {
            return true;
        }
        var hasComma = cleaned.Contains(',');
        var hasDot = cleaned.Contains('.');
        if (hasComma && hasDot)
        {
            if (cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.'))
            {
                cleaned = cleaned.Replace(".", "").Replace(',', '.');
            }
            else
            {
                cleaned = cleaned.Replace(",", "");
            }
        }
        else if (hasComma)
        {
            cleaned = cleaned.Replace(',', '.');
        }
        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }
        minor = (long)Math.Round(value * CurrencyStore.Factor, MidpointRounding.AwayFromZero);
        return true;
    }

    public static DateTime? ParseDate(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return null;
        }
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            return value;
        }
        return DateTime.TryParse(iso, Fr, DateTimeStyles.None, out value) ? value : null;
    }

    public static string DateOnly(string? iso)
    {
        var value = ParseDate(iso);
        return value is null ? "" : value.Value.ToString("d MMM yyyy", Fr);
    }

    public static string DateShort(string? iso)
    {
        var value = ParseDate(iso);
        return value is null ? "" : value.Value.ToString("dd/MM/yyyy", Fr);
    }

    public static string DateTimeHuman(string? iso)
    {
        var value = ParseDate(iso);
        if (value is null)
        {
            return "";
        }
        var today = DateTime.Today;
        if (value.Value.Date == today)
        {
            return $"Aujourd'hui · {value.Value:HH:mm}";
        }
        if (value.Value.Date == today.AddDays(-1))
        {
            return $"Hier · {value.Value:HH:mm}";
        }
        if (value.Value.Year == today.Year)
        {
            return value.Value.ToString("d MMM · HH:mm", Fr);
        }
        return value.Value.ToString("d MMM yyyy", Fr);
    }

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length >= 2
                ? parts[0][..2].ToUpperInvariant()
                : parts[0].ToUpperInvariant();
        }
        return string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }

    public static string MovementLabel(string? type) => type switch
    {
        "in" => "Entrée",
        "out" => "Sortie",
        "adjust" => "Ajustement",
        _ => "Mouvement"
    };
}
