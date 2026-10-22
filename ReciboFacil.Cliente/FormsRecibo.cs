using System.Globalization;
using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public static class Dinheiro
{
    static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    // Aceita "450", "450,00", "1.500,00", "R$ 1.500,00" e também "450.50" (ponto com 1 ou 2 casas = decimal).
    public static bool TentarLer(string? texto, out long centavos)
    {
        centavos = 0;
        var t = (texto ?? "").Replace("R$", "").Replace(" ", "").Trim();
        if (t.Length == 0) return false;
        int ultimoPonto = t.LastIndexOf('.');
        bool pontoDecimal = !t.Contains(',') && ultimoPonto >= 0 && t.IndexOf('.') == ultimoPonto && t.Length - ultimoPonto - 1 is >= 1 and <= 2;
        if (pontoDecimal) t = t.Replace(',', '.');
        else t = t.Replace(".", "").Replace(",", ".");
        if (!decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d)) return false;
        if (d <= 0m || d > 999_999_999.99m) return false;
        centavos = (long)Math.Round(d * 100m, MidpointRounding.AwayFromZero);
        return centavos > 0;
    }

    public static string Formatar(long centavos) => (centavos / 100m).ToString("C2", Br);

    public static string Maiuscula(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpper(s[0], Br) + s.Substring(1);
}

public class Opcao<T>
{
    public T Valor { get; }
    public string Texto { get; }
    public Opcao(T valor, string texto) { Valor = valor; Texto = texto; }
    public override string ToString() => Texto;
}

public class ReciboForm : Dialogo
{
    static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
    const int AltSimples = 170, AltItens = 304;

    readonly ComboBox cmbEmissor, cmbSentido;
    readonly TextBox txtContato, txtCidade;
    readonly CheckBox chkItens, chkDocPagador;
    readonly Panel pnlModo, pnlSimples, pnlItens;
    // modo simples (um valor e um texto)
    readonly TextBox txtValor, txtReferente;
    readonly Label lblExtenso;
    // modo itens (vários serviços, cada um com valor; o total é a soma)
    readonly TextBox txtTitulo;
    readonly DataGridView grid;
    readonly Label lblTotal, lblExtensoItens;
    readonly DateTimePicker dtData;
    readonly ReciboDto? _modelo;
    ContatoDto? _contato;
    string _cidadeAuto = "";

    static Label Rot(string texto, int y) =>
        new Label { Text = texto, Left = 0, Top = y + 3, Width = 150, Height = 20, AutoSize = false };

    static string CidadeDoEmissor(EmissorDto e) =>
        string.Join(" - ", new[] { (e.Cidade ?? "").Trim(), (e.Uf ?? "").Trim().ToUpperInvariant() }.Where(x => x.Length > 0));

    // "modelo" preenche o formulário a partir de um recibo anterior (duplicar); o novo recibo recebe número novo.
    public ReciboForm(ReciboDto? modelo = null) : base(modelo == null ? "Novo recibo" : "Duplicar recibo nº " + modelo.Numero)
    {
        _modelo = modelo;
        cmbEmissor = Combo("Emissor", false);
        cmbSentido = Combo("Tipo de recibo", false, "Pagamento: o emissor paga ao contato", "Recebimento: o emissor recebe do contato");
        cmbSentido.SelectedIndex = 0;

        txtContato = Texto("Contato", 150);
        txtContato.ReadOnly = true;
        txtContato.Width = CW - 110;
        var btnContato = new Button { Text = "Selecionar...", Left = CX + CW - 104, Top = txtContato.Top - 2, Width = 104, Height = 26 };
        Controls.Add(btnContato);

        chkItens = Check("Detalhar em vários itens (soma automática)");
        chkDocPagador = Check("Mostrar CPF/CNPJ do pagador no recibo");

        pnlModo = Adicionar(new Panel(), AltSimples);
        pnlSimples = new Panel { Dock = DockStyle.Fill };
        pnlItens = new Panel { Dock = DockStyle.Fill, Visible = false };
        pnlModo.Controls.Add(pnlSimples);
        pnlModo.Controls.Add(pnlItens);

        // --- modo simples ---
        pnlSimples.Controls.Add(Rot("Valor (R$)", 0));
        txtValor = new TextBox { Left = 156, Top = 0, Width = 360, MaxLength = 20 };
        pnlSimples.Controls.Add(txtValor);
        lblExtenso = new Label { Left = 0, Top = 32, Width = 516, Height = 34, AutoSize = false };
        pnlSimples.Controls.Add(lblExtenso);
        pnlSimples.Controls.Add(Rot("Referente a", 72));
        txtReferente = new TextBox { Left = 156, Top = 72, Width = 360, Height = 90, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, MaxLength = 1000 };
        pnlSimples.Controls.Add(txtReferente);

        // --- modo itens ---
        pnlItens.Controls.Add(Rot("Título (opcional)", 0));
        txtTitulo = new TextBox { Left = 156, Top = 0, Width = 360, MaxLength = 150 };
        pnlItens.Controls.Add(txtTitulo);

        grid = new DataGridView
        {
            Left = 0, Top = 34, Width = 516, Height = 200,
            AllowUserToAddRows = true, AllowUserToDeleteRows = true,
            AllowUserToResizeRows = false, AllowUserToResizeColumns = false, AllowUserToOrderColumns = false,
            RowHeadersVisible = false, MultiSelect = false,
            BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 24,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        grid.RowTemplate.Height = 22;
        var colDesc = new DataGridViewTextBoxColumn
        {
            HeaderText = "Descrição do serviço",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MaxInputLength = LimitesRecibo.MaxDescricaoItem,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var colValor = new DataGridViewTextBoxColumn
        {
            HeaderText = "Valor (R$)",
            Width = 120,
            MaxInputLength = 15,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        colValor.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns.AddRange(colDesc, colValor);
        pnlItens.Controls.Add(grid);

        lblTotal = new Label { Left = 0, Top = 240, Width = 390, Height = 22, AutoSize = false, Font = new Font(Font, FontStyle.Bold) };
        pnlItens.Controls.Add(lblTotal);
        var btnRemover = new Button { Text = "Remover linha", Left = 400, Top = 236, Width = 116, Height = 26 };
        pnlItens.Controls.Add(btnRemover);
        lblExtensoItens = new Label { Left = 0, Top = 266, Width = 516, Height = 34, AutoSize = false };
        pnlItens.Controls.Add(lblExtensoItens);

        // --- campos comuns ---
        dtData = Data("Data do recibo");
        txtCidade = Texto("Cidade / UF", 80);
        Fim("Visualizar e emitir");
        BtnOk.Width = 150;
        BtnOk.Left = CX + CW - 254;

        // --- eventos ---
        btnContato.Click += (s, e) => EscolherContato();
        chkItens.CheckedChanged += (s, e) => AlternarModo();
        txtValor.TextChanged += (s, e) => AtualizarExtenso();
        btnRemover.Click += (s, e) => RemoverLinha();
        grid.CellEndEdit += (s, e) => AoEditarCelula(e.RowIndex, e.ColumnIndex);
        grid.CellValueChanged += (s, e) => RecalcularItens();
        grid.RowsRemoved += (s, e) => { RecalcularItens(); AgendarLimite(); };
        cmbEmissor.SelectedIndexChanged += (s, e) =>
        {
            if (cmbEmissor.SelectedItem is Opcao<EmissorDto> o)
            {
                var atual = txtCidade.Text.Trim();
                if (atual.Length == 0 || atual == _cidadeAuto)
                {
                    _cidadeAuto = CidadeDoEmissor(o.Valor);
                    txtCidade.Text = _cidadeAuto;
                }
            }
        };

        // --- preenchimento a partir de um recibo anterior ---
        if (modelo != null)
        {
            cmbSentido.SelectedIndex = modelo.Sentido == "RECEBIMENTO" ? 1 : 0;
            _contato = modelo.Contato;
            txtContato.Text = modelo.Contato.Nome;
            txtCidade.Text = ReciboDesenho.CidadeCompleta(modelo);
            chkDocPagador.Checked = modelo.MostrarDocPagador;
            if (modelo.Itens.Count > 0)
            {
                txtTitulo.Text = modelo.Referente;
                foreach (var it in modelo.Itens)
                    grid.Rows.Add(it.Descricao, (it.ValorCentavos / 100m).ToString("N2", Br));
                chkItens.Checked = true;
                LimitarLinhas();
            }
            else
            {
                txtValor.Text = (modelo.ValorCentavos / 100m).ToString("N2", Br);
                txtReferente.Text = modelo.Referente;
            }
        }

        Shown += async (s, e) =>
        {
            try
            {
                var lista = await App.Api.ListarEmissores(false);
                foreach (var em in lista)
                    cmbEmissor.Items.Add(new Opcao<EmissorDto>(em, em.Nome + " - " + em.Documento));
                int alvo = _modelo != null ? _modelo.EmissorId : App.Config.UltimoEmissorId;
                for (int i = 0; i < cmbEmissor.Items.Count; i++)
                {
                    if (cmbEmissor.Items[i] is Opcao<EmissorDto> o && o.Valor.Id == alvo) { cmbEmissor.SelectedIndex = i; break; }
                }
                if (cmbEmissor.SelectedIndex < 0 && cmbEmissor.Items.Count > 0) cmbEmissor.SelectedIndex = 0;
                if (lista.Count == 0)
                {
                    Erros.Aviso(this, "Cadastre ao menos um emissor antes de emitir recibos (menu Cadastros → Emissores).");
                    DialogResult = DialogResult.Cancel;
                    return;
                }
                if (_contato == null) btnContato.Focus();
                else if (chkItens.Checked) grid.Focus();
                else txtValor.Focus();
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
        };

        BtnOk.Click += (s, e) => Visualizar();
        AtualizarExtenso();
        RecalcularItens();
    }

    void AlternarModo()
    {
        bool itens = chkItens.Checked;
        pnlSimples.Visible = !itens;
        pnlItens.Visible = itens;
        int novo = itens ? AltItens : AltSimples;
        int delta = novo - pnlModo.Height;
        if (delta == 0) return;
        int topo = pnlModo.Top;
        foreach (Control c in Controls)
            if (c != pnlModo && c.Top > topo) c.Top += delta;
        pnlModo.Height = novo;
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + delta);
        if (Visible)
        {
            var area = Screen.FromControl(this).WorkingArea;
            Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
        }
    }

    void EscolherContato()
    {
        using var f = new ContatosForm(true);
        if (f.ShowDialog(this) == DialogResult.OK && f.Escolhido != null)
        {
            _contato = f.Escolhido;
            txtContato.Text = _contato.Nome;
        }
    }

    void AtualizarExtenso()
    {
        lblExtenso.Text = Dinheiro.TentarLer(txtValor.Text, out var c)
            ? "Por extenso: " + Dinheiro.Maiuscula(Extenso.Moeda(c))
            : "";
    }

    // ---------- grade de itens ----------
    void AoEditarCelula(int linha, int coluna)
    {
        if (linha < 0 || linha >= grid.Rows.Count) return;
        if (coluna == 1)
        {
            var cel = grid.Rows[linha].Cells[1];
            var txt = (cel.Value as string ?? "").Trim();
            if (txt.Length == 0) cel.ErrorText = "";
            else if (Dinheiro.TentarLer(txt, out var c))
            {
                cel.Value = (c / 100m).ToString("N2", Br);
                cel.ErrorText = "";
            }
            else cel.ErrorText = "Valor inválido";
        }
        RecalcularItens();
        AgendarLimite();
    }

    void RecalcularItens()
    {
        long total = 0;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            if (Dinheiro.TentarLer(row.Cells[1].Value as string, out var c)) total += c;
        }
        lblTotal.Text = "Total: " + Dinheiro.Formatar(total);
        lblExtensoItens.Text = total > 0 && total <= 99_999_999_999L
            ? "Por extenso: " + Dinheiro.Maiuscula(Extenso.Moeda(total))
            : "";
    }

    // Quando chega ao máximo de itens, esconde a linha vazia de "novo item".
    void LimitarLinhas()
    {
        int linhas = 0;
        foreach (DataGridViewRow row in grid.Rows) if (!row.IsNewRow) linhas++;
        grid.AllowUserToAddRows = linhas < LimitesRecibo.MaxItens;
    }

    void AgendarLimite()
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(LimitarLinhas));
    }

    void RemoverLinha()
    {
        try
        {
            var linha = grid.CurrentRow;
            if (linha == null || linha.IsNewRow) return;
            grid.CancelEdit();
            grid.Rows.Remove(linha);
        }
        catch { }
    }

    string? ColetarItens(List<ReciboItemDto> destino)
    {
        grid.EndEdit();
        int n = 0;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            var desc = (row.Cells[0].Value as string ?? "").Trim();
            var val = (row.Cells[1].Value as string ?? "").Trim();
            if (desc.Length == 0 && val.Length == 0) continue;
            n++;
            if (desc.Length == 0) return "Informe a descrição do item " + n + ".";
            if (!Dinheiro.TentarLer(val, out var c)) return "Informe um valor válido para o item " + n + ".";
            destino.Add(new ReciboItemDto { Descricao = desc, ValorCentavos = c });
        }
        if (destino.Count == 0) return "Adicione ao menos um item com descrição e valor.";
        if (destino.Count > LimitesRecibo.MaxItens) return "Um recibo aceita no máximo " + LimitesRecibo.MaxItens + " itens.";
        return null;
    }

    void Visualizar()
    {
        if (!(cmbEmissor.SelectedItem is Opcao<EmissorDto> oe)) { Erros.Aviso(this, "Escolha o emissor."); return; }
        if (_contato == null) { Erros.Aviso(this, "Escolha o contato (botão Selecionar)."); return; }

        var itens = new List<ReciboItemDto>();
        long centavos;
        string referente;
        if (chkItens.Checked)
        {
            var erro = ColetarItens(itens);
            if (erro != null) { Erros.Aviso(this, erro); grid.Focus(); return; }
            centavos = itens.Sum(i => i.ValorCentavos);
            if (centavos > 99_999_999_999L) { Erros.Aviso(this, "O total dos itens passa do limite de R$ 999.999.999,99."); return; }
            referente = txtTitulo.Text.Trim();
        }
        else
        {
            if (!Dinheiro.TentarLer(txtValor.Text, out centavos))
            {
                Erros.Aviso(this, "Informe um valor válido, por exemplo 450,00.");
                txtValor.Focus();
                return;
            }
            referente = txtReferente.Text.Trim();
            if (referente.Length == 0) { Erros.Aviso(this, "Informe a que se refere o recibo."); txtReferente.Focus(); return; }
        }

        var req = new ReciboCriarReq
        {
            EmissorId = oe.Valor.Id,
            ContatoId = _contato.Id,
            Sentido = cmbSentido.SelectedIndex == 1 ? "RECEBIMENTO" : "PAGAMENTO",
            ValorCentavos = centavos,
            Referente = referente,
            Itens = itens,
            MostrarDocPagador = chkDocPagador.Checked,
            Data = dtData.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Cidade = txtCidade.Text.Trim(),
            DuplicadoDe = _modelo?.Numero
        };
        var previa = new ReciboDto
        {
            Numero = 0,
            Sentido = req.Sentido,
            EmissorId = req.EmissorId,
            ContatoId = req.ContatoId,
            Emissor = oe.Valor,
            Contato = _contato,
            ValorCentavos = centavos,
            ValorExtenso = Extenso.Moeda(centavos),
            Referente = referente,
            Itens = itens,
            MostrarDocPagador = req.MostrarDocPagador,
            Data = req.Data,
            Cidade = req.Cidade
        };

        using var v = new ReciboViewer(previa, false, req);
        v.ShowDialog(this);
        if (v.Emitido != null)
        {
            App.Config.UltimoEmissorId = oe.Valor.Id;
            App.Config.Salvar();
            DialogResult = DialogResult.OK;
        }
    }
}

public class CancelarReciboForm : Dialogo
{
    public CancelarReciboForm(int numero) : base("Cancelar recibo nº " + numero)
    {
        Nota("O recibo continua no histórico, marcado como CANCELADO, e o número fica reservado. Esta ação não pode ser desfeita.", 52);
        var txtMotivo = Multi("Motivo do cancelamento *", 80);
        Fim("Cancelar recibo");
        BtnOk.Width = 130;
        BtnOk.Left = CX + CW - 234;
        BtnCancelar.Text = "Voltar";

        BtnOk.Click += async (s, e) =>
        {
            if (txtMotivo.Text.Trim().Length < 3) { Erros.Aviso(this, "Informe o motivo do cancelamento."); txtMotivo.Focus(); return; }
            if (!Erros.Confirmar(this, "Confirma o cancelamento do recibo nº " + numero + "?")) return;
            BtnOk.Enabled = false;
            try
            {
                await App.Api.CancelarRecibo(numero, txtMotivo.Text.Trim());
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
            finally { if (!IsDisposed) BtnOk.Enabled = true; }
        };
    }
}

public class NumeracaoForm : Dialogo
{
    public NumeracaoForm() : base("Numeração dos recibos")
    {
        var info = Nota("Carregando...", 70);
        var txtNum = Texto("Próximo número", 9);
        Fim("Salvar");

        Shown += async (s, e) =>
        {
            try
            {
                var p = await App.Api.ProximoNumero();
                info.Text = "O próximo recibo emitido será o nº " + p.Proximo + ". Você pode aumentar esse número " +
                            "(por exemplo, para continuar a sequência de um programa anterior), mas nunca diminuir.";
                txtNum.Text = p.Proximo.ToString();
                txtNum.Focus();
                txtNum.SelectAll();
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
        };

        BtnOk.Click += async (s, e) =>
        {
            if (!int.TryParse(txtNum.Text.Trim(), out var n) || n < 1) { Erros.Aviso(this, "Informe um número válido."); return; }
            if (!Erros.Confirmar(this, "O próximo recibo será o nº " + n + ". Confirma?")) return;
            BtnOk.Enabled = false;
            try
            {
                await App.Api.DefinirProximoNumero(n);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
            finally { if (!IsDisposed) BtnOk.Enabled = true; }
        };
    }
}

public class HistoricoForm : Form
{
    static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    readonly TextBox txtNum = new() { Width = 70 };
    readonly CheckBox chkPeriodo = new() { Text = "Período", AutoSize = true };
    readonly DateTimePicker dtDe = new() { Format = DateTimePickerFormat.Short, Width = 105, Enabled = false };
    readonly DateTimePicker dtAte = new() { Format = DateTimePickerFormat.Short, Width = 105, Enabled = false };
    readonly ComboBox cmbEmissor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    readonly TextBox txtContato = new() { Width = 170 };
    readonly TextBox txtDoc = new() { Width = 130 };
    readonly TextBox txtVMin = new() { Width = 80 };
    readonly TextBox txtVMax = new() { Width = 80 };
    readonly TextBox txtDesc = new() { Width = 240 };
    readonly Button btnBuscar = UiLista.Botao("Buscar", 90);
    readonly Button btnLimpar = UiLista.Botao("Limpar", 90);
    readonly ListView lista = UiLista.Nova(("Nº", 60), ("Data", 85), ("Emissor", 170), ("Tipo", 90), ("Contato", 190),
        ("CPF/CNPJ", 125), ("Valor", 100), ("Referente", 250), ("Situação", 80));
    readonly Label lblTotal = new() { AutoSize = true, Margin = new Padding(12, 9, 3, 3) };
    readonly Button btnAbrir = UiLista.Botao("Abrir / 2ª via", 130);
    readonly Button btnDup = UiLista.Botao("Duplicar", 100);
    readonly Button btnCanc = UiLista.Botao("Cancelar recibo", 130);
    int versao;

    public HistoricoForm()
    {
        UiLista.Preparar(this, "Histórico de recibos", 1180, 640);
        lista.Columns[6].TextAlign = HorizontalAlignment.Right;

        var filtros = new Panel { Dock = DockStyle.Top, Height = 112 };
        void Rot(string texto, int x, int y) =>
            filtros.Controls.Add(new Label { Text = texto, Left = x, Top = y + 3, AutoSize = true });
        void Pos(Control c, int x, int y) { c.Left = x; c.Top = y; filtros.Controls.Add(c); }

        Rot("Nº", 10, 10); Pos(txtNum, 34, 10);
        Pos(chkPeriodo, 124, 12); Pos(dtDe, 200, 10);
        Rot("até", 312, 10); Pos(dtAte, 342, 10);
        Rot("Emissor", 470, 10); Pos(cmbEmissor, 525, 10);

        Rot("Contato", 10, 42); Pos(txtContato, 64, 42);
        Rot("CPF/CNPJ", 250, 42); Pos(txtDoc, 318, 42);
        Rot("Valor de", 470, 42); Pos(txtVMin, 525, 42);
        Rot("até", 612, 42); Pos(txtVMax, 640, 42);

        Rot("Descrição", 10, 74); Pos(txtDesc, 76, 74);
        Pos(btnBuscar, 480, 72); Pos(btnLimpar, 576, 72);

        var rodape = UiLista.Barra(DockStyle.Bottom, 44);
        var btnFechar = UiLista.Botao("Fechar", 90);
        btnFechar.DialogResult = DialogResult.Cancel;
        rodape.Controls.AddRange(new Control[] { btnAbrir, btnDup, btnCanc, btnFechar, lblTotal });

        Controls.Add(lista);
        Controls.Add(filtros);
        Controls.Add(rodape);
        lista.BringToFront();
        AcceptButton = btnBuscar;
        CancelButton = btnFechar;

        chkPeriodo.CheckedChanged += (s, e) => { dtDe.Enabled = chkPeriodo.Checked; dtAte.Enabled = chkPeriodo.Checked; };
        btnBuscar.Click += (s, e) => Buscar();
        btnLimpar.Click += (s, e) => Limpar();
        lista.SelectedIndexChanged += (s, e) => AtualizarBotoes();
        lista.DoubleClick += (s, e) => Abrir();
        btnAbrir.Click += (s, e) => Abrir();
        btnDup.Click += (s, e) => Duplicar();
        btnCanc.Click += (s, e) => Cancelar();

        dtDe.Value = DateTime.Today.AddMonths(-1);
        dtAte.Value = DateTime.Today;

        Shown += async (s, e) =>
        {
            try
            {
                cmbEmissor.Items.Add(new Opcao<int>(0, "Todos os emissores"));
                var emissores = await App.Api.ListarEmissores(true);
                foreach (var em in emissores)
                    cmbEmissor.Items.Add(new Opcao<int>(em.Id, em.Nome + (em.Ativo ? "" : " (inativo)")));
                cmbEmissor.SelectedIndex = 0;
            }
            catch (Exception ex) { Erros.Mostrar(this, ex); }
            Buscar();
        };
        AtualizarBotoes();
    }

    ReciboResumoDto? Selecionado() => lista.SelectedItems.Count > 0 ? lista.SelectedItems[0].Tag as ReciboResumoDto : null;

    void AtualizarBotoes()
    {
        var r = Selecionado();
        btnAbrir.Enabled = r != null;
        btnDup.Enabled = r != null;
        btnCanc.Enabled = r != null && !r.Cancelado && App.EhAdmin;
    }

    void Limpar()
    {
        txtNum.Text = ""; txtContato.Text = ""; txtDoc.Text = ""; txtVMin.Text = ""; txtVMax.Text = ""; txtDesc.Text = "";
        chkPeriodo.Checked = false;
        if (cmbEmissor.Items.Count > 0) cmbEmissor.SelectedIndex = 0;
        Buscar();
    }

    FiltroRecibos? MontarFiltro()
    {
        var f = new FiltroRecibos { IncluirCancelados = true };
        if (txtNum.Text.Trim().Length > 0)
        {
            if (!int.TryParse(txtNum.Text.Trim(), out var n)) { Erros.Aviso(this, "O número do recibo deve conter só dígitos."); return null; }
            f.Numero = n;
        }
        if (chkPeriodo.Checked)
        {
            f.De = dtDe.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            f.Ate = dtAte.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (cmbEmissor.SelectedItem is Opcao<int> o && o.Valor > 0) f.EmissorId = o.Valor;
        f.Contato = txtContato.Text.Trim();
        f.Documento = txtDoc.Text.Trim();
        f.Descricao = txtDesc.Text.Trim();
        if (txtVMin.Text.Trim().Length > 0)
        {
            if (!Dinheiro.TentarLer(txtVMin.Text, out var vmin)) { Erros.Aviso(this, "Valor mínimo inválido."); return null; }
            f.ValorMin = vmin;
        }
        if (txtVMax.Text.Trim().Length > 0)
        {
            if (!Dinheiro.TentarLer(txtVMax.Text, out var vmax)) { Erros.Aviso(this, "Valor máximo inválido."); return null; }
            f.ValorMax = vmax;
        }
        return f;
    }

    async void Buscar()
    {
        var filtro = MontarFiltro();
        if (filtro == null) return;
        int v = ++versao;
        try
        {
            var itens = await App.Api.ListarRecibos(filtro);
            if (v != versao || IsDisposed) return;
            lista.BeginUpdate();
            lista.Items.Clear();
            long pagos = 0, recebidos = 0;
            foreach (var r in itens)
            {
                var data = DateTime.TryParseExact(r.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d.ToString("dd/MM/yyyy", Br)
                    : r.Data;
                var item = new ListViewItem(new[]
                {
                    r.Numero.ToString(), data, r.EmissorNome, r.Sentido == "RECEBIMENTO" ? "Recebimento" : "Pagamento",
                    r.ContatoNome, r.ContatoDocumento, Dinheiro.Formatar(r.ValorCentavos), r.Referente,
                    r.Cancelado ? "Cancelado" : "Emitido"
                }) { Tag = r };
                if (r.Cancelado) item.ForeColor = Color.Firebrick;
                else if (r.Sentido == "RECEBIMENTO") recebidos += r.ValorCentavos;
                else pagos += r.ValorCentavos;
                lista.Items.Add(item);
            }
            lista.EndUpdate();
            lblTotal.Text = itens.Count + (itens.Count == 1 ? " recibo" : " recibos") +
                " • pagamentos: " + Dinheiro.Formatar(pagos) + " • recebimentos: " + Dinheiro.Formatar(recebidos) +
                " (cancelados, em vermelho, não somam)" + (itens.Count >= 1000 ? " • mostrando os 1000 mais recentes" : "");
            AtualizarBotoes();
        }
        catch (Exception ex)
        {
            if (v == versao && !IsDisposed) Erros.Mostrar(this, ex);
        }
    }

    async void Abrir()
    {
        var r = Selecionado();
        if (r == null) return;
        try
        {
            var dto = await App.Api.ObterRecibo(r.Numero);
            using var v = new ReciboViewer(dto, true, null);
            v.ShowDialog(this);
        }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
    }

    async void Duplicar()
    {
        var r = Selecionado();
        if (r == null) return;
        try
        {
            var dto = await App.Api.ObterRecibo(r.Numero);
            using var f = new ReciboForm(dto);
            if (f.ShowDialog(this) == DialogResult.OK) Buscar();
        }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
    }

    void Cancelar()
    {
        var r = Selecionado();
        if (r == null || r.Cancelado) return;
        using var f = new CancelarReciboForm(r.Numero);
        if (f.ShowDialog(this) == DialogResult.OK) Buscar();
    }
}
