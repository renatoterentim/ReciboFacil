using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public class ApiException : Exception
{
    public int Status { get; }
    public string Codigo { get; }
    public ApiException(int status, string codigo, string mensagem) : base(mensagem)
    {
        Status = status;
        Codigo = codigo;
    }
}

public class ApiClient
{
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);
    readonly HttpClient _http;
    public string? Token { get; set; }

    public ApiClient(string endereco)
    {
        endereco = endereco.Trim();
        if (!endereco.StartsWith("http", StringComparison.OrdinalIgnoreCase)) endereco = "http://" + endereco;
        _http = new HttpClient { BaseAddress = new Uri(endereco.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(20) };
    }

    async Task<string> Raw(HttpMethod metodo, string caminho, object? corpo = null)
    {
        using var req = new HttpRequestMessage(metodo, caminho.TrimStart('/'));
        if (Token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        if (corpo != null)
            req.Content = new StringContent(JsonSerializer.Serialize(corpo, corpo.GetType(), J), Encoding.UTF8, "application/json");
        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            throw new ApiException(0, "CONEXAO", "Não foi possível conectar ao servidor. Verifique se ele está ligado e se o endereço está correto.");
        }
        using (resp)
        {
            var txt = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                ErroDto? e = null;
                try { e = JsonSerializer.Deserialize<ErroDto>(txt, J); } catch { }
                throw new ApiException((int)resp.StatusCode, e?.Codigo ?? "", string.IsNullOrWhiteSpace(e?.Mensagem) ? "Erro " + (int)resp.StatusCode + " no servidor." : e!.Mensagem);
            }
            return txt;
        }
    }

    async Task<T> Obter<T>(string caminho) =>
        JsonSerializer.Deserialize<T>(await Raw(HttpMethod.Get, caminho).ConfigureAwait(false), J)!;

    async Task<T> Enviar<T>(HttpMethod metodo, string caminho, object? corpo) =>
        JsonSerializer.Deserialize<T>(await Raw(metodo, caminho, corpo).ConfigureAwait(false), J)!;

    static string B(bool v) => v ? "true" : "false";

    // ---- Sessão ----
    public Task<PingDto> Ping() => Obter<PingDto>("api/ping");

    public async Task<LoginResp> Login(string login, string senha)
    {
        var r = await Enviar<LoginResp>(HttpMethod.Post, "api/login", new LoginReq { Login = login, Senha = senha });
        Token = r.Token;
        return r;
    }

    public async Task<LoginResp> PrimeiroAdmin(string login, string nome, string senha)
    {
        var r = await Enviar<LoginResp>(HttpMethod.Post, "api/primeiro-admin", new PrimeiroAdminReq { Login = login, Nome = nome, Senha = senha });
        Token = r.Token;
        return r;
    }

    public async Task Logout()
    {
        try { await Raw(HttpMethod.Post, "api/logout"); } catch { }
        Token = null;
    }

    public Task AlterarMinhaSenha(string atual, string nova) =>
        Raw(HttpMethod.Post, "api/me/senha", new SenhaReq { SenhaAtual = atual, NovaSenha = nova });

    // ---- Usuários ----
    public Task<List<UsuarioDto>> ListarUsuarios() => Obter<List<UsuarioDto>>("api/usuarios");
    public Task<UsuarioDto> CriarUsuario(UsuarioSalvarReq r) => Enviar<UsuarioDto>(HttpMethod.Post, "api/usuarios", r);
    public Task<UsuarioDto> EditarUsuario(int id, UsuarioSalvarReq r) => Enviar<UsuarioDto>(HttpMethod.Put, "api/usuarios/" + id, r);
    public Task RedefinirSenha(int id, string nova) => Raw(HttpMethod.Post, "api/usuarios/" + id + "/senha", new SenhaReq { NovaSenha = nova });
    public Task Suspender(int id) => Raw(HttpMethod.Post, "api/usuarios/" + id + "/suspender");
    public Task Reativar(int id) => Raw(HttpMethod.Post, "api/usuarios/" + id + "/reativar");

    // ---- Emissores ----
    public Task<List<EmissorDto>> ListarEmissores(bool inativos) => Obter<List<EmissorDto>>("api/emissores?inativos=" + B(inativos));
    public Task<EmissorDto> CriarEmissor(EmissorDto d) => Enviar<EmissorDto>(HttpMethod.Post, "api/emissores", d);
    public Task<EmissorDto> EditarEmissor(int id, EmissorDto d) => Enviar<EmissorDto>(HttpMethod.Put, "api/emissores/" + id, d);

    // ---- Contatos ----
    public Task<List<ContatoDto>> ListarContatos(string busca, bool inativos) =>
        Obter<List<ContatoDto>>("api/contatos?q=" + Uri.EscapeDataString(busca ?? "") + "&inativos=" + B(inativos));
    public Task<ContatoDto> CriarContato(ContatoDto d, bool forcar) => Enviar<ContatoDto>(HttpMethod.Post, "api/contatos?forcar=" + B(forcar), d);
    public Task<ContatoDto> EditarContato(int id, ContatoDto d, bool forcar) => Enviar<ContatoDto>(HttpMethod.Put, "api/contatos/" + id + "?forcar=" + B(forcar), d);
    public Task<List<string>> Ocupacoes() => Obter<List<string>>("api/ocupacoes");

    // ---- Recibos ----
    public Task<ReciboDto> CriarRecibo(ReciboCriarReq r) => Enviar<ReciboDto>(HttpMethod.Post, "api/recibos", r);
    public Task<List<ReciboResumoDto>> ListarRecibos(FiltroRecibos f) => Obter<List<ReciboResumoDto>>("api/recibos" + f.ToQuery());
    public Task<ReciboDto> ObterRecibo(int numero) => Obter<ReciboDto>("api/recibos/" + numero);
    public Task<ReciboDto> CancelarRecibo(int numero, string motivo) =>
        Enviar<ReciboDto>(HttpMethod.Post, "api/recibos/" + numero + "/cancelar", new CancelarReq { Motivo = motivo });
    public Task RegistrarVia(int numero) => Raw(HttpMethod.Post, "api/recibos/" + numero + "/via");
    public Task<ProximoNumeroDto> ProximoNumero() => Obter<ProximoNumeroDto>("api/recibos/proximo-numero");
    public Task<ProximoNumeroDto> DefinirProximoNumero(int numero) =>
        Enviar<ProximoNumeroDto>(HttpMethod.Post, "api/recibos/proximo-numero", new NumeroReq { Numero = numero });
}

public class FiltroRecibos
{
    public int? Numero { get; set; }
    public string? De { get; set; }
    public string? Ate { get; set; }
    public int? EmissorId { get; set; }
    public string Contato { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Descricao { get; set; } = "";
    public long? ValorMin { get; set; }
    public long? ValorMax { get; set; }
    public bool IncluirCancelados { get; set; }

    public string ToQuery()
    {
        var p = new List<string>();
        void Add(string chave, string? valor)
        {
            if (!string.IsNullOrEmpty(valor)) p.Add(chave + "=" + Uri.EscapeDataString(valor));
        }
        Add("numero", Numero?.ToString());
        Add("de", De);
        Add("ate", Ate);
        Add("emissorId", EmissorId?.ToString());
        Add("contato", Contato);
        Add("documento", Documento);
        Add("descricao", Descricao);
        Add("valorMin", ValorMin?.ToString());
        Add("valorMax", ValorMax?.ToString());
        Add("cancelados", IncluirCancelados ? "true" : "false");
        return p.Count == 0 ? "" : "?" + string.Join("&", p);
    }
}
