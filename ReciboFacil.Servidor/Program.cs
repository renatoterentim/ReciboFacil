using ReciboFacil.Servidor;
using ReciboFacil.Shared;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Host.UseWindowsService(o => o.ServiceName = "ReciboFacilServidor");

// Sem DataDir configurado, os dados ficam em ..\Dados (ao lado da pasta Servidor), ex.: C:\ReciboFacil\Dados.
var dataDir = builder.Configuration["DataDir"];
Db.DataDir = !string.IsNullOrWhiteSpace(dataDir)
    ? dataDir
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Dados"));
if (string.IsNullOrWhiteSpace(builder.Configuration["Urls"]))
    builder.WebHost.UseUrls("http://0.0.0.0:5180");

try
{
    Db.Migrar();
    Db.AjustesUnicos();
}
catch (Exception ex)
{
    // Quando roda como serviço não há console: deixa o motivo num arquivo ao lado do programa.
    try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "erro-inicializacao.txt"), DateTime.Now + Environment.NewLine + ex); } catch { }
    throw;
}

builder.Services.AddHostedService<BackupAgendador>();

var app = builder.Build();

var anonimos = new HashSet<string> { "/api/ping", "/api/login", "/api/primeiro-admin" };

app.Use(async (ctx, next) =>
{
    var path = (ctx.Request.Path.Value ?? "").TrimEnd('/').ToLowerInvariant();
    if (!path.StartsWith("/api/") || anonimos.Contains(path))
    {
        await next();
        return;
    }
    var s = Sessoes.Validar(ctx.Request.Headers.Authorization.ToString());
    if (s == null)
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsJsonAsync(new ErroDto { Codigo = "NAO_AUTENTICADO", Mensagem = "Sua sessão expirou. Entre novamente." });
        return;
    }
    ctx.Items["sessao"] = s;
    await next();
});

EndpointsUsuarios.Mapear(app);
EndpointsCadastros.Mapear(app);
EndpointsRecibos.Mapear(app);
EndpointsBackup.Mapear(app);
EndpointsLicenca.Mapear(app);

app.Run();
