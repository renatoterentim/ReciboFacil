using ReciboFacil.Shared;
using static ReciboFacil.Servidor.Resp;

namespace ReciboFacil.Servidor;

public static class EndpointsLicenca
{
    public static void Mapear(WebApplication app)
    {
        // Qualquer usuário logado vê o estado da licença.
        app.MapGet("/api/licenca", () =>
        {
            using var c = Db.Abrir();
            return Results.Json(LicencaServico.Situacao(c).Status);
        });

        app.MapPost("/api/licenca", (HttpContext ctx, LicencaInstalarReq req) =>
        {
            var nao = ExigirAdmin(ctx);
            if (nao != null) return nao;
            using var c = Db.Abrir();
            var erro = LicencaServico.Instalar(c, req.Conteudo ?? "");
            if (erro != null) return Erro(400, "LICENCA_INVALIDA", erro);
            var st = LicencaServico.Situacao(c).Status;
            Db.Auditar(c, S(ctx), "LICENCA", "licenca", null, st.Cliente + " até " + st.ValidoAte + " (" + st.Tipo + ")");
            return Results.Json(st);
        });
    }
}
