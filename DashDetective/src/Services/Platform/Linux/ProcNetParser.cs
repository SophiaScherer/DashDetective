using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Net;

namespace DashDetective.Services.Platform.Linux;

/// <summary>One socket row from <c>/proc/net/{tcp,tcp6,udp,udp6}</c>. <c>State</c> is the kernel's own
/// code rather than a display value; <c>Inode</c> is the socket inode that identifies the owning process,
/// and <c>Uid</c> its owner.</summary>
internal readonly record struct ProcNetSocket(
    IPAddress LocalAddress, int LocalPort,
    IPAddress RemoteAddress, int RemotePort,
    int State, int Uid, long Inode);

/// <summary>
/// Parses the kernel's socket tables. All four files share one column layout, so one parser serves TCP and
/// UDP over both address families. Format knowledge lives here rather than in the interop, matching
/// <see cref="ProcMountsParser"/> and <see cref="ProcDiskstatsParser"/>.
///
/// <b>Addresses are hex 32-bit words printed in HOST byte order, not network order.</b> IPv4
/// <c>0100007F</c> is <c>127.0.0.1</c>, not <c>1.0.0.127</c>, and an IPv6 address is four such words.
/// Decoded the obvious way, every address comes out scrambled but plausible-looking, which nothing
/// downstream would catch. Ports sit in the same field but are plain hex and must NOT be swapped.
///
/// Pure and side-effect-free, and never throws: a short or malformed row is skipped, which is also what a
/// torn read of a file that changes under the reader looks like.
/// </summary>
internal static class ProcNetParser {
    // 0-based columns: 0 sl, 1 local_address, 2 rem_address, 3 st, 4 tx_queue:rx_queue, 5 tr:tm->when,
    // 6 retrnsmt, 7 uid, 8 timeout, 9 inode. Later columns (ref count, socket pointer, and the
    // protocol-specific trailer) differ between the four files and are not read.
    private const int LocalAddressField = 1;
    private const int RemoteAddressField = 2;
    private const int StateField = 3;
    private const int UidField = 7;
    private const int InodeField = 9;

    /// <summary>Through the inode column, the last field any of the four files is read for.</summary>
    private const int MinimumFields = 10;

    /// <summary>Hex chars per 32-bit word — and, for IPv4, the whole address.</summary>
    private const int HexCharsPerWord = 8;

    private const int BytesPerWord = 4;
    private const int Ipv4HexLength = 8;
    private const int Ipv6HexLength = 32;

    /// <summary>Parses every well-formed row, in file order. The <c>sl</c> header line survives a column
    /// count (it has twelve fields) and is dropped instead by failing to decode as an address.</summary>
    internal static IReadOnlyList<ProcNetSocket> Parse(IReadOnlyList<string> lines) {
        var sockets = new List<ProcNetSocket>();

        foreach (var line in lines) {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < MinimumFields)
                continue;

            if (!TryParseEndpoint(fields[LocalAddressField], out var local, out var localPort)
                || !TryParseEndpoint(fields[RemoteAddressField], out var remote, out var remotePort))
                continue;

            sockets.Add(new ProcNetSocket(
                local, localPort, remote, remotePort,
                ParseHex(fields[StateField]),
                ParseDecimal(fields[UidField]),
                ParseInode(fields[InodeField])));
        }

        return sockets;
    }

    /// <summary>Splits an <c>ADDRESS:PORT</c> field and decodes both halves.</summary>
    private static bool TryParseEndpoint(string field, out IPAddress address, out int port) {
        address = IPAddress.None;
        port = 0;

        var colon = field.IndexOf(':');
        if (colon <= 0)
            return false;

        if (TryParseAddress(field.AsSpan(0, colon), out address)
            && int.TryParse(
                field.AsSpan(colon + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out port))
            return true;

        address = IPAddress.None;
        port = 0;
        return false;
    }

    /// <summary>
    /// Decodes 8 hex chars as IPv4 or 32 as IPv6. Each 8-char group is one 32-bit word the kernel printed
    /// in host byte order, so on a little-endian machine its bytes come out reversed — <c>0100007F</c> is
    /// the word <c>0x0100007F</c>, whose bytes low-first are <c>7F 00 00 01</c>. <b>The reversal is
    /// per-word</b>: reversing all sixteen bytes of an IPv6 address instead puts a <c>::ffff:</c> mapping
    /// marker at the wrong end and yields a plausible but wrong global address. Every platform this app
    /// targets is little-endian; a big-endian host would need the reversal dropped, not added.
    /// </summary>
    internal static bool TryParseAddress(ReadOnlySpan<char> hex, out IPAddress address) {
        address = IPAddress.None;
        if (hex.Length is not (Ipv4HexLength or Ipv6HexLength))
            return false;

        Span<byte> bytes = stackalloc byte[hex.Length / 2];
        for (var word = 0; word < bytes.Length / BytesPerWord; word++) {
            if (!uint.TryParse(
                    hex.Slice(word * HexCharsPerWord, HexCharsPerWord),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                return false;

            // A little-endian write emits the low byte first, which is the order the address wants.
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(word * BytesPerWord, BytesPerWord), value);
        }

        address = new IPAddress(bytes);
        return true;
    }

    private static int ParseHex(string field) =>
        int.TryParse(field, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>The uid column is decimal, unlike its hex neighbours.</summary>
    private static int ParseDecimal(string field) =>
        int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>Also decimal. 0 is what the kernel writes for a socket with no inode — a TIME_WAIT slot,
    /// which no process owns.</summary>
    private static long ParseInode(string field) =>
        long.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
