using LarpStrap.App.ViewModels;

namespace LarpStrap.App.Tests;

public sealed class ShellViewModelTests
{
    private static ShellViewModel Shell(bool advanced = false)
    {
        var shell = new ShellViewModel { IsAdvancedMode = advanced };
        shell.Add("Inicio", "", "home", "_Inicio");
        shell.Add("Clientes abiertos", "", "clients", "C_lientes abiertos");
        shell.Add("FastFlags", "", "flags", "_FastFlags", NavGroup.Advanced);
        shell.Add("Ajustes", "", "settings", "A_justes", NavGroup.Footer);
        return shell;
    }

    [Fact]
    public void Pages_are_grouped_for_the_navigation_pane()
    {
        var shell = Shell();

        Assert.Equal(["Inicio", "Clientes abiertos"], shell.PlayPages.Select(p => p.Title));
        Assert.Equal(["FastFlags"], shell.AdvancedPages.Select(p => p.Title));
        Assert.Equal(["Ajustes"], shell.FooterPages.Select(p => p.Title));
        Assert.Equal(4, shell.Pages.Count);
    }

    [Fact]
    public void Leaving_advanced_mode_on_an_advanced_page_goes_back_home_and_says_so()
    {
        var shell = Shell(advanced: true);
        shell.Navigate(shell.AdvancedPages[0]);

        shell.IsAdvancedMode = false;

        Assert.Equal("Inicio", shell.Current?.Title);
        Assert.True(shell.PlayPages[0].IsSelected);
        Assert.StartsWith("Modo simple", shell.StatusMessage);
    }

    [Fact]
    public void Leaving_advanced_mode_on_a_simple_page_stays_there()
    {
        var shell = Shell(advanced: true);
        shell.Navigate(shell.FooterPages[0]);

        shell.IsAdvancedMode = false;

        Assert.Equal("Ajustes", shell.Current?.Title);
    }

    [Fact]
    public void Restoring_the_saved_mode_at_startup_announces_nothing() =>
        Assert.Equal("", Shell(advanced: true).StatusMessage);
}
