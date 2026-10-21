# ReciboFácil — Etapa 1 (base)

Esta etapa entrega: servidor + banco de dados, login, gerenciador de usuários,
cadastro de emissores e de contatos (agenda com busca em todos os campos).
A emissão de recibos vem na etapa 2.

## Como gerar os programas (escolha um)

**A) Pelo GitHub (sem instalar nada no seu PC)**
1. Crie um repositório no GitHub e envie todo o conteúdo desta pasta.
2. Abra a aba *Actions* → *Compilar ReciboFacil* → *Run workflow*.
3. Ao terminar, baixe os artefatos `ReciboFacil-win-x64` (Windows 64 bits) e `ReciboFacil-win-x86` (32 bits).

**B) No seu Windows**
1. Instale o *.NET 8 SDK* (https://dotnet.microsoft.com/download/dotnet/8.0).
2. Dê duplo clique em `build.bat`. O resultado fica na pasta `publicar`.

## Instalação para teste

1. Copie o conteúdo de `publicar\win-x64` (ou `win-x86`) para `C:\ReciboFacil` em cada computador
   (fica `C:\ReciboFacil\Servidor`, `C:\ReciboFacil\Cliente` e os .bat).
2. **No computador do servidor:** botão direito em `instalar-servidor.bat` → *Executar como administrador*.
   Ele cria o serviço do Windows, libera a porta 5180 no firewall e inicia. Os dados ficam em `C:\ReciboFacil\Dados`.
3. Em cada computador (inclusive o servidor), abra `C:\ReciboFacil\Cliente\ReciboFacil.exe`.
   - No servidor, use o endereço `http://localhost:5180`.
   - Nos outros, use o IP do servidor, por exemplo `http://192.168.0.10:5180`.
4. No primeiro acesso o programa pede para criar o usuário administrador.

Se o serviço não iniciar, veja o arquivo `erro-inicializacao.txt` dentro de `C:\ReciboFacil\Servidor`.

## O que testar

- Criar contatos (com e sem CPF/CNPJ), buscar por "pedreiro", por nome, por telefone.
- Cadastrar o mesmo CPF duas vezes (deve avisar).
- Criar um emissor, um usuário operador, suspender e reativar usuários.
- Abrir o cliente em outro computador e conferir que os mesmos cadastros aparecem.
