namespace Demos.Editeur;

/// <summary>
/// Éditeur de contenu de Projet Démos : crée des produits, des recettes, des types de bâtiments et des besoins dans
/// game/data, puis régénère le tableur d'équilibrage. À lancer avec editeur.bat, depuis le dossier demos.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            System.Windows.Forms.Application.Run(new EditorForm(ToolPaths.Find()));
        }
        catch (DirectoryNotFoundException e)
        {
            MessageBox.Show(e.Message, "Projet Démos — éditeur de contenu", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
