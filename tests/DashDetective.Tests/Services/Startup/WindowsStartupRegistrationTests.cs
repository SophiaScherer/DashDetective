using DashDetective.Services.Startup;
using System;
using Xunit;

namespace DashDetective.Tests.Services.Startup;

/// <summary>Pins how <see cref="WindowsStartupRegistration"/> combines <c>Run</c> with Task Manager's switch, through a fake store.</summary>
public class WindowsStartupRegistrationTests {
    private static readonly byte[] Disabled = [0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] Enabled = [0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    [Fact]
    public void IsEnabled_RunValuePresent_IsEnabled() {
        var store = new FakeStore { Run = "\"C:\app.exe\"" };

        Assert.True(new WindowsStartupRegistration(store).IsEnabled());
    }

    [Fact]
    public void IsEnabled_RunValueMissing_IsNotEnabled() =>
        Assert.False(new WindowsStartupRegistration(new FakeStore()).IsEnabled());

    [Fact]
    public void IsEnabled_DisabledInTaskManager_IsNotEnabled() {
        var store = new FakeStore { Run = "\"C:\app.exe\"", Approval = Disabled };

        Assert.False(new WindowsStartupRegistration(store).IsEnabled());
    }

    [Fact]
    public void IsEnabled_ApprovedInTaskManager_IsEnabled() {
        var store = new FakeStore { Run = "\"C:\app.exe\"", Approval = Enabled };

        Assert.True(new WindowsStartupRegistration(store).IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_ClearsTheDisabledState() {
        var store = new FakeStore { Run = "\"C:\app.exe\"", Approval = Disabled };
        var registration = new WindowsStartupRegistration(store);

        registration.SetEnabled(true);

        Assert.Null(store.Approval);
        Assert.True(registration.IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_LeavesAnApprovedStateAlone() {
        var store = new FakeStore { Approval = Enabled };

        new WindowsStartupRegistration(store).SetEnabled(true);

        Assert.Same(Enabled, store.Approval);
    }

    [Fact]
    public void SetEnabled_False_RemovesTheRunValue() {
        var store = new FakeStore { Run = "x" };

        new WindowsStartupRegistration(store).SetEnabled(false);

        Assert.Null(store.Run);
    }

    [Fact]
    public void IsEnabled_StoreThrows_DegradesToNotEnabled() {
        var store = new FakeStore { Run = "x", Throw = true };

        Assert.False(new WindowsStartupRegistration(store).IsEnabled());
    }

    private sealed class FakeStore : IStartupRunStore {
        public string? Run { get; set; }
        public byte[]? Approval { get; set; }
        public bool Throw { get; set; }

        public string? GetRun() => Throw ? throw new InvalidOperationException() : Run;
        public void SetRun(string command) => Run = command;
        public void DeleteRun() => Run = null;
        public byte[]? GetApproval() => Approval;
        public void DeleteApproval() => Approval = null;
    }
}
