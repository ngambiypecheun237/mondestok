using Microsoft.Data.Sqlite;
using System.Text.Json;
using Destock.App.Data;
using Destock.App.Models;

namespace Destock.App.Services;

public sealed class SettingsService
{
    private readonly DestockDb _db;

    public SettingsService(DestockDb db)
    {
        _db = db;
    }

    public Dictionary<string, string> GetAll()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";
        using var reader = cmd.ExecuteReader();
        var dict = new Dictionary<string, string>();
        while (reader.Read())
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }
        return dict;
    }

    public Dictionary<string, string> Set(JsonElement payload)
    {
        using var conn = _db.OpenConnection();
        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in payload.EnumerateObject())
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO settings (key, value) VALUES (@k, @v) ON CONFLICT(key) DO UPDATE SET value = @v;";
                cmd.Parameters.AddWithValue("@k", prop.Name);
                cmd.Parameters.AddWithValue("@v", prop.Value.ToString());
                cmd.ExecuteNonQuery();
            }
        }
        else
        {
            var key = payload.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
            var value = payload.TryGetProperty("value", out var v) ? v.ToString() : "";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO settings (key, value) VALUES (@k, @v) ON CONFLICT(key) DO UPDATE SET value = @v;";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }
        return GetAll();
    }

    public AppInfoDto AppInfo(string dbPath, string version)
    {
        using var conn = _db.OpenConnection();
        var productCount = Scalar(conn, "SELECT COUNT(*) FROM products;");
        var movementCount = Scalar(conn, "SELECT COUNT(*) FROM movements;");
        var userCount = Scalar(conn, "SELECT COUNT(*) FROM users;");
        var warehouseCount = Scalar(conn, "SELECT COUNT(*) FROM warehouses;");
        return new AppInfoDto
        {
            Version = version,
            DbPath = dbPath,
            ProductCount = productCount,
            MovementCount = movementCount,
            UserCount = userCount,
            WarehouseCount = warehouseCount
        };
    }

    private static long Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
    }
}
