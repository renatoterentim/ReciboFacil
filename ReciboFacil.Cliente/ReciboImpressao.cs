using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

// Desenha o recibo (usado na prévia, na impressão e no PDF).
public static class ReciboDesenho
{
    static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    sealed class Parte
    {
        public string Nome = "";
        public string Documento = "";
        public string Endereco = "";
    }

    static string Junta(string a, string b) => string.Join(" - ", new[] { a, b }.Where(x => x.Length > 0));
    static string CidadeUf(string cidade, string uf) => string.Join("/", new[] { cidade, uf }.Where(x => x.Length > 0));

    static Parte DeEmissor(EmissorDto e) =>
        new Parte { Nome = e.Nome, Documento = e.Documento, Endereco = Junta(e.Endereco, CidadeUf(e.Cidade, e.Uf)) };

    static Parte DeContato(ContatoDto c) =>
        new Parte { Nome = c.Nome, Documento = c.Documento, Endereco = Junta(c.Endereco, CidadeUf(c.Cidade, c.Uf)) };

    // Cidade com UF. Recibos antigos guardavam só a cidade: acrescenta a UF do emissor quando é a cidade dele.
    public static string CidadeCompleta(ReciboDto r)
    {
        var c = (r.Cidade ?? "").Trim();
        if (c.Length == 0) return "";
        var uf = (r.Emissor?.Uf ?? "").Trim();
        if (uf.Length == 2 && !c.Contains('/') && !c.Contains(" - ") && !c.Contains(',') &&
            string.Equals(c, (r.Emissor?.Cidade ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            return c + " - " + uf.ToUpperInvariant();
        return c;
    }

    static StringFormat Fmt(StringAlignment h, StringAlignment v, bool quebra) =>
        new StringFormat(quebra ? StringFormatFlags.LineLimit : StringFormatFlags.NoWrap)
        {
            Alignment = h,
            LineAlignment = v,
            Trimming = StringTrimming.EllipsisCharacter
        };

    static void Marca(Graphics g, RectangleF a, string texto, Color cor)
    {
        var estado = g.Save();
        g.TranslateTransform(a.X + a.Width / 2f, a.Y + a.Height / 2f);
        g.RotateTransform(-22f);
        using var f = new Font("Arial", 56f, FontStyle.Bold, GraphicsUnit.Point);
        using var b = new SolidBrush(cor);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(texto, f, b, PointF.Empty, sf);
        g.Restore(estado);
    }

    public static void Desenhar(Graphics g, RectangleF a, ReciboDto r, bool segundaVia)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool recebe = r.Sentido == "RECEBIMENTO";           // o emissor recebe do contato
        Parte emitente = recebe ? DeEmissor(r.Emissor) : DeContato(r.Contato);   // quem recebe e assina
        Parte pagador = recebe ? DeContato(r.Contato) : DeEmissor(r.Emissor);
        var digitos = new string(emitente.Documento.Where(char.IsDigit).ToArray());
        var verbo = digitos.Length == 14 ? "Recebemos de" : "Recebi de";

        float L = a.Left + 26f, R = a.Right - 26f, W = R - L;

        using var pFino = new Pen(Color.Black, 1f);
        using var pGrosso = new Pen(Color.Black, 2.5f);
        using var preto = new SolidBrush(Color.Black);
        using var cinza = new SolidBrush(Color.FromArgb(90, 90, 90));
        using var vermelho = new SolidBrush(Color.Firebrick);
        using var fTitulo = new Font("Arial", 22f, FontStyle.Bold, GraphicsUnit.Point);
        using var fRot = new Font("Arial", 9f, FontStyle.Regular, GraphicsUnit.Point);
        using var fTxt = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Point);
        using var fNeg = new Font("Arial", 11f, FontStyle.Bold, GraphicsUnit.Point);
        using var fPeq = new Font("Arial", 9f, FontStyle.Regular, GraphicsUnit.Point);
        using var fMini = new Font("Arial", 7f, FontStyle.Regular, GraphicsUnit.Point);
        using var pLeve = new Pen(Color.FromArgb(175, 175, 175), 0.6f);
        using var esq = Fmt(StringAlignment.Near, StringAlignment.Center, false);
        using var centro = Fmt(StringAlignment.Center, StringAlignment.Center, false);
        using var dir = Fmt(StringAlignment.Far, StringAlignment.Center, false);
        using var quebra = Fmt(StringAlignment.Near, StringAlignment.Near, true);

        // Molduras
        g.DrawRectangle(pGrosso, a.X, a.Y, a.Width, a.Height);
        g.DrawRectangle(pFino, a.X + 6f, a.Y + 6f, a.Width - 12f, a.Height - 12f);

        // Cabeçalho
        g.DrawString("Recibo", fTitulo, preto, new RectangleF(L, a.Y + 18f, 140f, 40f), esq);
        if (segundaVia)
        {
            var rv = new RectangleF(L + 140f, a.Y + 24f, 74f, 26f);
            g.DrawRectangle(pFino, rv.X, rv.Y, rv.Width, rv.Height);
            g.DrawString("2ª VIA", fNeg, preto, rv, centro);
        }
        float bx = a.X + a.Width * 0.5f - 40f;
        g.DrawString("Nº", fRot, preto, new RectangleF(bx - 28f, a.Y + 24f, 26f, 26f), esq);
        var boxN = new RectangleF(bx, a.Y + 22f, 100f, 30f);
        g.DrawRectangle(pFino, boxN.X, boxN.Y, boxN.Width, boxN.Height);
        g.DrawString(r.Numero > 0 ? r.Numero.ToString() : "—", fNeg, preto, boxN, centro);
        var boxV = new RectangleF(R - 150f, a.Y + 22f, 150f, 30f);
        g.DrawString("Valor", fRot, preto, new RectangleF(boxV.X - 44f, a.Y + 24f, 42f, 26f), esq);
        g.DrawRectangle(pFino, boxV.X, boxV.Y, boxV.Width, boxV.Height);
        g.DrawString(Dinheiro.Formatar(r.ValorCentavos), fNeg, preto, boxV, centro);
        g.DrawLine(pFino, a.X + 6f, a.Y + 70f, a.Right - 6f, a.Y + 70f);

        // "Recebi de ..."
        float y0 = a.Y + 84f;
        g.DrawString(verbo, fRot, preto, new RectangleF(L, y0 + 2f, 100f, 22f), esq);
        bool docPagador = r.MostrarDocPagador && pagador.Documento.Length > 0;
        float larguraDoc = docPagador ? 178f : 0f;
        g.DrawString(pagador.Nome, fNeg, preto, new RectangleF(L + 104f, y0, W - 104f - larguraDoc, 24f), esq);
        if (docPagador)
        {
            var dg = new string(pagador.Documento.Where(char.IsDigit).ToArray());
            var rotuloDoc = dg.Length == 14 ? "CNPJ " : "CPF ";
            g.DrawString(rotuloDoc + pagador.Documento, fPeq, preto, new RectangleF(R - larguraDoc, y0, larguraDoc, 24f), dir);
        }
        g.DrawLine(pFino, L + 104f, y0 + 26f, R, y0 + 26f);

        // "a quantia de ..."
        float y1 = y0 + 44f;
        g.DrawString("a quantia de", fRot, preto, new RectangleF(L, y1, W, 18f), esq);
        var boxE = new RectangleF(L, y1 + 18f, W, 42f);
        g.DrawRectangle(pFino, boxE.X, boxE.Y, boxE.Width, boxE.Height);
        g.DrawString(Dinheiro.Maiuscula(r.ValorExtenso), fTxt, preto,
            new RectangleF(boxE.X + 6f, boxE.Y + 4f, boxE.Width - 12f, boxE.Height - 8f), quebra);

        // "Correspondente a ..."
        float y2 = y1 + 74f;
        g.DrawString("Correspondente a", fRot, preto, new RectangleF(L, y2, W, 18f), esq);
        float altBox = 86f, folgaBox = 18f, folgaAss = 44f;
        int nItens = r.Itens?.Count ?? 0;
        if (nItens > 0)
        {
            // Vários itens: descrição à esquerda, valor à direita e o total no final (a caixa cresce, com limite).
            bool temTitulo = !string.IsNullOrWhiteSpace(r.Referente);
            const float pad = 4f, altTotal = 18f, disponivel = 136f;
            float lh = Math.Min(17f, (disponivel - 2 * pad - altTotal - (temTitulo ? 2f : 0f)) / (nItens + (temTitulo ? 1 : 0)));
            lh = Math.Max(lh, 11f);
            float tam = Math.Min(10f, lh / 1.65f);
            altBox = Math.Max(86f, 2 * pad + (temTitulo ? lh + 2f : 0f) + nItens * lh + altTotal);
            folgaBox = 12f;
            folgaAss = 38f;

            var boxI = new RectangleF(L, y2 + 18f, W, altBox);
            g.DrawRectangle(pFino, boxI.X, boxI.Y, boxI.Width, boxI.Height);
            using var fItem = new Font("Arial", tam, FontStyle.Regular, GraphicsUnit.Point);
            using var fItemNeg = new Font("Arial", tam, FontStyle.Bold, GraphicsUnit.Point);
            float xi = boxI.X + 6f, wi = boxI.Width - 12f, wv = 120f;
            float yy = boxI.Y + pad;
            if (temTitulo)
            {
                var titulo = r.Referente.Replace("\r", "").Split('\n')[0];
                g.DrawString(titulo, fItemNeg, preto, new RectangleF(xi, yy, wi, lh), esq);
                yy += lh + 2f;
            }
            for (int i = 0; i < nItens; i++)
            {
                var it = r.Itens![i];
                g.DrawString(it.Descricao, fItem, preto, new RectangleF(xi, yy, wi - wv - 8f, lh), esq);
                g.DrawString(Dinheiro.Formatar(it.ValorCentavos), fItem, preto, new RectangleF(xi + wi - wv, yy, wv, lh), dir);
                yy += lh;
                if (i < nItens - 1) g.DrawLine(pLeve, xi, yy, xi + wi, yy);
            }
            g.DrawLine(pFino, xi, yy + 2f, xi + wi, yy + 2f);
            g.DrawString("TOTAL", fNeg, preto, new RectangleF(xi, yy + 3f, 200f, altTotal - 2f), esq);
            g.DrawString(Dinheiro.Formatar(r.ValorCentavos), fNeg, preto, new RectangleF(xi + wi - 160f, yy + 3f, 160f, altTotal - 2f), dir);
        }
        else
        {
            var boxR = new RectangleF(L, y2 + 18f, W, altBox);
            g.DrawRectangle(pFino, boxR.X, boxR.Y, boxR.Width, boxR.Height);
            g.DrawString(r.Referente, fTxt, preto,
                new RectangleF(boxR.X + 6f, boxR.Y + 4f, boxR.Width - 12f, boxR.Height - 8f), quebra);
        }

        // Cidade e data
        float y3 = y2 + 18f + altBox + folgaBox;
        string dataTexto = DateTime.TryParseExact(r.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("d 'de' MMMM 'de' yyyy", Br)
            : r.Data;
        var cidade = CidadeCompleta(r).ToUpper(Br);
        var linha = cidade.Length > 0 ? cidade + ", " + dataTexto : dataTexto;
        g.DrawString(linha, fTxt, preto, new RectangleF(L, y3, W, 24f), centro);
        g.DrawLine(pFino, L + 60f, y3 + 26f, R - 60f, y3 + 26f);

        // Emitente (quem recebe) e assinatura
        float y4 = y3 + folgaAss;
        float larguraEsq = W * 0.52f;
        void Linha(string rotulo, string valor, float yy)
        {
            g.DrawString(rotulo, fPeq, cinza, new RectangleF(L, yy, 78f, 20f), esq);
            g.DrawString(valor, fPeq, preto, new RectangleF(L + 80f, yy, larguraEsq - 80f, 20f), esq);
        }
        Linha("Emitente:", emitente.Nome, y4);
        Linha("Endereço:", emitente.Endereco, y4 + 22f);
        Linha("CPF/CNPJ:", emitente.Documento, y4 + 44f);
        float sx = L + W * 0.56f;
        g.DrawLine(pFino, sx, y4 + 36f, R, y4 + 36f);
        g.DrawString(emitente.Nome, fPeq, preto, new RectangleF(sx, y4 + 40f, R - sx, 20f), centro);

        // Rodapé
        g.DrawString("ReciboFácil", fMini, cinza, new RectangleF(a.Right - 140f, a.Bottom - 26f, 124f, 14f), dir);

        if (r.Cancelado)
        {
            string quando = DateTime.TryParse(r.CanceladoEm, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dc)
                ? dc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Br)
                : "";
            var nota = "Cancelado em " + quando + " por " + r.CanceladoPor + ". Motivo: " + r.Motivo;
            g.DrawString(nota, fMini, vermelho, new RectangleF(L, a.Bottom - 46f, W * 0.7f, 30f), quebra);
            Marca(g, a, "CANCELADO", Color.FromArgb(90, 200, 0, 0));
        }
        else if (r.Numero == 0)
        {
            Marca(g, a, "PRÉVIA", Color.FromArgb(60, 90, 90, 90));
        }
    }
}

// Janela com a prévia do recibo, impressão (metade de cima ou de baixo da folha A4) e PDF.
// Com "criar" informado, é a prévia ANTES de emitir: o número só nasce ao confirmar.
public class ReciboViewer : Form
{
    const string ImpressoraPdf = "Microsoft Print to PDF";

    ReciboDto _r;
    ReciboCriarReq? _criar;
    readonly bool _segundaVia;
    readonly PrintPreviewControl _prev = new() { Dock = DockStyle.Fill, AutoZoom = true, UseAntiAlias = true };
    readonly ComboBox _pos = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    readonly Button _btnEmitir = UiLista.Botao("Confirmar e emitir", 150);
    readonly Button _btnImprimir = UiLista.Botao("Imprimir", 100);
    readonly Button _btnPdf = UiLista.Botao("Salvar PDF", 100);
    readonly Button _btnFechar = UiLista.Botao("Voltar", 90);
    readonly Label _info = new() { AutoSize = true, Margin = new Padding(12, 9, 3, 3) };

    // Preenchido se o recibo foi emitido nesta janela.
    public ReciboDto? Emitido { get; private set; }

    public ReciboViewer(ReciboDto recibo, bool segundaVia, ReciboCriarReq? criar)
    {
        _r = recibo;
        _segundaVia = segundaVia;
        _criar = criar;
        UiLista.Preparar(this, criar != null ? "Prévia do recibo" : "Recibo nº " + recibo.Numero, 980, 720);

        var barra = UiLista.Barra(DockStyle.Top, 46);
        barra.Controls.Add(new Label { Text = "Posição na folha:", AutoSize = true, Margin = new Padding(3, 9, 3, 3) });
        _pos.Items.AddRange(new object[] { "Metade de cima", "Metade de baixo" });
        _pos.SelectedIndex = 0;
        barra.Controls.Add(_pos);
        barra.Controls.AddRange(new Control[] { _btnEmitir, _btnImprimir, _btnPdf, _btnFechar, _info });

        Controls.Add(_prev);
        Controls.Add(barra);
        _prev.BringToFront();

        _btnFechar.DialogResult = DialogResult.Cancel;
        CancelButton = _btnFechar;

        _pos.SelectedIndexChanged += (s, e) => AtualizarPrevia();
        _btnEmitir.Click += async (s, e) => await Emitir();
        _btnImprimir.Click += async (s, e) => await Imprimir();
        _btnPdf.Click += async (s, e) => await SalvarPdf();

        AtualizarEstado();
        Shown += (s, e) => AtualizarPrevia();
    }

    void AtualizarEstado()
    {
        bool previa = _criar != null;
        _btnEmitir.Visible = previa;
        _btnImprimir.Enabled = !previa;
        _btnPdf.Enabled = !previa;
        _btnFechar.Text = previa ? "Voltar" : "Fechar";
        if (previa) _info.Text = "Confira os dados. O número só é gerado ao confirmar.";
        else if (Emitido != null) _info.Text = "Recibo nº " + _r.Numero + " emitido.";
        else _info.Text = _segundaVia ? "Será impresso como 2ª via." : "";
        Text = previa ? "Prévia do recibo" : "Recibo nº " + _r.Numero;
    }

    void AtualizarPrevia()
    {
        try
        {
            _prev.Document = CriarDocumento(null);
            _prev.InvalidatePreview();
        }
        catch (Exception ex)
        {
            _info.Text = "Não foi possível montar a prévia: " + ex.Message;
        }
    }

    static void ConfigurarPapel(PrintDocument pd)
    {
        PaperSize? a4 = null;
        try
        {
            foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
                if (ps.Kind == PaperKind.A4) { a4 = ps; break; }
        }
        catch { }
        pd.DefaultPageSettings.PaperSize = a4 ?? new PaperSize("A4", 827, 1169);
        pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        pd.DefaultPageSettings.Landscape = false;
    }

    PrintDocument CriarDocumento(string? impressora)
    {
        var pd = new PrintDocument { DocumentName = "Recibo " + _r.Numero };
        if (impressora != null) pd.PrinterSettings.PrinterName = impressora;
        ConfigurarPapel(pd);
        pd.PrintPage += (s, e) =>
        {
            float w = e.PageBounds.Width, h = e.PageBounds.Height;
            float altura = h / 2f - 60f;
            float y = _pos.SelectedIndex == 1 ? h / 2f + 20f : 40f;
            ReciboDesenho.Desenhar(e.Graphics!, new RectangleF(40f, y, w - 80f, altura), _r, _segundaVia);
            e.HasMorePages = false;
        };
        return pd;
    }

    async Task Emitir()
    {
        if (_criar == null) return;
        _btnEmitir.Enabled = false;
        try
        {
            var dto = await App.Api.CriarRecibo(_criar);
            _r = dto;
            Emitido = dto;
            _criar = null;
            AtualizarEstado();
            AtualizarPrevia();
        }
        catch (Exception ex)
        {
            Erros.Mostrar(this, ex);
            if (!IsDisposed) _btnEmitir.Enabled = true;
        }
    }

    async Task RegistrarViaSePreciso()
    {
        if (!_segundaVia) return;
        try { await App.Api.RegistrarVia(_r.Numero); } catch { }
    }

    async Task Imprimir()
    {
        try
        {
            var pd = CriarDocumento(null);
            using var dlg = new PrintDialog { Document = pd, UseEXDialog = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            ConfigurarPapel(pd);   // a escolha da impressora pode ter mudado o papel
            pd.Print();
            await RegistrarViaSePreciso();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível imprimir: " + ex.Message, App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    async Task SalvarPdf()
    {
        bool existe = false;
        try
        {
            foreach (string nome in PrinterSettings.InstalledPrinters)
                if (string.Equals(nome, ImpressoraPdf, StringComparison.OrdinalIgnoreCase)) existe = true;
        }
        catch { }
        if (!existe)
        {
            Erros.Aviso(this, "Salvar em PDF usa a impressora virtual \"" + ImpressoraPdf + "\", que não está instalada neste computador. " +
                "Use o botão Imprimir e escolha uma impressora de PDF.");
            return;
        }
        using var dlg = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "Recibo-" + _r.Numero + ".pdf" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var pd = CriarDocumento(ImpressoraPdf);
            pd.PrinterSettings.PrintToFile = true;
            pd.PrinterSettings.PrintFileName = dlg.FileName;
            pd.Print();
            await RegistrarViaSePreciso();
            Erros.Aviso(this, "PDF salvo em:\n" + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível salvar o PDF: " + ex.Message, App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
