using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Zylab.Interview.BinStorage
{
    public static class MD5Hashing
    {
        public static byte[] GetMd5HashFromStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using (var md5Hash = MD5.Create())
            {
                var hashBytes = md5Hash.ComputeHash(stream);

                return hashBytes;
            }
        }

        public static byte[] GetMd5HashFromStream(byte[] stremAsArray)
        {
            Stream stream = new MemoryStream(stremAsArray);
            stream.Seek(0, SeekOrigin.Begin);
            using (var md5Hash = MD5.Create())
            {
                var hashBytes = md5Hash.ComputeHash(stream);

                return hashBytes;
            }
        }

        public static bool IsHashesEqual(byte[] hash1, byte[] hash2)
            => hash1.SequenceEqual(hash2);
    }
}
