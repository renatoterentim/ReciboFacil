using System.Text.Json;
using ReciboFacil.Shared;

namespace ReciboFacil.Cliente;

public class ConfigCliente
{
    public string Servidor { get; set; } = "http://localhost:5180";
    public bool Configurado { get; set; }

    static string Caminho => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReciboFacil", "cliente.json");

    public static ConfigCliente Carregar()
    {
        try
        {
            if (File.Exists(Caminho))
                return JsonSerializer.Deserialize<ConfigCliente>(File.ReadAllText(Caminho)) ?? new ConfigCliente();
        }
        catch { }
        return new ConfigCliente();
    }

    public void Salvar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
            File.WriteAllText(Caminho, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}

public static class App
{
    public static ConfigCliente Config = new();
    public static ApiClient Api = null!;
    public static LoginResp Usuario = null!;
    public static MainForm? Principal;
    public static bool EhAdmin => Usuario != null && Usuario.Perfil == Constantes.Administrador;
    public const string Titulo = "ReciboFácil";
}

public static class Erros
{
    public static void Mostrar(IWin32Window? dono, Exception ex)
    {
        if (ex is ApiException a && a.Status == 401)
        {
            MessageBox.Show(dono, "Sua sessão expirou. Entre novamente.", App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);
            App.Principal?.ForcarLogin();
            return;
        }
        MessageBox.Show(dono, ex.Message, App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void Aviso(IWin32Window? dono, string texto) =>
        MessageBox.Show(dono, texto, App.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirmar(IWin32Window? dono, string texto) =>
        MessageBox.Show(dono, texto, App.Titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}
