using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public class ContatoEditForm : Dialogo
{
    readonly ContatoDto? _original;
    readonly TextBox txtNome, txtDoc, txtTel, txtTel2, txtEmail, txtEnd, txtCidade, txtUf, txtPix, txtObs;
    readonly ComboBox cmbOcupacao;
    readonly CheckBox chkAtivo;

    public ContatoEditForm(ContatoDto? original) : base(original == null ? "Novo contato" : "Editar contato")
    {
        _original = original;
        txtNome = Texto("Nome *", 150);
        cmbOcupacao = Combo("Ocupação (pedreiro, pintor...)", true);
        txtDoc = Texto("CPF/CNPJ", 20);
        txtTel = Telefone("Telefone");
        txtTel2 = Telefone("Telefone 2");
        txtEmail = Texto("E-mail", 120);
        txtEnd = Texto("Endereço", 200);
        txtCidade = Texto("Cidade", 80);
        txtUf = Texto("UF", 2);
        txtUf.Width = 60;
        txtPix = ChavePix("Chave Pix");
        txtObs = Multi("Observações", 60);
        chkAtivo = Check("Contato ativo");
        chkAtivo.Checked = true;
        Fim("Salvar");

        if (original != null)
        {
            txtNome.Text = original.Nome; cmbOcupacao.Text = original.Ocupacao; txtDoc.Text = original.Documento;
            txtTel.Text = original.Telefone; txtTel2.Text = original.Telefone2; txtEmail.Text = original.Email;
            txtEnd.Text = original.Endereco; txtCidade.Text = original.Cidade; txtUf.Text = original.Uf;
            txtPix.Text = original.ChavePix; txtObs.Text = original.Observacoes; chkAtivo.Checked = original.Ativo;
        }
        else chkAtivo.Visible = false;

        Shown += async (s, e) =>
        {
            try
            {
                var oc = await App.Api.Ocupacoes();
                var atual = cmbOcupacao.Text;
                cmbOcupacao.Items.AddRange(oc.Cast<object>().ToArray());
                cmbOcupacao.Text = atual;
            }
            catch { }
            txtNome.Focus();
        };

        BtnOk.Click += async (s, e) =>
        {
            if (txtNome.Text.Trim().Length == 0) { Erros.Aviso(this, "Informe o nome do contato."); txtNome.Focus(); return; }
            var d = new ContatoDto
            {
                Nome = txtNome.Text, Ocupacao = cmbOcupacao.Text, Documento = txtDoc.Text, Telefone = txtTel.Text,
                Telefone2 = txtTel2.Text, Email = txtEmail.Text, Endereco = txtEnd.Text, Cidade = txtCidade.Text,
                Uf = txtUf.Text, ChavePix = txtPix.Text, Observacoes = txtObs.Text, Ativo = chkAtivo.Checked
            };
            BtnOk.Enabled = false;
            try
            {
                await Salvar(d, false);
                DialogResult = DialogResult.OK;
            }
            catch (ApiException ex) when (ex.Codigo == "DUPLICADO")
            {
                if (Erros.Confirmar(this, ex.Message + "\n\nDeseja salvar mesmo assim?"))
                {
                    try { await Salvar(d, true); DialogResult = DialogResult.OK; }
                    catch (Exception ex2) { Erros.Mostrar(this, ex2); }
                }
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
            finally { if (!IsDisposed) BtnOk.Enabled = true; }
        };
    }

    Task Salvar(ContatoDto d, bool forcar) =>
        _original == null ? App.Api.CriarContato(d, forcar) : App.Api.EditarContato(_original.Id, d, forcar);
}

public class EmissorEditForm : Dialogo
{
    readonly EmissorDto? _original;

    public EmissorEditForm(EmissorDto? original) : base(original == null ? "Novo emissor" : "Editar emissor")
    {
        _original = original;
        var txtNome = Texto("Nome / razão social *", 150);
        var txtDoc = Texto("CPF/CNPJ *", 20);
        var txtEnd = Texto("Endereço", 200);
        var txtCidade = Texto("Cidade", 80);
        var txtUf = Texto("UF", 2);
        txtUf.Width = 60;
        var txtTel = Telefone("Telefone");
        var txtEmail = Texto("E-mail", 120);
        var txtPix = ChavePix("Chave Pix");
        var txtObs = Multi("Observações", 60);
        var chkAtivo = Check("Emissor ativo");
        chkAtivo.Checked = true;
        Fim("Salvar");

        if (original != null)
        {
            txtNome.Text = original.Nome; txtDoc.Text = original.Documento; txtEnd.Text = original.Endereco;
            txtCidade.Text = original.Cidade; txtUf.Text = original.Uf; txtTel.Text = original.Telefone;
            txtEmail.Text = original.Email; txtPix.Text = original.ChavePix; txtObs.Text = original.Observacoes; chkAtivo.Checked = original.Ativo;
        }
        else chkAtivo.Visible = false;

        BtnOk.Click += async (s, e) =>
        {
            if (txtNome.Text.Trim().Length == 0) { Erros.Aviso(this, "Informe o nome do emissor."); txtNome.Focus(); return; }
            var d = new EmissorDto
            {
                Nome = txtNome.Text, Documento = txtDoc.Text, Endereco = txtEnd.Text, Cidade = txtCidade.Text,
                Uf = txtUf.Text, Telefone = txtTel.Text, Email = txtEmail.Text, ChavePix = txtPix.Text, Observacoes = txtObs.Text, Ativo = chkAtivo.Checked
            };
            BtnOk.Enabled = false;
            try
            {
                if (_original == null) await App.Api.CriarEmissor(d);
                else await App.Api.EditarEmissor(_original.Id, d);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
            finally { if (!IsDisposed) BtnOk.Enabled = true; }
        };
    }
}

public class UsuarioEditForm : Dialogo
{
    public UsuarioEditForm(UsuarioDto? original) : base(original == null ? "Novo usuário" : "Editar usuário")
    {
        var txtLogin = Texto("Usuário (login) *", 30);
        var txtNome = Texto("Nome completo *", 100);
        var cmbPerfil = Combo("Perfil", false, Constantes.Operador, Constantes.Administrador);
        cmbPerfil.SelectedIndex = 0;
        TextBox? txtSenha = null, txtConfirma = null;
        if (original == null)
        {
            txtSenha = Texto("Senha (mínimo 6) *", 100, true);
            txtConfirma = Texto("Confirmar senha *", 100, true);
        }
        Nota("Administrador: gerencia usuários e emissores. Operador: cadastra contatos e emite recibos.", 36);
        Fim("Salvar");

        if (original != null)
        {
            txtLogin.Text = original.Login;
            txtLogin.ReadOnly = true;
            txtNome.Text = original.Nome;
            cmbPerfil.SelectedItem = original.Perfil;
        }

        BtnOk.Click += async (s, e) =>
        {
            if (txtSenha != null && txtSenha.Text != txtConfirma!.Text) { Erros.Aviso(this, "As senhas não conferem."); return; }
            var req = new UsuarioSalvarReq
            {
                Login = txtLogin.Text.Trim(), Nome = txtNome.Text.Trim(),
                Perfil = cmbPerfil.SelectedItem?.ToString() ?? Constantes.Operador, Senha = txtSenha?.Text
            };
            BtnOk.Enabled = false;
            try
            {
                if (original == null) await App.Api.CriarUsuario(req);
                else await App.Api.EditarUsuario(original.Id, req);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
            finally { if (!IsDisposed) BtnOk.Enabled = true; }
        };
    }
}
