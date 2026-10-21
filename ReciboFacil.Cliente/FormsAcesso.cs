using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public class ConexaoForm : Dialogo
{
    readonly TextBox txtEndereco;
    public string Endereco => txtEndereco.Text.Trim();

    public ConexaoForm(string atual, string? erro) : base("Conexão com o servidor")
    {
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Nota(erro == null
            ? "Informe o endereço do servidor do ReciboFácil. No computador do servidor use http://localhost:5180; nos demais, o IP do servidor, por exemplo http://192.168.0.10:5180"
            : erro + "\n\nConfira o endereço abaixo e tente novamente.", 64);
        txtEndereco = Texto("Endereço do servidor");
        txtEndereco.Text = atual;
        Fim("Conectar");
        BtnOk.Click += (s, e) =>
        {
            if (txtEndereco.Text.Trim().Length == 0) { Erros.Aviso(this, "Informe o endereço do servidor."); return; }
            try { _ = new Uri(txtEndereco.Text.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase) ? txtEndereco.Text.Trim() : "http://" + txtEndereco.Text.Trim()); }
            catch { Erros.Aviso(this, "Endereço inválido."); return; }
            DialogResult = DialogResult.OK;
        };
    }
}

public class LoginForm : Dialogo
{
    readonly TextBox txtLogin, txtSenha;
    public LoginResp? Resultado { get; private set; }

    public LoginForm() : base("ReciboFácil - Entrar")
    {
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        txtLogin = Texto("Usuário", 30);
        txtSenha = Texto("Senha", 100, true);
        Nota("Servidor: " + App.Config.Servidor, 20);
        Fim("Entrar");

        var btnServidor = new Button { Text = "Servidor...", Width = 90, Height = 28, Left = LX, Top = BtnOk.Top };
        btnServidor.Click += (s, e) =>
        {
            using var f = new ConexaoForm(App.Config.Servidor, null);
            if (f.ShowDialog(this) != DialogResult.OK) return;
            App.Config.Servidor = f.Endereco;
            App.Config.Configurado = true;
            App.Config.Salvar();
            App.Api = new ApiClient(f.Endereco);
            Erros.Aviso(this, "Endereço salvo. Feche e abra o programa novamente para aplicar.");
        };
        Controls.Add(btnServidor);

        BtnOk.Click += async (s, e) =>
        {
            if (txtLogin.Text.Trim().Length == 0 || txtSenha.Text.Length == 0) { Erros.Aviso(this, "Informe usuário e senha."); return; }
            BtnOk.Enabled = false;
            try
            {
                Resultado = await App.Api.Login(txtLogin.Text.Trim(), txtSenha.Text);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSenha.SelectAll();
                txtSenha.Focus();
            }
            finally { if (!IsDisposed) BtnOk.Enabled = true; }
        };
    }
}

public class PrimeiroAdminForm : Dialogo
{
    readonly TextBox txtLogin, txtNome, txtSenha, txtConfirma;
    public LoginResp? Resultado { get; private set; }

    public PrimeiroAdminForm() : base("ReciboFácil - Primeiro acesso")
    {
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        Nota("Este é o primeiro uso do sistema. Crie o usuário administrador, que poderá cadastrar os demais usuários.", 40);
        txtLogin = Texto("Usuário (login)", 30);
        txtNome = Texto("Nome completo", 100);
        txtSenha = Texto("Senha (mínimo 6)", 100, true);
        txtConfirma = Texto("Confirmar senha", 100, true);
        Fim("Criar");

        BtnOk.Click += async (s, e) =>
        {
            if (txtSenha.Text != txtConfirma.Text) { Erros.Aviso(this, "As senhas não conferem."); return; }
            BtnOk.Enabled = false;
            try
            {
                Resultado = await App.Api.PrimeiroAdmin(txtLogin.Text.Trim(), txtNome.Text.Trim(), txtSenha.Text);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { if (!IsDisposed) BtnOk.Enabled = true; }
        };
    }
}

public class SenhaForm : Dialogo
{
    // usuarioId = null: o próprio usuário troca a senha (pede a atual). Com id: administrador redefine.
    public SenhaForm(int? usuarioId, string? nomeUsuario) : base(usuarioId == null ? "Alterar minha senha" : "Redefinir senha")
    {
        if (usuarioId != null) Nota("Nova senha para: " + nomeUsuario, 20);
        var txtAtual = usuarioId == null ? Texto("Senha atual", 100, true) : null;
        var txtNova = Texto("Nova senha (mínimo 6)", 100, true);
        var txtConfirma = Texto("Confirmar nova senha", 100, true);
        Fim("Salvar");

        BtnOk.Click += async (s, e) =>
        {
            if (txtNova.Text != txtConfirma.Text) { Erros.Aviso(this, "As senhas não conferem."); return; }
            BtnOk.Enabled = false;
            try
            {
                if (usuarioId == null) await App.Api.AlterarMinhaSenha(txtAtual!.Text, txtNova.Text);
                else await App.Api.RedefinirSenha(usuarioId.Value, txtNova.Text);
                Erros.Aviso(this, "Senha alterada com sucesso.");
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
            finally { if (!IsDisposed) BtnOk.Enabled = true; }
        };
    }
}
