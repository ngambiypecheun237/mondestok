using Microsoft.Data.Sqlite;
using System.Text.Json;
using Destock.App.Data;
using Destock.App.Models;

namespace Destock.App.Services;

public sealed class StockService
{
    private readonly DestockDb _db;

    public StockService(DestockDb db)
    {
        _db = db;
    }

    public MovementDto CreateMovement(JsonElement payload)
    {
        var type = payload.TryGetProperty("type", out var t) ? (t.GetString() ?? "") : "";
        var productId = GetLong(payload, "productId");
        var warehouseId = GetLong(payload, "warehouseId");
        var quantity = payload.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDouble() : 0.0;
        var unitPrice = payload.TryGetProperty("unitPriceMinor", out var up) && up.ValueKind == JsonValueKind.Number ? up.GetInt64() : (long?)null;
        var reference = payload.TryGetProperty("reference", out var r) ? r.GetString() : null;
        var note = payload.TryGetProperty("note", out var n) ? n.GetString() : null;
        var userId = payload.TryGetProperty("userId", out var uid) && uid.ValueKind == JsonValueKind.Number ? (long?)uid.GetInt64() : null;

        if (type != "in" && type != "out" && type != "adjust")
            throw new DestockException("VALIDATION", "Type de mouvement invalide.");
        if (quantity <= 0 && type != "adjust")
            throw new DestockException("VALIDATION", "La quantité doit être supérieure à 0.");

        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();
        var now = Util.NowLocal();

        using var pinfo = conn.CreateCommand();
        pinfo.Transaction = tx;
        pinfo.CommandText = "SELECT purchase_price_minor FROM products WHERE id = @id;";
        pinfo.Parameters.AddWithValue("@id", productId);
        var price = unitPrice ?? (pinfo.ExecuteScalar() as long? ?? 0L);

        if (type == "in")
        {
            using var stk = conn.CreateCommand();
            stk.Transaction = tx;
            stk.CommandText = @"
                INSERT INTO stock (product_id, warehouse_id, quantity, updated_at)
                VALUES (@pid, @wid, @qty, @now)
                ON CONFLICT(product_id, warehouse_id) DO UPDATE SET quantity = stock.quantity + @qty, updated_at = @now;
            ";
            stk.Parameters.AddWithValue("@pid", productId);
            stk.Parameters.AddWithValue("@wid", warehouseId);
            stk.Parameters.AddWithValue("@qty", quantity);
            stk.Parameters.AddWithValue("@now", now);
            stk.ExecuteNonQuery();
        }
        else if (type == "out")
        {
            using var cur = conn.CreateCommand();
            cur.Transaction = tx;
            cur.CommandText = "SELECT quantity FROM stock WHERE product_id = @pid AND warehouse_id = @wid;";
            cur.Parameters.AddWithValue("@pid", productId);
            cur.Parameters.AddWithValue("@wid", warehouseId);
            var curQty = cur.ExecuteScalar() as double? ?? 0.0;
            if (curQty < quantity)
                throw new DestockException("INSUFFICIENT_STOCK", "Stock insuffisant pour ce mouvement.");
            using var stk = conn.CreateCommand();
            stk.Transaction = tx;
            stk.CommandText = "UPDATE stock SET quantity = quantity - @qty, updated_at = @now WHERE product_id = @pid AND warehouse_id = @wid;";
            stk.Parameters.AddWithValue("@qty", quantity);
            stk.Parameters.AddWithValue("@now", now);
            stk.Parameters.AddWithValue("@pid", productId);
            stk.Parameters.AddWithValue("@wid", warehouseId);
            stk.ExecuteNonQuery();
        }
        else if (type == "adjust")
        {
            using var stk = conn.CreateCommand();
            stk.Transaction = tx;
            stk.CommandText = @"
                INSERT INTO stock (product_id, warehouse_id, quantity, updated_at)
                VALUES (@pid, @wid, @qty, @now)
                ON CONFLICT(product_id, warehouse_id) DO UPDATE SET quantity = @qty, updated_at = @now;
            ";
            stk.Parameters.AddWithValue("@pid", productId);
            stk.Parameters.AddWithValue("@wid", warehouseId);
            stk.Parameters.AddWithValue("@qty", quantity);
            stk.Parameters.AddWithValue("@now", now);
            stk.ExecuteNonQuery();
        }

        using var mov = conn.CreateCommand();
        mov.Transaction = tx;
        mov.CommandText = @"INSERT INTO movements (type, product_id, warehouse_id, quantity, unit_price_minor, reference, note, user_id, created_at)
                            VALUES (@t,@pid,@wid,@qty,@up,@ref,@note,@uid,@now);";
        mov.Parameters.AddWithValue("@t", type);
        mov.Parameters.AddWithValue("@pid", productId);
        mov.Parameters.AddWithValue("@wid", warehouseId);
        mov.Parameters.AddWithValue("@qty", quantity);
        mov.Parameters.AddWithValue("@up", price);
        mov.Parameters.AddWithValue("@ref", (object?)reference ?? DBNull.Value);
        mov.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
        mov.Parameters.AddWithValue("@uid", (object?)userId ?? DBNull.Value);
        mov.Parameters.AddWithValue("@now", now);
        mov.ExecuteNonQuery();

        using var mid = conn.CreateCommand();
        mid.Transaction = tx;
        mid.CommandText = "SELECT last_insert_rowid();";
        var midv = Convert.ToInt64(mid.ExecuteScalar());
        tx.Commit();

        return GetMovement(conn, midv);
    }

    public List<MovementDto> ListMovements(JsonElement payload)
    {
        using var conn = _db.OpenConnection();
        var sql = @"
            SELECT m.id, m.type, m.product_id, p.name, p.sku, m.warehouse_id, w.name, m.quantity, m.unit_price_minor, m.reference, m.note, m.user_id, COALESCE(u.display_name,u.username,''), m.created_at
            FROM movements m
            JOIN products p ON p.id = m.product_id
            JOIN warehouses w ON w.id = m.warehouse_id
            LEFT JOIN users u ON u.id = m.user_id
            WHERE 1=1
        ";
        if (payload.TryGetProperty("from", out var f) && f.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(f.GetString())) sql += " AND m.created_at >= @from";
        if (payload.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(to.GetString())) sql += " AND m.created_at <= @to";
        if (payload.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(ty.GetString())) sql += " AND m.type = @type";
        if (payload.TryGetProperty("productId", out var pid) && pid.ValueKind == JsonValueKind.Number) sql += " AND m.product_id = @pid";
        if (payload.TryGetProperty("warehouseId", out var wid) && wid.ValueKind == JsonValueKind.Number) sql += " AND m.warehouse_id = @wid";
        sql += " ORDER BY m.id DESC LIMIT 200";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (payload.TryGetProperty("from", out var ff) && ff.ValueKind == JsonValueKind.String) cmd.Parameters.AddWithValue("@from", ff.GetString());
        if (payload.TryGetProperty("to", out var tto) && tto.ValueKind == JsonValueKind.String) cmd.Parameters.AddWithValue("@to", tto.GetString() + " 23:59:59");
        if (payload.TryGetProperty("type", out var tty) && tty.ValueKind == JsonValueKind.String) cmd.Parameters.AddWithValue("@type", tty.GetString());
        if (payload.TryGetProperty("productId", out var ppid) && ppid.ValueKind == JsonValueKind.Number) cmd.Parameters.AddWithValue("@pid", ppid.GetInt64());
        if (payload.TryGetProperty("warehouseId", out var wwid) && wwid.ValueKind == JsonValueKind.Number) cmd.Parameters.AddWithValue("@wid", wwid.GetInt64());

        using var reader = cmd.ExecuteReader();
        var list = new List<MovementDto>();
        while (reader.Read())
        {
            var qty = reader.GetDouble(8);
            var up = reader.GetInt64(9);
            list.Add(new MovementDto
            {
                Id = reader.GetInt64(0),
                Type = reader.GetString(1),
                ProductId = reader.GetInt64(2),
                ProductName = reader.GetString(3),
                ProductSku = reader.GetString(4),
                WarehouseId = reader.GetInt64(5),
                WarehouseName = reader.GetString(6),
                Quantity = qty,
                UnitPriceMinor = up,
                TotalMinor = (long)Math.Round(qty * (double)up),
                Reference = reader.IsDBNull(10) ? null : reader.GetString(10),
                Note = reader.IsDBNull(11) ? null : reader.GetString(11),
                UserId = reader.IsDBNull(12) ? null : reader.GetInt64(12),
                UserName = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                CreatedAt = reader.GetString(14)
            });
        }
        return list;
    }

    public List<AlertDto> ListAlerts(JsonElement payload)
    {
        using var conn = _db.OpenConnection();
        var sql = @"
            SELECT p.id, p.name, p.sku, p.unit, w.id, w.name, s.quantity, p.min_stock
            FROM stock s
            JOIN products p ON p.id = s.product_id
            JOIN warehouses w ON w.id = s.warehouse_id
            WHERE p.is_active = 1 AND p.min_stock > 0 AND s.quantity < p.min_stock
        ";
        if (payload.TryGetProperty("warehouseId", out var wid) && wid.ValueKind == JsonValueKind.Number) sql += " AND w.id = @wid";
        sql += " ORDER BY (p.min_stock - s.quantity) DESC LIMIT 100";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (payload.TryGetProperty("warehouseId", out var wwid) && wwid.ValueKind == JsonValueKind.Number) cmd.Parameters.AddWithValue("@wid", wwid.GetInt64());

        using var reader = cmd.ExecuteReader();
        var list = new List<AlertDto>();
        while (reader.Read())
        {
            var qty = reader.GetDouble(6);
            var min = reader.GetDouble(7);
            list.Add(new AlertDto
            {
                ProductId = reader.GetInt64(0),
                ProductName = reader.GetString(1),
                ProductSku = reader.GetString(2),
                Unit = reader.GetString(3),
                WarehouseId = reader.GetInt64(4),
                WarehouseName = reader.GetString(5),
                Quantity = qty,
                MinStock = min,
                Deficit = min - qty
            });
        }
        return list;
    }

    public DashboardSummaryDto DashboardSummary(JsonElement payload)
    {
        using var conn = _db.OpenConnection();
        var summary = new DashboardSummaryDto
        {
            ProductCount = ScalarLong(conn, "SELECT COUNT(*) FROM products WHERE is_active = 1;"),
            CategoryCount = ScalarLong(conn, "SELECT COUNT(*) FROM categories;"),
            WarehouseCount = ScalarLong(conn, "SELECT COUNT(*) FROM warehouses;"),
            MovementsToday = ScalarLong(conn, "SELECT COUNT(*) FROM movements WHERE date(created_at) = date('now','localtime');"),
            TotalQuantity = ScalarDouble(conn, "SELECT COALESCE(SUM(quantity),0) FROM stock;"),
            StockValueMinor = ScalarLong(conn, "SELECT COALESCE(SUM(s.quantity * p.purchase_price_minor),0) FROM stock s JOIN products p ON p.id = s.product_id;"),
            LowStockCount = ScalarLong(conn, @"SELECT COUNT(*) FROM (SELECT s.product_id, s.warehouse_id FROM stock s JOIN products p ON p.id = s.product_id WHERE p.is_active=1 AND p.min_stock>0 AND s.quantity < p.min_stock GROUP BY s.product_id,s.warehouse_id);"),
            Alerts = ListAlerts(JsonDocument.Parse("{\"limit\":10}").RootElement),
            RecentMovements = ListMovements(JsonDocument.Parse("{}").RootElement).Take(10).ToList()
        };

        var today = DateTime.Now;
        summary.MonthlyFlows = new List<MonthFlowDto>();
        for (int i = 5; i >= 0; i--)
        {
            var month = today.AddMonths(-i);
            var key = month.ToString("yyyy-MM");
            var label = month.ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
            using var cmdIn = conn.CreateCommand();
            cmdIn.CommandText = "SELECT COALESCE(SUM(quantity),0), COALESCE(SUM(quantity * unit_price_minor),0) FROM movements WHERE type='in' AND strftime('%Y-%m', created_at)=@k;";
            cmdIn.Parameters.AddWithValue("@k", key);
            using var rIn = cmdIn.ExecuteReader();
            double inQty = 0; long inVal = 0;
            if (rIn.Read()) { inQty = rIn.IsDBNull(0) ? 0 : rIn.GetDouble(0); inVal = rIn.IsDBNull(1) ? 0 : rIn.GetInt64(1); }

            using var cmdOut = conn.CreateCommand();
            cmdOut.CommandText = "SELECT COALESCE(SUM(quantity),0), COALESCE(SUM(quantity * unit_price_minor),0) FROM movements WHERE type='out' AND strftime('%Y-%m', created_at)=@k;";
            cmdOut.Parameters.AddWithValue("@k", key);
            using var rOut = cmdOut.ExecuteReader();
            double outQty = 0; long outVal = 0;
            if (rOut.Read()) { outQty = rOut.IsDBNull(0) ? 0 : rOut.GetDouble(0); outVal = rOut.IsDBNull(1) ? 0 : rOut.GetInt64(1); }

            summary.MonthlyFlows.Add(new MonthFlowDto { Month = key, Label = label, StockIn = inQty, StockOut = outQty, InValueMinor = inVal, OutValueMinor = outVal });
        }

        using var topCmd = conn.CreateCommand();
        topCmd.CommandText = "SELECT m.product_id, p.name, SUM(CASE WHEN m.type='out' THEN m.quantity ELSE 0 END) as qty, SUM(CASE WHEN m.type='out' THEN m.quantity*m.unit_price_minor ELSE 0 END) as val FROM movements m JOIN products p ON p.id=m.product_id WHERE strftime('%Y-%m', m.created_at)=strftime('%Y-%m','now','localtime') AND m.type='out' GROUP BY m.product_id ORDER BY qty DESC LIMIT 5;";
        using var tReader = topCmd.ExecuteReader();
        summary.TopProducts = new List<TopProductDto>();
        while (tReader.Read())
        {
            summary.TopProducts.Add(new TopProductDto { ProductId = tReader.GetInt64(0), Name = tReader.GetString(1), Quantity = tReader.GetDouble(2), ValueMinor = tReader.IsDBNull(3) ? 0 : tReader.GetInt64(3) });
        }

        return summary;
    }

    private MovementDto GetMovement(SqliteConnection conn, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT m.id, m.type, m.product_id, p.name, p.sku, m.warehouse_id, w.name, m.quantity, m.unit_price_minor, m.reference, m.note, m.user_id, COALESCE(u.display_name,u.username,''), m.created_at
            FROM movements m
            JOIN products p ON p.id = m.product_id
            JOIN warehouses w ON w.id = m.warehouse_id
            LEFT JOIN users u ON u.id = m.user_id
            WHERE m.id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new DestockException("NOT_FOUND", "Mouvement introuvable.");
        var qty = reader.GetDouble(8);
        var up = reader.GetInt64(9);
        return new MovementDto
        {
            Id = reader.GetInt64(0),
            Type = reader.GetString(1),
            ProductId = reader.GetInt64(2),
            ProductName = reader.GetString(3),
            ProductSku = reader.GetString(4),
            WarehouseId = reader.GetInt64(5),
            WarehouseName = reader.GetString(6),
            Quantity = qty,
            UnitPriceMinor = up,
            TotalMinor = (long)Math.Round(qty * (double)up),
            Reference = reader.IsDBNull(10) ? null : reader.GetString(10),
            Note = reader.IsDBNull(11) ? null : reader.GetString(11),
            UserId = reader.IsDBNull(12) ? null : reader.GetInt64(12),
            UserName = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
            CreatedAt = reader.GetString(14)
        };
    }

    private static long ScalarLong(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
    }

    private static double ScalarDouble(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var v = cmd.ExecuteScalar();
        return v == null || v == DBNull.Value ? 0.0 : Convert.ToDouble(v);
    }

    private static long GetLong(JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt64();
        throw new DestockException("VALIDATION", $"Paramètre {name} requis.");
    }
}
