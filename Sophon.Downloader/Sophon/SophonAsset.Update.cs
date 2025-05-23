﻿using Hi3Helper.Sophon.Helper;
using Hi3Helper.Sophon.Structs;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
#if !NET6_0_OR_GREATER
using System.Threading.Tasks.Dataflow;
#endif

// ReSharper disable InvalidXmlDocComment
// ReSharper disable PossibleNullReferenceException
// ReSharper disable AccessToDisposedClosure
// ReSharper disable IdentifierTypo
// ReSharper disable ConvertIfStatementToSwitchStatement

namespace Hi3Helper.Sophon
{
    public partial class SophonAsset
    {
        /// <summary>
        ///     Perform an update process to an existing or new file and run each chunk download sequentially.
        /// </summary>
        /// <param name="client">
        ///     The <see cref="HttpClient" /> to be used for downloading process.<br />Ensure that the maximum connection for the
        ///     <see cref="HttpClient" /> has been set to at least (Number of Threads/CPU core * 25%) or == Number of Threads/CPU
        ///     core
        /// </param>
        /// <param name="oldInputDir">
        ///     The directory of the old input file.
        /// </param>
        /// <param name="newOutputDir">
        ///     The directory of the new output file to be written.
        /// </param>
        /// <param name="chunkDir">
        ///     The directory of the staged chunk.
        /// </param>
        /// <param name="removeChunkAfterApply">
        ///     Remove chunk file after applying update
        /// </param>
        /// <param name="writeInfoDelegate">
        ///     <inheritdoc cref="DelegateWriteStreamInfo" />
        /// </param>
        /// <param name="downloadInfoDelegate">
        ///     <inheritdoc cref="DelegateWriteDownloadInfo" />
        /// </param>
        /// <param name="downloadCompleteDelegate">
        ///     <inheritdoc cref="DelegateDownloadAssetComplete" />
        /// </param>
        /// <param name="token">
        ///     Cancellation token for handling cancellation while the routine is running.
        /// </param>
        public async
        #if NET6_0_OR_GREATER
            ValueTask
        #else
            Task
        #endif
            WriteUpdateAsync(HttpClient                    client,
                             string                        oldInputDir,
                             string                        newOutputDir,
                             string                        chunkDir,
                             bool                          removeChunkAfterApply    = false,
                             DelegateWriteStreamInfo       writeInfoDelegate        = null,
                             DelegateWriteDownloadInfo     downloadInfoDelegate     = null,
                             DelegateDownloadAssetComplete downloadCompleteDelegate = null,
                             CancellationToken             token                    = default)
        {
            const string tempExt = "_tempUpdate";

            this.EnsureOrThrowChunksState();
            this.EnsureOrThrowOutputDirectoryExistence(oldInputDir);
            this.EnsureOrThrowOutputDirectoryExistence(newOutputDir);
            this.EnsureOrThrowOutputDirectoryExistence(chunkDir);

            string outputOldPath     = Path.Combine(oldInputDir,  AssetName);
            string outputNewPath     = Path.Combine(newOutputDir, AssetName);
            string outputNewTempPath = outputNewPath + tempExt;
            string outputNewDir      = Path.GetDirectoryName(outputNewPath);

            if (!Directory.Exists(outputNewDir) && outputNewDir != null)
            {
                Directory.CreateDirectory(outputNewDir);
            }

            // Assign path to FileInfo and try to unassign readonly attribute from existing new file info
            FileInfo outputOldFileInfo     = new FileInfo(outputOldPath).UnassignReadOnlyFromFileInfo();
            FileInfo outputNewFileInfo     = new FileInfo(outputNewPath).UnassignReadOnlyFromFileInfo();
            FileInfo outputNewTempFileInfo = new FileInfo(outputNewTempPath).UnassignReadOnlyFromFileInfo();

            foreach (SophonChunk chunk in Chunks)
            {
                await InnerWriteUpdateAsync(client,                chunkDir, writeInfoDelegate, downloadInfoDelegate,
                                            DownloadSpeedLimiter,  outputOldFileInfo,
                                            outputNewTempFileInfo, chunk, removeChunkAfterApply, token);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (outputNewTempFileInfo.FullName != outputNewFileInfo.FullName)
            {
#if NET6_0_OR_GREATER
                // no move file cringe
                outputNewTempFileInfo.Refresh();
#else
                outputNewFileInfo.Delete();
                outputNewTempFileInfo.MoveTo(outputNewFileInfo.FullName);
#endif
            }

        #if DEBUG
            this.PushLogInfo($"Asset: {AssetName} | (Hash: {AssetHash} -> {AssetSize} bytes) has been completely downloaded!");
        #endif
            downloadCompleteDelegate?.Invoke(this);
        }

        /// <summary>
        ///     Perform an update process to an existing or new file and run each chunk download in parallel instead of
        ///     sequentially.
        /// </summary>
        /// <param name="client">
        ///     The <see cref="HttpClient" /> to be used for downloading process.<br />Ensure that the maximum connection for the
        ///     <see cref="HttpClient" /> has been set to at least (Number of Threads/CPU core * 25%) or == Number of Threads/CPU
        ///     core
        /// </param>
        /// <param name="oldInputDir">
        ///     The directory of the old input file.
        /// </param>
        /// <param name="newOutputDir">
        ///     The directory of the new output file to be written.
        /// </param>
        /// <param name="chunkDir">
        ///     The directory of the staged chunk.
        /// </param>
        /// <param name="removeChunkAfterApply">
        ///     Remove chunk file after applying update
        /// </param>
        /// <param name="parallelOptions">
        ///     Parallelization settings to be used for downloading chunks and data hashing.
        ///     Remember that while using this method, the <seealso cref="CancellationToken" /> needs to be passed with
        ///     <c>CancellationToken</c> property.<br />
        ///     If it's being set to <c>null</c>, a default setting will be used as below:
        ///     <code>
        ///     CancellationToken = <paramref name="token" />,
        ///     MaxDegreeOfParallelism = [Number of CPU threads/cores available]
        ///     </code>
        /// </param>
        /// <param name="writeInfoDelegate">
        ///     <inheritdoc cref="DelegateWriteStreamInfo" />
        /// </param>
        /// <param name="downloadInfoDelegate">
        ///     <inheritdoc cref="DelegateWriteDownloadInfo" />
        /// </param>
        /// <param name="downloadCompleteDelegate">
        ///     <inheritdoc cref="DelegateDownloadAssetComplete" />
        /// </param>
        public async
        #if NET6_0_OR_GREATER
            ValueTask
        #else
            Task
        #endif
            WriteUpdateAsync(HttpClient                    client,
                             string                        oldInputDir,
                             string                        newOutputDir,
                             string                        chunkDir,
                             bool                          removeChunkAfterApply    = false,
                             ParallelOptions               parallelOptions          = null,
                             DelegateWriteStreamInfo       writeInfoDelegate        = null,
                             DelegateWriteDownloadInfo     downloadInfoDelegate     = null,
                             DelegateDownloadAssetComplete downloadCompleteDelegate = null)
        {
            const string tempExt = "_tempUpdate";

            this.EnsureOrThrowChunksState();
            this.EnsureOrThrowOutputDirectoryExistence(oldInputDir);
            this.EnsureOrThrowOutputDirectoryExistence(newOutputDir);
            this.EnsureOrThrowOutputDirectoryExistence(chunkDir);

            string outputOldPath     = Path.Combine(oldInputDir,  AssetName);
            string outputNewPath     = Path.Combine(newOutputDir, AssetName);
            string outputNewTempPath = outputNewPath + tempExt;
            string outputNewDir      = Path.GetDirectoryName(outputNewPath);

            if (!Directory.Exists(outputNewDir) && outputNewDir != null)
            {
                Directory.CreateDirectory(outputNewDir);
            }

            if (parallelOptions == null)
            {
                int maxChunksTask = Math.Min(8, Environment.ProcessorCount);
                parallelOptions = new ParallelOptions
                {
                    CancellationToken      = default,
                    MaxDegreeOfParallelism = maxChunksTask
                };
            }

            FileInfo outputOldFileInfo     = new FileInfo(outputOldPath).UnassignReadOnlyFromFileInfo();
            FileInfo outputNewFileInfo     = new FileInfo(outputNewPath).UnassignReadOnlyFromFileInfo();
            FileInfo outputNewTempFileInfo = new FileInfo(outputNewTempPath).UnassignReadOnlyFromFileInfo();
            if (outputNewFileInfo.Exists && outputNewFileInfo.Length == AssetSize)
            {
                outputNewTempFileInfo = outputNewFileInfo;
            }

        #if !NET6_0_OR_GREATER
            using (CancellationTokenSource actionToken = new CancellationTokenSource())
            {
                using (CancellationTokenSource linkedToken = CancellationTokenSource
                          .CreateLinkedTokenSource(actionToken.Token, parallelOptions.CancellationToken))
                {
                    ActionBlock<SophonChunk> actionBlock = new ActionBlock<SophonChunk>(
                     async chunk =>
                     {
                         await InnerWriteUpdateAsync(client, chunkDir, writeInfoDelegate, downloadInfoDelegate,
                                                     DownloadSpeedLimiter, outputOldFileInfo, outputNewTempFileInfo,
                                                     chunk, removeChunkAfterApply,
                                                     linkedToken.Token);
                     },
                     new ExecutionDataflowBlockOptions
                     {
                         MaxDegreeOfParallelism = parallelOptions.MaxDegreeOfParallelism,
                         CancellationToken = linkedToken.Token
                     });

                    foreach (SophonChunk chunk in Chunks)
                    {
                        await actionBlock.SendAsync(chunk, linkedToken.Token);
                    }

                    actionBlock.Complete();
                    await actionBlock.Completion;
                }
            }
        #else
            await Parallel.ForEachAsync(Chunks, parallelOptions,
                                        async (chunk, threadToken) =>
                                        {
                                            await InnerWriteUpdateAsync(client, chunkDir, writeInfoDelegate,
                                                                        downloadInfoDelegate,
                                                                        DownloadSpeedLimiter, outputOldFileInfo,
                                                                        outputNewTempFileInfo,
                                                                        chunk, removeChunkAfterApply,
                                                                        threadToken);
                                        });
        #endif

            // Refresh temp and current file info
            outputNewTempFileInfo.Refresh();
            outputNewFileInfo.Refresh();

            if (outputNewTempFileInfo.FullName != outputNewFileInfo.FullName && outputNewTempFileInfo.Exists)
            {
                if (outputNewFileInfo.Directory is { Exists: false })
                {
                    // Always create directory, no matter when the directory actually exist or not
                    outputNewFileInfo.Directory.Create();
                }

                // Refresh temp and current file info
                outputNewTempFileInfo.Refresh();
                outputNewFileInfo.Refresh();

                // Trust issue: Double check using .NET's File.Exists() to ensure the file is actually exist due to
                // possible multiple file access
                if (File.Exists(outputNewTempFileInfo.FullName))
                {
                #if NET6_0_OR_GREATER
                    outputNewTempFileInfo.MoveTo(outputNewFileInfo.FullName, true);
                #else
                    if (outputNewFileInfo.Exists)
                        outputNewFileInfo.Delete();

                    outputNewTempFileInfo.MoveTo(outputNewFileInfo.FullName);
                #endif
                }
            }

#if DEBUG
            this.PushLogInfo($"Asset: {AssetName} | (Hash: {AssetHash} -> {AssetSize} bytes) has been completely downloaded!");
        #endif
            downloadCompleteDelegate?.Invoke(this);
        }

        private async Task InnerWriteUpdateAsync(HttpClient                 client,
                                                 string                     chunkDir,
                                                 DelegateWriteStreamInfo    writeInfoDelegate,
                                                 DelegateWriteDownloadInfo  downloadInfoDelegate,
                                                 SophonDownloadSpeedLimiter downloadSpeedLimiter,
                                                 FileInfo                   outputOldFileInfo,
                                                 FileInfo                   outputNewFileInfo,
                                                 SophonChunk                chunk,
                                                 bool                       removeChunkAfterApply,
                                                 CancellationToken          token)
        {
            Stream           inputStream  = null;
            Stream           outputStream = null;
            SourceStreamType streamType   = SourceStreamType.Internet;

            try
            {
                bool isUseOldFile = chunk.ChunkOldOffset != -1 &&
                                    outputOldFileInfo.Exists &&
                                    outputOldFileInfo.Length >= chunk.ChunkOldOffset + chunk.ChunkSizeDecompressed;

                if (isUseOldFile)
                {
                    inputStream = outputOldFileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                    streamType  = SourceStreamType.OldReference;
                #if DEBUG
                    this.PushLogDebug($"Using old file as reference at offset: 0x{chunk.ChunkOldOffset:x8} -> 0x{chunk.ChunkSizeDecompressed:x8} for: {AssetName}");
                #endif
                }
                else
                {
                    string   cachedChunkName            = chunk.GetChunkStagingFilenameHash(this);
                    string   cachedChunkPath            = Path.Combine(chunkDir, cachedChunkName);
                    string   cachedChunkFileCheckedPath = cachedChunkPath + ".verified";
                    FileInfo cachedChunkInfo            = new FileInfo(cachedChunkPath).UnassignReadOnlyFromFileInfo();
                    if (cachedChunkInfo.Exists && cachedChunkInfo.Length != chunk.ChunkSize)
                    {
                        cachedChunkInfo.Delete();
                    #if DEBUG
                        this.PushLogDebug($"Cached/preloaded chunk has invalid size for: {AssetName}. Expecting: 0x{chunk.ChunkSize:x8} but get: 0x{cachedChunkInfo.Length:x8} instead. Fallback to download it instead!");
                    #endif
                    }
                    else if (cachedChunkInfo.Exists)
                    {
                        inputStream = new FileStream(cachedChunkInfo.FullName, FileMode.Open, FileAccess.Read,
                                                     FileShare.Read, 4 << 10,
                                                     removeChunkAfterApply
                                                         ? FileOptions.DeleteOnClose
                                                         : FileOptions.None);
                        streamType = SourceStreamType.CachedLocal;
                        if (File.Exists(cachedChunkFileCheckedPath))
                        {
                            File.Delete(cachedChunkFileCheckedPath);
                        }

                    #if DEBUG
                        this.PushLogDebug($"Using cached/preloaded chunk as reference at offset: 0x{chunk.ChunkOffset:x8} -> 0x{chunk.ChunkSizeDecompressed:x8} for: {AssetName}");
                    #endif
                    }
                }

                outputStream = outputNewFileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                await PerformWriteStreamThreadAsync(client, inputStream, streamType, outputStream, chunk, token,
                                                    writeInfoDelegate, downloadInfoDelegate, downloadSpeedLimiter);
                outputStream.Dispose();
            }
            finally
            {
            #if NET6_0_OR_GREATER
                if (inputStream != null)
                {
                    await inputStream.DisposeAsync();
                }

                if (outputStream != null)
                {
                    await outputStream.DisposeAsync();
                }
            #else
                inputStream?.Dispose();
                outputStream?.Dispose();
            #endif
            }
        }

        /// <summary>
        ///     Get the total size of the downloaded preload chunks.
        /// </summary>
        /// <param name="chunkDir">Directory of where the chunks are located</param>
        /// <param name="chunkDir">Directory of where the output assets are located</param>
        /// <param name="useCompressedSize">
        ///     If set true, it will return compressed size of the chunk. Set false to return the
        ///     decompressed size of the chunk.
        /// </param>
        /// <param name="token">Cancellation token context</param>
        /// <returns>The size of downloaded chunks for preload</returns>
        public async ValueTask<long> GetDownloadedPreloadSize(string chunkDir, string outputDir, bool useCompressedSize,
                                                              CancellationToken token = default)
        {
            // Check if the asset path has been completely downloaded, then return 0
            string   assetFullPath       = Path.Combine(outputDir, AssetName);
            FileInfo assetFileInfo       = new FileInfo(assetFullPath).UnassignReadOnlyFromFileInfo();
            bool     isAssetExist        = assetFileInfo.Exists;
            long     assetDownloadedSize = isAssetExist ? assetFileInfo.Length : 0L;

            // Selector to get the size of the downloaded chunks.
            long GetLength(SophonChunk chunk)
            {
                string   cachedChunkName   = chunk.GetChunkStagingFilenameHash(this);
                string   cachedChunkPath   = Path.Combine(chunkDir, cachedChunkName);
                FileInfo cachedChunkInfo   = new FileInfo(cachedChunkPath).UnassignReadOnlyFromFileInfo();
                long     chunkSizeToReturn = useCompressedSize ? chunk.ChunkSize : chunk.ChunkSizeDecompressed;

                // If the asset is fully downloaded, return the chunkSize.
                if (isAssetExist && assetDownloadedSize == AssetSize && !cachedChunkInfo.Exists)
                {
                    return 0L;
                }

                return cachedChunkInfo.Exists && cachedChunkInfo.Length <= chunk.ChunkSize ? chunkSizeToReturn : 0L;
            }

            // If the chunk array is null or empty, return 0
            if (Chunks == null || Chunks.Length == 0)
            {
                return 0L;
            }

            // Use iteration sum if chunk item is less than 512
            if (Chunks.Length < 512)
            {
                return Chunks.Select(GetLength).Sum();
            }

            // Otherwise, use SIMD way
            long[] chunkBuffers = ArrayPool<long>.Shared.Rent(Chunks.Length);
            try
            {
                // Select length of the file using parallel
                await Task.Run(
                               () => Parallel.For(0, Chunks.Length, i => chunkBuffers[i] = GetLength(Chunks[i])),
                               token);

                // Return it using .NET's SIMD Sum() for Array iteration
                return chunkBuffers.Sum();
            }
            finally
            {
                ArrayPool<long>.Shared.Return(chunkBuffers);
            }
        }
    }
}