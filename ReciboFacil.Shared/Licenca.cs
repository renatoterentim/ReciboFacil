using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReciboFacil.Shared;

// Conteúdo da licença (vai dentro do arquivo .rflic, assinado pelo desenvolvedor).
public class LicencaDados
{
    public string Id { get; set; } = "";             // identificador desta licença (para o seu controle)
    public string Cliente { get; set; } = "";
    public string Documento { get; set; } = "";      // CPF/CNPJ do cliente (informativo)
    public string InstalacaoId { get; set; } = "";   // vazio = vale em qualquer instalação
    public string Tipo { get; set; } = "anual";      // "anual" ou "mensal"
    public string Plano { get; set; } = "completo";
    public int MaxEmissores { get; set; }            // 0 = ilimitado
    public string EmitidaEm { get; set; } = "";      // yyyy-MM-dd
    public string ValidoAte { get; set; } = "";      // yyyy-MM-dd (último dia de uso)
}

public class LicencaArquivo
{
    public string Dados { get; set; } = "";          // JSON de LicencaDados (é exatamente este texto que é assinado)
    public string Assinatura { get; set; } = "";     // base64
}

// Estado da licença mostrado ao usuário.
public class LicencaStatusDto
{
    public string Estado { get; set; } = "gratis";   // gratis | ativa | tolerancia | vencida | invalida
    public string Titulo { get; set; } = "";
    public string Detalhe { get; set; } = "";
    public string Resumo { get; set; } = "";         // texto curto para a barra de status
    public string Cliente { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string ValidoAte { get; set; } = "";      // dd/MM/yyyy
    public int DiasRestantes { get; set; }
    public bool Alerta { get; set; }
    public bool Completa { get; set; }               // true = licença em vigor (ou em tolerância)
    public int MaxEmissores { get; set; }
    public string InstalacaoId { get; set; } = "";
    public bool ChaveConfigurada { get; set; }
}

public class LicencaInstalarReq
{
    public string Conteudo { get; set; } = "";
}

public static class LicencaCripto
{
    static readonly JsonSerializerOptions Bonito = new() { WriteIndented = true };

    // Gera o par de chaves (ECDSA P-256), em base64. A privada é só sua; a pública vai no programa.
    public static (string Privada, string Publica) GerarChaves()
    {
        using var k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (Convert.ToBase64String(k.ExportPkcs8PrivateKey()), Convert.ToBase64String(k.ExportSubjectPublicKeyInfo()));
    }

    public static string Assinar(LicencaDados d, string privadaBase64)
    {
        var json = JsonSerializer.Serialize(d);
        using var k = ECDsa.Create();
        k.ImportPkcs8PrivateKey(Convert.FromBase64String(privadaBase64.Trim()), out _);
        var sig = k.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256);
        return JsonSerializer.Serialize(new LicencaArquivo { Dados = json, Assinatura = Convert.ToBase64String(sig) }, Bonito);
    }

    // Confere a assinatura. Devolve os dados ou null (com a explicação em "erro").
    public static LicencaDados? Verificar(string conteudo, string? publicaBase64, out string erro)
    {
        erro = "";
        if (string.IsNullOrWhiteSpace(publicaBase64))
        {
            erro = "Esta versão do programa ainda não está habilitada para licenças (chave pública não configurada).";
            return null;
        }
        try
        {
            var arq = JsonSerializer.Deserialize<LicencaArquivo>(conteudo);
            if (arq == null || arq.Dados.Length == 0 || arq.Assinatura.Length == 0)
            {
                erro = "O arquivo não é uma licença válida.";
                return null;
            }
            using var k = ECDsa.Create();
            k.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicaBase64.Trim()), out _);
            if (!k.VerifyData(Encoding.UTF8.GetBytes(arq.Dados), Convert.FromBase64String(arq.Assinatura), HashAlgorithmName.SHA256))
            {
                erro = "A assinatura da licença não confere. O arquivo foi alterado ou não foi emitido pelo desenvolvedor.";
                return null;
            }
            var d = JsonSerializer.Deserialize<LicencaDados>(arq.Dados);
            if (d == null) { erro = "O arquivo não é uma licença válida."; return null; }
            return d;
        }
        catch (Exception)
        {
            erro = "O arquivo não é uma licença válida.";
            return null;
        }
    }
}
