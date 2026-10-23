namespace ReciboFacil.Cliente;

// Base para janelas de formulário: monta rótulo + campo em linhas, com OK / Cancelar no final.
public abstract class Dialogo : Form
{
    protected const int LX = 14, CX = 170, CW = 360;
    int y = 16;
    protected readonly Button BtnOk = new() { Text = "OK", Width = 90, Height = 28 };
    protected readonly Button BtnCancelar = new() { Text = "Cancelar", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };

    protected Dialogo(string titulo)
    {
        Text = titulo;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        if (App.Icone != null) Icon = App.Icone;
    }

    void Rotulo(string texto, int yy) =>
        Controls.Add(new Label { Text = texto, Left = LX, Top = yy + 3, Width = CX - LX - 6, Height = 20, AutoSize = false });

    // Campo de telefone: ao sair do campo, padroniza para (00) 00000-0000.
    protected TextBox Telefone(string rotulo)
    {
        var t = Texto(rotulo, 30);
        t.Leave += (s, e) => t.Text = ReciboFacil.Shared.FoneBr.Formatar(t.Text);
        return t;
    }

    readonly Dictionary<TextBox, (ComboBox Tipo, ErrorProvider Erro)> _pix = new();

    // Campo de chave Pix com seletor de tipo (Automático, CPF, CNPJ, Celular, E-mail, Aleatória).
    // Ao sair do campo (ou trocar o tipo) a chave é padronizada; se não confere com o tipo, aparece um aviso ao lado do campo.
    protected TextBox ChavePix(string rotulo)
    {
        var t = Texto(rotulo, 120);
        t.Width = CW - 112;
        var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = t.Left + t.Width + 6, Top = t.Top, Width = 106 };
        cmb.Items.AddRange(ReciboFacil.Shared.ChavePixBr.Tipos);
        cmb.SelectedIndex = 0;
        Controls.Add(cmb);
        var ep = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
        _pix[t] = (cmb, ep);

        t.Leave += (s, e) => AplicarPix(t);
        cmb.SelectedIndexChanged += (s, e) => AplicarPix(t);
        // Ao abrir um cadastro existente, mostra o tipo reconhecido pela formatação da chave salva.
        Shown += (s, e) =>
        {
            if (t.Text.Length > 0 && cmb.SelectedIndex == 0) cmb.SelectedIndex = ReciboFacil.Shared.ChavePixBr.Detectar(t.Text);
        };
        return t;
    }

    string? AplicarPix(TextBox t)
    {
        var (cmb, ep) = _pix[t];
        t.Text = ReciboFacil.Shared.ChavePixBr.Formatar(t.Text, cmb.SelectedIndex, out var erro);
        ep.SetError(t, erro ?? "");
        return erro;
    }

    // Use ao salvar: se a chave não confere com o tipo, pergunta se quer salvar mesmo assim.
    protected bool ChavePixConfere(TextBox t)
    {
        var erro = AplicarPix(t);
        return erro == null || Erros.Confirmar(this, erro + "\n\nSalvar mesmo assim?");
    }

    protected Label Nota(string texto, int altura = 36)
    {
        var l = new Label { Text = texto, Left = LX, Top = y, Width = CX + CW - LX, Height = altura, AutoSize = false };
        Controls.Add(l);
        y += altura + 6;
        return l;
    }

    // Insere um controle (painel, grade...) ocupando a largura útil, na próxima linha.
    protected T Adicionar<T>(T c, int altura) where T : Control
    {
        c.Left = LX;
        c.Top = y;
        c.Width = CX + CW - LX;
        c.Height = altura;
        Controls.Add(c);
        y += altura + 8;
        return c;
    }

    protected DateTimePicker Data(string rotulo)
    {
        Rotulo(rotulo, y);
        var d = new DateTimePicker { Left = CX, Top = y, Width = 140, Format = DateTimePickerFormat.Short };
        Controls.Add(d);
        y += 30;
        return d;
    }

    protected TextBox Texto(string rotulo, int max = 200, bool senha = false)
    {
        Rotulo(rotulo, y);
        var t = new TextBox { Left = CX, Top = y, Width = CW, MaxLength = max, UseSystemPasswordChar = senha };
        Controls.Add(t);
        y += 30;
        return t;
    }

    protected TextBox Multi(string rotulo, int altura = 60)
    {
        Rotulo(rotulo, y);
        var t = new TextBox { Left = CX, Top = y, Width = CW, Height = altura, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, MaxLength = 1000 };
        Controls.Add(t);
        y += altura + 10;
        return t;
    }

    protected ComboBox Combo(string rotulo, bool editavel, params string[] itens)
    {
        Rotulo(rotulo, y);
        var c = new ComboBox
        {
            Left = CX, Top = y, Width = CW,
            DropDownStyle = editavel ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList
        };
        c.Items.AddRange(itens);
        if (editavel)
        {
            c.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            c.AutoCompleteSource = AutoCompleteSource.ListItems;
            c.MaxLength = 100;
        }
        Controls.Add(c);
        y += 30;
        return c;
    }

    protected CheckBox Check(string texto)
    {
        var c = new CheckBox { Text = texto, Left = CX, Top = y, Width = CW, Height = 24 };
        Controls.Add(c);
        y += 28;
        return c;
    }

    // Chame por último: posiciona os botões e ajusta o tamanho da janela.
    protected void Fim(string textoOk = "OK")
    {
        BtnOk.Text = textoOk;
        BtnOk.Left = CX + CW - 190;
        BtnOk.Top = y + 8;
        BtnCancelar.Left = CX + CW - 94;
        BtnCancelar.Top = y + 8;
        Controls.Add(BtnOk);
        Controls.Add(BtnCancelar);
        ClientSize = new Size(CX + CW + 16, y + 8 + 28 + 16);
        AcceptButton = BtnOk;
        CancelButton = BtnCancelar;
    }
}
