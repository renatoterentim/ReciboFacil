using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (s, e) =>
            MessageBox.Show("Ocorreu um erro inesperado:\n\n" + e.Exception.Message, App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);

        App.Config = ConfigCliente.Carregar();

        // Ferramenta de restauração de backup (não precisa de login; exige administrador do Windows).
        if (args.Any(a => string.Equals(a, "--restaurar", StringComparison.OrdinalIgnoreCase)))
        {
            RestaurarForm.Abrir();
            return;
        }

        while (true)
        {
            var ping = Conectar();
            if (ping == null) return;

            LoginResp? usuario;
            if (!ping.TemUsuarios)
            {
                using var f = new PrimeiroAdminForm();
                usuario = f.ShowDialog() == DialogResult.OK ? f.Resultado : null;
            }
            else
            {
                using var f = new LoginForm();
                usuario = f.ShowDialog() == DialogResult.OK ? f.Resultado : null;
            }
            if (usuario == null) return;

            App.Usuario = usuario;
            var principal = new MainForm();
            App.Principal = principal;
            Application.Run(principal);
            App.Principal = null;
            if (!principal.TrocarUsuario) break;
        }
    }

    // Garante que há um servidor respondendo e compatível; senão, pede o endereço.
    static PingDto? Conectar()
    {
        string? erro = null;
        while (true)
        {
            if (erro != null || !App.Config.Configurado)
            {
                using var f = new ConexaoForm(App.Config.Servidor, erro);
                if (f.ShowDialog() != DialogResult.OK) return null;
                App.Config.Servidor = f.Endereco;
                App.Config.Configurado = true;
                App.Config.Salvar();
            }
            try
            {
                var api = new ApiClient(App.Config.Servidor);
                var ping = Task.Run(() => api.Ping()).GetAwaiter().GetResult();
                if (ping.Programa != Constantes.Programa) { erro = "O endereço informado não é um servidor do ReciboFácil."; continue; }
                if (ping.Protocolo != Constantes.Versao)
                {
                    erro = "A versão deste programa é incompatível com a do servidor. Atualize o ReciboFácil neste computador (ou no servidor).";
                    continue;
                }
                App.Api = api;
                return ping;
            }
            catch (Exception ex)
            {
                erro = ex.Message;
            }
        }
    }
}
