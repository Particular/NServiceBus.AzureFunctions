namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System;
using System.Buffers.Binary;

/// <summary>
/// Provides helper methods for working with <see cref="Guid"/>.
/// </summary>
/// <remarks>
/// Inspired by <a href="https://github.com/bgrainger/NGuid">NGuid</a> by Bradley Grainger,
/// used under the <a href="https://github.com/bgrainger/NGuid/blob/master/LICENSE">MIT License</a>.
/// </remarks>
static class GuidHelper
{
    /// <summary>
    /// Creates a Version 8 UUID with a v7-style layout: <paramref name="timestamp"/> as Unix
    /// milliseconds in bytes 0–7 (time-sortable, stable across redeliveries) and
    /// <paramref name="sequenceNumber"/> in bytes 8–15, both big-endian.
    /// </summary>
    public static Guid CreateVersion8(DateTimeOffset timestamp, long sequenceNumber)
    {
        Span<byte> guidBytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt64BigEndian(guidBytes, timestamp.ToUnixTimeMilliseconds());
        BinaryPrimitives.WriteInt64BigEndian(guidBytes[8..], sequenceNumber);
        guidBytes[6] = (byte)(0x80 | (guidBytes[6] & 0xF));
        guidBytes[8] = (byte)(0x80 | (guidBytes[8] & 0x3F));
        return new Guid(guidBytes, bigEndian: true);
    }
}
