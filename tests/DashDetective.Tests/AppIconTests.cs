using DashDetective.Tests.Services.Theming;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DashDetective.Tests;

/// <summary>
/// Pins the application icon: <c>Assets/dashdetective.ico</c> is a real multi-size ICO carrying the
/// sizes Windows asks for (16 for the title bar and tray through 256 for the large views), the csproj
/// stamps it into the executable, and nothing still points at the Avalonia sample icon it replaced.
/// Pure file reads, so no render backend is needed.
/// </summary>
public class AppIconTests {
    private static readonly int[] ExpectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    private static string IconPath => Path.Combine(PaletteFile.SourceRoot(), "Assets", "dashdetective.ico");

    [Fact]
    public void Icon_IsAnIcoHoldingEveryExpectedSize() {
        var bytes = File.ReadAllBytes(IconPath);

        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));

        var count = BitConverter.ToUInt16(bytes, 4);
        var sizes = new List<int>();
        for (var i = 0; i < count; i++) {
            // ICONDIRENTRY: width and height are one byte each, where 0 means 256.
            var entry = 6 + 16 * i;
            sizes.Add(bytes[entry] == 0 ? 256 : bytes[entry]);
        }

        Assert.Equal(ExpectedSizes, sizes.Order().ToArray());
    }

    [Fact]
    public void Icon_EveryEntryPointsAtAPngOfItsDeclaredSize() {
        var bytes = File.ReadAllBytes(IconPath);
        var count = BitConverter.ToUInt16(bytes, 4);

        for (var i = 0; i < count; i++) {
            var entry = 6 + 16 * i;
            var declared = bytes[entry] == 0 ? 256 : bytes[entry];
            var length = (int)BitConverter.ToUInt32(bytes, entry + 8);
            var offset = (int)BitConverter.ToUInt32(bytes, entry + 12);

            Assert.True(offset + length <= bytes.Length, $"The {declared}px entry runs past the end of the file.");
            Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], bytes[offset..(offset + 4)]);

            // IHDR width and height are big-endian at 16 and 20 into a PNG.
            Assert.Equal(declared, ReadBigEndian(bytes, offset + 16));
            Assert.Equal(declared, ReadBigEndian(bytes, offset + 20));
        }
    }

    [Fact]
    public void Project_StampsTheIconIntoTheExecutable() {
        var csproj = File.ReadAllText(Path.Combine(PaletteFile.SourceRoot(), "DashDetective.csproj"));

        Assert.Contains(@"<ApplicationIcon>Assets\dashdetective.ico</ApplicationIcon>", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_NoLongerReferencesTheAvaloniaSampleIcon() {
        var root = PaletteFile.SourceRoot();

        Assert.False(File.Exists(Path.Combine(root, "Assets", "avalonia-logo.ico")));

        var offenders = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".axaml" or ".cs" or ".csproj")
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("avalonia-logo", StringComparison.Ordinal))
            .ToList();

        Assert.True(offenders.Count == 0, "Still references avalonia-logo: " + string.Join(", ", offenders));
    }

    private static int ReadBigEndian(byte[] bytes, int at) =>
        (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
}
