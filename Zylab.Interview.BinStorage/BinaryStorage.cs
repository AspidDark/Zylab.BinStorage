using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.Concurrent;
using Zylab.Interview.BinStorage.Data;
using System.Threading.Tasks;
using Zylab.Interview.BinStorage.Serializers;
using Zylab.Interview.BinStorage.Cache;
using Zylab.Interview.BinStorage.Compression;
using Zylab.Interview.BinStorage.Dto;

namespace Zylab.Interview.BinStorage
{
    public class BinaryStorage : IBinaryStorage
    {
        private static readonly object positionLock = new object();
        private readonly string storageFilePath;
        private static FileStream storageFileStream;

        private readonly string indexFilePath;
        private static FileStream indexFileStream;
        private static readonly object indexLock = new object();

        const int smallObjectHeap = 80000;

        private readonly long maxStorageFile;
        private readonly long maxIndexFile;
        private readonly long compressionThreshold;

        private readonly ConcurrentDictionary<string, object> currentWritingData = new ConcurrentDictionary<string, object>();
        private readonly ICacheingWrapper _cacheingWrapper;

        public BinaryStorage(StorageConfiguration configuration, ICacheingWrapper cacheingWrapper = null)
        {
            _cacheingWrapper = cacheingWrapper ?? new CacheingWrapper();

            const string storageFileName = "storage.bin";

            storageFilePath = Path.Combine(configuration.WorkingFolder, storageFileName);
            storageFileStream = new FileStream(storageFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);

            const string indexFileName = "index.bin";

            indexFilePath = Path.Combine(configuration.WorkingFolder, indexFileName);
            indexFileStream = new FileStream(indexFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);

            maxStorageFile = configuration.MaxStorageFile;
            maxIndexFile = configuration.MaxIndexFile;
            compressionThreshold = configuration.CompressionThreshold;
        }

        public async void Add(string key, Stream data, StreamInfo parameters)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException();
            }

            if (data == null)
            {
                throw new ArgumentNullException();
            }

            if (currentWritingData.ContainsKey(key))
            {
                throw new ArgumentException();
            }

            bool toDecompress = false;
            Stream streamTosave = data;
            byte[] zipedArray = null;
            if (compressionThreshold > 0 && data.Length > compressionThreshold && parameters != null && !parameters.IsCompressed)
            {
                toDecompress = true;
                zipedArray = CompressionHelper.Compress(data);
                streamTosave = new MemoryStream(zipedArray);
            }
            long streamLength = parameters?.Length ?? streamTosave.Length;

            streamTosave.Seek(0, SeekOrigin.Begin);
            MemoryStream memoryStream = new MemoryStream();
            streamTosave.CopyTo(memoryStream);
            var dataArray = memoryStream.ToArray();

            byte[] hashforDataChangeCheck;
            if (toDecompress)
            {
                hashforDataChangeCheck= MD5Hashing.GetMd5HashFromStream(zipedArray);
            }
            else
            {
                hashforDataChangeCheck = MD5Hashing.GetMd5HashFromStream(dataArray);
            }

            if (parameters != null)
            {
                if (parameters.Length != null)
                {
                    streamLength = parameters.Length.Value;
                }

                if (parameters.Hash != null && parameters.Hash.Length > 0)
                {
                    if (MD5Hashing.IsHashesEqual(parameters.Hash, hashforDataChangeCheck))
                    {
                        throw new ArgumentException();
                    }
                }
            }

            if (!currentWritingData.TryAdd(key, null))
            {
                throw new ArgumentException();
            }

            long streamStartingPosition = GetStartingPositionForDataFile(streamLength);

            if (maxStorageFile > 0 && storageFileStream.Length + streamLength > maxStorageFile)
            {
                throw new IOException();
            }

            var savedataTask = await SaveData(dataArray, streamStartingPosition);

            if (savedataTask == SaveResult.SaveError)
            {
                throw new ArgumentException();
            }

            var saveIndex = await SaveIndex(key, hashforDataChangeCheck, streamLength, streamStartingPosition);

            if (saveIndex.SaveResult == SaveResult.SizeExpired)
            {
                await DeleteFromDataStorage(streamStartingPosition, streamLength);
                throw new IOException();
            }

            if (saveIndex.SaveResult == SaveResult.SaveError)
            {
                await DeleteFromDataStorage(streamStartingPosition, streamLength);
                throw new ArgumentException();
            }

            await _cacheingWrapper.TryAdd(key, new StreamCache
            {
                IsCached = false,
                DataStream = null,
                ReadCount = 0,
                Length = saveIndex.Length,
                StartingPosition = saveIndex.StartingPosition,
                ToDecompress = toDecompress
            });

            currentWritingData.TryRemove(key, out _);
        }

        public Stream Get(string key)
        {
            if (!Contains(key))
            {
                throw new KeyNotFoundException();
            }
            if (_cacheingWrapper.TryGetValue(key, out var streamCache))
            {
                _cacheingWrapper.IncrementWiews(key);
                if (streamCache.IsCached)
                {
                    Stream result = new MemoryStream(streamCache.DataStream);
                    return result;
                }
                var stream = GetStreamFromFile(key, streamCache.ToDecompress, streamCache.StartingPosition, streamCache.Length).GetAwaiter().GetResult();

                if (stream == null || !stream.IsOk)
                {
                    throw new IOException();
                }

                _cacheingWrapper.TryAddStreamDataToCache(key, stream.DataStream);

                MemoryStream response = new MemoryStream(stream.DataStream);
                response.Seek(0, SeekOrigin.Begin);

                return response;

            }
            throw new ArgumentException();
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }
            return _cacheingWrapper.ContainsKey(key);
        }

        public void Dispose()
        {
            storageFileStream.Dispose();
            indexFileStream.Dispose();
        }

        private async Task<SaveResult> SaveData(byte[] dataAsArray, long streamStartingPosition)
        {
            Stream data = new MemoryStream(dataAsArray);
            data.Seek(0, SeekOrigin.Begin);
            using (var write = new FileStream(storageFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, smallObjectHeap, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                try
                {
                    write.Seek(streamStartingPosition, SeekOrigin.Begin);
                    await data.CopyToAsync(write);
                    write.Flush(true);
                    return SaveResult.Ok;
                }
                catch
                {
                    return SaveResult.SaveError;
                }
            }
        }

        private async Task<SaveResultWithData> SaveIndex(string key, byte[] hashforDataChangeCheck, long length, long startingPosition)
        {
            IndexDto indexDto = new IndexDto
            {
                Key = key,
                HashforDataChangeCheck = hashforDataChangeCheck,
                Length = length,
                StartingPosition = startingPosition
            };

            var indexBytes = IndexSerializer.Serilalize(indexDto);

            if (maxIndexFile > 0 && indexFileStream.Length + indexBytes.Length > maxIndexFile)
            {
                return new SaveResultWithData { SaveResult = SaveResult.SizeExpired };
            }

            long indexStreamStartPosition = GetStartingPositionForIndexFile(indexBytes.Length);
            Stream indexStream = new MemoryStream(indexBytes);
            indexStream.Seek(0, SeekOrigin.Begin);

            using (var write = new FileStream(indexFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, smallObjectHeap, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                try
                {
                    write.Seek(indexStreamStartPosition, SeekOrigin.Begin);
                    await indexStream.CopyToAsync(write);
                    write.Flush(true);
                    return new SaveResultWithData { SaveResult = SaveResult.Ok, StartingPosition = indexStreamStartPosition, Length = indexStream.Length };
                }
                catch
                {
                    return new SaveResultWithData { SaveResult = SaveResult.SaveError };
                }
            }
        }

        private long GetStartingPositionForDataFile(long currentStreamLength)
        {
            //size check
            long position;
            //semaphoreSlim ??
            lock (positionLock)
            {
                position = storageFileStream.Length;
                storageFileStream.SetLength(position + currentStreamLength);
            }
            return position;
        }

        private long GetStartingPositionForIndexFile(long currentStreamLength)
        {
            //size check
            long position;
            //semaphoreSlim ??
            lock (indexLock)
            {
                position = indexFileStream.Length;
                indexFileStream.SetLength(position + currentStreamLength);
            }
            return position;
        }

        private async Task<bool> DeleteFromDataStorage(long streamStartingPosition, long length)
        {
            byte[] delete = new byte[length];
            using (var write = new FileStream(storageFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, smallObjectHeap, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                try
                {
                    write.Seek(streamStartingPosition - length, SeekOrigin.Begin);
                    await write.WriteAsync(delete, 0, delete.Length);
                    write.Flush(true);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private async Task<ReturnResultWithStream> GetStreamFromFile(string key, bool toDecompress, long startingPosition, long indexLength)
        {
            byte[] indexBuffer;
            try
            {
                indexFileStream.Seek(startingPosition, SeekOrigin.Begin);
                indexBuffer = new byte[indexLength];
                indexFileStream.Read(indexBuffer, 0, (int)indexLength);
            }
            catch
            {
                return new ReturnResultWithStream { IsOk = false };
            }

            var indexDto = IndexSerializer.IndexDeserialize(indexBuffer);
            if (indexDto == null)
            {
                return new ReturnResultWithStream { IsOk = false };
            }
            byte[] buffer;
            try
            {
                storageFileStream.Seek(indexDto.StartingPosition, SeekOrigin.Begin);
                buffer = new byte[indexDto.Length];
                storageFileStream.Read(buffer, 0, (int)indexDto.Length);
            }
            catch
            {
                return new ReturnResultWithStream { IsOk = false };
            }


            var hash = MD5Hashing.GetMd5HashFromStream(buffer);

            if (!MD5Hashing.IsHashesEqual(hash, indexDto.HashforDataChangeCheck))
            {
                return new ReturnResultWithStream { IsOk = false };
            }

            var result = buffer;
            if (toDecompress)
            {
                result = CompressionHelper.Decompress(buffer);
            }
            return new ReturnResultWithStream { DataStream = result, IsOk = true };
        }
    }
}
