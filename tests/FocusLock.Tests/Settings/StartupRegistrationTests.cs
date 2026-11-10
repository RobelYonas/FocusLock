using FocusLock.Core.Abstractions;
using FocusLock.Core.Settings;
using FocusLock.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusLock.Tests.Settings;

public class StartupRegistrationTests
{
    private readonly FakeRegistry _registry = new();
    private readonly StartupRegistration _registration;

    public StartupRegistrationTests()
    {
        _registration = new StartupRegistration(
            _registry,
            new FakeAppEnvironment { ExecutablePath = @"C:\Apps\Focus Lock\FocusLock.exe" },
            NullLogger<StartupRegistration>.Instance);
    }

    [Fact]
    public void Enabling_writes_a_quoted_command_with_the_minimized_argument()
    {
        _registration.Apply(true);

        Assert.True(_registration.IsRegistered);
        Assert.Equal(
            "\"C:\\Apps\\Focus Lock\\FocusLock.exe\" --minimized",
            _registry.GetString(RegistryRoot.CurrentUser, StartupRegistration.RunKey, StartupRegistration.ValueName));
    }

    [Fact]
    public void Disabling_removes_the_entry()
    {
        _registration.Apply(true);

        _registration.Apply(false);

        Assert.False(_registration.IsRegistered);
    }
}
