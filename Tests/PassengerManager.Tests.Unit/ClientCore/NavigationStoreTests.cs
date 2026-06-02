using PassengerManager.Client.Core.Stores;

namespace PassengerManager.Tests.Unit.ClientCore;

public class NavigationStoreTests
{
    [Test]
    public void SettingCurrentViewModelRaisesChanged()
    {
        NavigationStore store = new();
        int changeCount = 0;
        store.CurrentViewModelChanged += () => changeCount++;

        store.CurrentViewModel = new TestViewModel();

        Assert.That(changeCount, Is.EqualTo(1));
    }

    private sealed class TestViewModel : PassengerManager.Client.Core.ViewModels.BaseViewModel
    {
        public TestViewModel() : base(new StubNavigationService(), new StubAccountStore())
        {
        }
    }

    private sealed class StubNavigationService : PassengerManager.Client.Core.Services.Interfaces.INavigationService
    {
        public void NavigateTo<TViewModel>() where TViewModel : class
        {
        }
    }

    private sealed class StubAccountStore : PassengerManager.Client.Core.Stores.AccountStore
    {
    }
}
