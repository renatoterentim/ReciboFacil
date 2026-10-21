namespace ReciboFacil.Shared;

// Valor monetário por extenso, em português (ex.: 1500,00 -> "mil e quinhentos reais").
public static class Extenso
{
    static readonly string[] U = { "zero", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove", "dez", "onze", "doze", "treze", "quatorze", "quinze", "dezesseis", "dezessete", "dezoito", "dezenove" };
    static readonly string[] D = { "", "", "vinte", "trinta", "quarenta", "cinquenta", "sessenta", "setenta", "oitenta", "noventa" };
    static readonly string[] C = { "", "cento", "duzentos", "trezentos", "quatrocentos", "quinhentos", "seiscentos", "setecentos", "oitocentos", "novecentos" };

    static string Ate999(int n)
    {
        if (n == 100) return "cem";
        int c = n / 100, r = n % 100;
        var p = new List<string>();
        if (c > 0) p.Add(C[c]);
        if (r > 0)
        {
            if (r < 20) p.Add(U[r]);
            else
            {
                int d = r / 10, u = r % 10;
                p.Add(D[d] + (u > 0 ? " e " + U[u] : ""));
            }
        }
        return string.Join(" e ", p);
    }

    static string Inteiro(long n)
    {
        if (n == 0) return "zero";
        int mi = (int)(n / 1_000_000), mil = (int)(n % 1_000_000 / 1000), r = (int)(n % 1000);
        var partes = new List<(int V, string T)>();
        if (mi > 0) partes.Add((mi, Ate999(mi) + (mi == 1 ? " milhão" : " milhões")));
        if (mil > 0) partes.Add((mil, mil == 1 ? "mil" : Ate999(mil) + " mil"));
        if (r > 0) partes.Add((r, Ate999(r)));
        var s = partes[0].T;
        for (int i = 1; i < partes.Count; i++)
        {
            int v = partes[i].V;
            s += (v < 100 || v % 100 == 0) ? " e " : " ";
            s += partes[i].T;
        }
        return s;
    }

    // Aceita de R$ 0,01 até R$ 999.999.999,99. Fora disso devolve texto vazio.
    public static string Moeda(long centavos)
    {
        if (centavos <= 0 || centavos > 99_999_999_999L) return "";
        long reais = centavos / 100;
        int cts = (int)(centavos % 100);
        var partes = new List<string>();
        if (reais > 0)
        {
            var de = (reais >= 1_000_000 && reais % 1_000_000 == 0) ? " de" : "";
            partes.Add(Inteiro(reais) + de + (reais == 1 ? " real" : " reais"));
        }
        if (cts > 0) partes.Add(Inteiro(cts) + (cts == 1 ? " centavo" : " centavos"));
        return string.Join(" e ", partes);
    }
}
