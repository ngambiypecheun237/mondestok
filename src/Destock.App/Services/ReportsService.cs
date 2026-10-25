using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;
using Destock.App.Data;
using Destock.App.Models;

namespace Destock.App.Services;

public sealed class ReportsService
{
    private readonly DestockDb _db;

    public ReportsService(DestockDb db)
    {
        _db = db;
    }

    public ReportDto InventoryCsv(JsonElement payload)
    {
        using var conn = _db.OpenConnection();
        var warehouseId = payload.TryGetProperty("warehouseId", out var wid) && wid.ValueKind == JsonValueKind.Number ? (long?)wid.GetInt64() : null;

        var sql = @"
            SELECT p.sku, p.barcode, p.name, COALESCE(c.name,''), w.name, s.quantity, p.unit, p.purchase_price_minor, p.sale_price_minor, p.min_stock
            FROM stock s
            JOIN products p ON p.id = s.product_id
            JOIN warehouses w ON w.id = s.warehouse_id
            LEFT JOIN categories c ON c.id = p.category_id
            WHERE p.is_active = 1
        ";
        if (warehouseId.HasValue) sql += " AND w.id = @wid";
        sql += " ORDER BY w.name, p.name;";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (warehouseId.HasValue) cmd.Parameters.AddWithValue("@wid", warehouseId.Value);

        var sb = new StringBuilder();
        sb.AppendLine("SKU;Code barre;Produit;Categorie;Depot;Quantite;Unite;Prix achat (FCFA);Prix vente (FCFA);Stock minimum;Valeur achat;Alerte");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var sku = reader.GetString(0);
            var barcode = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var name = reader.GetString(2);
            var cat = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var wh = reader.GetString(4);
            var qty = reader.GetDouble(5);
            var unit = reader.GetString(6);
            var pa = reader.GetInt64(7);
            var pv = reader.GetInt64(8);
            var min = reader.GetDouble(9);
            var value = (long)Math.Round(qty * (double)pa);
            var alert = (min > 0 && qty < min) ? "OUI" : "NON";

            sb.AppendLine(string.Join(";", new[]
            {
                Util.CsvEscape(sku),
                Util.CsvEscape(barcode),
                Util.CsvEscape(name),
                Util.CsvEscape(cat),
                Util.CsvEscape(wh),
                qty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Util.CsvEscape(unit),
                pa.ToString(),
                pv.ToString(),
                min.ToString(System.Globalization.CultureInfo.InvariantCulture),
                value.ToString(),
                alert
            }));
        }

        var filename = $"inventaire_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        return new ReportDto { Filename = filename, Content = sb.ToString() };
    }

    public ReportDto MovementsCsv(JsonElement payload)
    {
        using var conn = _db.OpenConnection();
        var from = payload.TryGetProperty("from", out var f) ? f.GetString() : null;
        var to = payload.TryGetProperty("to", out var t) ? t.GetString() : null;
        var warehouseId = payload.TryGetProperty("warehouseId", out var wid) && wid.ValueKind == JsonValueKind.Number ? (long?)wid.GetInt64() : null;

        var sql = @"
            SELECT m.created_at, m.type, p.sku, p.name, w.name, m.quantity, m.unit_price_minor, (m.quantity * m.unit_price_minor), m.reference, m.note, COALESCE(u.display_name,'')
            FROM movements m
            JOIN products p ON p.id = m.product_id
            JOIN warehouses w ON w.id = m.warehouse_id
            LEFT JOIN users u ON u.id = m.user_id
            WHERE 1=1
        ";
        if (!string.IsNullOrEmpty(from)) sql += " AND m.created_at >= @from";
        if (!string.IsNullOrEmpty(to)) sql += " AND m.created_at <= @to";
        if (warehouseId.HasValue) sql += " AND m.warehouse_id = @wid";
        sql += " ORDER BY m.created_at DESC;";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (!string.IsNullOrEmpty(from)) cmd.Parameters.AddWithValue("@from", from);
        if (!string.IsNullOrEmpty(to)) cmd.Parameters.AddWithValue("@to", to + " 23:59:59");
        if (warehouseId.HasValue) cmd.Parameters.AddWithValue("@wid", warehouseId.Value);

        var sb = new StringBuilder();
        sb.AppendLine("Date;Type;SKU;Produit;Depot;Quantite;Prix unitaire (FCFA);Montant (FCFA);Reference;Note;Utilisateur");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var date = reader.GetString(0);
            var type = reader.GetString(1) == "in" ? "Entree" : reader.GetString(1) == "out" ? "Sortie" : "Ajustement";
            var sku = reader.GetString(2);
            var name = reader.GetString(3);
            var wh = reader.GetString(4);
            var qty = reader.GetDouble(5);
            var up = reader.GetInt64(6);
            var total = reader.GetInt64(7);
            var reference = reader.IsDBNull(8) ? "" : reader.GetString(8);
            var note = reader.IsDBNull(9) ? "" : reader.GetString(9);
            var user = reader.IsDBNull(10) ? "" : reader.GetString(10);

            sb.AppendLine(string.Join(";", new[]
            {
                Util.CsvEscape(date),
                Util.CsvEscape(type),
                Util.CsvEscape(sku),
                Util.CsvEscape(name),
                Util.CsvEscape(wh),
                qty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                up.ToString(),
                total.ToString(),
                Util.CsvEscape(reference),
                Util.CsvEscape(note),
                Util.CsvEscape(user)
            }));
        }

        var filename = $"mouvements_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        return new ReportDto { Filename = filename, Content = sb.ToString() };
    }
}
