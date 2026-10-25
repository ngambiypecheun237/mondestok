using Microsoft.Data.Sqlite;
using System.IO;
using Destock.App.Services;

namespace Destock.App.Data;

public sealed class DestockDb
{
    private const int SchemaVersion = 1;
    private readonly string _dbPath;

    public DestockDb()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "destock.db");
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        Initialize();
    }

    public string DbPath => _dbPath;

    public SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            PRAGMA synchronous = NORMAL;
        ";
        cmd.ExecuteNonQuery();

        return conn;
    }

    private void Initialize()
    {
        using var conn = OpenConnection();
        using var versionCmd = conn.CreateCommand();
        versionCmd.CommandText = "PRAGMA user_version;";
        var versionObj = versionCmd.ExecuteScalar();
        var version = versionObj == null || versionObj == DBNull.Value ? 0L : Convert.ToInt64(versionObj);

        if (version < SchemaVersion)
        {
            using var tx = conn.BeginTransaction();
            ExecuteSchema(conn, tx);
            using var pragma = conn.CreateCommand();
            pragma.Transaction = tx;
            pragma.CommandText = $"PRAGMA user_version = {SchemaVersion};";
            pragma.ExecuteNonQuery();
            tx.Commit();
        }

        Seed(conn);
    }

    private static void ExecuteSchema(SqliteConnection conn, SqliteTransaction tx)
    {
        var schema = @"
CREATE TABLE IF NOT EXISTS users (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    username      TEXT NOT NULL UNIQUE COLLATE NOCASE,
    password_hash TEXT NOT NULL,
    display_name  TEXT NOT NULL DEFAULT '',
    role          TEXT NOT NULL DEFAULT 'user' CHECK (role IN ('admin', 'user')),
    is_active     INTEGER NOT NULL DEFAULT 1,
    created_at    TEXT NOT NULL,
    last_login_at TEXT
);

CREATE TABLE IF NOT EXISTS warehouses (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    name       TEXT NOT NULL,
    code       TEXT NOT NULL UNIQUE COLLATE NOCASE,
    address    TEXT NOT NULL DEFAULT '',
    phone      TEXT NOT NULL DEFAULT '',
    is_default INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS categories (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    name       TEXT NOT NULL UNIQUE COLLATE NOCASE,
    color      TEXT NOT NULL DEFAULT '#10B981',
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS products (
    id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    sku                  TEXT NOT NULL UNIQUE COLLATE NOCASE,
    barcode              TEXT UNIQUE,
    name                 TEXT NOT NULL,
    description          TEXT NOT NULL DEFAULT '',
    category_id          INTEGER REFERENCES categories(id) ON DELETE SET NULL,
    unit                 TEXT NOT NULL DEFAULT 'unité',
    purchase_price_minor INTEGER NOT NULL DEFAULT 0,
    sale_price_minor     INTEGER NOT NULL DEFAULT 0,
    min_stock            REAL NOT NULL DEFAULT 0,
    is_active            INTEGER NOT NULL DEFAULT 1,
    created_at           TEXT NOT NULL,
    updated_at           TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS stock (
    product_id   INTEGER NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    warehouse_id INTEGER NOT NULL REFERENCES warehouses(id) ON DELETE RESTRICT,
    quantity     REAL NOT NULL DEFAULT 0,
    updated_at   TEXT NOT NULL,
    PRIMARY KEY (product_id, warehouse_id)
);

CREATE TABLE IF NOT EXISTS movements (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    type               TEXT NOT NULL CHECK (type IN ('in', 'out', 'adjust')),
    product_id         INTEGER NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    warehouse_id       INTEGER NOT NULL REFERENCES warehouses(id) ON DELETE RESTRICT,
    quantity           REAL NOT NULL,
    unit_price_minor   INTEGER NOT NULL DEFAULT 0,
    reference          TEXT,
    note               TEXT,
    user_id            INTEGER REFERENCES users(id) ON DELETE SET NULL,
    created_at         TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_movements_date ON movements(created_at);
CREATE INDEX IF NOT EXISTS idx_movements_product ON movements(product_id);
CREATE INDEX IF NOT EXISTS idx_movements_warehouse ON movements(warehouse_id);
CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_id);
CREATE INDEX IF NOT EXISTS idx_products_name ON products(name);
CREATE INDEX IF NOT EXISTS idx_stock_warehouse ON stock(warehouse_id);
";

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = schema;
        cmd.ExecuteNonQuery();
    }

    private static void Seed(SqliteConnection conn)
    {
        var now = Util.NowLocal();
        using var tx = conn.BeginTransaction();

        using var userCountCmd = conn.CreateCommand();
        userCountCmd.Transaction = tx;
        userCountCmd.CommandText = "SELECT COUNT(*) FROM users;";
        var userCount = Convert.ToInt64(userCountCmd.ExecuteScalar());

        if (userCount == 0)
        {
            var hash = PasswordHasher.Hash("admin123");
            using var insertUser = conn.CreateCommand();
            insertUser.Transaction = tx;
            insertUser.CommandText = "INSERT INTO users (username, password_hash, display_name, role, is_active, created_at) VALUES (@u, @h, @d, 'admin', 1, @now);";
            insertUser.Parameters.AddWithValue("@u", "admin");
            insertUser.Parameters.AddWithValue("@h", hash);
            insertUser.Parameters.AddWithValue("@d", "Administrateur");
            insertUser.Parameters.AddWithValue("@now", now);
            insertUser.ExecuteNonQuery();
        }

        using var whCountCmd = conn.CreateCommand();
        whCountCmd.Transaction = tx;
        whCountCmd.CommandText = "SELECT COUNT(*) FROM warehouses;";
        var whCount = Convert.ToInt64(whCountCmd.ExecuteScalar());

        if (whCount == 0)
        {
            using var insertWh = conn.CreateCommand();
            insertWh.Transaction = tx;
            insertWh.CommandText = "INSERT INTO warehouses (name, code, address, phone, is_default, created_at) VALUES (@n, @c, '', '', 1, @now);";
            insertWh.Parameters.AddWithValue("@n", "Dépôt principal");
            insertWh.Parameters.AddWithValue("@c", "DEP-01");
            insertWh.Parameters.AddWithValue("@now", now);
            insertWh.ExecuteNonQuery();
        }

        var defaults = new[] { ("Boissons", "#0EA5E9"), ("Alimentation", "#10B981"), ("Hygiène", "#8B5CF6"), ("Maison", "#F59E0B"), ("Électronique", "#EF4444") };
        foreach (var (name, color) in defaults)
        {
            using var insCat = conn.CreateCommand();
            insCat.Transaction = tx;
            insCat.CommandText = "INSERT OR IGNORE INTO categories (name, color, created_at) VALUES (@n, @c, @now);";
            insCat.Parameters.AddWithValue("@n", name);
            insCat.Parameters.AddWithValue("@c", color);
            insCat.Parameters.AddWithValue("@now", now);
            insCat.ExecuteNonQuery();
        }

        var settings = new[] { ("currency", "XOF"), ("theme", "system"), ("company_name", "Destock"), ("barcode_sound", "true"), ("barcode_vibrate", "true"), ("demo_seeded", "0") };
        foreach (var (k, v) in settings)
        {
            using var insSet = conn.CreateCommand();
            insSet.Transaction = tx;
            insSet.CommandText = "INSERT OR IGNORE INTO settings (key, value) VALUES (@k, @v);";
            insSet.Parameters.AddWithValue("@k", k);
            insSet.Parameters.AddWithValue("@v", v);
            insSet.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
