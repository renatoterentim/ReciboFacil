using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

static class UiLista
{
    public static ListView Nova(params (string Titulo, int Largura)[] colunas)
    {
        var l = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, HideSelection = false, Dock = DockStyle.Fill };
        foreach (var c in colunas) l.Columns.Add(c.Titulo, c.Largura);
        return l;
    }

    public static FlowLayoutPanel Barra(DockStyle dock, int altura) =>
        new() { Dock = dock, Height = altura, Padding = new Padding(8, 6, 8, 6), WrapContents = false };

    public static Button Botao(string texto, int largura = 120) => new() { Text = texto, Width = largura, Height = 28 };

    public static void Preparar(Form f, string titulo, int w, int h)
    {
        f.Text = titulo;
        f.StartPosition = FormStartPosition.CenterParent;
        f.AutoScaleDimensions = new SizeF(96F, 96F);
        f.AutoScaleMode = AutoScaleMode.Dpi;
        f.Font = new Font("Segoe UI", 9F);
        f.ClientSize = new Size(w, h);
        f.ShowInTaskbar = false;
        if (App.Icone != null) f.Icon = App.Icone;
    }
}

public class ContatosForm : Form
{
    readonly TextBox txtBusca = new() { Width = 380 };
    readonly CheckBox chkInativos = new() { Text = "Mostrar inativos", AutoSize = true, Margin = new Padding(12, 7, 3, 3) };
    readonly Label lblTotal = new() { AutoSize = true, Margin = new Padding(12, 9, 3, 3) };
    readonly ListView lista = UiLista.Nova(("Nome", 250), ("Ocupação", 130), ("Telefone", 120), ("CPF/CNPJ", 130), ("Cidade/UF", 130), ("Chave Pix", 180));
    readonly Button btnEditar = UiLista.Botao("Editar");
    readonly Button btnAtivo = UiLista.Botao("Inativar", 100);
    readonly System.Windows.Forms.Timer timer = new() { Interval = 300 };
    readonly bool _selecionar;
    int versao;

    // Preenchido quando a janela é aberta em modo de seleção (usado ao emitir recibos).
    public ContatoDto? Escolhido { get; private set; }

    public ContatosForm(bool selecionar = false)
    {
        _selecionar = selecionar;
        UiLista.Preparar(this, selecionar ? "Selecionar contato" : "Contatos", 1000, 560);

        var topo = UiLista.Barra(DockStyle.Top, 42);
        var lblBusca = new Label { Text = "Buscar:", AutoSize = true, Margin = new Padding(3, 9, 3, 3) };
        topo.Controls.AddRange(new Control[] { lblBusca, txtBusca, chkInativos, lblTotal });

        var rodape = UiLista.Barra(DockStyle.Bottom, 44);
        var btnNovo = UiLista.Botao("Novo contato");
        var btnFechar = UiLista.Botao("Fechar", 90);
        btnFechar.DialogResult = DialogResult.Cancel;
        if (selecionar)
        {
            var btnSel = UiLista.Botao("Selecionar", 110);
            btnSel.Click += (s, e) => Escolher();
            rodape.Controls.Add(btnSel);
        }
        rodape.Controls.AddRange(new Control[] { btnNovo, btnEditar, btnAtivo, btnFechar });

        Controls.Add(lista);
        Controls.Add(topo);
        Controls.Add(rodape);
        lista.BringToFront();
        CancelButton = btnFechar;

        timer.Tick += (s, e) => { timer.Stop(); Carregar(); };
        txtBusca.TextChanged += (s, e) => { timer.Stop(); timer.Start(); };
        chkInativos.CheckedChanged += (s, e) => Carregar();
        lista.SelectedIndexChanged += (s, e) => AtualizarBotoes();
        lista.DoubleClick += (s, e) => { if (_selecionar) Escolher(); else EditarSelecionado(); };
        btnNovo.Click += (s, e) => Abrir(null);
        btnEditar.Click += (s, e) => EditarSelecionado();
        btnAtivo.Click += (s, e) => AlternarAtivo();
        Shown += (s, e) => { txtBusca.Focus(); Carregar(); };
        FormClosed += (s, e) => timer.Dispose();
        AtualizarBotoes();
    }

    ContatoDto? Selecionado() => lista.SelectedItems.Count > 0 ? lista.SelectedItems[0].Tag as ContatoDto : null;

    void AtualizarBotoes()
    {
        var c = Selecionado();
        btnEditar.Enabled = c != null;
        btnAtivo.Enabled = c != null;
        btnAtivo.Text = c != null && !c.Ativo ? "Reativar" : "Inativar";
    }

    async void Carregar()
    {
        int v = ++versao;
        try
        {
            var itens = await App.Api.ListarContatos(txtBusca.Text, chkInativos.Checked);
            if (v != versao || IsDisposed) return;
            lista.BeginUpdate();
            lista.Items.Clear();
            foreach (var c in itens)
            {
                var cidade = string.Join("/", new[] { c.Cidade, c.Uf }.Where(x => x.Length > 0));
                var item = new ListViewItem(new[] { c.Ativo ? c.Nome : c.Nome + " (inativo)", c.Ocupacao, c.Telefone, c.Documento, cidade, c.ChavePix }) { Tag = c };
                if (!c.Ativo) item.ForeColor = SystemColors.GrayText;
                lista.Items.Add(item);
            }
            lista.EndUpdate();
            lblTotal.Text = itens.Count + (itens.Count == 1 ? " contato" : " contatos");
            AtualizarBotoes();
        }
        catch (Exception ex)
        {
            if (v == versao && !IsDisposed) Erros.Mostrar(this, ex);
        }
    }

    void EditarSelecionado()
    {
        var c = Selecionado();
        if (c != null) Abrir(c);
    }

    void Escolher()
    {
        var c = Selecionado();
        if (c == null) { Erros.Aviso(this, "Selecione um contato na lista."); return; }
        if (!c.Ativo) { Erros.Aviso(this, "Este contato está inativo. Reative-o antes de usá-lo em um recibo."); return; }
        Escolhido = c;
        DialogResult = DialogResult.OK;
    }

    void Abrir(ContatoDto? c)
    {
        using var f = new ContatoEditForm(c);
        if (f.ShowDialog(this) == DialogResult.OK) Carregar();
    }

    async void AlternarAtivo()
    {
        var c = Selecionado();
        if (c == null) return;
        var acao = c.Ativo ? "inativar" : "reativar";
        if (!Erros.Confirmar(this, "Deseja " + acao + " o contato \"" + c.Nome + "\"?")) return;
        try
        {
            c.Ativo = !c.Ativo;
            await App.Api.EditarContato(c.Id, c, true);
            Carregar();
        }
        catch (Exception ex)
        {
            c.Ativo = !c.Ativo;
            Erros.Mostrar(this, ex);
        }
    }
}

public class EmissoresForm : Form
{
    readonly CheckBox chkInativos = new() { Text = "Mostrar inativos", AutoSize = true, Margin = new Padding(3, 7, 3, 3) };
    readonly ListView lista = UiLista.Nova(("Nome / razão social", 280), ("CPF/CNPJ", 140), ("Cidade/UF", 150), ("Telefone", 130), ("Situação", 80));
    readonly Button btnEditar = UiLista.Botao("Editar");
    readonly Button btnAtivo = UiLista.Botao("Inativar", 100);

    public EmissoresForm()
    {
        UiLista.Preparar(this, "Emissores", 860, 480);

        var topo = UiLista.Barra(DockStyle.Top, 40);
        topo.Controls.Add(chkInativos);

        var rodape = UiLista.Barra(DockStyle.Bottom, 44);
        var btnNovo = UiLista.Botao("Novo emissor");
        var btnFechar = UiLista.Botao("Fechar", 90);
        btnFechar.DialogResult = DialogResult.Cancel;
        rodape.Controls.AddRange(new Control[] { btnNovo, btnEditar, btnAtivo, btnFechar });

        Controls.Add(lista);
        Controls.Add(topo);
        Controls.Add(rodape);
        lista.BringToFront();
        CancelButton = btnFechar;

        chkInativos.CheckedChanged += (s, e) => Carregar();
        lista.SelectedIndexChanged += (s, e) => AtualizarBotoes();
        lista.DoubleClick += (s, e) => EditarSelecionado();
        btnNovo.Click += (s, e) => Abrir(null);
        btnEditar.Click += (s, e) => EditarSelecionado();
        btnAtivo.Click += (s, e) => AlternarAtivo();
        Shown += (s, e) => Carregar();
        AtualizarBotoes();
    }

    EmissorDto? Selecionado() => lista.SelectedItems.Count > 0 ? lista.SelectedItems[0].Tag as EmissorDto : null;

    void AtualizarBotoes()
    {
        var d = Selecionado();
        var admin = App.EhAdmin;
        btnEditar.Enabled = d != null && admin;
        btnAtivo.Enabled = d != null && admin;
        btnAtivo.Text = d != null && !d.Ativo ? "Reativar" : "Inativar";
    }

    async void Carregar()
    {
        try
        {
            var itens = await App.Api.ListarEmissores(chkInativos.Checked);
            if (IsDisposed) return;
            lista.BeginUpdate();
            lista.Items.Clear();
            foreach (var d in itens)
            {
                var cidade = string.Join("/", new[] { d.Cidade, d.Uf }.Where(x => x.Length > 0));
                var item = new ListViewItem(new[] { d.Nome, d.Documento, cidade, d.Telefone, d.Ativo ? "Ativo" : "Inativo" }) { Tag = d };
                if (!d.Ativo) item.ForeColor = SystemColors.GrayText;
                lista.Items.Add(item);
            }
            lista.EndUpdate();
            AtualizarBotoes();
        }
        catch (Exception ex) { if (!IsDisposed) Erros.Mostrar(this, ex); }
    }

    void EditarSelecionado()
    {
        var d = Selecionado();
        if (d != null) Abrir(d);
    }

    void Abrir(EmissorDto? d)
    {
        if (!App.EhAdmin) { Erros.Aviso(this, "Apenas administradores podem cadastrar ou editar emissores."); return; }
        using var f = new EmissorEditForm(d);
        if (f.ShowDialog(this) == DialogResult.OK) Carregar();
    }

    async void AlternarAtivo()
    {
        var d = Selecionado();
        if (d == null) return;
        var acao = d.Ativo ? "inativar" : "reativar";
        if (!Erros.Confirmar(this, "Deseja " + acao + " o emissor \"" + d.Nome + "\"?")) return;
        try
        {
            d.Ativo = !d.Ativo;
            await App.Api.EditarEmissor(d.Id, d);
            Carregar();
        }
        catch (Exception ex)
        {
            d.Ativo = !d.Ativo;
            Erros.Mostrar(this, ex);
        }
    }
}

public class UsuariosForm : Form
{
    readonly ListView lista = UiLista.Nova(("Usuário", 150), ("Nome", 280), ("Perfil", 120), ("Situação", 90));
    readonly Button btnEditar = UiLista.Botao("Editar");
    readonly Button btnSenha = UiLista.Botao("Redefinir senha", 130);
    readonly Button btnAtivo = UiLista.Botao("Suspender", 100);

    public UsuariosForm()
    {
        UiLista.Preparar(this, "Usuários", 780, 440);

        var rodape = UiLista.Barra(DockStyle.Bottom, 44);
        var btnNovo = UiLista.Botao("Novo usuário");
        var btnFechar = UiLista.Botao("Fechar", 90);
        btnFechar.DialogResult = DialogResult.Cancel;
        rodape.Controls.AddRange(new Control[] { btnNovo, btnEditar, btnSenha, btnAtivo, btnFechar });

        Controls.Add(lista);
        Controls.Add(rodape);
        lista.BringToFront();
        CancelButton = btnFechar;

        lista.SelectedIndexChanged += (s, e) => AtualizarBotoes();
        lista.DoubleClick += (s, e) => EditarSelecionado();
        btnNovo.Click += (s, e) => Abrir(null);
        btnEditar.Click += (s, e) => EditarSelecionado();
        btnSenha.Click += (s, e) => Redefinir();
        btnAtivo.Click += (s, e) => AlternarAtivo();
        Shown += (s, e) => Carregar();
        AtualizarBotoes();
    }

    UsuarioDto? Selecionado() => lista.SelectedItems.Count > 0 ? lista.SelectedItems[0].Tag as UsuarioDto : null;

    void AtualizarBotoes()
    {
        var u = Selecionado();
        btnEditar.Enabled = u != null;
        btnSenha.Enabled = u != null;
        btnAtivo.Enabled = u != null;
        btnAtivo.Text = u != null && !u.Ativo ? "Reativar" : "Suspender";
    }

    async void Carregar()
    {
        try
        {
            var itens = await App.Api.ListarUsuarios();
            if (IsDisposed) return;
            lista.BeginUpdate();
            lista.Items.Clear();
            foreach (var u in itens)
            {
                var item = new ListViewItem(new[] { u.Login, u.Nome, u.Perfil, u.Ativo ? "Ativo" : "Suspenso" }) { Tag = u };
                if (!u.Ativo) item.ForeColor = SystemColors.GrayText;
                lista.Items.Add(item);
            }
            lista.EndUpdate();
            AtualizarBotoes();
        }
        catch (Exception ex) { if (!IsDisposed) Erros.Mostrar(this, ex); }
    }

    void EditarSelecionado()
    {
        var u = Selecionado();
        if (u != null) Abrir(u);
    }

    void Abrir(UsuarioDto? u)
    {
        using var f = new UsuarioEditForm(u);
        if (f.ShowDialog(this) == DialogResult.OK) Carregar();
    }

    void Redefinir()
    {
        var u = Selecionado();
        if (u == null) return;
        using var f = new SenhaForm(u.Id, u.Nome);
        f.ShowDialog(this);
    }

    async void AlternarAtivo()
    {
        var u = Selecionado();
        if (u == null) return;
        var suspender = u.Ativo;
        if (!Erros.Confirmar(this, (suspender ? "Suspender" : "Reativar") + " o usuário \"" + u.Login + "\"?")) return;
        try
        {
            if (suspender) await App.Api.Suspender(u.Id);
            else await App.Api.Reativar(u.Id);
            Carregar();
        }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
    }
}
