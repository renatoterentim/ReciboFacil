using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ReciboFacil.Servidor;

public class Sessao
{
    public string Token { get; set; } = "";
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Perfil { get; set; } = "";
    public DateTime Expira { get; set; }
}

public static class Senhas
{
    const int Iteracoes = 100_000;

    public static (string Hash, string Salt) Criar(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iteracoes, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Conferir(string senha, string hashB64, string saltB64)
    {
        var salt = Convert.FromBase64String(saltB64);
        var esperado = Convert.FromBase64String(hashB64);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iteracoes, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(hash, esperado);
    }
}

public static class Sessoes
{
    static readonly ConcurrentDictionary<string, Sessao> _s = new();

    public static Sessao Criar(int id, string login, string nome, string perfil)
    {
        var s = new Sessao
        {
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            Id = id, Login = login, Nome = nome, Perfil = perfil,
            Expira = DateTime.UtcNow.AddHours(12)
        };
        _s[s.Token] = s;
        return s;
    }

    public static void Remover(string token) => _s.TryRemove(token, out _);

    public static void RemoverDoUsuario(int id)
    {
        foreach (var kv in _s)
            if (kv.Value.Id == id) _s.TryRemove(kv.Key, out _);
    }

    public static Sessao? Validar(string cabecalho)
    {
        if (string.IsNullOrEmpty(cabecalho) || !cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = cabecalho.Substring(7).Trim();
        if (!_s.TryGetValue(token, out var s)) return null;
        if (s.Expira < DateTime.UtcNow) { _s.TryRemove(token, out _); return null; }

        // Confere no banco se o usuário continua ativo e atualiza nome/perfil.
        using var c = Db.Abrir();
        using var cmd = c.Cmd("SELECT nome, perfil, ativo FROM usuarios WHERE id=$id", ("$id", s.Id));
        using var r = cmd.ExecuteReader();
        if (!r.Read() || r.GetInt32(2) != 1) { _s.TryRemove(token, out _); return null; }
        s.Nome = r.GetString(0);
        s.Perfil = r.GetString(1);
        s.Expira = DateTime.UtcNow.AddHours(12);
        return s;
    }
}
