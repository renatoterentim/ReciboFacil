using Microsoft.Data.Sqlite;
using ReciboFacil.Shared;
using static ReciboFacil.Servidor.Resp;

namespace ReciboFacil.Servidor;

public static class EndpointsUsuarios
{
    sealed record UsuarioRow(int Id, string Login, string Nome, string Perfil, string Hash, string Salt, bool Ativo);

    static UsuarioRow? Buscar(SqliteConnection c, string onde, params (string, object?)[] ps)
    {
        using var cmd = c.Cmd("SELECT id, login, nome, perfil, senha_hash, senha_salt, ativo FROM usuarios WHERE " + onde, ps);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new UsuarioRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt32(6) == 1);
    }

    static string? NormalizarPerfil(string? p)
    {
        if (string.Equals(p, Constantes.Administrador, StringComparison.OrdinalIgnoreCase)) return Constantes.Administrador;
        if (string.Equals(p, Constantes.Operador, StringComparison.OrdinalIgnoreCase)) return Constantes.Operador;
        return null;
    }

    static string? ValidarLogin(string login)
    {
        if (login.Length < 3 || login.Length > 30) return "O login deve ter de 3 a 30 caracteres.";
        if (login.Any(char.IsWhiteSpace)) return "O login não pode conter espaços.";
        return null;
    }

    static string? ValidarSenha(string senha) =>
        senha.Length < 6 ? "A senha deve ter pelo menos 6 caracteres." : null;

    static int Criar(SqliteConnection c, string login, string nome, string perfil, string senha)
    {
        var (hash, salt) = Senhas.Criar(senha);
        using var cmd = c.Cmd(
            "INSERT INTO usuarios(login, nome, perfil, senha_hash, senha_salt, ativo, criado_em) VALUES($l,$n,$p,$h,$s,1,$d); SELECT last_insert_rowid();",
            ("$l", login), ("$n", nome), ("$p", perfil), ("$h", hash), ("$s", salt), ("$d", Agora()));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    static void DefinirSenha(SqliteConnection c, int id, string senha)
    {
        var (hash, salt) = Senhas.Criar(senha);
        using var cmd = c.Cmd("UPDATE usuarios SET senha_hash=$h, senha_salt=$s WHERE id=$id", ("$h", hash), ("$s", salt), ("$id", id));
        cmd.ExecuteNonQuery();
    }

    static int AdminsAtivosExceto(SqliteConnection c, int idExcluir)
    {
        using var cmd = c.Cmd("SELECT COUNT(*) FROM usuarios WHERE perfil=$p AND ativo=1 AND id<>$id",
            ("$p", Constantes.Administrador), ("$id", idExcluir));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    static LoginResp Resposta(Sessao s) => new() { Token = s.Token, Id = s.Id, Login = s.Login, Nome = s.Nome, Perfil = s.Perfil };

    public static void Mapear(WebApplication app)
    {
        app.MapGet("/api/ping", () =>
        {
            using var c = Db.Abrir();
            using var cmd = c.Cmd("SELECT COUNT(*) FROM usuarios");
            var n = Convert.ToInt32(cmd.ExecuteScalar());
            return Results.Json(new PingDto
            {
                Programa = Constantes.Programa,
                VersaoApp = Constantes.VersaoApp,
                Protocolo = Constantes.Versao,
                Esquema = Db.Migracoes.Length,
                TemUsuarios = n > 0
            });
        });

        app.MapPost("/api/primeiro-admin", (PrimeiroAdminReq req) =>
        {
            var login = (req.Login ?? "").Trim();
            var nome = (req.Nome ?? "").Trim();
            var senha = req.Senha ?? "";
            var erro = ValidarLogin(login) ?? (nome.Length == 0 ? "Informe o nome." : null) ?? ValidarSenha(senha);
            if (erro != null) return Erro(400, "DADOS_INVALIDOS", erro);
            using var c = Db.Abrir();
            using (var cmd = c.Cmd("SELECT COUNT(*) FROM usuarios"))
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    return Erro(409, "JA_CONFIGURADO", "O sistema já possui usuários cadastrados.");
            var id = Criar(c, login, nome, Constantes.Administrador, senha);
            var s = Sessoes.Criar(id, login, nome, Constantes.Administrador);
            Db.Auditar(c, s, "CRIAR", "usuario", id, login + " (primeiro administrador)");
            return Results.Json(Resposta(s));
        });

        app.MapPost("/api/login", async (LoginReq req) =>
        {
            var login = (req.Login ?? "").Trim();
            using var c = Db.Abrir();
            var u = Buscar(c, "login=$l", ("$l", login));
            if (u == null || !Senhas.Conferir(req.Senha ?? "", u.Hash, u.Salt))
            {
                await Task.Delay(600);
                return Erro(401, "LOGIN_INVALIDO", "Usuário ou senha incorretos.");
            }
            if (!u.Ativo)
                return Erro(403, "USUARIO_SUSPENSO", "Este usuário está suspenso. Procure um administrador.");
            var s = Sessoes.Criar(u.Id, u.Login, u.Nome, u.Perfil);
            Db.Auditar(c, s, "LOGIN", "usuario", u.Id);
            return Results.Json(Resposta(s));
        });

        app.MapPost("/api/logout", (HttpContext ctx) =>
        {
            Sessoes.Remover(S(ctx).Token);
            return Results.Ok();
        });

        app.MapPost("/api/me/senha", (HttpContext ctx, SenhaReq req) =>
        {
            var s = S(ctx);
            var nova = req.NovaSenha ?? "";
            var erro = ValidarSenha(nova);
            if (erro != null) return Erro(400, "DADOS_INVALIDOS", erro);
            using var c = Db.Abrir();
            var u = Buscar(c, "id=$id", ("$id", s.Id));
            if (u == null || !Senhas.Conferir(req.SenhaAtual ?? "", u.Hash, u.Salt))
                return Erro(400, "SENHA_ATUAL", "A senha atual está incorreta.");
            DefinirSenha(c, s.Id, nova);
            Db.Auditar(c, s, "ALTERAR_SENHA", "usuario", s.Id);
            return Results.Ok();
        });

        app.MapGet("/api/usuarios", (HttpContext ctx) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var lista = new List<UsuarioDto>();
            using var c = Db.Abrir();
            using var cmd = c.Cmd("SELECT id, login, nome, perfil, ativo, criado_em FROM usuarios ORDER BY nome COLLATE NOCASE");
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add(new UsuarioDto { Id = r.GetInt32(0), Login = r.GetString(1), Nome = r.GetString(2), Perfil = r.GetString(3), Ativo = r.GetInt32(4) == 1, CriadoEm = r.GetString(5) });
            return Results.Json(lista);
        });

        app.MapPost("/api/usuarios", (HttpContext ctx, UsuarioSalvarReq req) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var login = (req.Login ?? "").Trim();
            var nome = (req.Nome ?? "").Trim();
            var perfil = NormalizarPerfil(req.Perfil);
            var erro = ValidarLogin(login)
                ?? (nome.Length == 0 ? "Informe o nome." : null)
                ?? (perfil == null ? "Perfil inválido." : null)
                ?? ValidarSenha(req.Senha ?? "");
            if (erro != null) return Erro(400, "DADOS_INVALIDOS", erro);
            using var c = Db.Abrir();
            if (Buscar(c, "login=$l", ("$l", login)) != null)
                return Erro(409, "LOGIN_DUPLICADO", "Já existe um usuário com este login.");
            var id = Criar(c, login, nome, perfil!, req.Senha!);
            Db.Auditar(c, S(ctx), "CRIAR", "usuario", id, login + " (" + perfil + ")");
            return Results.Json(new UsuarioDto { Id = id, Login = login, Nome = nome, Perfil = perfil!, Ativo = true, CriadoEm = Agora() });
        });

        app.MapPut("/api/usuarios/{id:int}", (HttpContext ctx, int id, UsuarioSalvarReq req) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var nome = (req.Nome ?? "").Trim();
            var perfil = NormalizarPerfil(req.Perfil);
            if (nome.Length == 0) return Erro(400, "DADOS_INVALIDOS", "Informe o nome.");
            if (perfil == null) return Erro(400, "DADOS_INVALIDOS", "Perfil inválido.");
            using var c = Db.Abrir();
            var u = Buscar(c, "id=$id", ("$id", id));
            if (u == null) return Erro(404, "NAO_ENCONTRADO", "Usuário não encontrado.");
            if (u.Perfil == Constantes.Administrador && perfil != Constantes.Administrador && u.Ativo && AdminsAtivosExceto(c, id) == 0)
                return Erro(400, "ULTIMO_ADMIN", "Deve existir ao menos um administrador ativo.");
            using (var cmd = c.Cmd("UPDATE usuarios SET nome=$n, perfil=$p WHERE id=$id", ("$n", nome), ("$p", perfil), ("$id", id)))
                cmd.ExecuteNonQuery();
            Db.Auditar(c, S(ctx), "EDITAR", "usuario", id, u.Login + " (" + perfil + ")");
            return Results.Json(new UsuarioDto { Id = id, Login = u.Login, Nome = nome, Perfil = perfil, Ativo = u.Ativo });
        });

        app.MapPost("/api/usuarios/{id:int}/senha", (HttpContext ctx, int id, SenhaReq req) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var erro = ValidarSenha(req.NovaSenha ?? "");
            if (erro != null) return Erro(400, "DADOS_INVALIDOS", erro);
            using var c = Db.Abrir();
            var u = Buscar(c, "id=$id", ("$id", id));
            if (u == null) return Erro(404, "NAO_ENCONTRADO", "Usuário não encontrado.");
            DefinirSenha(c, id, req.NovaSenha!);
            Sessoes.RemoverDoUsuario(id == S(ctx).Id ? -1 : id);
            Db.Auditar(c, S(ctx), "REDEFINIR_SENHA", "usuario", id, u.Login);
            return Results.Ok();
        });

        app.MapPost("/api/usuarios/{id:int}/suspender", (HttpContext ctx, int id) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            if (id == S(ctx).Id) return Erro(400, "AUTO", "Você não pode suspender o seu próprio usuário.");
            using var c = Db.Abrir();
            var u = Buscar(c, "id=$id", ("$id", id));
            if (u == null) return Erro(404, "NAO_ENCONTRADO", "Usuário não encontrado.");
            if (u.Perfil == Constantes.Administrador && u.Ativo && AdminsAtivosExceto(c, id) == 0)
                return Erro(400, "ULTIMO_ADMIN", "Deve existir ao menos um administrador ativo.");
            using (var cmd = c.Cmd("UPDATE usuarios SET ativo=0 WHERE id=$id", ("$id", id))) cmd.ExecuteNonQuery();
            Sessoes.RemoverDoUsuario(id);
            Db.Auditar(c, S(ctx), "SUSPENDER", "usuario", id, u.Login);
            return Results.Ok();
        });

        app.MapPost("/api/usuarios/{id:int}/reativar", (HttpContext ctx, int id) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            using var c = Db.Abrir();
            var u = Buscar(c, "id=$id", ("$id", id));
            if (u == null) return Erro(404, "NAO_ENCONTRADO", "Usuário não encontrado.");
            using (var cmd = c.Cmd("UPDATE usuarios SET ativo=1 WHERE id=$id", ("$id", id))) cmd.ExecuteNonQuery();
            Db.Auditar(c, S(ctx), "REATIVAR", "usuario", id, u.Login);
            return Results.Ok();
        });
    }
}
