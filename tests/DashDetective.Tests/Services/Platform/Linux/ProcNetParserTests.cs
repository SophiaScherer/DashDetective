using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcNetParser"/>: the host-byte-order address decode every row depends on,
/// the column layout the four socket tables share, and the malformed rows a file that changes under the
/// reader produces.</summary>
public class ProcNetParserTests {
    private static IReadOnlyList<ProcNetSocket> Parse(string body) =>
        ProcNetParser.Parse(body.Replace("\r\n", "\n").Split('\n'));

    private static IReadOnlyList<ProcNetSocket> Tcp() => Parse(ProcFixtures.ProcNetTcp);

    private static IReadOnlyList<ProcNetSocket> Tcp6() => Parse(ProcFixtures.ProcNetTcp6);

    /// <summary>The trap the parser exists for: the kernel prints each 32-bit word in HOST byte order, so
    /// <c>0100007F</c> is 127.0.0.1. Read as network order it is 1.0.0.127 — still a valid-looking
    /// address, which is why nothing downstream would catch it.</summary>
    [Fact]
    public void Parse_DecodesIpv4AddressesInHostByteOrder() {
        var loopback = Tcp()[0];

        Assert.Equal("127.0.0.1", loopback.LocalAddress.ToString());
        Assert.Equal(53, loopback.LocalPort);
    }

    /// <summary>Ports share the field with the address but are plain hex: 01BB is 443, and the byte swap
    /// the address needs would turn it into 48129.</summary>
    [Fact]
    public void Parse_ReadsPortsWithoutAByteSwap() {
        var established = Tcp()[2];

        Assert.Equal("192.168.0.101", established.LocalAddress.ToString());
        Assert.Equal(52010, established.LocalPort);
        Assert.Equal("142.250.187.238", established.RemoteAddress.ToString());
        Assert.Equal(443, established.RemotePort);
    }

    /// <summary>The state column passes through as the kernel's own code: 0A is LISTEN here, not the 10
    /// that Windows numbers Last-ack. Translating to the display numbering is the interop's job.</summary>
    [Fact]
    public void Parse_ReportsTheKernelStateCodeUntranslated() {
        var rows = Tcp();

        Assert.Equal(0x0A, rows[0].State);
        Assert.Equal(0x01, rows[2].State);
        Assert.Equal(0x06, rows[3].State);
    }

    /// <summary>The owner columns: uid is decimal, unlike its hex neighbours, and the kernel writes inode 0
    /// for a TIME_WAIT slot, which no process can own.</summary>
    [Fact]
    public void Parse_ReadsTheOwnerColumns() {
        var rows = Tcp();

        Assert.Equal(101, rows[0].Uid);
        Assert.Equal(21344, rows[0].Inode);
        Assert.Equal(0, rows[1].Uid);
        Assert.Equal(1000, rows[2].Uid);
        Assert.Equal(0, rows[3].Inode);
    }

    /// <summary>The header has twelve fields, so a column-count check alone lets it through — it is dropped
    /// because "local_address" is not an address. The truncated last line is dropped by the count.</summary>
    [Fact]
    public void Parse_DropsTheHeaderAndTornLines() {
        var rows = Tcp();

        // Six lines in: the header, four sockets, and a torn tail.
        Assert.Equal(4, rows.Count);
        Assert.Equal("127.0.0.1", rows[0].LocalAddress.ToString());
    }

    /// <summary>An IPv6 address is four independent host-order words. The unspecified and loopback
    /// addresses are the two a whole-16-byte reversal still gets right, so they are asserted next to a
    /// real one rather than on their own.</summary>
    [Fact]
    public void Parse_DecodesIpv6AddressesWordByWord() {
        var rows = Tcp6();

        Assert.Equal("::", rows[0].LocalAddress.ToString());
        Assert.Equal(8080, rows[0].LocalPort);
        Assert.Equal("::1", rows[1].LocalAddress.ToString());
        Assert.Equal(631, rows[1].LocalPort);
    }

    /// <summary>The v4-mapped form a dual-stack socket reports. Its third word is FFFF0000, which only
    /// decodes to the ::ffff: prefix if the reversal is per-word — reversing all sixteen bytes puts the
    /// mapping marker at the wrong end and yields a plausible but wrong global address.</summary>
    [Fact]
    public void Parse_DecodesV4MappedAddresses() {
        var mapped = Tcp6()[2];

        Assert.Equal("::ffff:192.168.1.100", mapped.LocalAddress.ToString());
        Assert.Equal("::ffff:142.250.187.238", mapped.RemoteAddress.ToString());
        Assert.Equal(443, mapped.RemotePort);
    }

    /// <summary>A hex group of any other length is not an address — the guard that stops the header's
    /// column names, or a half-written row, decoding into something.</summary>
    [Theory]
    [InlineData("local_address")]
    [InlineData("0100007")]
    [InlineData("0100007FF")]
    [InlineData("")]
    public void TryParseAddress_RejectsAnythingButEightOrThirtyTwoHexChars(string hex) =>
        Assert.False(ProcNetParser.TryParseAddress(hex, out _));

    /// <summary>Right length, wrong alphabet: rejected rather than decoded as zeroes.</summary>
    [Fact]
    public void TryParseAddress_RejectsNonHexOfTheRightLength() =>
        Assert.False(ProcNetParser.TryParseAddress("ZZZZZZZZ", out _));

    /// <summary>An unreadable file yields no sockets rather than throwing — the empty contract every
    /// <c>IProcFileSystem</c> caller degrades to.</summary>
    [Fact]
    public void Parse_EmptyInput_YieldsNothing() =>
        Assert.Empty(ProcNetParser.Parse([]));
}
