using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public class MainForm : Form
{
    public bool TrocarUsuario { get; private set; }

    public MainForm()
    {
        Text = App.Titulo;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(900, 560);

        var menu = new MenuStrip();

        var mCad = new ToolStripMenuItem("&Cadastros");
        mCad.DropDownItems.Add("&Contatos", null, (s, e) => Abrir(new ContatosForm()));
        mCad.DropDownItems.Add("&Emissores", null, (s, e) => Abrir(new EmissoresForm()));
        menu.Items.Add(mCad);

        if (App.EhAdmin)
        {
            var mAdm = new ToolStripMenuItem("&Administração");
            mAdm.DropDownItems.Add("&Usuários", null, (s, e) => Abrir(new UsuariosForm()));
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
            Erros.Aviso(this, "ReciboFácil " + Constantes.VersaoApp + "\nServidor: " + App.Config.Servidor));
        menu.Items.Add(mAjuda);

        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel("Usuário: " + App.Usuario.Nome + " (" + App.Usuario.Perfil + ")"));
        status.Items.Add(new ToolStripStatusLabel("Servidor: " + App.Config.Servidor) { Spring = true, TextAlign = ContentAlignment.MiddleRight });

        var boasVindas = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F),
            ForeColor = SystemColors.GrayText,
            Text = "ReciboFácil\n\nUse o menu Cadastros para gerenciar contatos e emissores."
        };

        Controls.Add(boasVindas);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;
        boasVindas.BringToFront();
    }

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
