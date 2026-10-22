using System.Globalization;
using System.Text;
using ReciboFacil.Shared;

namespace ReciboFacil.Licencas;

// Ferramenta SÓ DO DESENVOLVEDOR: cria as chaves e gera os arquivos de licença (.rflic) dos clientes.
// NUNCA entregue este programa nem o arquivo chave-privada.txt a ninguém.
static class Programa
{
    static readonly string Pasta = AppContext.BaseDirectory;
    static string ArqPrivada => Path.Combine(Pasta, "chave-privada.txt");
    static string ArqPublica => Path.Combine(Pasta, "chave-publica.txt");
    static string PastaLicencas => Path.Combine(Pasta, "licencas");
    static string ArqRegistro => Path.Combine(Pasta, "licencas-emitidas.csv");
    const string Fmt = "yyyy-MM-dd";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.Title = "ReciboFácil - Gerador de licenças";
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== ReciboFácil - Gerador de licenças (uso exclusivo do desenvolvedor) ===");
            Console.WriteLine("1) Criar minhas chaves (fazer UMA única vez)");
            Console.WriteLine("2) Gerar licença para um cliente (nova ou renovação)");
            Console.WriteLine("3) Conferir um arquivo de licença");
            Console.WriteLine("0) Sair");
            Console.Write("Escolha: ");
            switch ((Console.ReadLine() ?? "").Trim())
            {
                case "1": CriarChaves(); break;
                case "2": GerarLicenca(); break;
                case "3": Conferir(); break;
                case "0": return;
            }
        }
    }

    static string Perguntar(string texto, string padrao = "")
    {
        Console.Write(texto + (padrao.Length > 0 ? " [" + padrao + "]" : "") + ": ");
        var r = (Console.ReadLine() ?? "").Trim();
        return r.Length == 0 ? padrao : r;
    }

    static void CriarChaves()
    {
        if (File.Exists(ArqPrivada))
        {
            Console.WriteLine("\nJá existe uma chave em " + ArqPrivada + ".");
            Console.WriteLine("Criar outra INVALIDARIA todas as licenças já emitidas. Nada foi alterado.");
            return;
        }
        var (priv, pub) = LicencaCripto.GerarChaves();
        File.WriteAllText(ArqPrivada, priv);
        File.WriteAllText(ArqPublica, pub);
        Console.WriteLine("\nChaves criadas na pasta do gerador:");
        Console.WriteLine("  " + ArqPrivada + "   <- SECRETA. Faça cópia de segurança (pen drive/nuvem privada) e NUNCA envie a ninguém.");
        Console.WriteLine("  " + ArqPublica + "   <- pública. Esta vai para o programa.");
        Console.WriteLine("\nPróximo passo: coloque o arquivo chave-publica.txt na pasta ReciboFacil.Servidor do seu projeto (substituindo o que já existe)");
        Console.WriteLine("e gere o programa de novo. Se perder a chave privada, não será possível gerar licenças que o programa aceite.");
    }

    static DateOnly? LerData(string txt)
    {
        txt = txt.Trim();
        if (txt.Length == 0) return null;
        if (DateOnly.TryParseExact(txt, "dd/MM/yyyy", Inv, DateTimeStyles.None, out var d)) return d;
        return DateOnly.MinValue; // sinal de formato inválido
    }

    static void GerarLicenca()
    {
        if (!File.Exists(ArqPrivada))
        {
            Console.WriteLine("\nPrimeiro crie as chaves (opção 1).");
            return;
        }
        Console.WriteLine();
        var cliente = Perguntar("Nome do cliente");
        if (cliente.Length == 0) { Console.WriteLine("Cancelado."); return; }
        var doc = Perguntar("CPF/CNPJ do cliente (opcional)");
        var inst = Perguntar("Código da instalação do cliente (ex.: A1B2-C3D4-E5F6-0718; vazio = vale em qualquer instalação)").ToUpperInvariant();
        var tipoTxt = Perguntar("Tipo: A = anual, M = mensal", "A").ToUpperInvariant();
        var mensal = tipoTxt.StartsWith("M");
        var limTxt = Perguntar("Máximo de emissores ativos (0 = ilimitado)", "0");
        if (!int.TryParse(limTxt, out var max) || max < 0) { Console.WriteLine("Número inválido."); return; }

        var hoje = DateOnly.FromDateTime(DateTime.Now);
        var atualTxt = Perguntar("Vencimento atual do cliente, se for renovação antecipada (dd/mm/aaaa; vazio = nova ou já vencida)");
        var atual = LerData(atualTxt);
        if (atual == DateOnly.MinValue) { Console.WriteLine("Data inválida. Use dd/mm/aaaa."); return; }

        // A renovação soma o período a partir do vencimento atual (se ainda não venceu), para o cliente não perder dias.
        var baseData = atual.HasValue && atual.Value >= hoje ? atual.Value : hoje;
        var ate = mensal ? baseData.AddMonths(1) : baseData.AddYears(1);
        var sobrescrever = Perguntar("Vence em (último dia de uso)", ate.ToString("dd/MM/yyyy", Inv));
        var personalizada = LerData(sobrescrever);
        if (personalizada == DateOnly.MinValue || personalizada == null) { Console.WriteLine("Data inválida. Use dd/mm/aaaa."); return; }
        ate = personalizada.Value;

        var dados = new LicencaDados
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant(),
            Cliente = cliente,
            Documento = doc,
            InstalacaoId = inst,
            Tipo = mensal ? "mensal" : "anual",
            Plano = "completo",
            MaxEmissores = max,
            EmitidaEm = hoje.ToString(Fmt, Inv),
            ValidoAte = ate.ToString(Fmt, Inv)
        };

        Console.WriteLine("\nResumo:");
        Console.WriteLine("  Cliente : " + cliente + (doc.Length > 0 ? " (" + doc + ")" : ""));
        Console.WriteLine("  Tipo    : " + (mensal ? "mensal" : "anual") + ", vence em " + ate.ToString("dd/MM/yyyy", Inv));
        Console.WriteLine("  Limite  : " + (max == 0 ? "emissores ilimitados" : max + " emissor(es)"));
        Console.WriteLine("  Instalação: " + (inst.Length > 0 ? inst : "qualquer"));
        if (!Perguntar("Gerar? (S/N)", "S").ToUpperInvariant().StartsWith("S")) { Console.WriteLine("Cancelado."); return; }

        var conteudo = LicencaCripto.Assinar(dados, File.ReadAllText(ArqPrivada));
        Directory.CreateDirectory(PastaLicencas);
        var nome = new string(cliente.Where(ch => char.IsLetterOrDigit(ch)).ToArray());
        if (nome.Length == 0) nome = "cliente";
        var arq = Path.Combine(PastaLicencas, "licenca-" + nome + "-" + ate.ToString("yyyyMMdd", Inv) + ".rflic");
        File.WriteAllText(arq, conteudo);

        var novo = !File.Exists(ArqRegistro);
        File.AppendAllText(ArqRegistro,
            (novo ? "emitida_em;cliente;documento;instalacao;tipo;max_emissores;vence_em;id;arquivo\r\n" : "") +
            string.Join(";", new[] { dados.EmitidaEm, cliente, doc, inst, dados.Tipo, max.ToString(), dados.ValidoAte, dados.Id, Path.GetFileName(arq) }
                .Select(x => x.Replace(";", ","))) + "\r\n", new UTF8Encoding(true));

        Console.WriteLine("\nLicença gerada: " + arq);
        Console.WriteLine("Envie este arquivo ao cliente (Administração → Licença → Carregar arquivo).");
        Console.WriteLine("O registro de todas as licenças emitidas fica em " + ArqRegistro);
    }

    static void Conferir()
    {
        if (!File.Exists(ArqPublica)) { Console.WriteLine("\nChave pública não encontrada. Crie as chaves (opção 1)."); return; }
        var caminho = Perguntar("\nCaminho do arquivo .rflic (pode arrastar o arquivo para esta janela)").Trim('"');
        if (!File.Exists(caminho)) { Console.WriteLine("Arquivo não encontrado."); return; }
        var d = LicencaCripto.Verificar(File.ReadAllText(caminho), File.ReadAllText(ArqPublica), out var erro);
        if (d == null) { Console.WriteLine("INVÁLIDA: " + erro); return; }
        Console.WriteLine("VÁLIDA (assinatura confere)");
        Console.WriteLine("  Cliente   : " + d.Cliente + (d.Documento.Length > 0 ? " (" + d.Documento + ")" : ""));
        Console.WriteLine("  Tipo      : " + d.Tipo + ", vence em " + d.ValidoAte);
        Console.WriteLine("  Emissores : " + (d.MaxEmissores == 0 ? "ilimitado" : d.MaxEmissores.ToString()));
        Console.WriteLine("  Instalação: " + (d.InstalacaoId.Length > 0 ? d.InstalacaoId : "qualquer"));
        Console.WriteLine("  Emitida em: " + d.EmitidaEm + "   (id " + d.Id + ")");
    }
}
