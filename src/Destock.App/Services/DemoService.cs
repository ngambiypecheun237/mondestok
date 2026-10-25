using Microsoft.Data.Sqlite;
using System.Text.Json;
using Destock.App.Data;

namespace Destock.App.Services;

public sealed class DemoService
{
    private readonly DestockDb _db;

    public DemoService(DestockDb db)
    {
        _db = db;
    }

    public bool DemoSeed()
    {
        using var conn = _db.OpenConnection();
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT value FROM settings WHERE key = 'demo_seeded';";
        var v = check.ExecuteScalar() as string;
        if (v == "1") return false;

        using var tx = conn.BeginTransaction();
        var now = Util.NowLocal();

        // Add some stock
        using var prod = conn.CreateCommand();
        prod.Transaction = tx;
        prod.CommandText = "SELECT id FROM products LIMIT 10;";
        using var reader = prod.ExecuteReader();
        var prodIds = new List<long>();
        while (reader.Read()) prodIds.Add(reader.GetInt64(0));
        reader.Close();

        if (prodIds.Count == 0)
        {
            // Create demo products
            var demos = new[] { ("DEMO-001", "Eau minérale 1.5L", "Boissons"), ("DEMO-002", "Riz 5kg", "Alimentation"), ("DEMO-003", "Savon liquide", "Hygiène") };
            foreach (var d in demos)
            {
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT OR IGNORE INTO products (sku, name, category_id, unit, purchase_price_minor, sale_price_minor, min_stock, is_active, created_at, updated_at) VALUES (@sku,@name,(SELECT id FROM categories WHERE name=@cat LIMIT 1),'unité',100,150,10,1,@now,@now);";
                ins.Parameters.AddWithValue("@sku", d.Item1);
                ins.Parameters.AddWithValue("@name", d.Item2);
                ins.Parameters.AddWithValue("@cat", d.Item3);
                ins.Parameters.AddWithValue("@now", now);
                ins.ExecuteNonQuery();
            }
        }

        using var wcmd = conn.CreateCommand();
        wcmd.Transaction = tx;
        wcmd.CommandText = "SELECT id FROM warehouses LIMIT 1;";
        var wid = Convert.ToInt64(wcmd.ExecuteScalar() ?? 1);

        using var pcmd = conn.CreateCommand();
        pcmd.Transaction = tx;
        pcmd.CommandText = "SELECT id FROM products WHERE sku LIKE 'DEMO%' OR id IN (SELECT id FROM products LIMIT 5);";
        using var pr = pcmd.ExecuteReader();
        var pids = new List<long>();
        while (pr.Read()) pids.Add(pr.GetInt64(0));
        pr.Close();

        foreach (var pid in pids)
        {
            using var stk = conn.CreateCommand();
            stk.Transaction = tx;
            stk.CommandText = "INSERT OR IGNORE INTO stock (product_id, warehouse_id, quantity, updated_at) VALUES (@pid,@wid,50,@now);";
            stk.Parameters.AddWithValue("@pid", pid);
            stk.Parameters.AddWithValue("@wid", wid);
            stk.Parameters.AddWithValue("@now", now);
            stk.ExecuteNonQuery();
        }

        using var set = conn.CreateCommand();
        set.Transaction = tx;
        set.CommandText = "INSERT INTO settings (key, value) VALUES ('demo_seeded', '1') ON CONFLICT(key) DO UPDATE SET value='1';";
        set.ExecuteNonQuery();

        tx.Commit();
        return true;
    }
}
