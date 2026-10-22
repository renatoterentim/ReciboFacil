using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReciboFacil.Shared;
using static ReciboFacil.Servidor.Resp;

namespace ReciboFacil.Servidor;

public static class EndpointsRecibos
{
    const string ColsRecibo =
        "numero, sentido, emissor_id, contato_id, emissor_json, contato_json, valor_centavos, valor_extenso, referente, data_recibo, cidade, " +
        "emitido_em, emitido_por_login, duplicado_de, cancelado, cancelado_em, cancelado_por_login, motivo_cancelamento, itens_json, mostrar_doc_pagador";

    static string L(string? s) => (s ?? "").Trim();

    static bool DataValida(string? s) =>
        DateTime.TryParseExact(s ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    static int Proximo(SqliteConnection c)
    {
        using var cmd = c.Cmd("SELECT seq FROM sqlite_sequence WHERE name='recibos'");
        var o = cmd.ExecuteScalar();
        int atual = (o == null || o is DBNull) ? 0 : Convert.ToInt32(o);
        return atual + 1;
    }

    static ReciboDto LerRecibo(SqliteDataReader r) => new()
    {
        Numero = r.GetInt32(0),
        Sentido = r.GetString(1),
        EmissorId = r.GetInt32(2),
        ContatoId = r.GetInt32(3),
        Emissor = JsonSerializer.Deserialize<EmissorDto>(r.GetString(4)) ?? new EmissorDto(),
        Contato = JsonSerializer.Deserialize<ContatoDto>(r.GetString(5)) ?? new ContatoDto(),
        ValorCentavos = r.GetInt64(6),
        ValorExtenso = r.GetString(7),
        Referente = r.GetString(8),
        Data = r.GetString(9),
        Cidade = r.GetString(10),
        EmitidoEm = r.GetString(11),
        EmitidoPor = r.GetString(12),
        DuplicadoDe = r.IsDBNull(13) ? null : r.GetInt32(13),
        Cancelado = r.GetInt32(14) == 1,
        CanceladoEm = r.IsDBNull(15) ? "" : r.GetString(15),
        CanceladoPor = r.IsDBNull(16) ? "" : r.GetString(16),
        Motivo = r.IsDBNull(17) ? "" : r.GetString(17),
        Itens = LerItens(r.GetString(18)),
        MostrarDocPagador = r.GetInt32(19) == 1
    };

    static List<ReciboItemDto> LerItens(string json)
    {
        try { return JsonSerializer.Deserialize<List<ReciboItemDto>>(json) ?? new(); }
        catch { return new(); }
    }

    // Texto usado na lista e na busca por descrição: título + descrição dos itens.
    static string ResumoTexto(string referente, List<ReciboItemDto> itens)
    {
        if (itens.Count == 0) return referente;
        var partes = new List<string>();
        if (referente.Length > 0) partes.Add(referente);
        partes.AddRange(itens.Select(i => i.Descricao));
        return string.Join("; ", partes);
    }

    static ReciboDto? PorNumero(SqliteConnection c, int numero)
    {
        using var cmd = c.Cmd("SELECT " + ColsRecibo + " FROM recibos WHERE numero=$n", ("$n", numero));
        using var r = cmd.ExecuteReader();
        return r.Read() ? LerRecibo(r) : null;
    }

    static bool TodosOsTermos(string texto, string[] termos)
    {
        var alvo = Db.Normalizar(texto);
        foreach (var t in termos)
            if (!alvo.Contains(Db.Normalizar(t))) return false;
        return true;
    }

    public static void Mapear(WebApplication app)
    {
        // ---------- Numeração ----------
        app.MapGet("/api/recibos/proximo-numero", () =>
        {
            using var c = Db.Abrir();
            return Results.Json(new ProximoNumeroDto { Proximo = Proximo(c) });
        });

        app.MapPost("/api/recibos/proximo-numero", (HttpContext ctx, NumeroReq req) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            using var c = Db.Abrir();
            int minimo = Proximo(c);
            if (req.Numero < minimo)
                return Erro(400, "DADOS_INVALIDOS", "O próximo número deve ser " + minimo + " ou maior. A numeração nunca volta atrás.");
            if (req.Numero > 999_999_999)
                return Erro(400, "DADOS_INVALIDOS", "Número muito grande.");
            using var cmd = c.Cmd("UPDATE sqlite_sequence SET seq=$s WHERE name='recibos'", ("$s", req.Numero - 1));
            if (cmd.ExecuteNonQuery() == 0)
            {
                using var ins = c.Cmd("INSERT INTO sqlite_sequence(name, seq) VALUES('recibos', $s)", ("$s", req.Numero - 1));
                ins.ExecuteNonQuery();
            }
            Db.Auditar(c, S(ctx), "NUMERACAO", "recibo", null, "Próximo número: " + req.Numero);
            return Results.Json(new ProximoNumeroDto { Proximo = req.Numero });
        });

        // ---------- Emitir ----------
        app.MapPost("/api/recibos", (HttpContext ctx, ReciboCriarReq req) =>
        {
            var sessao = S(ctx);
            var sentido = L(req.Sentido).ToUpperInvariant();
            if (sentido != "PAGAMENTO" && sentido != "RECEBIMENTO")
                return Erro(400, "DADOS_INVALIDOS", "Tipo de recibo inválido.");
            var referente = L(req.Referente);
            var itens = new List<ReciboItemDto>();
            long total = req.ValorCentavos;
            if (req.Itens != null && req.Itens.Count > 0)
            {
                if (req.Itens.Count > LimitesRecibo.MaxItens)
                    return Erro(400, "DADOS_INVALIDOS", "Um recibo aceita no máximo " + LimitesRecibo.MaxItens + " itens.");
                if (referente.Length > 150)
                    return Erro(400, "DADOS_INVALIDOS", "O título é muito longo (máximo 150 caracteres).");
                total = 0;
                int n = 0;
                foreach (var it in req.Itens)
                {
                    n++;
                    var desc = L(it.Descricao);
                    if (desc.Length == 0) return Erro(400, "DADOS_INVALIDOS", "Informe a descrição do item " + n + ".");
                    if (desc.Length > LimitesRecibo.MaxDescricaoItem)
                        return Erro(400, "DADOS_INVALIDOS", "A descrição do item " + n + " é muito longa (máximo " + LimitesRecibo.MaxDescricaoItem + " caracteres).");
                    if (it.ValorCentavos < 1 || it.ValorCentavos > 99_999_999_999L)
                        return Erro(400, "DADOS_INVALIDOS", "Informe um valor válido para o item " + n + ".");
                    total += it.ValorCentavos;
                    itens.Add(new ReciboItemDto { Descricao = desc, ValorCentavos = it.ValorCentavos });
                }
            }
            else
            {
                if (referente.Length == 0)
                    return Erro(400, "DADOS_INVALIDOS", "Informe a que se refere o recibo.");
                if (referente.Length > 1000)
                    return Erro(400, "DADOS_INVALIDOS", "O texto \"referente a\" é muito longo (máximo 1000 caracteres).");
            }
            if (total < 1 || total > 99_999_999_999L)
                return Erro(400, "DADOS_INVALIDOS", "Informe um valor entre R$ 0,01 e R$ 999.999.999,99.");
            if (!DataValida(req.Data))
                return Erro(400, "DADOS_INVALIDOS", "Data inválida.");
            var cidade = L(req.Cidade);

            using var c = Db.Abrir();
            var emissor = EndpointsCadastros.EmissorPorId(c, req.EmissorId);
            if (emissor == null) return Erro(404, "NAO_ENCONTRADO", "Emissor não encontrado.");
            if (!emissor.Ativo) return Erro(400, "DADOS_INVALIDOS", "O emissor está inativo.");
            if (!LicencaServico.EmissorLiberado(c, LicencaServico.Situacao(c), emissor.Id))
                return Erro(403, "LICENCA_NECESSARIA", "O emissor \"" + emissor.Nome + "\" precisa de licença ativa para emitir recibos. " + LicencaServico.MsgLimiteGratis);
            var contato = EndpointsCadastros.ContatoPorId(c, req.ContatoId);
            if (contato == null) return Erro(404, "NAO_ENCONTRADO", "Contato não encontrado.");
            if (!contato.Ativo) return Erro(400, "DADOS_INVALIDOS", "O contato está inativo. Reative-o em Cadastros → Contatos.");

            var extenso = Extenso.Moeda(total);
            int? dup = (req.DuplicadoDe.HasValue && req.DuplicadoDe.Value > 0) ? req.DuplicadoDe : null;

            int numero;
            using (var cmd = c.Cmd(
                "INSERT INTO recibos(sentido, emissor_id, contato_id, emissor_nome, contato_nome, contato_documento, contato_documento_digitos, " +
                "emissor_json, contato_json, valor_centavos, valor_extenso, referente, data_recibo, cidade, emitido_em, emitido_por_id, emitido_por_login, duplicado_de, itens_json, mostrar_doc_pagador) " +
                "VALUES($s,$ei,$ci,$en,$cn,$cd,$cdd,$ej,$cj,$v,$x,$ref,$dt,$cid,$em,$ui,$ul,$dup,$it,$mp); SELECT last_insert_rowid();",
                ("$s", sentido), ("$ei", emissor.Id), ("$ci", contato.Id), ("$en", emissor.Nome), ("$cn", contato.Nome),
                ("$cd", contato.Documento), ("$cdd", Doc.Digitos(contato.Documento)),
                ("$ej", JsonSerializer.Serialize(emissor)), ("$cj", JsonSerializer.Serialize(contato)),
                ("$v", total), ("$x", extenso), ("$ref", referente), ("$dt", req.Data), ("$cid", cidade),
                ("$em", Agora()), ("$ui", sessao.Id), ("$ul", sessao.Login), ("$dup", dup), ("$it", JsonSerializer.Serialize(itens)), ("$mp", req.MostrarDocPagador ? 1 : 0)))
            {
                numero = Convert.ToInt32(cmd.ExecuteScalar());
            }
            Db.Auditar(c, sessao, "EMITIR", "recibo", numero, sentido + " " + total + " centavos" + (itens.Count > 0 ? " (" + itens.Count + " itens)" : ""));
            return Results.Json(PorNumero(c, numero));
        });

        // ---------- Consultar ----------
        app.MapGet("/api/recibos", (int? numero, string? de, string? ate, int? emissorId, string? contato, string? documento,
            long? valorMin, long? valorMax, string? descricao, bool? cancelados) =>
        {
            var sql = new StringBuilder(
                "SELECT numero, data_recibo, sentido, emissor_nome, contato_nome, contato_documento, valor_centavos, referente, cancelado, emitido_por_login, itens_json, motivo_cancelamento, cancelado_por_login, cancelado_em " +
                "FROM recibos WHERE 1=1");
            var ps = new List<(string, object?)>();
            if (numero.HasValue) { sql.Append(" AND numero=$num"); ps.Add(("$num", (object?)numero.Value)); }
            if (DataValida(de)) { sql.Append(" AND data_recibo>=$de"); ps.Add(("$de", (object?)de)); }
            if (DataValida(ate)) { sql.Append(" AND data_recibo<=$ate"); ps.Add(("$ate", (object?)ate)); }
            if (emissorId.HasValue) { sql.Append(" AND emissor_id=$em"); ps.Add(("$em", (object?)emissorId.Value)); }
            var dig = Doc.Digitos(documento);
            if (dig.Length > 0) { sql.Append(" AND contato_documento_digitos LIKE $doc"); ps.Add(("$doc", (object?)("%" + dig + "%"))); }
            if (valorMin.HasValue) { sql.Append(" AND valor_centavos>=$vmin"); ps.Add(("$vmin", (object?)valorMin.Value)); }
            if (valorMax.HasValue) { sql.Append(" AND valor_centavos<=$vmax"); ps.Add(("$vmax", (object?)valorMax.Value)); }
            if (cancelados != true) sql.Append(" AND cancelado=0");
            sql.Append(" ORDER BY numero DESC");

            var termosContato = L(contato).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var termosDesc = L(descricao).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            var lista = new List<ReciboResumoDto>();
            using var c = Db.Abrir();
            using var cmd = c.Cmd(sql.ToString(), ps.ToArray());
            using var r = cmd.ExecuteReader();
            while (r.Read() && lista.Count < 1000)
            {
                var item = new ReciboResumoDto
                {
                    Numero = r.GetInt32(0), Data = r.GetString(1), Sentido = r.GetString(2), EmissorNome = r.GetString(3),
                    ContatoNome = r.GetString(4), ContatoDocumento = r.GetString(5), ValorCentavos = r.GetInt64(6),
                    Referente = ResumoTexto(r.GetString(7), LerItens(r.GetString(10))), Cancelado = r.GetInt32(8) == 1, EmitidoPor = r.GetString(9),
                    Motivo = r.IsDBNull(11) ? "" : r.GetString(11),
                    CanceladoPor = r.IsDBNull(12) ? "" : r.GetString(12),
                    CanceladoEm = r.IsDBNull(13) ? "" : r.GetString(13)
                };
                if (termosContato.Length > 0 && !TodosOsTermos(item.ContatoNome, termosContato)) continue;
                if (termosDesc.Length > 0 && !TodosOsTermos(item.Referente, termosDesc)) continue;
                lista.Add(item);
            }
            return Results.Json(lista);
        });

        app.MapGet("/api/recibos/{numero:int}", (int numero) =>
        {
            using var c = Db.Abrir();
            var r = PorNumero(c, numero);
            return r == null ? Erro(404, "NAO_ENCONTRADO", "Recibo não encontrado.") : Results.Json(r);
        });

        // ---------- Cancelar ----------
        app.MapPost("/api/recibos/{numero:int}/cancelar", (HttpContext ctx, int numero, CancelarReq req) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var motivo = L(req.Motivo);
            if (motivo.Length < 3) return Erro(400, "DADOS_INVALIDOS", "Informe o motivo do cancelamento.");
            if (motivo.Length > 500) return Erro(400, "DADOS_INVALIDOS", "O motivo é muito longo (máximo 500 caracteres).");
            var sessao = S(ctx);
            using var c = Db.Abrir();
            var atual = PorNumero(c, numero);
            if (atual == null) return Erro(404, "NAO_ENCONTRADO", "Recibo não encontrado.");
            if (atual.Cancelado) return Erro(409, "JA_CANCELADO", "Este recibo já está cancelado.");
            using (var cmd = c.Cmd(
                "UPDATE recibos SET cancelado=1, cancelado_em=$e, cancelado_por_id=$i, cancelado_por_login=$l, motivo_cancelamento=$m WHERE numero=$n AND cancelado=0",
                ("$e", Agora()), ("$i", sessao.Id), ("$l", sessao.Login), ("$m", motivo), ("$n", numero)))
            {
                cmd.ExecuteNonQuery();
            }
            Db.Auditar(c, sessao, "CANCELAR", "recibo", numero, motivo);
            return Results.Json(PorNumero(c, numero));
        });

        // ---------- Registro de segunda via ----------
        app.MapPost("/api/recibos/{numero:int}/via", (HttpContext ctx, int numero) =>
        {
            using var c = Db.Abrir();
            if (PorNumero(c, numero) == null) return Erro(404, "NAO_ENCONTRADO", "Recibo não encontrado.");
            Db.Auditar(c, S(ctx), "SEGUNDA_VIA", "recibo", numero);
            return Results.Ok();
        });
    }
}
