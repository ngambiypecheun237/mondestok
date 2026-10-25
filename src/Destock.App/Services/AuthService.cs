using Microsoft.Data.Sqlite;
using Destock.App.Data;
using Destock.App.Models;
using System.Text.Json;

namespace Destock.App.Services;

public sealed class AuthService
{
    private readonly DestockDb _db;

    public AuthService(DestockDb db)
    {
        _db = db;
    }

    public UserDto Login(JsonElement payload)
    {
        var username = payload.TryGetProperty("username", out var u) ? (u.GetString() ?? "").Trim() : "";
        var password = payload.TryGetProperty("password", out var p) ? (p.GetString() ?? "") : "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            throw new DestockException("VALIDATION", "Nom d'utilisateur et mot de passe obligatoires.");

        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, display_name, role, is_active, created_at, last_login_at, password_hash FROM users WHERE username = @u COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@u", username);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new DestockException("AUTH_FAILED", "Identifiants incorrects.");

        var id = reader.GetInt64(0);
        var uname = reader.GetString(1);
        var display = reader.GetString(2);
        var role = reader.GetString(3);
        var isActive = reader.GetInt64(4) == 1;
        var created = reader.GetString(5);
        var lastLogin = reader.IsDBNull(6) ? (string?)null : reader.GetString(6);
        var hash = reader.GetString(7);

        if (!isActive)
            throw new DestockException("ACCOUNT_DISABLED", "Ce compte est désactivé. Contactez un administrateur.");

        if (!PasswordHasher.Verify(password, hash))
            throw new DestockException("AUTH_FAILED", "Identifiants incorrects.");

        var now = Util.NowLocal();
        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE users SET last_login_at = @now WHERE id = @id;";
        upd.Parameters.AddWithValue("@now", now);
        upd.Parameters.AddWithValue("@id", id);
        upd.ExecuteNonQuery();

        return new UserDto { Id = id, Username = uname, DisplayName = display, Role = role, IsActive = isActive, CreatedAt = created, LastLoginAt = now };
    }

    public UserDto Me(JsonElement payload)
    {
        var userId = GetLong(payload, "userId");
        using var conn = _db.OpenConnection();
        var user = GetUser(conn, userId);
        if (!user.IsActive)
            throw new DestockException("ACCOUNT_DISABLED", "Ce compte est désactivé.");
        return user;
    }

    public void ChangePassword(JsonElement payload)
    {
        var userId = GetLong(payload, "userId");
        var oldPass = payload.TryGetProperty("oldPassword", out var op) ? (op.GetString() ?? "") : "";
        var newPass = payload.TryGetProperty("newPassword", out var np) ? (np.GetString() ?? "") : "";

        if (newPass.Length < 4)
            throw new DestockException("VALIDATION", "Le nouveau mot de passe doit contenir au moins 4 caractères.");

        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT password_hash FROM users WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", userId);
        var hash = cmd.ExecuteScalar() as string;
        if (hash == null)
            throw new DestockException("NOT_FOUND", "Utilisateur introuvable.");
        if (!PasswordHasher.Verify(oldPass, hash))
            throw new DestockException("AUTH_FAILED", "Le mot de passe actuel est incorrect.");

        var newHash = PasswordHasher.Hash(newPass);
        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE users SET password_hash = @h WHERE id = @id;";
        upd.Parameters.AddWithValue("@h", newHash);
        upd.Parameters.AddWithValue("@id", userId);
        upd.ExecuteNonQuery();
    }

    public List<UserDto> ListUsers(JsonElement payload)
    {
        var actorId = payload.TryGetProperty("actorId", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt64() : (long?)null;
        using var conn = _db.OpenConnection();
        EnsureAdmin(conn, actorId);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, display_name, role, is_active, created_at, last_login_at FROM users ORDER BY role = 'admin' DESC, username COLLATE NOCASE;";
        using var reader = cmd.ExecuteReader();
        var list = new List<UserDto>();
        while (reader.Read())
        {
            list.Add(new UserDto
            {
                Id = reader.GetInt64(0),
                Username = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Role = reader.GetString(3),
                IsActive = reader.GetInt64(4) == 1,
                CreatedAt = reader.GetString(5),
                LastLoginAt = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return list;
    }

    public UserDto SaveUser(JsonElement payload)
    {
        var actorId = payload.TryGetProperty("actorId", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt64() : (long?)null;
        using var conn = _db.OpenConnection();
        EnsureAdmin(conn, actorId);

        var id = payload.TryGetProperty("id", out var idp) && idp.ValueKind == JsonValueKind.Number ? (long?)idp.GetInt64() : null;
        var username = payload.TryGetProperty("username", out var un) ? (un.GetString() ?? "").Trim() : "";
        var displayName = payload.TryGetProperty("displayName", out var dn) ? (dn.GetString() ?? "").Trim() : "";
        var role = payload.TryGetProperty("role", out var r) ? (r.GetString() ?? "user") : "user";
        var isActive = payload.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True ? (bool?)ia.GetBoolean() : null;
        var password = payload.TryGetProperty("password", out var pw) ? pw.GetString() : null;

        if (username.Length < 3)
            throw new DestockException("VALIDATION", "Le nom d'utilisateur doit contenir au moins 3 caractères.");
        if (role != "admin" && role != "user")
            throw new DestockException("VALIDATION", "Rôle invalide.");
        if (string.IsNullOrEmpty(displayName)) displayName = username;

        using var tx = conn.BeginTransaction();
        long userId;

        if (id.HasValue)
        {
            var existing = GetUser(conn, id.Value, tx);
            if (existing.Id == (actorId ?? -1) && isActive == false)
                throw new DestockException("VALIDATION", "Vous ne pouvez pas désactiver votre propre compte.");

            if (existing.Role == "admin" && role != "admin")
            {
                using var cnt = conn.CreateCommand();
                cnt.Transaction = tx;
                cnt.CommandText = "SELECT COUNT(*) FROM users WHERE role = 'admin' AND is_active = 1 AND id != @id;";
                cnt.Parameters.AddWithValue("@id", id.Value);
                var admins = Convert.ToInt64(cnt.ExecuteScalar());
                if (admins == 0)
                    throw new DestockException("VALIDATION", "Il doit rester au moins un administrateur actif.");
            }

            if (existing.Role == "admin" && existing.IsActive && isActive == false)
            {
                using var cnt = conn.CreateCommand();
                cnt.Transaction = tx;
                cnt.CommandText = "SELECT COUNT(*) FROM users WHERE role = 'admin' AND is_active = 1 AND id != @id;";
                cnt.Parameters.AddWithValue("@id", id.Value);
                var admins = Convert.ToInt64(cnt.ExecuteScalar());
                if (admins == 0)
                    throw new DestockException("VALIDATION", "Il doit rester au moins un administrateur actif.");
            }

            using var upd = conn.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = "UPDATE users SET username = @u, display_name = @d, role = @r, is_active = @a WHERE id = @id;";
            upd.Parameters.AddWithValue("@u", username);
            upd.Parameters.AddWithValue("@d", displayName);
            upd.Parameters.AddWithValue("@r", role);
            upd.Parameters.AddWithValue("@a", (isActive ?? existing.IsActive) ? 1 : 0);
            upd.Parameters.AddWithValue("@id", id.Value);
            upd.ExecuteNonQuery();

            if (!string.IsNullOrWhiteSpace(password))
            {
                if (password.Length < 4)
                    throw new DestockException("VALIDATION", "Le mot de passe doit contenir au moins 4 caractères.");
                var ph = PasswordHasher.Hash(password);
                using var updp = conn.CreateCommand();
                updp.Transaction = tx;
                updp.CommandText = "UPDATE users SET password_hash = @h WHERE id = @id;";
                updp.Parameters.AddWithValue("@h", ph);
                updp.Parameters.AddWithValue("@id", id.Value);
                updp.ExecuteNonQuery();
            }
            userId = id.Value;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new DestockException("VALIDATION", "Le mot de passe est obligatoire.");
            if (password.Length < 4)
                throw new DestockException("VALIDATION", "Le mot de passe doit contenir au moins 4 caractères.");
            var ph = PasswordHasher.Hash(password);
            using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO users (username, password_hash, display_name, role, is_active, created_at) VALUES (@u, @h, @d, @r, 1, @now);";
            ins.Parameters.AddWithValue("@u", username);
            ins.Parameters.AddWithValue("@h", ph);
            ins.Parameters.AddWithValue("@d", displayName);
            ins.Parameters.AddWithValue("@r", role);
            ins.Parameters.AddWithValue("@now", Util.NowLocal());
            ins.ExecuteNonQuery();

            using var lid = conn.CreateCommand();
            lid.Transaction = tx;
            lid.CommandText = "SELECT last_insert_rowid();";
            userId = Convert.ToInt64(lid.ExecuteScalar());
        }

        tx.Commit();
        return GetUser(_db.OpenConnection(), userId);
    }

    public void DeleteUser(JsonElement payload)
    {
        var actorId = payload.TryGetProperty("actorId", out var a) && a.ValueKind == JsonValueKind.Number ? (long?)a.GetInt64() : null;
        var id = payload.TryGetProperty("id", out var ip) && ip.ValueKind == JsonValueKind.Number ? ip.GetInt64() : throw new DestockException("VALIDATION", "id requis");
        using var conn = _db.OpenConnection();
        EnsureAdmin(conn, actorId);
        if (actorId.HasValue && actorId.Value == id)
            throw new DestockException("VALIDATION", "Vous ne pouvez pas supprimer votre propre compte.");
        var user = GetUser(conn, id);
        if (user.Role == "admin")
        {
            using var cnt = conn.CreateCommand();
            cnt.CommandText = "SELECT COUNT(*) FROM users WHERE role = 'admin' AND is_active = 1 AND id != @id;";
            cnt.Parameters.AddWithValue("@id", id);
            var admins = Convert.ToInt64(cnt.ExecuteScalar());
            if (admins == 0)
                throw new DestockException("VALIDATION", "Il doit rester au moins un administrateur actif.");
        }
        using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM users WHERE id = @id;";
        del.Parameters.AddWithValue("@id", id);
        del.ExecuteNonQuery();
    }

    private static void EnsureAdmin(SqliteConnection conn, long? actorId)
    {
        if (!actorId.HasValue)
            throw new DestockException("FORBIDDEN", "Action réservée aux administrateurs.");
        var user = GetUser(conn, actorId.Value);
        if (!user.IsActive || user.Role != "admin")
            throw new DestockException("FORBIDDEN", "Action réservée aux administrateurs.");
    }

    private static UserDto GetUser(SqliteConnection conn, long id, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        if (tx != null) cmd.Transaction = tx;
        cmd.CommandText = "SELECT id, username, display_name, role, is_active, created_at, last_login_at FROM users WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new DestockException("NOT_FOUND", "Utilisateur introuvable.");
        return new UserDto
        {
            Id = reader.GetInt64(0),
            Username = reader.GetString(1),
            DisplayName = reader.GetString(2),
            Role = reader.GetString(3),
            IsActive = reader.GetInt64(4) == 1,
            CreatedAt = reader.GetString(5),
            LastLoginAt = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    private static long GetLong(JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt64();
        throw new DestockException("VALIDATION", $"Paramètre {name} requis.");
    }
}
