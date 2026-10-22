using System.Globalization;
using ReciboFacil.Shared;
using static ReciboFacil.Servidor.Resp;

namespace ReciboFacil.Servidor;

public static class EndpointsBackup
{
    static BackupStatusDto Status()
    {
        var cfg = BackupServico.LerConfig();
        var e = BackupServico.LerEstado();
        string proximo = "";
        if (cfg.Automatico)
        {
            var agora = DateTime.Now;
            var prox = agora.Date + BackupServico.HoraAgendada(cfg);
            if (prox <= agora) prox = prox.AddDays(1);
            proximo = prox.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
        }
        return new BackupStatusDto
        {
            Config = cfg,
            UltimoOkEm = e.UltimoOkEm,
            UltimoOkTipo = e.UltimoOkTipo,
            UltimoOkArquivo = e.UltimoOkArquivo,
            UltimoOkBytes = e.UltimoOkBytes,
            UltimaFalhaEm = e.UltimaFalhaEm,
            UltimaFalhaMensagem = e.UltimaFalhaMensagem,
            ExtraUltimoOkEm = e.ExtraUltimoOkEm,
            ExtraErro = e.ExtraErro,
            ProximoAutomatico = proximo,
            Arquivos = BackupServico.Listar()
        };
    }

    public static void Mapear(WebApplication app)
    {
        app.MapGet("/api/backup", (HttpContext ctx) =>
        {
            var nao = ExigirAdmin(ctx);
            return nao ?? Results.Json(Status());
        });

        app.MapPost("/api/backup/agora", (HttpContext ctx) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var r = BackupServico.Executar("manual");
            using (var c = Db.Abrir())
                Db.Auditar(c, S(ctx), r.Ok ? "BACKUP" : "BACKUP_FALHOU", "backup", null, r.Ok ? r.Arquivo : r.Mensagem);
            if (!r.Ok) return Erro(500, "BACKUP_FALHOU", "O backup falhou: " + r.Mensagem);
            return Results.Json(Status());
        });

        app.MapPut("/api/backup/config", (HttpContext ctx, BackupConfigDto cfg) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;

            var horaTxt = (cfg.Hora ?? "").Trim();
            if (!TimeSpan.TryParse(horaTxt, CultureInfo.InvariantCulture, out var hora) || hora < TimeSpan.Zero || hora >= TimeSpan.FromDays(1))
                return Erro(400, "DADOS_INVALIDOS", "Horário inválido. Use o formato HH:mm, por exemplo 23:00.");
            if (cfg.Manter < 3 || cfg.Manter > 365)
                return Erro(400, "DADOS_INVALIDOS", "Informe de 3 a 365 backups automáticos para manter.");

            var pasta = (cfg.PastaExtra ?? "").Trim();
            if (pasta.Length > 0)
            {
                var erro = BackupServico.TestarPasta(pasta);
                if (erro != null)
                    return Erro(400, "PASTA_INVALIDA", "Não foi possível gravar na pasta de cópia extra (\"" + pasta + "\") a partir do servidor: " + erro);
            }

            var nova = new BackupConfigDto
            {
                Automatico = cfg.Automatico,
                Hora = hora.ToString("hh\\:mm", CultureInfo.InvariantCulture),
                Manter = cfg.Manter,
                PastaExtra = pasta
            };
            BackupServico.SalvarConfig(nova);
            using (var c = Db.Abrir())
                Db.Auditar(c, S(ctx), "CONFIG_BACKUP", "backup", null,
                    (nova.Automatico ? "automático às " + nova.Hora : "automático desligado") + "; manter " + nova.Manter +
                    (pasta.Length > 0 ? "; cópia extra: " + pasta : ""));
            return Results.Json(Status());
        });

        app.MapGet("/api/backup/arquivo/{nome}", (HttpContext ctx, string nome) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            var caminho = BackupServico.CaminhoDoArquivo(nome);
            if (caminho == null) return Erro(404, "NAO_ENCONTRADO", "Backup não encontrado.");
            using (var c = Db.Abrir())
                Db.Auditar(c, S(ctx), "BACKUP_DOWNLOAD", "backup", null, nome);
            return Results.File(caminho, "application/octet-stream", nome);
        });
    }
}
