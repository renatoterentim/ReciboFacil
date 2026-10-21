using ReciboFacil.Shared;

namespace ReciboFacil.Servidor;

public static class Resp
{
    public static IResult Erro(int status, string codigo, string mensagem) =>
        Results.Json(new ErroDto { Codigo = codigo, Mensagem = mensagem }, statusCode: status);

    public static Sessao S(HttpContext ctx) => (Sessao)ctx.Items["sessao"]!;

    public static IResult? ExigirAdmin(HttpContext ctx) =>
        S(ctx).Perfil == Constantes.Administrador
            ? null
            : Erro(403, "SEM_PERMISSAO", "Apenas administradores podem executar esta ação.");

    public static string Agora() => DateTime.UtcNow.ToString("o");
}

public static class Doc
{
    public static string Digitos(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());

    // Retorna a mensagem de erro (ou null se estiver ok). Documento vazio é aceito.
    public static string? Normalizar(string? bruto, out string formatado, out string digitos)
    {
        formatado = "";
        digitos = Digitos(bruto);
        if (digitos.Length == 0) return null;
        if (digitos.Length == 11)
        {
            if (!CpfValido(digitos)) return "CPF inválido. Confira os números digitados.";
            formatado = digitos.Substring(0, 3) + "." + digitos.Substring(3, 3) + "." + digitos.Substring(6, 3) + "-" + digitos.Substring(9, 2);
            return null;
        }
        if (digitos.Length == 14)
        {
            if (!CnpjValido(digitos)) return "CNPJ inválido. Confira os números digitados.";
            formatado = digitos.Substring(0, 2) + "." + digitos.Substring(2, 3) + "." + digitos.Substring(5, 3) + "/" + digitos.Substring(8, 4) + "-" + digitos.Substring(12, 2);
            return null;
        }
        return "O CPF deve ter 11 dígitos e o CNPJ 14 dígitos.";
    }

    static bool CpfValido(string d)
    {
        if (d.Distinct().Count() == 1) return false;
        int[] n = d.Select(ch => ch - '0').ToArray();
        int s1 = 0;
        for (int i = 0; i < 9; i++) s1 += n[i] * (10 - i);
        int d1 = (s1 * 10) % 11; if (d1 == 10) d1 = 0;
        if (d1 != n[9]) return false;
        int s2 = 0;
        for (int i = 0; i < 10; i++) s2 += n[i] * (11 - i);
        int d2 = (s2 * 10) % 11; if (d2 == 10) d2 = 0;
        return d2 == n[10];
    }

    static bool CnpjValido(string d)
    {
        if (d.Distinct().Count() == 1) return false;
        int[] n = d.Select(ch => ch - '0').ToArray();
        int[] p1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] p2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int s1 = 0;
        for (int i = 0; i < 12; i++) s1 += n[i] * p1[i];
        int r1 = s1 % 11; int d1 = r1 < 2 ? 0 : 11 - r1;
        if (d1 != n[12]) return false;
        int s2 = 0;
        for (int i = 0; i < 13; i++) s2 += n[i] * p2[i];
        int r2 = s2 % 11; int d2 = r2 < 2 ? 0 : 11 - r2;
        return d2 == n[13];
    }
}
