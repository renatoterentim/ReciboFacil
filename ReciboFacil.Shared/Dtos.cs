namespace ReciboFacil.Shared;

public static class Constantes
{
    public const string Programa = "ReciboFacil";
    public const string VersaoApp = "1.4.0";
    // Sobe quando a comunicação cliente/servidor muda de forma incompatível.
    public const int Versao = 4;
    public const string Administrador = "Administrador";
    public const string Operador = "Operador";
}

public class ErroDto
{
    public string Codigo { get; set; } = "";
    public string Mensagem { get; set; } = "";
}

public class PingDto
{
    public string Programa { get; set; } = "";
    public string VersaoApp { get; set; } = "";
    public int Protocolo { get; set; }
    public int Esquema { get; set; }
    public bool TemUsuarios { get; set; }
}

public class LoginReq
{
    public string Login { get; set; } = "";
    public string Senha { get; set; } = "";
}

public class PrimeiroAdminReq
{
    public string Login { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Senha { get; set; } = "";
}

public class LoginResp
{
    public string Token { get; set; } = "";
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Perfil { get; set; } = "";
}

public class UsuarioDto
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Perfil { get; set; } = "";
    public bool Ativo { get; set; }
    public string CriadoEm { get; set; } = "";
}

public class UsuarioSalvarReq
{
    public string Login { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Perfil { get; set; } = "";
    public string? Senha { get; set; }
}

public class SenhaReq
{
    public string? SenhaAtual { get; set; }
    public string NovaSenha { get; set; } = "";
}

public class EmissorDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Observacoes { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public class ContatoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Telefone2 { get; set; } = "";
    public string Email { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";
    public string Ocupacao { get; set; } = "";
    public string ChavePix { get; set; } = "";
    public string Observacoes { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public class ReciboItemDto
{
    public string Descricao { get; set; } = "";
    public long ValorCentavos { get; set; }
}

public static class LimitesRecibo
{
    public const int MaxItens = 8;
    public const int MaxDescricaoItem = 100;
}

public class ReciboCriarReq
{
    public int EmissorId { get; set; }
    public int ContatoId { get; set; }
    // PAGAMENTO: o emissor paga ao contato. RECEBIMENTO: o emissor recebe do contato.
    public string Sentido { get; set; } = "PAGAMENTO";
    // Com itens, o valor total é a soma deles (o servidor recalcula) e "Referente" vira um título opcional.
    public long ValorCentavos { get; set; }
    public string Referente { get; set; } = "";
    public List<ReciboItemDto> Itens { get; set; } = new();
    public bool MostrarDocPagador { get; set; }
    public string Data { get; set; } = "";      // yyyy-MM-dd
    public string Cidade { get; set; } = "";
    public int? DuplicadoDe { get; set; }
}

public class ReciboDto
{
    public int Numero { get; set; }
    public string Sentido { get; set; } = "PAGAMENTO";
    public int EmissorId { get; set; }
    public int ContatoId { get; set; }
    // Cópia dos dados no momento da emissão (não muda se o cadastro for alterado depois).
    public EmissorDto Emissor { get; set; } = new();
    public ContatoDto Contato { get; set; } = new();
    public long ValorCentavos { get; set; }
    public string ValorExtenso { get; set; } = "";
    public string Referente { get; set; } = "";
    public List<ReciboItemDto> Itens { get; set; } = new();
    public bool MostrarDocPagador { get; set; }
    public string Data { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string EmitidoEm { get; set; } = "";
    public string EmitidoPor { get; set; } = "";
    public bool Cancelado { get; set; }
    public string CanceladoEm { get; set; } = "";
    public string CanceladoPor { get; set; } = "";
    public string Motivo { get; set; } = "";
    public int? DuplicadoDe { get; set; }
}

public class ReciboResumoDto
{
    public int Numero { get; set; }
    public string Data { get; set; } = "";
    public string Sentido { get; set; } = "";
    public string EmissorNome { get; set; } = "";
    public string ContatoNome { get; set; } = "";
    public string ContatoDocumento { get; set; } = "";
    public long ValorCentavos { get; set; }
    public string Referente { get; set; } = "";
    public bool Cancelado { get; set; }
    public string EmitidoPor { get; set; } = "";
    public string Motivo { get; set; } = "";
    public string CanceladoPor { get; set; } = "";
    public string CanceladoEm { get; set; } = "";
}

public class CancelarReq
{
    public string Motivo { get; set; } = "";
}

public class NumeroReq
{
    public int Numero { get; set; }
}

public class ProximoNumeroDto
{
    public int Proximo { get; set; }
}
