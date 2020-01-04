using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using DragonLib;
using DragonLib.IO;
using JetBrains.Annotations;

namespace Cethleann.Ninja
{
    /// <summary>
    ///     Compression helper class for TN's fork of KTGL
    /// </summary>
    [PublicAPI]
    public static class Compression
    {
        /// <summary>
        ///     Decompresses a .gz stream.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="decompressedSize"></param>
        /// <returns></returns>
        public static Span<byte> Decompress(Span<byte> data, int decompressedSize)
        {
            unsafe
            {
                fixed (byte* pin = &data.GetPinnableReference())
                {
                    var decPtr = 0;
                    Span<byte> decompressed = new byte[decompressedSize];
                    using var stream = new UnmanagedMemoryStream(pin, data.Length);
                    while (true)
                    {
                        if (stream.Position == stream.Length) break;

                        var size = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice((int) stream.Position));
                        var chunk = decompressed.Slice(decPtr);
                        stream.Position += 4;
                        var cursor = stream.Position;

                        if (size <= 0x8000)
                        {
                            data.Slice((int) stream.Position, (int) size).CopyTo(chunk);
                            decPtr += (int) size;
                            Logger.Assert(decPtr == decompressedSize, "decPtr == decompressedSize");
                            Logger.Assert(size <= 0xFF, "size <= 0xFF");
                        }
                        else
                        {
                            size -= 0x8000;
                            stream.Position += 2;
                            using var inflate = new DeflateStream(stream, CompressionMode.Decompress, true);
                            decPtr += inflate.Read(chunk);
                        }

                        stream.Position = (cursor + size).Align(0x10);
                    }

                    return decompressed;
                }
            }
        }

        /// <summary>
        ///     Compresses a stream into a .gz stream.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="blockSize"></param>
        /// <returns></returns>
        public static Span<byte> Compress(Span<byte> data, int blockSize = 0x4000)
        {
            var buffer = new Span<byte>(new byte[data.Length]);
            var cursor = 0;
            for (int i = 0; i < data.Length; i += blockSize)
            {
                using var ms = new MemoryStream(blockSize);
                using var deflateStream = new DeflateStream(ms, CompressionLevel.Optimal);

                var block = data.Slice(i, Math.Min(blockSize, data.Length - i));
                deflateStream.Write(block);
                deflateStream.Flush();
                var write = block.Length;
                var compressed = false;
                if (0x100 < block.Length) // special case where the last block is too small to compress properly.
                {
                    write = (int) ms.Position + 2;
                    block = new Span<byte>(new byte[ms.Length]);
                    ms.Position = 0;
                    ms.Read(block);
                    compressed = true;
                }

                var absWrite = write;
                if (compressed) absWrite = write + 0x4000;

                MemoryMarshal.Write(buffer.Slice(cursor), ref absWrite);
                if (compressed)
                {
                    buffer[cursor + 4] = 0x78;
                    buffer[cursor + 5] = 0xDA;
                }

                block.CopyTo(buffer.Slice(cursor + 4 + (compressed ? 2 : 0)));
                cursor = (cursor + write + 4 + (compressed ? 2 : 0)).Align(0x80);
            }

            return buffer.Slice(0, cursor);
        }
    }
}
