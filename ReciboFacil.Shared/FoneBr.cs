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
