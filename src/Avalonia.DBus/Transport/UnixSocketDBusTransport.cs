using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Avalonia.DBus.Transport;

/// <summary>
/// Internal helper that manages the background reader and writer tasks
/// bridging a <see cref="Socket"/> to <see cref="Channel{T}"/> of
/// <see cref="DBusSerializedMessage"/> using D-Bus wire framing.
/// </summary>
internal static class UnixSocketDBusTransport
{
    /// <summary>
    /// Maximum D-Bus message length: 128 MiB (2^27).
    /// </summary>
    private const int MaxMessageLength = 134217728;

    /// <summary>
    /// Starts a background reader task that reads D-Bus messages from the socket
    /// and writes them to the inbound channel.
    /// </summary>
    public static Task StartReaderAsync(
        Socket socket,
        ChannelWriter<DBusSerializedMessage> inboundWriter,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                var headerBuf = new byte[16];

                while (!cancellationToken.IsCancellationRequested)
                {
                    // 1. Read the 16-byte fixed header
                    await ReadExactAsync(socket, headerBuf, cancellationToken).ConfigureAwait(false);

                    // 2. Parse endianness
                    var endianByte = headerBuf[0];
                    bool isLittleEndian;
                    if (endianByte == (byte)'l')
                        isLittleEndian = true;
                    else if (endianByte == (byte)'B')
                        isLittleEndian = false;
                    else
                        throw new InvalidDataException(
                            $"Invalid D-Bus endianness byte: 0x{endianByte:X2}");

                    // 3. Parse body length (bytes 4-7) and header fields array length (bytes 12-15)
                    uint bodyLen;
                    uint headerFieldsLen;

                    if (isLittleEndian)
                    {
                        bodyLen = BinaryPrimitives.ReadUInt32LittleEndian(
                            headerBuf.AsSpan(4, 4));
                        headerFieldsLen = BinaryPrimitives.ReadUInt32LittleEndian(
                            headerBuf.AsSpan(12, 4));
                    }
                    else
                    {
                        bodyLen = BinaryPrimitives.ReadUInt32BigEndian(
                            headerBuf.AsSpan(4, 4));
                        headerFieldsLen = BinaryPrimitives.ReadUInt32BigEndian(
                            headerBuf.AsSpan(12, 4));
                    }

                    // 4. Compute total message length with checked arithmetic
                    var paddedHeaderFieldsLen = AlignUp(headerFieldsLen, 8);
                    long total;
                    try
                    {
                        total = checked(16L + paddedHeaderFieldsLen + bodyLen);
                    }
                    catch (OverflowException ex)
                    {
                        throw new InvalidDataException("D-Bus message length overflow.", ex);
                    }

                    // 5. Validate
                    if (total >= MaxMessageLength)
                        throw new InvalidDataException(
                            $"D-Bus message length {total} exceeds maximum {MaxMessageLength}.");

                    if (total > int.MaxValue)
                    {
                        throw new InvalidDataException(
                            $"D-Bus message length {total} exceeds supported managed buffer limit.");
                    }

                    var totalInt = (int)total;

                    // 6. Allocate full buffer and copy header
                    var messageBytes = new byte[totalInt];
                    Array.Copy(headerBuf, 0, messageBytes, 0, 16);

                    // 7. Read the remaining bytes
                    if (totalInt > 16)
                    {
                        await ReadExactAsync(
                            socket,
                            messageBytes.AsMemory(16, totalInt - 16),
                            cancellationToken).ConfigureAwait(false);
                    }

                    // 8. Write to inbound channel
                    var msg = new DBusSerializedMessage(messageBytes, []);
                    await inboundWriter.WriteAsync(msg, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (IOException)
            {
                // Connection closed or broken
            }
            catch (SocketException)
            {
                // Connection closed or broken
            }
            finally
            {
                inboundWriter.TryComplete();
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Starts a background writer task that reads messages from the outbound channel
    /// and sends them to the socket.
    /// </summary>
    public static Task StartWriterAsync(
        Socket socket,
        ChannelReader<DBusSerializedMessage> outboundReader,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in outboundReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    var data = msg.Message;
                    var totalSent = 0;

                    while (totalSent < data.Length)
                    {
                        var sent = await socket.SendAsync(
                            data.AsMemory(totalSent),
                            SocketFlags.None,
                            cancellationToken).ConfigureAwait(false);

                        if (sent == 0)
                            throw new IOException("Socket send returned 0 bytes.");

                        totalSent += sent;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (ChannelClosedException)
            {
                // Outbound channel completed — normal shutdown
            }
            catch (IOException)
            {
                // Connection closed or broken
            }
            catch (SocketException)
            {
                // Connection closed or broken
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Reads exactly <paramref name="buffer"/>.Length bytes from the socket,
    /// handling partial reads.
    /// </summary>
    internal static async Task ReadExactAsync(
        Socket socket,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await socket.ReceiveAsync(
                buffer[totalRead..],
                SocketFlags.None,
                cancellationToken).ConfigureAwait(false);

            if (read == 0)
                throw new IOException("Connection closed.");

            totalRead += read;
        }
    }

    /// <summary>
    /// Rounds <paramref name="pos"/> up to the next multiple of <paramref name="alignment"/>.
    /// </summary>
    internal static int Padded(int pos, int alignment)
    {
        var remainder = pos % alignment;
        return remainder == 0 ? pos : pos + (alignment - remainder);
    }

    private static long AlignUp(long value, int alignment)
    {
        if (alignment <= 0)
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Alignment must be greater than 0.");

        var remainder = value % alignment;
        return remainder == 0 ? value : checked(value + (alignment - remainder));
    }
}
