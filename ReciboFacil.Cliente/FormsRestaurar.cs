using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Data.Sqlite;
using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

// Lógica da restauração (roda no computador servidor, sem depender do servidor estar no ar).
public static class Restauracao
{
    public const string NomeServico = "ReciboFacilServidor";

    public static bool EhAdministrador()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // Procura a pasta Dados: ao lado da pasta do programa (C:\ReciboFacil\Cliente -> C:\ReciboFacil\Dados) ou no local padrão.
    public static string SugerirPastaDados()
    {
        var candidatos = new List<string>();
        try { candidatos.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Dados"))); } catch { }
        candidatos.Add(@"C:\ReciboFacil\Dados");
        foreach (var c in candidatos)
            if (File.Exists(Path.Combine(c, "recibofacil.db"))) return c;
        return candidatos[^1];
    }

    // Aceita um arquivo .db ou uma pasta que contenha recibofacil.db (cópia da pasta Dados inteira).
    public static string? ResolverBackup(string caminho)
    {
        caminho = (caminho ?? "").Trim().Trim('"');
        if (caminho.Length == 0) return null;
        if (File.Exists(caminho)) return caminho;
        if (Directory.Exists(caminho))
        {
            var db = Path.Combine(caminho, "recibofacil.db");
            if (File.Exists(db)) return db;
            var dados = Path.Combine(caminho, "Dados", "recibofacil.db");
            if (File.Exists(dados)) return dados;
        }
        return null;
    }

    static SqliteConnection AbrirLeitura(string caminho)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = caminho, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        var c = new SqliteConnection(cs);
        c.Open();
        return c;
    }

    static object? Escalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    public class InfoBackup
    {
        public int Versao;
        public int Recibos;
        public long UltimoNumero;
        public string? UltimoEmitidoEm;
    }

    // Confere se o arquivo é mesmo um banco do ReciboFácil, íntegro e de versão compatível. Lança exceção com a explicação.
    public static InfoBackup Validar(string arquivo)
    {
        try
        {
            using var c = AbrirLeitura(arquivo);
            var integ = Escalar(c, "PRAGMA integrity_check") as string;
            if (!string.Equals(integ, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("O arquivo está danificado (" + integ + ").");
            foreach (var t in new[] { "esquema", "usuarios", "emissores", "contatos", "recibos" })
                if (Convert.ToInt32(Escalar(c, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='" + t + "'")) == 0)
                    throw new InvalidOperationException("Este arquivo não parece ser um banco do ReciboFácil (falta a tabela \"" + t + "\").");
            var info = new InfoBackup
            {
                Versao = Convert.ToInt32(Escalar(c, "SELECT COALESCE(MAX(versao),0) FROM esquema")),
                Recibos = Convert.ToInt32(Escalar(c, "SELECT COUNT(*) FROM recibos")),
                UltimoNumero = Convert.ToInt64(Escalar(c, "SELECT COALESCE(MAX(numero),0) FROM recibos"))
            };
            if (info.Versao > Constantes.VersaoEsquema)
                throw new InvalidOperationException("Este backup é de uma versão mais nova do ReciboFácil. Atualize o programa antes de restaurar.");
            if (info.Versao < 1)
                throw new InvalidOperationException("Este arquivo não parece ser um banco do ReciboFácil.");
            return info;
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException("Não foi possível ler o arquivo como banco do ReciboFácil: " + ex.Message);
        }
    }

    static long MaiorSequencia(string arquivo)
    {
        try
        {
            using var c = AbrirLeitura(arquivo);
            var seq = Convert.ToInt64(Escalar(c, "SELECT COALESCE(MAX(seq),0) FROM sqlite_sequence WHERE name='recibos'"));
            var max = Convert.ToInt64(Escalar(c, "SELECT COALESCE(MAX(numero),0) FROM recibos"));
            return Math.Max(seq, max);
        }
        catch { return 0; }
    }

    static void AjustarSequencia(string arquivo, long minimo)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = arquivo, Pooling = false }.ToString();
        using var c = new SqliteConnection(cs);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE sqlite_sequence SET seq = MAX(seq, $m) WHERE name='recibos'";
        cmd.Parameters.AddWithValue("$m", minimo);
        if (cmd.ExecuteNonQuery() == 0)
        {
            cmd.CommandText = "INSERT INTO sqlite_sequence(name, seq) VALUES('recibos', $m)";
            cmd.ExecuteNonQuery();
        }
    }

    // Faz a restauração. Passos: para o serviço, guarda cópia de segurança do banco atual, troca o arquivo, religa o serviço.
    public static void Restaurar(string pastaDados, string arquivoBackup, bool manterNumeracao, Action<string> log)
    {
        var atual = Path.Combine(pastaDados, "recibofacil.db");
        var pastaBk = Path.Combine(pastaDados, "Backups");
        Directory.CreateDirectory(pastaBk);

        log("Conferindo o backup...");
        var info = Validar(arquivoBackup);
        log("Backup válido: " + info.Recibos + " recibo(s), último número " + info.UltimoNumero + ".");

        ServiceController? svc = null;
        bool estavaRodando = false;
        try
        {
            var s = new ServiceController(NomeServico);
            _ = s.Status; // lança se o serviço não existe
            svc = s;
        }
        catch { svc = null; }

        try
        {
            if (svc != null && svc.Status != ServiceControllerStatus.Stopped)
            {
                estavaRodando = true;
                log("Parando o servidor do ReciboFácil...");
                svc.Stop();
                svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
            }
            else if (svc == null)
            {
                log("Aviso: o serviço do servidor não foi encontrado neste computador. Certifique-se de que nenhum programa está usando o banco.");
            }

            long seqAtual = 0;
            if (File.Exists(atual))
            {
                seqAtual = MaiorSequencia(atual);
                var seguranca = Path.Combine(pastaBk, "pre-restauracao-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".db");
                log("Guardando cópia de segurança do banco atual...");
                try
                {
                    var cs = new SqliteConnectionStringBuilder { DataSource = atual, Pooling = false }.ToString();
                    using var c = new SqliteConnection(cs);
                    c.Open();
                    using var cmd = c.CreateCommand();
                    cmd.CommandText = "VACUUM INTO '" + seguranca.Replace("'", "''") + "'";
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    File.Copy(atual, seguranca, true);
                }
                log("Cópia de segurança: " + Path.GetFileName(seguranca));
            }

            SqliteConnection.ClearAllPools();
            var novo = atual + ".novo";
            File.Copy(arquivoBackup, novo, true);
            if (manterNumeracao && seqAtual > 0)
            {
                AjustarSequencia(novo, seqAtual);
                log("Numeração preservada: o próximo recibo continuará após o número " + seqAtual + ".");
            }
            SqliteConnection.ClearAllPools();

            foreach (var ext in new[] { "-wal", "-shm", "-journal" })
            {
                var lixo = atual + ext;
                if (File.Exists(lixo)) File.Delete(lixo);
            }
            File.Move(novo, atual, true);
            log("Banco de dados restaurado.");
        }
        finally
        {
            if (svc != null && estavaRodando)
            {
                try
                {
                    log("Iniciando o servidor do ReciboFácil...");
                    svc.Start();
                    svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                    log("Servidor iniciado.");
                }
                catch (Exception ex)
                {
                    log("Não foi possível iniciar o servidor automaticamente: " + ex.Message + " Inicie o serviço \"ReciboFacil Servidor\" manualmente.");
                }
            }
        }
    }
}

public class RestaurarForm : Form
{
    readonly TextBox txtDados = new() { Left = 16, Top = 118, Width = 560 };
    readonly TextBox txtBackup = new() { Left = 16, Top = 186, Width = 560 };
    readonly CheckBox chkNumeracao = new()
    {
        Text = "Manter a numeração atual (o próximo recibo continua depois do último número já usado)",
        Left = 16, Top = 222, Width = 640, Height = 24, Checked = true
    };
    readonly Button btnDados = UiLista.Botao("Procurar...", 100);
    readonly Button btnArquivo = UiLista.Botao("Arquivo...", 100);
    readonly Button btnPasta = UiLista.Botao("Pasta...", 100);
    readonly Button btnRestaurar = UiLista.Botao("Restaurar", 120);
    readonly Button btnFechar = UiLista.Botao("Fechar", 100);
    readonly TextBox log = new() { Left = 16, Top = 296, Width = 680, Height = 150, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    public RestaurarForm()
    {
        UiLista.Preparar(this, "ReciboFácil - Restaurar backup", 712, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(new Label
        {
            Left = 16, Top = 12, Width = 680, Height = 66, ForeColor = Color.Firebrick,
            Text = "ATENÇÃO: restaurar substitui TODOS os dados atuais (recibos, contatos, emissores e usuários) pelos do backup. " +
                   "Os recibos emitidos depois da data do backup serão perdidos (uma cópia de segurança do banco atual é guardada antes). " +
                   "Faça isto no computador servidor e peça para os demais fecharem o programa."
        });
        Controls.Add(new Label { Left = 16, Top = 96, Width = 600, Height = 20, Text = "Pasta de dados do servidor (onde fica o recibofacil.db):" });
        Controls.Add(txtDados);
        btnDados.Left = 590; btnDados.Top = 117;
        Controls.Add(btnDados);
        Controls.Add(new Label { Left = 16, Top = 164, Width = 680, Height = 20, Text = "Backup a restaurar (arquivo .db ou uma pasta Dados copiada):" });
        Controls.Add(txtBackup);
        btnArquivo.Left = 590; btnArquivo.Top = 185;
        btnPasta.Left = 590; btnPasta.Top = 215;
        Controls.AddRange(new Control[] { btnArquivo, btnPasta });
        chkNumeracao.Top = 226;
        Controls.Add(chkNumeracao);
        log.Top = 300; log.Height = 150;
        btnRestaurar.Left = 16; btnRestaurar.Top = 458;
        btnFechar.Left = 596; btnFechar.Top = 458;
        Controls.AddRange(new Control[] { btnRestaurar, btnFechar, log });
        CancelButton = btnFechar;
        btnFechar.Click += (s, e) => Close();
        ClientSize = new Size(712, 500);

        txtDados.Text = Restauracao.SugerirPastaDados();

        btnDados.Click += (s, e) =>
        {
            using var d = new FolderBrowserDialog { Description = "Escolha a pasta de dados do servidor", SelectedPath = txtDados.Text };
            if (d.ShowDialog(this) == DialogResult.OK) txtDados.Text = d.SelectedPath;
        };
        btnArquivo.Click += (s, e) =>
        {
            using var d = new OpenFileDialog { Filter = "Backup do ReciboFácil (*.db)|*.db|Todos os arquivos|*.*", Title = "Escolha o backup" };
            var bk = Path.Combine(txtDados.Text, "Backups");
            if (Directory.Exists(bk)) d.InitialDirectory = bk;
            if (d.ShowDialog(this) == DialogResult.OK) txtBackup.Text = d.FileName;
        };
        btnPasta.Click += (s, e) =>
        {
            using var d = new FolderBrowserDialog { Description = "Escolha a pasta que contém o recibofacil.db" };
            if (d.ShowDialog(this) == DialogResult.OK) txtBackup.Text = d.SelectedPath;
        };
        btnRestaurar.Click += async (s, e) => await Executar();
    }

    void Log(string texto)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Log), texto); return; }
        log.AppendText(texto + Environment.NewLine);
    }

    async Task Executar()
    {
        var pasta = txtDados.Text.Trim().Trim('"');
        if (pasta.Length == 0) { Erros.Aviso(this, "Informe a pasta de dados do servidor."); return; }
        var arquivo = Restauracao.ResolverBackup(txtBackup.Text);
        if (arquivo == null) { Erros.Aviso(this, "Informe um arquivo de backup (.db) ou uma pasta que contenha o recibofacil.db."); return; }

        // Mesma pasta de dados? Não faz sentido restaurar o próprio banco.
        var atual = Path.GetFullPath(Path.Combine(pasta, "recibofacil.db"));
        if (string.Equals(atual, Path.GetFullPath(arquivo), StringComparison.OrdinalIgnoreCase))
        {
            Erros.Aviso(this, "O backup escolhido é o próprio banco em uso. Escolha outro arquivo.");
            return;
        }

        Restauracao.InfoBackup info;
        try { info = Restauracao.Validar(arquivo); }
        catch (Exception ex) { Erros.Aviso(this, ex.Message); return; }

        var textoConf = "Restaurar este backup?\n\n" + arquivo + "\nRecibos no backup: " + info.Recibos + " (último número " + info.UltimoNumero + ")\n\n" +
                        "Os dados atuais serão substituídos. Uma cópia de segurança do banco atual será guardada em " + Path.Combine(pasta, "Backups") + ".";
        if (!Erros.Confirmar(this, textoConf)) return;

        btnRestaurar.Enabled = btnDados.Enabled = btnArquivo.Enabled = btnPasta.Enabled = false;
        log.Clear();
        bool manter = chkNumeracao.Checked;
        try
        {
            await Task.Run(() => Restauracao.Restaurar(pasta, arquivo, manter, Log));
            Log("");
            Log("Concluído. Abra o ReciboFácil normalmente.");
            Erros.Aviso(this, "Restauração concluída.");
        }
        catch (Exception ex)
        {
            Log("ERRO: " + ex.Message);
            Erros.Aviso(this, "A restauração não foi concluída:\n\n" + ex.Message + "\n\nO banco anterior continua guardado na pasta Backups.");
        }
        finally
        {
            if (!IsDisposed) btnRestaurar.Enabled = btnDados.Enabled = btnArquivo.Enabled = btnPasta.Enabled = true;
        }
    }

    // Abre esta ferramenta; se não estiver como administrador, relança o programa pedindo elevação.
    public static void Abrir()
    {
        if (!Restauracao.EhAdministrador())
        {
            try
            {
                Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? Application.ExecutablePath, "--restaurar")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("A restauração precisa de permissão de administrador.", App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }
        Application.Run(new RestaurarForm());
    }
}
