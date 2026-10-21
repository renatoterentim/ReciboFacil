# ReciboFácil — Etapa 2 (recibos)

Etapa 1: servidor, banco de dados, login, usuários, emissores e contatos.
Etapa 2 (esta): emissão de recibos, prévia, impressão em meia folha A4, PDF, histórico com filtros,
segunda via, duplicar, cancelamento com motivo e numeração sequencial.

## Como gerar os programas

**Pelo GitHub:** envie o conteúdo desta pasta para o repositório (arraste as pastas `ReciboFacil.Cliente`,
`ReciboFacil.Servidor`, `ReciboFacil.Shared` e `scripts`, mais `build.bat`, `ReciboFacil.sln` e este arquivo;
os arquivos de mesmo nome são substituídos). Depois, na aba *Actions*, aguarde a compilação e baixe
`ReciboFacil-win-x64` (e `ReciboFacil-win-x86` para Windows 32 bits).

**No Windows:** instale o .NET 8 SDK e dê duplo clique em `build.bat` (resultado na pasta `publicar`).

## Atualizar de uma versão anterior (servidor e demais computadores)

1. **No servidor:** botão direito em `parar-servidor.bat` → *Executar como administrador*.
2. Copie as pastas `Servidor` e `Cliente` novas e os arquivos `.bat` para `C:\ReciboFacil`, substituindo os antigos.
   A pasta `C:\ReciboFacil\Dados` **não** deve ser apagada nem substituída.
3. **No servidor:** botão direito em `instalar-servidor.bat` → *Executar como administrador*.
   Na primeira partida o banco é atualizado sozinho; antes disso o servidor guarda uma cópia do banco antigo em
   `C:\ReciboFacil\Dados\Backups\pre-migracao-...db`.
4. **Nos outros computadores:** feche o programa e substitua a pasta `Cliente`.
   Um cliente antigo conectado a um servidor novo avisa que a versão é incompatível.

## O que testar na etapa 2

**Emissão**
- Recibos > Novo recibo (Ctrl+N). Escolha o emissor, o tipo, o contato (botão Selecionar; dentro dele dá para cadastrar
  um contato novo na hora), digite o valor e confira o "Por extenso" que aparece.
- Visualizar e emitir: confira a prévia (marca d'água "PRÉVIA"), troque a posição entre metade de cima e de baixo,
  clique em Confirmar e emitir. O número aparece só agora.
- Imprimir em papel A4 e conferir a metade usada. Salvar PDF.

**Tipos de recibo**
- Pagamento (o emissor paga ao contato): quem assina é o contato; o texto diz "Recebi de [emissor]".
- Recebimento (o emissor recebe do contato): quem assina é o emissor.
- Emitente com CNPJ: o texto usa "Recebemos de"; com CPF, "Recebi de".

**Histórico**
- Recibos > Histórico: filtre por número, período, emissor, contato, CPF/CNPJ, valor (de/até) e descrição.
- Abrir / 2ª via: sai marcado "2ª VIA" e sem número novo.
- Duplicar: abre o formulário preenchido; o novo recibo recebe número novo.
- Cancelar (só administrador): o recibo continua no histórico (marque "Incluir cancelados") com a marca CANCELADO.

**Regras**
- Os números são sequenciais para todos os emissores. O cancelamento não libera o número.
- Altere o endereço de um contato e reabra um recibo antigo: o recibo antigo não muda.
- Administração > Numeração dos recibos: aumenta o próximo número (nunca diminui).
- Operador não vê o menu Administração e não consegue cancelar.
