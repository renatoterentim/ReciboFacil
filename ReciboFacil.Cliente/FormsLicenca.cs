using System.Diagnostics;
using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public class LicencaForm : Form
{
    readonly Label lblTitulo = new() { Left = 16, Top = 14, Width = 600, Height = 26, Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
    readonly Label lblDetalhe = new() { Left = 16, Top = 46, Width = 600, Height = 74 };
    readonly Label lblCliente = new() { Left = 150, Top = 130, Width = 460, Height = 20 };
    readonly Label lblValidade = new() { Left = 150, Top = 154, Width = 460, Height = 20 };
    readonly TextBox txtId = new() { Left = 150, Top = 192, Width = 230, ReadOnly = true, Font = new Font("Consolas", 10F) };
    readonly Button btnCopiar = UiLista.Botao("Copiar código", 110);
    readonly Button btnCarregar = UiLista.Botao("Carregar arquivo de licença...", 230);
    readonly Button btnFechar = UiLista.Botao("Fechar", 90);

    public LicencaForm()
    {
        UiLista.Preparar(this, "Licença", 632, 436);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        Controls.Add(lblTitulo);
        Controls.Add(lblDetalhe);
        Controls.Add(new Label { Text = "Licenciado para:", Left = 16, Top = 130, Width = 130, Height = 20 });
        Controls.Add(lblCliente);
        Controls.Add(new Label { Text = "Validade:", Left = 16, Top = 154, Width = 130, Height = 20 });
        Controls.Add(lblValidade);
        Controls.Add(new Label { Text = "Código da instalação:", Left = 16, Top = 195, Width = 135, Height = 20 });
        Controls.Add(txtId);
        btnCopiar.Left = 390; btnCopiar.Top = 190;
        Controls.Add(btnCopiar);
        Controls.Add(new Label
        {
            Left = 16, Top = 226, Width = 600, Height = 52, ForeColor = SystemColors.GrayText,
            Text = "Para comprar ou renovar a licença, envie o código da instalação ao desenvolvedor. " +
                   "Ele enviará um arquivo de licença (.rflic), que você carrega aqui. A licença vale para todos os computadores que usam este servidor."
        });

        Controls.Add(new Label
        {
            Text = "Para comprar ou renovar, fale com " + Constantes.Desenvolvedor + ":",
            Left = 16, Top = 286, Width = 600, Height = 20, Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        });
        var lnkWhats = new LinkLabel { Text = "WhatsApp " + Constantes.ContatoTelefone, Left = 16, Top = 310, Width = 280, Height = 20 };
        var lnkEmail = new LinkLabel { Text = Constantes.ContatoEmail, Left = 310, Top = 310, Width = 300, Height = 20 };
        lnkWhats.LinkClicked += (s, e) => Abrir("https://wa.me/" + Constantes.ContatoWhatsApp + "?text=" +
            Uri.EscapeDataString("Olá! Quero a licença do ReciboFácil. Código da instalação: " + txtId.Text));
        lnkEmail.LinkClicked += (s, e) => Abrir("mailto:" + Constantes.ContatoEmail + "?subject=" +
            Uri.EscapeDataString("Licença do ReciboFácil") + "&body=" +
            Uri.EscapeDataString("Olá! Quero a licença do ReciboFácil.\r\nCódigo da instalação: " + txtId.Text));
        Controls.AddRange(new Control[] { lnkWhats, lnkEmail });

        btnCarregar.Left = 16; btnCarregar.Top = 350;
        btnFechar.Left = 526; btnFechar.Top = 396;
        Controls.AddRange(new Control[] { btnCarregar, btnFechar });
        CancelButton = btnFechar;
        btnFechar.Click += (s, e) => Close();

        btnCopiar.Click += (s, e) =>
        {
            try { Clipboard.SetText(txtId.Text); Erros.Aviso(this, "Código copiado."); }
            catch (Exception ex) { Erros.Aviso(this, "Não foi possível copiar: " + ex.Message); }
        };
        btnCarregar.Click += async (s, e) => await Carregar();
        Shown += async (s, e) => await Atualizar();
    }

    void Abrir(string endereco)
    {
        try { Process.Start(new ProcessStartInfo(endereco) { UseShellExecute = true }); }
        catch (Exception ex) { Erros.Aviso(this, "Não foi possível abrir o link: " + ex.Message); }
    }

    async Task Atualizar()
    {
        try { Mostrar(await App.Api.Licenca()); }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
    }

    void Mostrar(LicencaStatusDto st)
    {
        lblTitulo.Text = st.Titulo;
        lblTitulo.ForeColor = st.Alerta ? Color.Firebrick : (st.Estado == "ativa" ? Color.DarkGreen : SystemColors.ControlText);
        lblDetalhe.Text = st.Detalhe;
        lblCliente.Text = st.Cliente.Length > 0 ? st.Cliente : "-";
        lblValidade.Text = st.ValidoAte.Length > 0 ? st.ValidoAte + " (" + st.Tipo.ToLowerInvariant() + ")" : "sem prazo (plano grátis)";
        txtId.Text = st.InstalacaoId;
    }

    async Task Carregar()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Licença do ReciboFácil (*.rflic)|*.rflic|Todos os arquivos|*.*",
            Title = "Escolha o arquivo de licença"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        btnCarregar.Enabled = false;
        try
        {
            var texto = await File.ReadAllTextAsync(dlg.FileName);
            var st = await App.Api.InstalarLicenca(texto);
            Mostrar(st);
            Erros.Aviso(this, "Licença carregada com sucesso.\n\n" + st.Detalhe);
        }
        catch (Exception ex) { Erros.Mostrar(this, ex); }
        finally { if (!IsDisposed) btnCarregar.Enabled = true; }
    }
}
