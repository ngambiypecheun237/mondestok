using Microsoft.Data.Sqlite;
using System.Text.Json;
using Destock.App.Data;
using Destock.App.Models;

namespace Destock.App.Services;

public sealed class CatalogService
{
    private readonly DestockDb _db;

    public CatalogService(DestockDb db)
    {
        _db = db;
    }

    public List<CategoryDto> ListCategories()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.id, c.name, c.color, COUNT(p.id) as product_count
            FROM categories c
            LEFT JOIN products p ON p.category_id = c.id
            GROUP BY c.id, c.name, c.color
            ORDER BY c.name COLLATE NOCASE;
        ";
        using var reader = cmd.ExecuteReader();
        var list = new List<CategoryDto>();
        while (reader.Read())
        {
            list.Add(new CategoryDto
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Color = reader.GetString(2),
                ProductCount = reader.GetInt64(3)
            });
        }
        return list;
    }

    public CategoryDto SaveCategory(JsonElement payload)
    {
        var id = payload.TryGetProperty("id", out var idp) && idp.ValueKind == JsonValueKind.Number ? (long?)idp.GetInt64() : null;
        var name = payload.TryGetProperty("name", out var n) ? (n.GetString() ?? "").Trim() : "";
        var color = payload.TryGetProperty("color", out var c) ? (c.GetString() ?? "#10B981") : "#10B981";
        if (string.IsNullOrEmpty(name))
            throw new DestockException("VALIDATION", "Le nom de la catégorie est obligatoire.");

        using var conn = _db.OpenConnection();
        if (id.HasValue)
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE categories SET name = @n, color = @c WHERE id = @id;";
            upd.Parameters.AddWithValue("@n", name);
            upd.Parameters.AddWithValue("@c", color);
            upd.Parameters.AddWithValue("@id", id.Value);
            upd.ExecuteNonQuery();
        }
        else
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO categories (name, color, created_at) VALUES (@n, @c, @now);";
            ins.Parameters.AddWithValue("@n", name);
            ins.Parameters.AddWithValue("@c", color);
            ins.Parameters.AddWithValue("@now", Util.NowLocal());
            ins.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.id, c.name, c.color, COUNT(p.id) as product_count
            FROM categories c
            LEFT JOIN products p ON p.category_id = c.id
            WHERE c.id = (SELECT id FROM categories WHERE name = @n COLLATE NOCASE ORDER BY id DESC LIMIT 1)
            GROUP BY c.id;
        ";
        cmd.Parameters.AddWithValue("@n", name);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new CategoryDto
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Color = reader.GetString(2),
                ProductCount = reader.GetInt64(3)
            };
        }
        throw new DestockException("DB_ERROR", "Erreur lors de l'enregistrement.");
    }

    public CategoryDto DeleteCategory(JsonElement payload)
    {
        var id = GetLong(payload, "id");
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM categories WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return new CategoryDto { Id = id, Name = string.Empty, Color = "#10B981", ProductCount = 0 };
    }

    public List<WarehouseDto> ListWarehouses()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT w.id, w.name, w.code, w.address, w.phone, w.is_default,
                   COUNT(DISTINCT p.id) as product_count,
                   COALESCE(SUM(s.quantity), 0) as total_quantity,
                   COALESCE(SUM(s.quantity * p.purchase_price_minor), 0) as stock_value
            FROM warehouses w
            LEFT JOIN stock s ON s.warehouse_id = w.id
            LEFT JOIN products p ON p.id = s.product_id
            GROUP BY w.id, w.name, w.code, w.address, w.phone, w.is_default
            ORDER BY w.is_default DESC, w.name COLLATE NOCASE;
        ";
        using var reader = cmd.ExecuteReader();
        var list = new List<WarehouseDto>();
        while (reader.Read())
        {
            list.Add(new WarehouseDto
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Code = reader.GetString(2),
                Address = reader.GetString(3),
                Phone = reader.GetString(4),
                IsDefault = reader.GetInt64(5) == 1,
                ProductCount = reader.GetInt64(6),
                TotalQuantity = reader.GetDouble(7),
                StockValueMinor = reader.GetInt64(8)
            });
        }
        return list;
    }

    public WarehouseDto SaveWarehouse(JsonElement payload)
    {
        var id = payload.TryGetProperty("id", out var idp) && idp.ValueKind == JsonValueKind.Number ? (long?)idp.GetInt64() : null;
        var name = payload.TryGetProperty("name", out var n) ? (n.GetString() ?? "").Trim() : "";
        var code = payload.TryGetProperty("code", out var c) ? (c.GetString() ?? "").Trim().ToUpperInvariant() : "";
        var address = payload.TryGetProperty("address", out var a) ? (a.GetString() ?? "").Trim() : "";
        var phone = payload.TryGetProperty("phone", out var ph) ? (ph.GetString() ?? "").Trim() : "";
        var isDefault = payload.TryGetProperty("isDefault", out var idf) && idf.ValueKind == JsonValueKind.True ? idf.GetBoolean() : false;

        if (string.IsNullOrEmpty(name))
            throw new DestockException("VALIDATION", "Le nom du dépôt est obligatoire.");
        if (string.IsNullOrEmpty(code))
            code = name.Replace(" ", "-").ToUpperInvariant();

        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        if (isDefault)
        {
            using var clr = conn.CreateCommand();
            clr.Transaction = tx;
            clr.CommandText = "UPDATE warehouses SET is_default = 0;";
            clr.ExecuteNonQuery();
        }

        if (id.HasValue)
        {
            using var upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = "UPDATE warehouses SET name = @n, code = @c, address = @a, phone = @p, is_default = @d WHERE id = @id;";
            upd.Parameters.AddWithValue("@n", name);
            upd.Parameters.AddWithValue("@c", code);
            upd.Parameters.AddWithValue("@a", address);
            upd.Parameters.AddWithValue("@p", phone);
            upd.Parameters.AddWithValue("@d", isDefault ? 1 : 0);
            upd.Parameters.AddWithValue("@id", id.Value);
            upd.ExecuteNonQuery();
        }
        else
        {
            using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO warehouses (name, code, address, phone, is_default, created_at) VALUES (@n, @c, @a, @p, @d, @now);";
            ins.Parameters.AddWithValue("@n", name);
            ins.Parameters.AddWithValue("@c", code);
            ins.Parameters.AddWithValue("@a", address);
            ins.Parameters.AddWithValue("@p", phone);
            ins.Parameters.AddWithValue("@d", isDefault ? 1 : 0);
            ins.Parameters.AddWithValue("@now", Util.NowLocal());
            ins.ExecuteNonQuery();
        }

        tx.Commit();
        return ListWarehouses().FirstOrDefault(w => w.Name == name) ?? ListWarehouses().Last();
    }

    public WarehouseDto SetDefaultWarehouse(JsonElement payload)
    {
        var id = GetLong(payload, "id");
        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();
        using var clr = conn.CreateCommand();
        clr.Transaction = tx;
        clr.CommandText = "UPDATE warehouses SET is_default = 0;";
        clr.ExecuteNonQuery();
        using var upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = "UPDATE warehouses SET is_default = 1 WHERE id = @id;";
        upd.Parameters.AddWithValue("@id", id);
        upd.ExecuteNonQuery();
        tx.Commit();
        return ListWarehouses().First(w => w.Id == id);
    }

    public WarehouseDto DeleteWarehouse(JsonElement payload)
    {
        var id = GetLong(payload, "id");
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM warehouses WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return new WarehouseDto { Id = id, Name = string.Empty, Code = string.Empty, Address = string.Empty, Phone = string.Empty };
    }

    public List<ProductSummaryDto> ListProducts(JsonElement payload)
    {
        using var conn = _db.OpenConnection();
        var search = payload.TryGetProperty("search", out var s) ? s.GetString() : null;
        var categoryId = payload.TryGetProperty("categoryId", out var cid) && cid.ValueKind == JsonValueKind.Number ? (long?)cid.GetInt64() : null;
        var warehouseId = payload.TryGetProperty("warehouseId", out var wid) && wid.ValueKind == JsonValueKind.Number ? (long?)wid.GetInt64() : null;
        var lowStockOnly = payload.TryGetProperty("lowStockOnly", out var lso) && lso.ValueKind == JsonValueKind.True && lso.GetBoolean();

        var sql = @"
            SELECT p.id, p.sku, p.barcode, p.name, p.category_id, c.name, c.color, p.unit,
                   p.purchase_price_minor, p.sale_price_minor, p.min_stock, p.is_active,
                   COALESCE(SUM(s.quantity), 0) as total_quantity,
                   COALESCE(SUM(s.quantity * p.purchase_price_minor), 0) as stock_value
            FROM products p
            LEFT JOIN categories c ON c.id = p.category_id
            LEFT JOIN stock s ON s.product_id = p.id
            WHERE p.is_active = 1
        ";

        if (categoryId.HasValue) sql += " AND p.category_id = @cid";
        if (warehouseId.HasValue) sql += " AND s.warehouse_id = @wid";
        if (!string.IsNullOrWhiteSpace(search)) sql += " AND (p.name LIKE @q OR p.sku LIKE @q OR p.barcode LIKE @q)";

        sql += " GROUP BY p.id";

        if (lowStockOnly) sql += " HAVING total_quantity < p.min_stock AND p.min_stock > 0";

        sql += " ORDER BY p.name COLLATE NOCASE";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (categoryId.HasValue) cmd.Parameters.AddWithValue("@cid", categoryId.Value);
        if (warehouseId.HasValue) cmd.Parameters.AddWithValue("@wid", warehouseId.Value);
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@q", $"%{search}%");

        using var reader = cmd.ExecuteReader();
        var list = new List<ProductSummaryDto>();
        while (reader.Read())
        {
            var totalQty = reader.GetDouble(13);
            var minStock = reader.GetDouble(11);
            list.Add(new ProductSummaryDto
            {
                Id = reader.GetInt64(0),
                Sku = reader.GetString(1),
                Barcode = reader.IsDBNull(2) ? null : reader.GetString(2),
                Name = reader.GetString(3),
                CategoryId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                CategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
                CategoryColor = reader.IsDBNull(6) ? null : reader.GetString(6),
                Unit = reader.GetString(7),
                PurchasePriceMinor = reader.GetInt64(8),
                SalePriceMinor = reader.GetInt64(9),
                MinStock = minStock,
                IsActive = reader.GetInt64(12) == 1,
                TotalQuantity = totalQty,
                StockValueMinor = reader.GetInt64(14),
                IsLowStock = minStock > 0 && totalQty < minStock
            });
        }
        return list;
    }

    public ProductDetailDto GetProduct(JsonElement payload)
    {
        var id = GetLong(payload, "id");
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT p.id, p.sku, p.barcode, p.name, p.category_id, c.name, c.color, p.unit,
                   p.purchase_price_minor, p.sale_price_minor, p.min_stock, p.is_active, p.description,
                   COALESCE(SUM(s.quantity), 0) as total_quantity,
                   COALESCE(SUM(s.quantity * p.purchase_price_minor), 0) as stock_value
            FROM products p
            LEFT JOIN categories c ON c.id = p.category_id
            LEFT JOIN stock s ON s.product_id = p.id
            WHERE p.id = @id
            GROUP BY p.id;
        ";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new DestockException("NOT_FOUND", "Produit introuvable.");
        var totalQty = reader.GetDouble(14);
        var minStock = reader.GetDouble(11);
        var detail = new ProductDetailDto
        {
            Id = reader.GetInt64(0),
            Sku = reader.GetString(1),
            Barcode = reader.IsDBNull(2) ? null : reader.GetString(2),
            Name = reader.GetString(3),
            CategoryId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
            CategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
            CategoryColor = reader.IsDBNull(6) ? null : reader.GetString(6),
            Unit = reader.GetString(7),
            PurchasePriceMinor = reader.GetInt64(8),
            SalePriceMinor = reader.GetInt64(9),
            MinStock = minStock,
            IsActive = reader.GetInt64(12) == 1,
            Description = reader.IsDBNull(13) ? "" : reader.GetString(13),
            TotalQuantity = totalQty,
            StockValueMinor = reader.GetInt64(15),
            IsLowStock = minStock > 0 && totalQty < minStock,
            Stocks = new List<StockEntryDto>()
        };

        using var scmd = conn.CreateCommand();
        scmd.CommandText = "SELECT s.warehouse_id, w.name, s.quantity FROM stock s JOIN warehouses w ON w.id = s.warehouse_id WHERE s.product_id = @id ORDER BY w.name;";
        scmd.Parameters.AddWithValue("@id", id);
        using var sreader = scmd.ExecuteReader();
        while (sreader.Read())
        {
            detail.Stocks.Add(new StockEntryDto { WarehouseId = sreader.GetInt64(0), WarehouseName = sreader.GetString(1), Quantity = sreader.GetDouble(2) });
        }
        return detail;
    }

    public ProductSummaryDto LookupBarcode(JsonElement payload)
    {
        var barcode = payload.TryGetProperty("barcode", out var b) ? (b.GetString() ?? "").Trim() : "";
        if (string.IsNullOrEmpty(barcode))
            throw new DestockException("VALIDATION", "Code-barres requis.");
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT p.id, p.sku, p.barcode, p.name, p.category_id, c.name, c.color, p.unit,
                   p.purchase_price_minor, p.sale_price_minor, p.min_stock, p.is_active,
                   COALESCE(SUM(s.quantity), 0), COALESCE(SUM(s.quantity * p.purchase_price_minor), 0)
            FROM products p
            LEFT JOIN categories c ON c.id = p.category_id
            LEFT JOIN stock s ON s.product_id = p.id
            WHERE p.barcode = @b
            GROUP BY p.id;
        ";
        cmd.Parameters.AddWithValue("@b", barcode);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new DestockException("NOT_FOUND", "Aucun produit trouvé pour ce code-barres.");
        var totalQty = reader.GetDouble(13);
        var minStock = reader.GetDouble(11);
        return new ProductSummaryDto
        {
            Id = reader.GetInt64(0),
            Sku = reader.GetString(1),
            Barcode = reader.IsDBNull(2) ? null : reader.GetString(2),
            Name = reader.GetString(3),
            CategoryId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
            CategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
            CategoryColor = reader.IsDBNull(6) ? null : reader.GetString(6),
            Unit = reader.GetString(7),
            PurchasePriceMinor = reader.GetInt64(8),
            SalePriceMinor = reader.GetInt64(9),
            MinStock = minStock,
            IsActive = reader.GetInt64(12) == 1,
            TotalQuantity = totalQty,
            StockValueMinor = reader.GetInt64(14),
            IsLowStock = minStock > 0 && totalQty < minStock
        };
    }

    public ProductSummaryDto SaveProduct(JsonElement payload)
    {
        var id = payload.TryGetProperty("id", out var idp) && idp.ValueKind == JsonValueKind.Number ? (long?)idp.GetInt64() : null;
        var sku = payload.TryGetProperty("sku", out var sk) ? (sk.GetString() ?? "").Trim() : "";
        var barcode = payload.TryGetProperty("barcode", out var b) ? (b.GetString()?.Trim()) : null;
        if (string.IsNullOrEmpty(barcode)) barcode = null;
        var name = payload.TryGetProperty("name", out var nm) ? (nm.GetString() ?? "").Trim() : "";
        var description = payload.TryGetProperty("description", out var d) ? (d.GetString() ?? "").Trim() : "";
        var categoryId = payload.TryGetProperty("categoryId", out var cid) && cid.ValueKind == JsonValueKind.Number ? (long?)cid.GetInt64() : null;
        var unit = payload.TryGetProperty("unit", out var u) ? (u.GetString() ?? "unité") : "unité";
        var purchase = payload.TryGetProperty("purchasePriceMinor", out var pp) && pp.ValueKind == JsonValueKind.Number ? pp.GetInt64() : 0;
        var sale = payload.TryGetProperty("salePriceMinor", out var sp) && sp.ValueKind == JsonValueKind.Number ? sp.GetInt64() : 0;
        var minStock = payload.TryGetProperty("minStock", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetDouble() : 0.0;
        var isActive = payload.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True ? ia.GetBoolean() : true;

        if (string.IsNullOrEmpty(sku) || string.IsNullOrEmpty(name))
            throw new DestockException("VALIDATION", "SKU et nom obligatoires.");

        using var conn = _db.OpenConnection();
        var now = Util.NowLocal();
        if (id.HasValue)
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = @"UPDATE products SET sku=@sku, barcode=@barcode, name=@name, description=@desc, category_id=@cid, unit=@unit,
                                purchase_price_minor=@pp, sale_price_minor=@sp, min_stock=@ms, is_active=@ia, updated_at=@now WHERE id=@id;";
            upd.Parameters.AddWithValue("@sku", sku);
            upd.Parameters.AddWithValue("@barcode", (object?)barcode ?? DBNull.Value);
            upd.Parameters.AddWithValue("@name", name);
            upd.Parameters.AddWithValue("@desc", description);
            upd.Parameters.AddWithValue("@cid", (object?)categoryId ?? DBNull.Value);
            upd.Parameters.AddWithValue("@unit", unit);
            upd.Parameters.AddWithValue("@pp", purchase);
            upd.Parameters.AddWithValue("@sp", sale);
            upd.Parameters.AddWithValue("@ms", minStock);
            upd.Parameters.AddWithValue("@ia", isActive ? 1 : 0);
            upd.Parameters.AddWithValue("@now", now);
            upd.Parameters.AddWithValue("@id", id.Value);
            upd.ExecuteNonQuery();
        }
        else
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = @"INSERT INTO products (sku, barcode, name, description, category_id, unit, purchase_price_minor, sale_price_minor, min_stock, is_active, created_at, updated_at)
                                VALUES (@sku,@barcode,@name,@desc,@cid,@unit,@pp,@sp,@ms,@ia,@now,@now);";
            ins.Parameters.AddWithValue("@sku", sku);
            ins.Parameters.AddWithValue("@barcode", (object?)barcode ?? DBNull.Value);
            ins.Parameters.AddWithValue("@name", name);
            ins.Parameters.AddWithValue("@desc", description);
            ins.Parameters.AddWithValue("@cid", (object?)categoryId ?? DBNull.Value);
            ins.Parameters.AddWithValue("@unit", unit);
            ins.Parameters.AddWithValue("@pp", purchase);
            ins.Parameters.AddWithValue("@sp", sale);
            ins.Parameters.AddWithValue("@ms", minStock);
            ins.Parameters.AddWithValue("@ia", isActive ? 1 : 0);
            ins.Parameters.AddWithValue("@now", now);
            ins.ExecuteNonQuery();
        }

        using var g = conn.CreateCommand();
        g.CommandText = @"
            SELECT p.id, p.sku, p.barcode, p.name, p.category_id, c.name, c.color, p.unit,
                   p.purchase_price_minor, p.sale_price_minor, p.min_stock, p.is_active,
                   COALESCE(SUM(s.quantity), 0), COALESCE(SUM(s.quantity * p.purchase_price_minor), 0)
            FROM products p LEFT JOIN categories c ON c.id = p.category_id LEFT JOIN stock s ON s.product_id = p.id
            WHERE p.sku = @sku GROUP BY p.id;
        ";
        g.Parameters.AddWithValue("@sku", sku);
        using var reader = g.ExecuteReader();
        if (reader.Read())
        {
            var totalQty = reader.GetDouble(13);
            var mins = reader.GetDouble(11);
            return new ProductSummaryDto
            {
                Id = reader.GetInt64(0),
                Sku = reader.GetString(1),
                Barcode = reader.IsDBNull(2) ? null : reader.GetString(2),
                Name = reader.GetString(3),
                CategoryId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                CategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
                CategoryColor = reader.IsDBNull(6) ? null : reader.GetString(6),
                Unit = reader.GetString(7),
                PurchasePriceMinor = reader.GetInt64(8),
                SalePriceMinor = reader.GetInt64(9),
                MinStock = mins,
                IsActive = reader.GetInt64(12) == 1,
                TotalQuantity = totalQty,
                StockValueMinor = reader.GetInt64(14),
                IsLowStock = mins > 0 && totalQty < mins
            };
        }
        throw new DestockException("DB_ERROR", "Erreur lors de l'enregistrement du produit.");
    }

    public ProductSummaryDto DeleteProduct(JsonElement payload)
    {
        var id = GetLong(payload, "id");
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM products WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return new ProductSummaryDto { Id = id, Name = string.Empty, Sku = string.Empty };
    }

    private static long GetLong(JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt64();
        throw new DestockException("VALIDATION", $"Paramètre {name} requis.");
    }
}
