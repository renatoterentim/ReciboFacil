namespace ReciboFacil.Shared;

public static class Constantes
{
    public const string Programa = "ReciboFacil";
    public const string Desenvolvedor = "Renato Terentim";
    public const string ContatoTelefone = "(17) 99178-8011";   // WhatsApp
    public const string ContatoWhatsApp = "5517991788011";     // só dígitos, com DDI, para o link wa.me
    public const string ContatoEmail = "renatoterentim@gmail.com";
    public const string VersaoApp = "2.3.1";
    // Sobe quando a comunicação cliente/servidor muda de forma incompatível.
    public const int Versao = 4;
    // Quantidade de migrações do banco (Db.Migracoes). A ferramenta de restauração usa para recusar backups de versão mais nova.
    public const int VersaoEsquema = 7;
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
    public string ChavePix { get; set; } = "";
    public bool Ativo { get; set; } = true;
    // Só preenchido na lista de emissores: false = este emissor precisa de licença para emitir recibos.
    public bool Liberado { get; set; } = true;
    // Emissor excluído: some das telas, mas continua no banco para o histórico de recibos.
    public bool Excluido { get; set; }
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

public class BackupConfigDto
{
    public bool Automatico { get; set; } = true;
    public string Hora { get; set; } = "23:00";
    public int Manter { get; set; } = 30;
    // Pasta no computador SERVIDOR (outro disco ou pasta de rede) que recebe uma cópia de cada backup.
    public string PastaExtra { get; set; } = "";
}

public class BackupArquivoDto
{
    public string Nome { get; set; } = "";
    // auto, manual, pre-migracao, pre-restauracao, pre-atualizacao, outro
    public string Tipo { get; set; } = "";
    public string Quando { get; set; } = "";   // ISO (UTC)
    public long Bytes { get; set; }
}

public class BackupStatusDto
{
    public BackupConfigDto Config { get; set; } = new();
    public string UltimoOkEm { get; set; } = "";
    public string UltimoOkTipo { get; set; } = "";
    public string UltimoOkArquivo { get; set; } = "";
    public long UltimoOkBytes { get; set; }
    public string UltimaFalhaEm { get; set; } = "";
    public string UltimaFalhaMensagem { get; set; } = "";
    public string ExtraUltimoOkEm { get; set; } = "";
    public string ExtraErro { get; set; } = "";
    public string ProximoAutomatico { get; set; } = "";   // texto já formatado no servidor
    public List<BackupArquivoDto> Arquivos { get; set; } = new();
}
