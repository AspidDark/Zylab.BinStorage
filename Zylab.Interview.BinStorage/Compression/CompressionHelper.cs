using System;
using System.IO;
using System.IO.Compression;

namespace Zylab.Interview.BinStorage.Compression
{
    public static class CompressionHelper
    {
        public static byte[] Compress(Stream input)
        {
            using (var compressStream = new MemoryStream())
            using (var compressor = new DeflateStream(compressStream, CompressionMode.Compress))
            {
                input.Seek(0, SeekOrigin.Begin);
                input.CopyTo(compressor);
                compressStream.Seek(0, SeekOrigin.Begin);
                compressor.Close();
                return compressStream.ToArray();
            }
        }

        public static Stream Decompress(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using (var bigStream = new DeflateStream(stream, CompressionMode.Decompress))
            using (var bigStreamOut = new MemoryStream())
            {
                bigStream.Seek(0, SeekOrigin.Begin);
                bigStream.CopyTo(bigStreamOut);
                return bigStreamOut;
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            byte[] decompressedArray = null;
            try
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    using (MemoryStream compressStream = new MemoryStream(data))
                    {
                        using (DeflateStream deflateStream = new DeflateStream(compressStream, CompressionMode.Decompress))
                        {
                            deflateStream.CopyTo(decompressedStream);
                        }
                    }
                    decompressedArray = decompressedStream.ToArray();
                }
            }
            catch (Exception e)
            {
               throw new IOException();
            }

            return decompressedArray;
        }
    }
}
