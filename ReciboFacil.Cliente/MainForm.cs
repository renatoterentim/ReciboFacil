using System.Diagnostics;
using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public class MainForm : Form
{
    public bool TrocarUsuario { get; private set; }
    readonly ToolStripStatusLabel lblBackup = new("") { BorderSides = ToolStripStatusLabelBorderSides.Left };

    public MainForm()
    {
        Text = App.Titulo;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(900, 560);
        if (App.Icone != null) Icon = App.Icone;

        var menu = new MenuStrip();

        var mRec = new ToolStripMenuItem("&Recibos");
        mRec.DropDownItems.Add(new ToolStripMenuItem("&Novo recibo", null, (s, e) => NovoRecibo()) { ShortcutKeys = Keys.Control | Keys.N });
        mRec.DropDownItems.Add(new ToolStripMenuItem("&Histórico de recibos", null, (s, e) => Abrir(new HistoricoForm())) { ShortcutKeys = Keys.Control | Keys.H });
        menu.Items.Add(mRec);

        var mCad = new ToolStripMenuItem("&Cadastros");
        mCad.DropDownItems.Add("&Contatos", null, (s, e) => Abrir(new ContatosForm()));
        mCad.DropDownItems.Add("&Emissores", null, (s, e) => Abrir(new EmissoresForm()));
        menu.Items.Add(mCad);

        if (App.EhAdmin)
        {
            var mAdm = new ToolStripMenuItem("&Administração");
            mAdm.DropDownItems.Add("&Usuários", null, (s, e) => Abrir(new UsuariosForm()));
            mAdm.DropDownItems.Add("&Numeração dos recibos", null, (s, e) => Abrir(new NumeracaoForm()));
            mAdm.DropDownItems.Add(new ToolStripSeparator());
            mAdm.DropDownItems.Add("&Backup...", null, (s, e) => { Abrir(new BackupForm()); _ = AtualizarBackup(); });
            mAdm.DropDownItems.Add("&Restaurar um backup...", null, (s, e) => AbrirRestauracao());
            menu.Items.Add(mAdm);
        }

        var mConta = new ToolStripMenuItem("C&onta");
        mConta.DropDownItems.Add("Alterar minha &senha", null, (s, e) => Abrir(new SenhaForm(null, null)));
        mConta.DropDownItems.Add("&Trocar de usuário", null, async (s, e) => { await App.Api.Logout(); TrocarUsuario = true; Close(); });
        mConta.DropDownItems.Add(new ToolStripSeparator());
        mConta.DropDownItems.Add("&Sair", null, async (s, e) => { await App.Api.Logout(); Close(); });
        menu.Items.Add(mConta);

        var mAjuda = new ToolStripMenuItem("A&juda");
        mAjuda.DropDownItems.Add("&Sobre", null, (s, e) =>
            Erros.Aviso(this, "ReciboFácil " + Constantes.VersaoApp +
                "\n\nDesenvolvedor: " + Constantes.Desenvolvedor +
                "\n\nServidor: " + App.Config.Servidor));
        menu.Items.Add(mAjuda);

        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel("Usuário: " + App.Usuario.Nome + " (" + App.Usuario.Perfil + ")"));
        if (App.EhAdmin) status.Items.Add(lblBackup);
        status.Items.Add(new ToolStripStatusLabel("Servidor: " + App.Config.Servidor) { Spring = true, TextAlign = ContentAlignment.MiddleRight });

        // Tela inicial com os dois atalhos principais, centralizados.
        var centro = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        centro.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        centro.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var painel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Anchor = AnchorStyles.None
        };
        painel.Controls.Add(new Label
        {
            Text = "ReciboFácil",
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 24)
        });
        var btnNovo = new Button { Text = "Novo recibo", Width = 280, Height = 56, Font = new Font("Segoe UI", 12F), Margin = new Padding(0, 0, 0, 12) };
        var btnHist = new Button { Text = "Histórico de recibos", Width = 280, Height = 56, Font = new Font("Segoe UI", 12F), Margin = new Padding(0, 0, 0, 12) };
        btnNovo.Click += (s, e) => NovoRecibo();
        btnHist.Click += (s, e) => Abrir(new HistoricoForm());
        painel.Controls.Add(btnNovo);
        painel.Controls.Add(btnHist);
        centro.Controls.Add(painel, 0, 0);

        Controls.Add(centro);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;
        centro.BringToFront();

        if (App.EhAdmin) Shown += async (s, e) => await AtualizarBackup();
    }

    async Task AtualizarBackup()
    {
        try
        {
            var (texto, alerta) = FormatoBackup.Resumo(await App.Api.BackupStatus());
            lblBackup.Text = texto;
            lblBackup.ForeColor = alerta ? Color.Firebrick : SystemColors.ControlText;
        }
        catch { lblBackup.Text = ""; }
    }

    void AbrirRestauracao()
    {
        if (!Erros.Confirmar(this,
            "A restauração deve ser feita no computador SERVIDOR (onde está a pasta C:\\ReciboFacil\\Dados), " +
            "com os demais computadores fora do programa.\n\nAbrir a ferramenta de restauração neste computador?")) return;
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? Application.ExecutablePath, "--restaurar") { UseShellExecute = true });
        }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
    }

    void NovoRecibo() => Abrir(new ReciboForm());

    void Abrir(Form f)
    {
        using (f) f.ShowDialog(this);
    }

    public void ForcarLogin()
    {
        TrocarUsuario = true;
        BeginInvoke(new Action(Close));
    }
}
