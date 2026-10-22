# ReciboFácil — Etapa 2 (recibos) — versão 1.3.0

## Novidades da 1.4.0

- **Ícone personalizado** (recibo com selo de "pago") no `ReciboFacil.exe`, no `ReciboFacil.Servidor.exe` e nas janelas do programa.
  O arquivo `icone.ico` está nas pastas `ReciboFacil.Cliente` e `ReciboFacil.Servidor` (suba as duas pastas ao GitHub).
- Se o Windows continuar mostrando o ícone antigo no Explorer ou na barra de tarefas, é o cache de ícones: reinicie o computador
  ou apague o atalho antigo e crie outro. O ícone de um atalho já criado só muda se o atalho apontar para o .exe novo.
- Só o cliente (e o ícone do servidor) mudam; o banco de dados e a comunicação não mudam.

## Novidades da 1.3.2

- O histórico mostra uma coluna **"Motivo do cancelamento"** para os recibos cancelados. Passando o mouse sobre a linha aparece
  quem cancelou e quando. Para o motivo aparecer, atualize também o **servidor** (o cliente sozinho mostra a coluna vazia).

## Novidades da 1.3.1

- Os recibos cancelados agora aparecem sempre no histórico, em vermelho (não há mais a caixa "Incluir cancelados").
  Eles não entram nos totais de pagamentos e recebimentos. Só o programa cliente mudou: não é preciso trocar o servidor.

## Novidades da 1.3.0

- Na tela de Novo recibo há a caixa **"Mostrar CPF/CNPJ do pagador no recibo"**, desmarcada por padrão. Marcada, o documento
  de quem paga aparece em letra menor, à direita do nome, na linha "Recebi de". Só aparece se o pagador tiver CPF/CNPJ cadastrado.
  A escolha fica gravada no recibo (a 2ª via sai igual) e é copiada ao duplicar.
- Teste: emita um recibo com a caixa desmarcada (sai como antes) e outro marcada, em Pagamento (pagador = emissor) e em
  Recebimento (pagador = contato, com e sem CPF/CNPJ cadastrado). Abra a 2ª via de cada um. Teste um nome de pagador bem longo.
- Servidor e todos os computadores precisam ser atualizados juntos.

## Novidades da 1.2.0

- **Cidade e UF** na linha de local e data do recibo ("SÃO JOSÉ DO RIO PRETO - SP, 22 de outubro de 2026").
  O campo "Cidade / UF" vem preenchido com a cidade e a UF cadastradas no emissor e pode ser editado.
  Recibos antigos passam a mostrar a UF do emissor quando a cidade do recibo é a dele (só na impressão; nada é gravado de novo).
- **Recibo com vários itens**: no Novo recibo, marque "Detalhar em vários itens". Preencha uma linha por serviço
  (descrição e valor) e o total, com o valor por extenso, é calculado sozinho. No recibo impresso, cada item aparece com
  o seu valor e o TOTAL no final. Até 8 itens por recibo. O "Título" é opcional (ex.: "Serviços de outubro").
- No histórico, a busca por descrição também encontra o texto dos itens. Duplicar um recibo com itens traz os itens.
- Servidor e todos os computadores precisam ser atualizados juntos (um cliente antigo avisa que é incompatível).

## Testes da 1.2.0

1. Novo recibo com emissor que tenha cidade e UF cadastradas: o campo "Cidade / UF" já vem "Cidade - UF". Emita e confira na impressão.
2. Abra um recibo antigo (2ª via) de um emissor com UF: a linha de local agora mostra a UF.
3. Marque "Detalhar em vários itens". Digite 3 itens ("Pintura" 1200, "Material" 350,50, "Transporte" 80). O total e o extenso mudam sozinhos.
   Digite o valor de várias formas (1200, 1.200,00, R$ 1200,50): a coluna padroniza para 1.200,00.
4. Deixe uma linha só com descrição ou só com valor e clique em Visualizar e emitir: deve avisar o item incompleto.
5. Preencha 8 itens: a linha vazia de "novo item" deixa de aparecer (máximo). Use "Remover linha" para apagar um item.
6. Emita com título e sem título. Imprima em A4: confira as linhas, o valor à direita e o TOTAL embaixo; teste 2, 5 e 8 itens
   (com 8 itens a letra fica menor, é esperado).
7. No histórico, abra o recibo (2ª via), duplique-o (os itens voltam) e cancele um recibo com itens (marca CANCELADO).
8. Busque no histórico por uma palavra que só existe na descrição de um item.
9. Recibo simples (sem marcar itens) continua funcionando como antes.


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
- Cancelar (só administrador): o recibo continua na lista do histórico, em vermelho, com a situação "Cancelado"; ao abri-lo, aparece a marca CANCELADO.

**Regras**
- Os números são sequenciais para todos os emissores. O cancelamento não libera o número.
- Altere o endereço de um contato e reabra um recibo antigo: o recibo antigo não muda.
- Administração > Numeração dos recibos: aumenta o próximo número (nunca diminui).
- Operador não vê o menu Administração e não consegue cancelar.
