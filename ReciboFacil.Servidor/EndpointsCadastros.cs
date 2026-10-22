using Microsoft.Data.Sqlite;
using ReciboFacil.Shared;
using static ReciboFacil.Servidor.Resp;

namespace ReciboFacil.Servidor;

public static class EndpointsCadastros
{
    const string ColsEmissor = "id, nome, documento, endereco, cidade, uf, telefone, email, observacoes, ativo, excluido";
    const string ColsContato = "id, nome, documento, telefone, telefone2, email, endereco, cidade, uf, ocupacao, chave_pix, observacoes, ativo";

    static string L(string? s) => (s ?? "").Trim();

    static EmissorDto LerEmissor(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0), Nome = r.GetString(1), Documento = r.GetString(2), Endereco = r.GetString(3),
        Cidade = r.GetString(4), Uf = r.GetString(5), Telefone = r.GetString(6), Email = r.GetString(7),
        Observacoes = r.GetString(8), Ativo = r.GetInt32(9) == 1, Excluido = r.GetInt32(10) == 1
    };

    static ContatoDto LerContato(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0), Nome = r.GetString(1), Documento = r.GetString(2), Telefone = r.GetString(3),
        Telefone2 = r.GetString(4), Email = r.GetString(5), Endereco = r.GetString(6), Cidade = r.GetString(7),
        Uf = r.GetString(8), Ocupacao = r.GetString(9), ChavePix = r.GetString(10), Observacoes = r.GetString(11),
        Ativo = r.GetInt32(12) == 1
    };

    // Limpa espaços, padroniza UF e formata CPF/CNPJ. Retorna mensagem de erro ou null.
    static string? PrepararEmissor(EmissorDto d, out string digitos)
    {
        d.Nome = L(d.Nome); d.Endereco = L(d.Endereco); d.Cidade = L(d.Cidade); d.Uf = L(d.Uf).ToUpperInvariant();
        d.Telefone = L(d.Telefone); d.Email = L(d.Email); d.Observacoes = L(d.Observacoes);
        digitos = "";
        if (d.Nome.Length == 0) return "Informe o nome do emissor.";
        if (Doc.Digitos(d.Documento).Length == 0) return "Informe o CPF ou CNPJ do emissor.";
        var erro = Doc.Normalizar(d.Documento, out var fmt, out digitos);
        if (erro != null) return erro;
        d.Documento = fmt;
        if (d.Uf.Length != 0 && d.Uf.Length != 2) return "A UF deve ter 2 letras.";
        return null;
    }

    static string? PrepararContato(ContatoDto d, out string digitos)
    {
        d.Nome = L(d.Nome); d.Telefone = L(d.Telefone); d.Telefone2 = L(d.Telefone2); d.Email = L(d.Email);
        d.Endereco = L(d.Endereco); d.Cidade = L(d.Cidade); d.Uf = L(d.Uf).ToUpperInvariant();
        d.Ocupacao = L(d.Ocupacao); d.ChavePix = L(d.ChavePix); d.Observacoes = L(d.Observacoes);
        digitos = "";
        if (d.Nome.Length == 0) return "Informe o nome do contato.";
        var erro = Doc.Normalizar(d.Documento, out var fmt, out digitos);
        if (erro != null) return erro;
        d.Documento = fmt;
        if (d.Uf.Length != 0 && d.Uf.Length != 2) return "A UF deve ter 2 letras.";
        return null;
    }

    static string? ContatoComMesmoDocumento(SqliteConnection c, string digitos, int idExcluir)
    {
        if (digitos.Length == 0) return null;
        using var cmd = c.Cmd("SELECT nome FROM contatos WHERE documento_digitos=$d AND id<>$id LIMIT 1", ("$d", digitos), ("$id", idExcluir));
        return cmd.ExecuteScalar() as string;
    }

    static (string, object?)[] ParamsEmissor(EmissorDto d, string digitos) => new (string, object?)[]
    {
        ("$n", d.Nome), ("$doc", d.Documento), ("$dig", digitos), ("$e", d.Endereco), ("$c", d.Cidade), ("$u", d.Uf),
        ("$t", d.Telefone), ("$m", d.Email), ("$o", d.Observacoes), ("$a", d.Ativo ? 1 : 0), ("$agora", Agora())
    };

    static (string, object?)[] ParamsContato(ContatoDto d, string digitos) => new (string, object?)[]
    {
        ("$n", d.Nome), ("$doc", d.Documento), ("$dig", digitos), ("$t1", d.Telefone), ("$t2", d.Telefone2), ("$m", d.Email),
        ("$e", d.Endereco), ("$c", d.Cidade), ("$u", d.Uf), ("$oc", d.Ocupacao), ("$px", d.ChavePix), ("$ob", d.Observacoes),
        ("$a", d.Ativo ? 1 : 0), ("$agora", Agora())
    };

    // Busca por todos os campos, sem diferenciar maiúsculas/minúsculas nem acentos.
    static bool Casa(ContatoDto c, string[] termos)
    {
        var palco = Db.Normalizar(string.Join(" ", c.Nome, c.Documento, c.Telefone, c.Telefone2, c.Email, c.Endereco,
            c.Cidade, c.Uf, c.Ocupacao, c.ChavePix, c.Observacoes));
        var digitosPalco = Doc.Digitos(c.Documento + c.Telefone + c.Telefone2);
        foreach (var t in termos)
        {
            var nt = Db.Normalizar(t);
            if (palco.Contains(nt)) continue;
            var dt = Doc.Digitos(t);
            if (dt.Length >= 3 && digitosPalco.Contains(dt)) continue;
            return false;
        }
        return true;
    }

    public static EmissorDto? EmissorPorId(SqliteConnection c, int id)
    {
        using var cmd = c.Cmd("SELECT " + ColsEmissor + " FROM emissores WHERE id=$id", ("$id", id));
        using var r = cmd.ExecuteReader();
        return r.Read() ? LerEmissor(r) : null;
    }

    public static ContatoDto? ContatoPorId(SqliteConnection c, int id)
    {
        using var cmd = c.Cmd("SELECT " + ColsContato + " FROM contatos WHERE id=$id", ("$id", id));
        using var r = cmd.ExecuteReader();
        return r.Read() ? LerContato(r) : null;
    }

    public static void Mapear(WebApplication app)
    {
        // ---------- Emissores ----------
        app.MapGet("/api/emissores", (bool? inativos, bool? excluidos) =>
        {
            var lista = new List<EmissorDto>();
            using var c = Db.Abrir();
            var sit = LicencaServico.Situacao(c);
            int idGratis = sit.Completa ? 0 : LicencaServico.IdEmissorGratis(c);
            using (var cmd = c.Cmd("SELECT " + ColsEmissor + " FROM emissores " + (excluidos == true ? "" : inativos == true ? "WHERE excluido=0 " : "WHERE excluido=0 AND ativo=1 ") + "ORDER BY nome COLLATE NOCASE"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) lista.Add(LerEmissor(r));
            foreach (var e in lista) e.Liberado = sit.Completa || e.Id == idGratis;
            return Results.Json(lista);
        });

        app.MapPost("/api/emissores", (HttpContext ctx, EmissorDto d) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var erro = PrepararEmissor(d, out var dig);
            if (erro != null) return Erro(400, "DADOS_INVALIDOS", erro);
            using var c = Db.Abrir();
            var bloqueio = LicencaServico.ValidarEmissor(c, d, dig, null);
            if (bloqueio != null) return Erro(403, "LICENCA_NECESSARIA", bloqueio);
            using var cmd = c.Cmd(
                "INSERT INTO emissores(nome, documento, documento_digitos, endereco, cidade, uf, telefone, email, observacoes, ativo, criado_em, atualizado_em) " +
                "VALUES($n,$doc,$dig,$e,$c,$u,$t,$m,$o,$a,$agora,$agora); SELECT last_insert_rowid();", ParamsEmissor(d, dig));
            d.Id = Convert.ToInt32(cmd.ExecuteScalar());
            Db.Auditar(c, S(ctx), "CRIAR", "emissor", d.Id, d.Nome);
            return Results.Json(d);
        });

        app.MapPut("/api/emissores/{id:int}", (HttpContext ctx, int id, EmissorDto d) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var erro = PrepararEmissor(d, out var dig);
            if (erro != null) return Erro(400, "DADOS_INVALIDOS", erro);
            using var c = Db.Abrir();
            var antes = EmissorPorId(c, id);
            if (antes == null || antes.Excluido) return Erro(404, "NAO_ENCONTRADO", "Emissor não encontrado.");
            d.Id = id;
            var bloqueio = LicencaServico.ValidarEmissor(c, d, dig, antes);
            if (bloqueio != null) return Erro(403, "LICENCA_NECESSARIA", bloqueio);
            var ps = ParamsEmissor(d, dig).Append(("$id", (object?)id)).ToArray();
            using var cmd = c.Cmd(
                "UPDATE emissores SET nome=$n, documento=$doc, documento_digitos=$dig, endereco=$e, cidade=$c, uf=$u, telefone=$t, email=$m, observacoes=$o, ativo=$a, atualizado_em=$agora WHERE id=$id", ps);
            if (cmd.ExecuteNonQuery() == 0) return Erro(404, "NAO_ENCONTRADO", "Emissor não encontrado.");
            d.Id = id;
            Db.Auditar(c, S(ctx), "EDITAR", "emissor", id, d.Nome + (d.Ativo ? "" : " (inativo)"));
            return Results.Json(d);
        });

        // Exclusão lógica: o emissor some das telas e deixa de contar no limite do plano grátis;
        // os recibos já emitidos continuam no histórico (com os dados congelados na emissão).
        app.MapDelete("/api/emissores/{id:int}", (HttpContext ctx, int id) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            using var c = Db.Abrir();
            var e = EmissorPorId(c, id);
            if (e == null || e.Excluido) return Erro(404, "NAO_ENCONTRADO", "Emissor não encontrado.");
            using var cmd = c.Cmd("UPDATE emissores SET excluido=1, ativo=0, atualizado_em=$agora WHERE id=$id", ("$agora", Agora()), ("$id", id));
            cmd.ExecuteNonQuery();
            Db.Auditar(c, S(ctx), "EXCLUIR", "emissor", id, e.Nome);
            return Results.Json(new { ok = true });
        });

        // ---------- Contatos ----------
        app.MapGet("/api/contatos", (string? q, bool? inativos) =>
        {
            var todos = new List<ContatoDto>();
            using (var c = Db.Abrir())
            using (var cmd = c.Cmd("SELECT " + ColsContato + " FROM contatos " + (inativos == true ? "" : "WHERE ativo=1")))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) todos.Add(LerContato(r));

            var termos = (q ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<ContatoDto> res = termos.Length == 0 ? todos : todos.Where(x => Casa(x, termos));
            var cmp = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), true);
            return Results.Json(res.OrderBy(x => x.Nome, cmp).ToList());
        });

        app.MapGet("/api/ocupacoes", () =>
        {
            var lista = new List<string>();
            using var c = Db.Abrir();
            using var cmd = c.Cmd("SELECT DISTINCT ocupacao FROM contatos WHERE ocupacao <> ''");
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(r.GetString(0));
            var unicas = lista.GroupBy(x => Db.Normalizar(x)).Select(g => g.First())
                .OrderBy(x => x, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), true)).ToList();
            return Results.Json(unicas);
        });

        app.MapPost("/api/contatos", (HttpContext ctx, ContatoDto d, bool? forcar) =>
        {
            var erro = PrepararContato(d, out var dig);
            if (erro != null) return Erro(400, "DADOS_INVALIDOS", erro);
            using var c = Db.Abrir();
            if (forcar != true)
            {
                var outro = ContatoComMesmoDocumento(c, dig, 0);
                if (outro != null) return Erro(409, "DUPLICADO", "Já existe um contato com este CPF/CNPJ: " + outro + ".");
            }
            using var cmd = c.Cmd(
                "INSERT INTO contatos(nome, documento, documento_digitos, telefone, telefone2, email, endereco, cidade, uf, ocupacao, chave_pix, observacoes, ativo, criado_em, atualizado_em) " +
                "VALUES($n,$doc,$dig,$t1,$t2,$m,$e,$c,$u,$oc,$px,$ob,$a,$agora,$agora); SELECT last_insert_rowid();", ParamsContato(d, dig));
            d.Id = Convert.ToInt32(cmd.ExecuteScalar());
            Db.Auditar(c, S(ctx), "CRIAR", "contato", d.Id, d.Nome);
            return Results.Json(d);
        });

        app.MapPut("/api/contatos/{id:int}", (HttpContext ctx, int id, ContatoDto d, bool? forcar) =>
        {
            var erro = PrepararContato(d, out var dig);
            if (erro != null) return Erro(400, "DADOS_INVALIDOS", erro);
            using var c = Db.Abrir();
            if (forcar != true)
            {
                var outro = ContatoComMesmoDocumento(c, dig, id);
                if (outro != null) return Erro(409, "DUPLICADO", "Já existe um contato com este CPF/CNPJ: " + outro + ".");
            }
            var ps = ParamsContato(d, dig).Append(("$id", (object?)id)).ToArray();
            using var cmd = c.Cmd(
                "UPDATE contatos SET nome=$n, documento=$doc, documento_digitos=$dig, telefone=$t1, telefone2=$t2, email=$m, endereco=$e, cidade=$c, uf=$u, ocupacao=$oc, chave_pix=$px, observacoes=$ob, ativo=$a, atualizado_em=$agora WHERE id=$id", ps);
            if (cmd.ExecuteNonQuery() == 0) return Erro(404, "NAO_ENCONTRADO", "Contato não encontrado.");
            d.Id = id;
            Db.Auditar(c, S(ctx), "EDITAR", "contato", id, d.Nome + (d.Ativo ? "" : " (inativo)"));
            return Results.Json(d);
        });
    }
}
