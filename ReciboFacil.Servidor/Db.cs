using ReciboFacil.Shared;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace ReciboFacil.Servidor;

public static class Db
{
    public static string DataDir = @"C:\ReciboFacil\Dados";
    public static string DbPath => Path.Combine(DataDir, "recibofacil.db");

    // Cada item é uma migração, aplicada uma única vez e em ordem.
    // NUNCA altere uma migração já publicada: para mudar o banco, ADICIONE uma nova ao final.
    public static readonly string[] Migracoes = new[]
    {
        // 1 - Etapa 1: usuários, emissores, contatos, auditoria
        @"
CREATE TABLE usuarios (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  login TEXT NOT NULL COLLATE NOCASE UNIQUE,
  nome TEXT NOT NULL,
  perfil TEXT NOT NULL,
  senha_hash TEXT NOT NULL,
  senha_salt TEXT NOT NULL,
  ativo INTEGER NOT NULL DEFAULT 1,
  criado_em TEXT NOT NULL
);
CREATE TABLE emissores (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  nome TEXT NOT NULL,
  documento TEXT NOT NULL DEFAULT '',
  documento_digitos TEXT NOT NULL DEFAULT '',
  endereco TEXT NOT NULL DEFAULT '',
  cidade TEXT NOT NULL DEFAULT '',
  uf TEXT NOT NULL DEFAULT '',
  telefone TEXT NOT NULL DEFAULT '',
  email TEXT NOT NULL DEFAULT '',
  observacoes TEXT NOT NULL DEFAULT '',
  ativo INTEGER NOT NULL DEFAULT 1,
  criado_em TEXT NOT NULL,
  atualizado_em TEXT NOT NULL
);
CREATE TABLE contatos (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  nome TEXT NOT NULL,
  documento TEXT NOT NULL DEFAULT '',
  documento_digitos TEXT NOT NULL DEFAULT '',
  telefone TEXT NOT NULL DEFAULT '',
  telefone2 TEXT NOT NULL DEFAULT '',
  email TEXT NOT NULL DEFAULT '',
  endereco TEXT NOT NULL DEFAULT '',
  cidade TEXT NOT NULL DEFAULT '',
  uf TEXT NOT NULL DEFAULT '',
  ocupacao TEXT NOT NULL DEFAULT '',
  chave_pix TEXT NOT NULL DEFAULT '',
  observacoes TEXT NOT NULL DEFAULT '',
  ativo INTEGER NOT NULL DEFAULT 1,
  criado_em TEXT NOT NULL,
  atualizado_em TEXT NOT NULL
);
CREATE INDEX ix_contatos_doc ON contatos(documento_digitos);
CREATE TABLE auditoria (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  data_hora TEXT NOT NULL,
  usuario_id INTEGER,
  usuario_login TEXT NOT NULL,
  acao TEXT NOT NULL,
  entidade TEXT NOT NULL,
  entidade_id INTEGER,
  detalhe TEXT NOT NULL DEFAULT ''
);
",
        // 2 - Etapa 2: recibos (numeração sequencial que nunca volta, dados congelados, sem edição/exclusão)
        @"
CREATE TABLE recibos (
  numero INTEGER PRIMARY KEY AUTOINCREMENT,
  sentido TEXT NOT NULL,
  emissor_id INTEGER NOT NULL REFERENCES emissores(id),
  contato_id INTEGER NOT NULL REFERENCES contatos(id),
  emissor_nome TEXT NOT NULL,
  contato_nome TEXT NOT NULL,
  contato_documento TEXT NOT NULL DEFAULT '',
  contato_documento_digitos TEXT NOT NULL DEFAULT '',
  emissor_json TEXT NOT NULL,
  contato_json TEXT NOT NULL,
  valor_centavos INTEGER NOT NULL,
  valor_extenso TEXT NOT NULL,
  referente TEXT NOT NULL,
  data_recibo TEXT NOT NULL,
  cidade TEXT NOT NULL DEFAULT '',
  emitido_em TEXT NOT NULL,
  emitido_por_id INTEGER,
  emitido_por_login TEXT NOT NULL,
  duplicado_de INTEGER,
  cancelado INTEGER NOT NULL DEFAULT 0,
  cancelado_em TEXT,
  cancelado_por_id INTEGER,
  cancelado_por_login TEXT,
  motivo_cancelamento TEXT
);
CREATE INDEX ix_recibos_data ON recibos(data_recibo);
CREATE INDEX ix_recibos_emissor ON recibos(emissor_id);
CREATE INDEX ix_recibos_contato ON recibos(contato_id);
INSERT INTO sqlite_sequence(name, seq) VALUES('recibos', 0);
CREATE TRIGGER recibos_sem_exclusao BEFORE DELETE ON recibos
BEGIN
  SELECT RAISE(ABORT, 'Recibos não podem ser apagados.');
END;
CREATE TRIGGER recibos_sem_alteracao BEFORE UPDATE ON recibos
WHEN OLD.numero <> NEW.numero OR OLD.sentido <> NEW.sentido OR OLD.valor_centavos <> NEW.valor_centavos
  OR OLD.valor_extenso <> NEW.valor_extenso OR OLD.referente <> NEW.referente OR OLD.data_recibo <> NEW.data_recibo
  OR OLD.cidade <> NEW.cidade OR OLD.emissor_json <> NEW.emissor_json OR OLD.contato_json <> NEW.contato_json
  OR OLD.emitido_em <> NEW.emitido_em OR OLD.cancelado = 1
BEGIN
  SELECT RAISE(ABORT, 'Recibo emitido não pode ser alterado.');
END;
",
        // 3 - Recibo com vários itens (descrição + valor); o total é a soma dos itens
        @"
ALTER TABLE recibos ADD COLUMN itens_json TEXT NOT NULL DEFAULT '[]';
DROP TRIGGER recibos_sem_alteracao;
CREATE TRIGGER recibos_sem_alteracao BEFORE UPDATE ON recibos
WHEN OLD.numero <> NEW.numero OR OLD.sentido <> NEW.sentido OR OLD.valor_centavos <> NEW.valor_centavos
  OR OLD.valor_extenso <> NEW.valor_extenso OR OLD.referente <> NEW.referente OR OLD.data_recibo <> NEW.data_recibo
  OR OLD.cidade <> NEW.cidade OR OLD.emissor_json <> NEW.emissor_json OR OLD.contato_json <> NEW.contato_json
  OR OLD.itens_json <> NEW.itens_json OR OLD.emitido_em <> NEW.emitido_em OR OLD.cancelado = 1
BEGIN
  SELECT RAISE(ABORT, 'Recibo emitido não pode ser alterado.');
END;
",
        // 4 - Opção de mostrar o CPF/CNPJ do pagador no recibo (definida na emissão, vale também para a 2ª via)
        @"
ALTER TABLE recibos ADD COLUMN mostrar_doc_pagador INTEGER NOT NULL DEFAULT 0;
DROP TRIGGER recibos_sem_alteracao;
CREATE TRIGGER recibos_sem_alteracao BEFORE UPDATE ON recibos
WHEN OLD.numero <> NEW.numero OR OLD.sentido <> NEW.sentido OR OLD.valor_centavos <> NEW.valor_centavos
  OR OLD.valor_extenso <> NEW.valor_extenso OR OLD.referente <> NEW.referente OR OLD.data_recibo <> NEW.data_recibo
  OR OLD.cidade <> NEW.cidade OR OLD.emissor_json <> NEW.emissor_json OR OLD.contato_json <> NEW.contato_json
  OR OLD.itens_json <> NEW.itens_json OR OLD.mostrar_doc_pagador <> NEW.mostrar_doc_pagador
  OR OLD.emitido_em <> NEW.emitido_em OR OLD.cancelado = 1
BEGIN
  SELECT RAISE(ABORT, 'Recibo emitido não pode ser alterado.');
END;
",
        // 5 - Licença: configurações gerais (id da instalação, última data vista)
        @"
CREATE TABLE configuracoes (
  chave TEXT PRIMARY KEY,
  valor TEXT NOT NULL
);
",
        // 6 - Emissor excluído (exclusão lógica: preserva o histórico dos recibos)
        @"
ALTER TABLE emissores ADD COLUMN excluido INTEGER NOT NULL DEFAULT 0;
"
    };

    public static SqliteConnection Abrir()
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = DbPath, ForeignKeys = true }.ToString();
        var c = new SqliteConnection(cs);
        c.Open();
        return c;
    }

    public static SqliteCommand Cmd(this SqliteConnection c, string sql, params (string Nome, object? Valor)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in ps) cmd.Parameters.AddWithValue(p.Nome, p.Valor ?? DBNull.Value);
        return cmd;
    }

    public static void Migrar()
    {
        if (Migracoes.Length != Constantes.VersaoEsquema)
            throw new InvalidOperationException("Constantes.VersaoEsquema precisa ser igual ao número de migrações.");
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(Path.Combine(DataDir, "Backups"));
        bool existia = File.Exists(DbPath);
        using var c = Abrir();
        using (var cmd = c.Cmd("CREATE TABLE IF NOT EXISTS esquema (versao INTEGER NOT NULL)")) cmd.ExecuteNonQuery();
        int atual;
        using (var cmd = c.Cmd("SELECT COALESCE(MAX(versao), 0) FROM esquema")) atual = Convert.ToInt32(cmd.ExecuteScalar());

        if (atual > Migracoes.Length)
            throw new InvalidOperationException("O banco de dados é de uma versão mais nova que este programa. Atualize o ReciboFácil antes de continuar.");
        if (atual == Migracoes.Length) return;

        // Antes de mexer num banco que já tem dados, guarda uma cópia de segurança.
        if (existia && atual > 0)
        {
            var destino = Path.Combine(DataDir, "Backups", $"pre-migracao-v{atual}-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            using var bk = c.Cmd("VACUUM INTO '" + destino.Replace("'", "''") + "'");
            bk.ExecuteNonQuery();
        }

        for (int i = atual; i < Migracoes.Length; i++)
        {
            using var tx = c.BeginTransaction();
            using (var cmd = c.Cmd(Migracoes[i])) { cmd.Transaction = tx; cmd.ExecuteNonQuery(); }
            using (var cmd = c.Cmd("INSERT INTO esquema(versao) VALUES($v)", ("$v", i + 1))) { cmd.Transaction = tx; cmd.ExecuteNonQuery(); }
            tx.Commit();
        }
    }

    public static void Auditar(SqliteConnection c, Sessao? s, string acao, string entidade, long? entidadeId, string detalhe = "")
    {
        using var cmd = c.Cmd(
            "INSERT INTO auditoria(data_hora, usuario_id, usuario_login, acao, entidade, entidade_id, detalhe) VALUES($d,$u,$l,$a,$e,$i,$t)",
            ("$d", Resp.Agora()), ("$u", s?.Id), ("$l", s?.Login ?? "-"), ("$a", acao), ("$e", entidade), ("$i", entidadeId), ("$t", detalhe));
        cmd.ExecuteNonQuery();
    }

    public static string Normalizar(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
