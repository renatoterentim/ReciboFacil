using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using ReciboFacil.Shared;

namespace ReciboFacil.Servidor;

public class SituacaoLicenca
{
    public LicencaStatusDto Status { get; set; } = new();
    public bool Completa => Status.Completa;
    public int MaxEmissores => Status.MaxEmissores;
}

// Plano grátis: acesso completo, mas só 1 emissor ativo e pessoa física (CPF).
// Com licença válida (arquivo .rflic assinado): emissores ilimitados (ou o limite do plano), inclusive CNPJ.
public static class LicencaServico
{
    public const int ToleranciaDias = 7;
    const string Fmt = "yyyy-MM-dd";

    static string ArqLicenca => Path.Combine(Db.DataDir, "licenca.rflic");

    public const string MsgLimiteGratis =
        "No plano grátis é possível ter um único emissor, e ele deve ser pessoa física (CPF). " +
        "Para usar outro emissor, exclua o atual em Cadastros → Emissores (os recibos já emitidos continuam no histórico). " +
        "Para ter vários emissores ou um emissor com CNPJ, ative uma licença em Administração → Licença.";

    static string? ChavePublica()
    {
        try
        {
            using var s = typeof(LicencaServico).Assembly.GetManifestResourceStream("chave-publica.txt");
            if (s == null) return null;
            using var r = new StreamReader(s);
            var t = r.ReadToEnd().Trim();
            if (t.Length == 0) return null;
            Convert.FromBase64String(t); // lança se for o texto de espaço reservado
            return t;
        }
        catch { return null; }
    }

    static string? LerCfg(SqliteConnection c, string chave)
    {
        using var cmd = c.Cmd("SELECT valor FROM configuracoes WHERE chave=$k", ("$k", chave));
        return cmd.ExecuteScalar() as string;
    }

    static void GravarCfg(SqliteConnection c, string chave, string valor)
    {
        using var cmd = c.Cmd(
            "INSERT INTO configuracoes(chave, valor) VALUES($k,$v) ON CONFLICT(chave) DO UPDATE SET valor=$v",
            ("$k", chave), ("$v", valor));
        cmd.ExecuteNonQuery();
    }

    // Código desta instalação (o cliente informa ao desenvolvedor para receber a licença). Fica no banco, então vai junto no backup.
    public static string InstalacaoId(SqliteConnection c)
    {
        var v = LerCfg(c, "instalacao_id");
        if (!string.IsNullOrEmpty(v)) return v;
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        v = string.Join("-", Enumerable.Range(0, 4).Select(i => hex.Substring(i * 4, 4)));
        GravarCfg(c, "instalacao_id", v);
        return v;
    }

    // Data de hoje, protegida contra "voltar o relógio" do Windows: nunca volta para antes da maior data já vista.
    static DateOnly Hoje(SqliteConnection c)
    {
        var real = DateOnly.FromDateTime(DateTime.Now);
        var u = LerCfg(c, "ultima_data");
        if (u != null && DateOnly.TryParseExact(u, Fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ud) && ud > real)
            return ud;
        var txt = real.ToString(Fmt, CultureInfo.InvariantCulture);
        if (u != txt) GravarCfg(c, "ultima_data", txt);
        return real;
    }

    static string SoLetrasNumeros(string s) => new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    static string Br(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static SituacaoLicenca Situacao(SqliteConnection c)
    {
        var st = new LicencaStatusDto { InstalacaoId = InstalacaoId(c), ChaveConfigurada = ChavePublica() != null };
        var sit = new SituacaoLicenca { Status = st };
        var hoje = Hoje(c);

        string? conteudo = null;
        try { if (File.Exists(ArqLicenca)) conteudo = File.ReadAllText(ArqLicenca); } catch { }

        if (conteudo == null)
        {
            Gratis(st);
            return sit;
        }

        var d = LicencaCripto.Verificar(conteudo, ChavePublica(), out var erro);
        if (d == null) { Invalida(st, erro); return sit; }
        if (d.InstalacaoId.Length > 0 && SoLetrasNumeros(d.InstalacaoId) != SoLetrasNumeros(st.InstalacaoId))
        {
            Invalida(st, "Esta licença foi emitida para outra instalação do programa.");
            return sit;
        }
        if (!DateOnly.TryParseExact(d.ValidoAte, Fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ate))
        {
            Invalida(st, "A data de validade da licença é inválida.");
            return sit;
        }

        st.Cliente = d.Cliente;
        st.Tipo = d.Tipo == "mensal" ? "Mensal" : "Anual";
        st.ValidoAte = Br(ate);
        st.MaxEmissores = Math.Max(0, d.MaxEmissores);
        st.DiasRestantes = ate.DayNumber - hoje.DayNumber;
        int aviso = d.Tipo == "mensal" ? 5 : 15;
        var limite = st.MaxEmissores > 0 ? "até " + st.MaxEmissores + " emissores" : "emissores ilimitados (inclusive CNPJ)";

        if (st.DiasRestantes >= 0)
        {
            st.Estado = "ativa";
            st.Completa = true;
            st.Alerta = st.DiasRestantes <= aviso;
            st.Titulo = "Licença " + st.Tipo.ToLowerInvariant() + " ativa";
            st.Detalhe = "Licenciado para " + d.Cliente + ". Válida até " + st.ValidoAte + " (" +
                (st.DiasRestantes == 0 ? "vence hoje" : st.DiasRestantes + " dia(s) restante(s)") + "). Plano com " + limite + ".";
            st.Resumo = st.Alerta
                ? "Licença vence " + (st.DiasRestantes == 0 ? "hoje" : "em " + st.DiasRestantes + " dia(s)") + " (" + st.ValidoAte + ")"
                : "Licença até " + st.ValidoAte;
        }
        else if (-st.DiasRestantes <= ToleranciaDias)
        {
            var fim = ate.AddDays(ToleranciaDias);
            st.Estado = "tolerancia";
            st.Completa = true;
            st.Alerta = true;
            st.Titulo = "Licença vencida (período de tolerância)";
            st.Detalhe = "A licença venceu em " + st.ValidoAte + ". O programa continua completo até " + Br(fim) +
                "; depois volta ao plano grátis (1 emissor CPF). Carregue a licença renovada para continuar.";
            st.Resumo = "Licença VENCIDA em " + st.ValidoAte + " - renove até " + Br(fim);
        }
        else
        {
            st.Estado = "vencida";
            st.Completa = false;
            st.Alerta = true;
            st.Titulo = "Licença vencida - plano grátis";
            st.Detalhe = "A licença venceu em " + st.ValidoAte + ". O programa está no plano grátis: só é possível emitir com um emissor pessoa física (CPF). " +
                "Histórico, 2ª via e backup continuam funcionando. Carregue a licença renovada para liberar os demais emissores.";
            st.Resumo = "Licença vencida - plano grátis";
        }
        return sit;
    }

    static void Gratis(LicencaStatusDto st)
    {
        st.Estado = "gratis";
        st.Completa = false;
        st.Titulo = "Licença pessoal grátis";
        st.Detalhe = "Acesso completo ao programa, com 1 emissor pessoa física (CPF). " +
            "Para ter vários emissores ou um emissor com CNPJ, é preciso uma licença.";
        st.Resumo = "Licença pessoal grátis";
    }

    static void Invalida(LicencaStatusDto st, string motivo)
    {
        st.Estado = "invalida";
        st.Completa = false;
        st.Alerta = true;
        st.Titulo = "Licença inválida - plano grátis";
        st.Detalhe = motivo + " Enquanto isso o programa funciona no plano grátis (1 emissor CPF).";
        st.Resumo = "Licença inválida - plano grátis";
    }

    // Valida e instala um arquivo de licença. Devolve a mensagem de erro (ou null se deu certo).
    public static string? Instalar(SqliteConnection c, string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo) || conteudo.Length > 20000) return "O arquivo não é uma licença válida.";
        var d = LicencaCripto.Verificar(conteudo, ChavePublica(), out var erro);
        if (d == null) return erro;
        if (d.InstalacaoId.Length > 0 && SoLetrasNumeros(d.InstalacaoId) != SoLetrasNumeros(InstalacaoId(c)))
            return "Esta licença foi emitida para outra instalação do programa. Confira o código da instalação informado ao desenvolvedor.";
        if (!DateOnly.TryParseExact(d.ValidoAte, Fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ate))
            return "A data de validade da licença é inválida.";
        if (ate.AddDays(ToleranciaDias) < Hoje(c))
            return "Esta licença já está vencida (venceu em " + Br(ate) + "). Peça uma licença renovada.";

        Directory.CreateDirectory(Db.DataDir);
        var tmp = ArqLicenca + ".novo";
        File.WriteAllText(tmp, conteudo);
        File.Move(tmp, ArqLicenca, true);
        return null;
    }

    // Menor id entre os emissores (não excluídos) pessoa física: é o único que o plano grátis libera para emitir.
    public static int IdEmissorGratis(SqliteConnection c)
    {
        using var cmd = c.Cmd("SELECT MIN(id) FROM emissores WHERE excluido=0 AND length(documento_digitos)=11");
        var v = cmd.ExecuteScalar();
        return v == null || v is DBNull ? 0 : Convert.ToInt32(v);
    }

    public static bool EmissorLiberado(SqliteConnection c, SituacaoLicenca sit, int emissorId) =>
        sit.Completa || emissorId == IdEmissorGratis(c);

    // Regras para cadastrar/reativar/alterar emissor. Devolve a mensagem de bloqueio (ou null se pode).
    // No plano grátis conta QUALQUER emissor não excluído (ativo ou inativo): inativar não libera vaga, só excluir.
    public static string? ValidarEmissor(SqliteConnection c, EmissorDto novo, string digitos, EmissorDto? antes)
    {
        if (!novo.Ativo) return null;
        bool mudouDoc = antes == null || Doc.Digitos(antes.Documento) != digitos;
        bool reativando = antes != null && !antes.Ativo;
        if (antes != null && !mudouDoc && !reativando) return null; // nada relevante mudou

        var sit = Situacao(c);
        int outros;
        using (var cmd = c.Cmd("SELECT COUNT(*) FROM emissores WHERE excluido=0 AND id<>$id", ("$id", antes?.Id ?? 0)))
            outros = Convert.ToInt32(cmd.ExecuteScalar());

        if (sit.Completa)
        {
            if (sit.MaxEmissores > 0 && outros >= sit.MaxEmissores)
                return "O plano da sua licença permite até " + sit.MaxEmissores + " emissor(es). Exclua um emissor que não usa mais ou peça um plano maior.";
            return null;
        }
        if (digitos.Length == 14) return MsgLimiteGratis;
        if (antes == null) return outros >= 1 ? MsgLimiteGratis : null;
        if (reativando && antes.Id != IdEmissorGratis(c)) return MsgLimiteGratis;
        return null;
    }
}
