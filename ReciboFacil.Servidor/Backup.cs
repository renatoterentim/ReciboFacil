using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReciboFacil.Shared;

namespace ReciboFacil.Servidor;

public class BackupEstado
{
    public string UltimoOkEm { get; set; } = "";          // UTC (ISO)
    public string UltimoOkTipo { get; set; } = "";
    public string UltimoOkArquivo { get; set; } = "";
    public long UltimoOkBytes { get; set; }
    public string UltimoAutoOkEm { get; set; } = "";
    public string UltimaTentativaAutoEm { get; set; } = "";
    public string UltimaFalhaEm { get; set; } = "";
    public string UltimaFalhaMensagem { get; set; } = "";
    public string ExtraUltimoOkEm { get; set; } = "";
    public string ExtraErro { get; set; } = "";
}

public static class BackupServico
{
    static readonly object Trava = new();
    static readonly JsonSerializerOptions J = new() { WriteIndented = true };
    static readonly string[] Tipos = { "auto", "manual", "pre-migracao", "pre-restauracao", "pre-atualizacao" };

    public static string PastaBackups => Path.Combine(Db.DataDir, "Backups");
    static string ArqConfig => Path.Combine(Db.DataDir, "backup-config.json");
    static string ArqEstado => Path.Combine(Db.DataDir, "backup-estado.json");

    static string AgoraUtc() => DateTime.UtcNow.ToString("o");

    public static BackupConfigDto LerConfig()
    {
        try
        {
            if (File.Exists(ArqConfig))
                return JsonSerializer.Deserialize<BackupConfigDto>(File.ReadAllText(ArqConfig)) ?? new BackupConfigDto();
        }
        catch { }
        return new BackupConfigDto();
    }

    public static void SalvarConfig(BackupConfigDto c)
    {
        Directory.CreateDirectory(Db.DataDir);
        File.WriteAllText(ArqConfig, JsonSerializer.Serialize(c, J));
    }

    public static BackupEstado LerEstado()
    {
        try
        {
            if (File.Exists(ArqEstado))
                return JsonSerializer.Deserialize<BackupEstado>(File.ReadAllText(ArqEstado)) ?? new BackupEstado();
        }
        catch { }
        return new BackupEstado();
    }

    static void SalvarEstado(BackupEstado e)
    {
        try
        {
            Directory.CreateDirectory(Db.DataDir);
            File.WriteAllText(ArqEstado, JsonSerializer.Serialize(e, J));
        }
        catch { }
    }

    // Cria o backup com o servidor funcionando (cópia consistente do banco), confere o arquivo e aplica a retenção.
    public static (bool Ok, string Mensagem, string Arquivo) Executar(string tipo)
    {
        lock (Trava)
        {
            var estado = LerEstado();
            var cfg = LerConfig();
            if (tipo == "auto") estado.UltimaTentativaAutoEm = AgoraUtc();
            try
            {
                Directory.CreateDirectory(PastaBackups);
                var baseNome = tipo + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var nome = baseNome + ".db";
                for (int i = 2; File.Exists(Path.Combine(PastaBackups, nome)); i++) nome = baseNome + "-" + i + ".db";
                var destino = Path.Combine(PastaBackups, nome);

                using (var c = Db.Abrir())
                using (var cmd = c.Cmd("VACUUM INTO '" + destino.Replace("'", "''") + "'"))
                    cmd.ExecuteNonQuery();

                var erroVerif = Verificar(destino);
                if (erroVerif != null)
                {
                    try { File.Delete(destino); } catch { }
                    throw new InvalidOperationException("O backup gerado não passou na verificação: " + erroVerif);
                }

                estado.UltimoOkEm = AgoraUtc();
                estado.UltimoOkTipo = tipo;
                estado.UltimoOkArquivo = nome;
                estado.UltimoOkBytes = new FileInfo(destino).Length;
                if (tipo == "auto") estado.UltimoAutoOkEm = estado.UltimoOkEm;
                estado.UltimaFalhaEm = "";
                estado.UltimaFalhaMensagem = "";

                CopiarExtra(cfg, destino, nome, estado);
                ApagarAntigos(PastaBackups, cfg.Manter);
                SalvarEstado(estado);
                return (true, "Backup concluído: " + nome, nome);
            }
            catch (Exception ex)
            {
                estado.UltimaFalhaEm = AgoraUtc();
                estado.UltimaFalhaMensagem = ex.Message;
                SalvarEstado(estado);
                return (false, ex.Message, "");
            }
        }
    }

    static void CopiarExtra(BackupConfigDto cfg, string origem, string nome, BackupEstado estado)
    {
        var pasta = (cfg.PastaExtra ?? "").Trim();
        if (pasta.Length == 0) { estado.ExtraErro = ""; return; }
        try
        {
            Directory.CreateDirectory(pasta);
            File.Copy(origem, Path.Combine(pasta, nome), false);
            estado.ExtraUltimoOkEm = AgoraUtc();
            estado.ExtraErro = "";
            ApagarAntigos(pasta, cfg.Manter);
        }
        catch (Exception ex)
        {
            estado.ExtraErro = "Falha ao copiar para " + pasta + ": " + ex.Message;
        }
    }

    // Só apaga backups automáticos antigos. Os manuais e os de segurança (antes de atualizar/restaurar) nunca são apagados.
    static void ApagarAntigos(string pasta, int manter)
    {
        try
        {
            var arquivos = new DirectoryInfo(pasta).GetFiles("auto-*.db").OrderByDescending(f => f.Name).ToList();
            foreach (var f in arquivos.Skip(Math.Max(1, manter)))
            {
                try { f.Delete(); } catch { }
            }
        }
        catch { }
    }

    // Abre o arquivo só para leitura e confere a integridade. Devolve a mensagem de erro (ou null se estiver tudo certo).
    public static string? Verificar(string caminho)
    {
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = caminho, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            using var c = new SqliteConnection(cs);
            c.Open();
            using var cmd = c.Cmd("PRAGMA integrity_check");
            var r = cmd.ExecuteScalar() as string;
            return string.Equals(r, "ok", StringComparison.OrdinalIgnoreCase) ? null : (r ?? "resultado vazio");
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static string TipoDoNome(string nome)
    {
        foreach (var t in Tipos)
            if (nome.StartsWith(t + "-", StringComparison.OrdinalIgnoreCase)) return t;
        return "outro";
    }

    public static List<BackupArquivoDto> Listar()
    {
        var lista = new List<BackupArquivoDto>();
        try
        {
            if (!Directory.Exists(PastaBackups)) return lista;
            foreach (var f in new DirectoryInfo(PastaBackups).GetFiles("*.db").OrderByDescending(x => x.LastWriteTimeUtc))
                lista.Add(new BackupArquivoDto { Nome = f.Name, Tipo = TipoDoNome(f.Name), Quando = f.LastWriteTimeUtc.ToString("o"), Bytes = f.Length });
        }
        catch { }
        return lista;
    }

    // Só aceita nomes simples de arquivos .db dentro da pasta de backups (sem caminhos).
    public static string? CaminhoDoArquivo(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || nome != Path.GetFileName(nome) ||
            !nome.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) return null;
        var p = Path.Combine(PastaBackups, nome);
        return File.Exists(p) ? p : null;
    }

    // Tenta criar e apagar um arquivo de teste na pasta. Devolve a mensagem de erro (ou null).
    public static string? TestarPasta(string pasta)
    {
        try
        {
            Directory.CreateDirectory(pasta);
            var teste = Path.Combine(pasta, ".teste-recibofacil-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(teste, "teste");
            File.Delete(teste);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static TimeSpan HoraAgendada(BackupConfigDto cfg) =>
        TimeSpan.TryParse(cfg.Hora, CultureInfo.InvariantCulture, out var h) && h >= TimeSpan.Zero && h < TimeSpan.FromDays(1)
            ? h
            : new TimeSpan(23, 0, 0);
}

// Verifica a cada minuto se já passou da hora do backup automático e ainda não houve um hoje.
// Se o computador estava desligado na hora marcada, o backup sai assim que o servidor volta.
public class BackupAgendador : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try { Verificar(); } catch { }
            try { await Task.Delay(TimeSpan.FromSeconds(60), ct); } catch (OperationCanceledException) { break; }
        }
    }

    static void Verificar()
    {
        var cfg = BackupServico.LerConfig();
        if (!cfg.Automatico) return;
        var agora = DateTime.Now;
        var agendado = agora.Date + BackupServico.HoraAgendada(cfg);
        if (agora < agendado) return;

        var est = BackupServico.LerEstado();
        if (DateTime.TryParse(est.UltimoAutoOkEm, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ok) &&
            ok.ToLocalTime() >= agendado) return;
        if (DateTime.TryParse(est.UltimaTentativaAutoEm, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var tent) &&
            DateTime.UtcNow - tent.ToUniversalTime() < TimeSpan.FromMinutes(30)) return;

        BackupServico.Executar("auto");
    }
}
