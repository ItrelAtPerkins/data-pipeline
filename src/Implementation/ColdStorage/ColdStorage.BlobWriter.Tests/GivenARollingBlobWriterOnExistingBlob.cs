﻿namespace Microsoft.Practices.DataPipeline.ColdStorage.BlobWriter.Tests
{
    using System;
    using System.Configuration;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Practices.DataPipeline.ColdStorage.Instrumentation;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Moq;
    using Xunit;

    public class GivenARollingBlobWriterOnExistingBlob : IDisposable
    {
        private const string Prefix = "prefix/";
        private const int BlocksAllowed = 6;
        private const int BlockSize = 1024 * 1024;

        private CloudStorageAccount _storageAccount;
        private string _containerName;
        private string _existingBlobContents;

        private RollingBlobWriter _sut;

        public GivenARollingBlobWriterOnExistingBlob()
        {
            _storageAccount = TestUtilities.GetStorageAccount();
            _containerName = "test" + Guid.NewGuid().ToString("N").ToLowerInvariant();

            GetContainer().Create();

            var existingBlobWriter =
                new RollingBlobWriter(
                    Mock.Of<IBlobNamingStrategy>(ns => ns.GetNamePrefix() == Prefix),
                    Mock.Of<IColdStorageInstrumentationPublisher>(),
                    _storageAccount,
                    _containerName,
                    rollSizeMb: 1,
                    blocksAllowed: BlocksAllowed,
                    blockSize: BlockSize);

            _existingBlobContents = new string('z', 300);

            existingBlobWriter.WriteAsync(
                new[] 
                {
                    TestUtilities.CreateBlockData(_existingBlobContents, BlockSize)
                },
                CancellationToken.None).Wait();

            _sut =
                new RollingBlobWriter(
                    Mock.Of<IBlobNamingStrategy>(ns => ns.GetNamePrefix() == Prefix),
                    Mock.Of<IColdStorageInstrumentationPublisher>(),
                    _storageAccount,
                    _containerName,
                    rollSizeMb: 1,
                    blocksAllowed: BlocksAllowed,
                    blockSize: BlockSize);
        }

        [Fact]
        [Trait("Running time", "Long")]
        [Trait("Kind", "Integration")]
        public async Task WhenWritingSingleBlock_ThenBlockIsAppendedToExistingBlob()
        {
            var payloadString = new string('a', 50);

            await _sut.WriteAsync(
                new[] 
                {
                    TestUtilities.CreateBlockData(payloadString, BlockSize)
                },
                CancellationToken.None);

            Assert.Equal(
                _existingBlobContents + payloadString,
                await GetContainer()
                        .GetBlockBlobReference(Prefix + "0")
                        .DownloadTextAsync(Encoding.UTF8, AccessCondition.GenerateEmptyCondition(), null, null));
        }

        public void Dispose()
        {
            GetContainer().DeleteIfExists();
        }

        private CloudBlobContainer GetContainer()
        {
            var client = _storageAccount.CreateCloudBlobClient();
            return client.GetContainerReference(_containerName);
        }
    }
}