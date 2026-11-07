using DashDetective.Services.Diagnostics;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Services.Diagnostics;

/// <summary>Pins that the log file is named from the day of each write, so a long session rolls over.</summary>
public class LogTests : IDisposable {
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dd-log-" + Guid.NewGuid().ToString("N"));

    public LogTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Append_OnTwoDays_WritesTwoFiles() {
        Log.Append(_dir, new DateTime(2026, 11, 3, 23, 59, 0), "first");
        Log.Append(_dir, new DateTime(2026, 11, 4, 0, 1, 0), "second");

        Assert.Equal(["dashdetective-20261103.log", "dashdetective-20261104.log"],
            Directory.GetFiles(_dir).Select(path => Path.GetFileName(path)!).Order().ToArray());
    }
}
