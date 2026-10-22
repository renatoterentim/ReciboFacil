using System.Globalization;
using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public static class FormatoBackup
{
    static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static DateTime? Data(string? iso) =>
        DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToLocalTime() : null;

    public static string DataHora(string? iso) => Data(iso)?.ToString("dd/MM/yyyy HH:mm", Br) ?? "";

    public static string Tamanho(long bytes) =>
        bytes >= 1024 * 1024 ? (bytes / 1048576.0).ToString("0.0", Br) + " MB" : Math.Max(1, bytes / 1024) + " KB";

    public static string Tipo(string tipo) => tipo switch
    {
        "auto" => "Automático",
        "manual" => "Manual",
        "pre-migracao" => "Antes de atualizar o banco",
        "pre-restauracao" => "Antes de restaurar",
        "pre-atualizacao" => "Antes de atualizar o programa",
        _ => "Outro"
    };

    // Texto e cor do aviso de "último backup" (usado na tela de backup e na barra de status).
    public static (string Texto, bool Alerta) Resumo(BackupStatusDto s)
    {
        var ultimo = Data(s.UltimoOkEm);
        if (ultimo == null) return ("Nenhum backup foi feito ainda", true);
        var texto = "Último backup: " + ultimo.Value.ToString("dd/MM/yyyy HH:mm", Br);
        var falha = Data(s.UltimaFalhaEm);
        bool alerta = DateTime.Now - ultimo.Value > TimeSpan.FromHours(48);
        if (falha != null && falha > ultimo) { texto += " (a última tentativa falhou)"; alerta = true; }
        else if (alerta) texto += " (faz mais de 2 dias)";
        return (texto, alerta);
    }
}

public class BackupForm : Form
{
    readonly Label lblUltimo = new() { Left = 16, Top = 14, Width = 720, Height = 24, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
    readonly Label lblFalha = new() { Left = 16, Top = 40, Width = 720, Height = 20, ForeColor = Color.Firebrick };
    readonly Label lblExtra = new() { Left = 16, Top = 62, Width = 720, Height = 20 };
    readonly CheckBox chkAuto = new() { Text = "Fazer backup automático todos os dias", Left = 16, Top = 26, Width = 380, Height = 24 };
    readonly DateTimePicker dtHora = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Left = 100, Top = 56, Width = 70 };
    readonly NumericUpDown numManter = new() { Left = 340, Top = 56, Width = 60, Minimum = 3, Maximum = 365, Value = 30 };
    readonly TextBox txtExtra = new() { Left = 16, Top = 114, Width = 480, MaxLength = 250 };
    readonly Label lblProximo = new() { Left = 16, Top = 148, Width = 700, Height = 20, ForeColor = SystemColors.GrayText };
    readonly Button btnSalvarConfig = UiLista.Botao("Salvar configuração", 170);
    readonly Button btnAgora = UiLista.Botao("Fazer backup agora", 170);
    readonly Button btnCopia = UiLista.Botao("Salvar uma cópia neste computador...", 260);
    readonly ListView lista = UiLista.Nova(("Data e hora", 140), ("Tipo", 200), ("Arquivo", 290), ("Tamanho", 90));
    bool _configCarregada;

    public BackupForm()
    {
        UiLista.Preparar(this, "Backup", 780, 650);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var grupo = new GroupBox { Text = "Backup automático", Left = 16, Top = 92, Width = 744, Height = 176 };
        grupo.Controls.Add(chkAuto);
        grupo.Controls.Add(new Label { Text = "Horário:", Left = 36, Top = 59, Width = 60, Height = 20 });
        grupo.Controls.Add(dtHora);
        grupo.Controls.Add(new Label { Text = "Manter os últimos", Left = 210, Top = 59, Width = 125, Height = 20 });
        grupo.Controls.Add(numManter);
        grupo.Controls.Add(new Label { Text = "backups automáticos", Left = 408, Top = 59, Width = 140, Height = 20 });
        grupo.Controls.Add(new Label
        {
            Text = "Cópia extra (pasta vista a partir do SERVIDOR, de preferência em outro disco ou em outro computador):",
            Left = 16, Top = 90, Width = 700, Height = 20
        });
        grupo.Controls.Add(txtExtra);
        grupo.Controls.Add(new Label
        {
            Text = "Exemplos: D:\\Backup   ou   \\\\COMPUTADOR2\\Backup", Left = 16, Top = 140, Width = 480, Height = 20, ForeColor = SystemColors.GrayText
        });
        btnSalvarConfig.Left = 520;
        btnSalvarConfig.Top = 110;
        grupo.Controls.Add(btnSalvarConfig);

        lblProximo.Top = 272;

        btnAgora.Left = 16; btnAgora.Top = 298;
        btnCopia.Left = 196; btnCopia.Top = 298;
        var btnFechar = UiLista.Botao("Fechar", 90);
        btnFechar.Left = 670; btnFechar.Top = 298;
        btnFechar.DialogResult = DialogResult.Cancel;

        lista.Dock = DockStyle.None;
        lista.Left = 16; lista.Top = 338; lista.Width = 744; lista.Height = 296;
        lista.Columns[3].TextAlign = HorizontalAlignment.Right;

        Controls.AddRange(new Control[] { lblUltimo, lblFalha, lblExtra, grupo, lblProximo, btnAgora, btnCopia, btnFechar, lista });
        CancelButton = btnFechar;

        btnAgora.Click += async (s, e) => await FazerAgora();
        btnCopia.Click += async (s, e) => await SalvarCopia();
        btnSalvarConfig.Click += async (s, e) => await SalvarConfig();
        lista.SelectedIndexChanged += (s, e) => btnCopia.Enabled = lista.SelectedItems.Count > 0;
        btnCopia.Enabled = false;
        Shown += async (s, e) => await Carregar();
    }

    async Task Carregar()
    {
        try
        {
            Mostrar(await App.Api.BackupStatus());
        }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
    }

    void Mostrar(BackupStatusDto st)
    {
        var (texto, alerta) = FormatoBackup.Resumo(st);
        lblUltimo.Text = texto;
        lblUltimo.ForeColor = alerta ? Color.Firebrick : Color.DarkGreen;

        var ultimoOk = FormatoBackup.Data(st.UltimoOkEm);
        var falha = FormatoBackup.Data(st.UltimaFalhaEm);
        lblFalha.Text = falha != null && (ultimoOk == null || falha > ultimoOk)
            ? "Falha em " + falha.Value.ToString("dd/MM/yyyy HH:mm") + ": " + st.UltimaFalhaMensagem
            : "";

        if (st.Config.PastaExtra.Length == 0)
        {
            lblExtra.ForeColor = SystemColors.GrayText;
            lblExtra.Text = "Cópia extra: não configurada. Recomendado: use outro disco ou outro computador.";
        }
        else if (st.ExtraErro.Length > 0)
        {
            lblExtra.ForeColor = Color.Firebrick;
            lblExtra.Text = st.ExtraErro;
        }
        else
        {
            lblExtra.ForeColor = SystemColors.ControlText;
            lblExtra.Text = "Cópia extra em " + st.Config.PastaExtra + (st.ExtraUltimoOkEm.Length > 0 ? " (última: " + FormatoBackup.DataHora(st.ExtraUltimoOkEm) + ")" : "");
        }

        if (!_configCarregada)
        {
            chkAuto.Checked = st.Config.Automatico;
            numManter.Value = Math.Min(numManter.Maximum, Math.Max(numManter.Minimum, st.Config.Manter));
            txtExtra.Text = st.Config.PastaExtra;
            if (TimeSpan.TryParse(st.Config.Hora, CultureInfo.InvariantCulture, out var h))
                dtHora.Value = DateTime.Today + h;
            _configCarregada = true;
        }
        lblProximo.Text = st.Config.Automatico && st.ProximoAutomatico.Length > 0
            ? "Próximo backup automático: " + st.ProximoAutomatico
            : "O backup automático está desligado.";

        lista.BeginUpdate();
        lista.Items.Clear();
        foreach (var a in st.Arquivos)
            lista.Items.Add(new ListViewItem(new[] { FormatoBackup.DataHora(a.Quando), FormatoBackup.Tipo(a.Tipo), a.Nome, FormatoBackup.Tamanho(a.Bytes) }) { Tag = a });
        lista.EndUpdate();
    }

    async Task FazerAgora()
    {
        btnAgora.Enabled = false;
        var textoAntes = btnAgora.Text;
        btnAgora.Text = "Fazendo backup...";
        try
        {
            Mostrar(await App.Api.BackupAgora());
            Erros.Aviso(this, "Backup concluído.");
        }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
        finally
        {
            if (!IsDisposed) { btnAgora.Enabled = true; btnAgora.Text = textoAntes; }
        }
    }

    async Task SalvarCopia()
    {
        if (lista.SelectedItems.Count == 0 || !(lista.SelectedItems[0].Tag is BackupArquivoDto a)) return;
        using var dlg = new SaveFileDialog { Filter = "Backup do ReciboFácil (*.db)|*.db", FileName = a.Nome, Title = "Salvar cópia do backup" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        btnCopia.Enabled = false;
        try
        {
            await App.Api.BaixarBackup(a.Nome, dlg.FileName);
            Erros.Aviso(this, "Cópia salva em:\n" + dlg.FileName);
        }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
        finally { if (!IsDisposed) btnCopia.Enabled = lista.SelectedItems.Count > 0; }
    }

    async Task SalvarConfig()
    {
        var cfg = new BackupConfigDto
        {
            Automatico = chkAuto.Checked,
            Hora = dtHora.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
            Manter = (int)numManter.Value,
            PastaExtra = txtExtra.Text.Trim()
        };
        btnSalvarConfig.Enabled = false;
        try
        {
            _configCarregada = false;
            Mostrar(await App.Api.BackupSalvarConfig(cfg));
            Erros.Aviso(this, "Configuração salva.");
        }
        catch (Exception ex)
        {
            _configCarregada = true;
            Erros.Mostrar(this, ex);
        }
        finally { if (!IsDisposed) btnSalvarConfig.Enabled = true; }
    }
}
