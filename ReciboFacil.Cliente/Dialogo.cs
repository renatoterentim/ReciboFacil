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
    }

    void Rotulo(string texto, int yy) =>
        Controls.Add(new Label { Text = texto, Left = LX, Top = yy + 3, Width = CX - LX - 6, Height = 20, AutoSize = false });

    protected void Nota(string texto, int altura = 36)
    {
        Controls.Add(new Label { Text = texto, Left = LX, Top = y, Width = CX + CW - LX, Height = altura, AutoSize = false });
        y += altura + 6;
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
