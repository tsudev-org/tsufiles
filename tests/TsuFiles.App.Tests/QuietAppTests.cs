// tsudev AUTH_AND_ACCOUNT 14.4 (case 17) and PRODUCT_POLICY 3.8: nothing invites the user on its own; the account
// entry is passive.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TsuFiles.App.Services;
using TsuFiles.App.ViewModels;
using TsuFiles.App.Views;
using TsuFiles.App.Views.Pages;
using TsuFiles.Core.Localization;
using TsuFiles.Core.Settings;

namespace TsuFiles.App.Tests;

public class QuietAppTests
{
    [AvaloniaTheory]
    [InlineData("vi", "Chưa đăng nhập")]
    [InlineData("en", "Not signed in")]
    public void Settings_has_a_neutral_account_entry(string language, string status)
    {
        TestServices.EnsureInitialized(language);
        var vm = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>(), AppHost.Get<UserDataStore>());
        vm.Navigate("general");
        var window = new MainWindow { DataContext = vm, Width = 1080, Height = 820 };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var card = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "AccountCard");
            Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == status);
            var button = Assert.Single(card.GetVisualDescendants().OfType<Button>());
            Assert.DoesNotContain("accent", button.Classes);
            Assert.Equal("https://tsudev.com/privacy#dang-nhap", GeneralPage.WhySignInUrl.AbsoluteUri);
        }
        finally
        {
            window.Close();
            TestServices.EnsureInitialized("en");
        }
    }
}
