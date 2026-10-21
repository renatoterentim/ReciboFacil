namespace ReciboFacil.Shared;

public static class Constantes
{
    public const string Programa = "ReciboFacil";
    public const string VersaoApp = "1.0.0-etapa1";
    // Sobe quando a comunicação cliente/servidor muda de forma incompatível.
    public const int Versao = 1;
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
