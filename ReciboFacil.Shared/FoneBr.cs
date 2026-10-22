namespace ReciboFacil.Shared;

// Padroniza telefones brasileiros: (00) 00000-0000 (celular) ou (00) 0000-0000 (fixo).
// Números que não se encaixam (internacionais, 0800, sem DDD...) são mantidos como foram digitados.
public static class FoneBr
{
    public static string Formatar(string? bruto)
    {
        var t = (bruto ?? "").Trim();
        if (t.Length == 0) return "";
        if (t.StartsWith("+") && !t.StartsWith("+55")) return t;

        var d = new string(t.Where(char.IsDigit).ToArray());
        if (d.StartsWith("0800") || d.StartsWith("0300") || d.StartsWith("0500") || d.StartsWith("0900")) return t;
        if (d.StartsWith("55") && (d.Length == 12 || d.Length == 13)) d = d.Substring(2);
        if (d.StartsWith("0") && (d.Length == 11 || d.Length == 12)) d = d.Substring(1);

        if (d.Length == 11) return "(" + d.Substring(0, 2) + ") " + d.Substring(2, 5) + "-" + d.Substring(7, 4);
        if (d.Length == 10) return "(" + d.Substring(0, 2) + ") " + d.Substring(2, 4) + "-" + d.Substring(6, 4);
        return t;
    }
}

// Padroniza a chave Pix digitada: CPF com pontos, CNPJ formatado, celular como telefone.
// E-mail e chave aleatória (UUID) ficam exatamente como foram digitados.
public static class ChavePixBr
{
    static bool CpfValido(string d)
    {
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;
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
        if (d.Length != 14 || d.Distinct().Count() == 1) return false;
        int[] n = d.Select(ch => ch - '0').ToArray();
        int[] p1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] p2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int s1 = 0;
        for (int i = 0; i < 12; i++) s1 += n[i] * p1[i];
        int d1 = s1 % 11 < 2 ? 0 : 11 - s1 % 11;
        if (d1 != n[12]) return false;
        int s2 = 0;
        for (int i = 0; i < 13; i++) s2 += n[i] * p2[i];
        int d2 = s2 % 11 < 2 ? 0 : 11 - s2 % 11;
        return d2 == n[13];
    }

    static string Cpf(string d) => d.Substring(0, 3) + "." + d.Substring(3, 3) + "." + d.Substring(6, 3) + "-" + d.Substring(9, 2);
    static string Cnpj(string d) => d.Substring(0, 2) + "." + d.Substring(2, 3) + "." + d.Substring(5, 3) + "/" + d.Substring(8, 4) + "-" + d.Substring(12, 2);

    public static string Formatar(string? bruto)
    {
        var t = (bruto ?? "").Trim();
        if (t.Length == 0) return "";
        if (t.Contains('@')) return t;                       // e-mail
        if (t.Any(char.IsLetter)) return t;                  // chave aleatória (UUID) ou qualquer coisa com letras
        var d = new string(t.Where(char.IsDigit).ToArray());

        bool dicaTelefone = t.StartsWith("+") || t.Contains('(');
        if (dicaTelefone) return FoneBr.Formatar(t);

        if (d.Length == 14) return CnpjValido(d) ? Cnpj(d) : t;
        if (d.Length == 11)
        {
            if (CpfValido(d)) return Cpf(d);
            if (d[2] == '9') return FoneBr.Formatar(d);      // celular: DDD + 9 + número
            return t;
        }
        if (d.Length == 10 || ((d.Length == 12 || d.Length == 13) && d.StartsWith("55"))) return FoneBr.Formatar(d);
        return t;
    }
}
