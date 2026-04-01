using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;

namespace ContosoUniversity.Services
{
    /// <summary>
    /// Service for managing file operations with Azure Blob Storage
    /// </summary>
    public class AzureBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _blobEndpoint;

        public AzureBlobStorageService(string blobEndpoint)
        {
            _blobEndpoint = blobEndpoint ?? throw new ArgumentNullException(nameof(blobEndpoint));
            
            // Initialize BlobServiceClient using DefaultAzureCredential for Managed Identity
            _blobServiceClient = new BlobServiceClient(
                new Uri(_blobEndpoint),
                new DefaultAzureCredential());
        }

        /// <summary>
        /// Gets or creates a blob container
        /// </summary>
        public async Task<BlobContainerClient> GetOrCreateContainerAsync(string containerName)
        {
            try
            {
                var container = _blobServiceClient.GetBlobContainerClient(containerName);
                
                // Create container if it doesn't exist
                await container.CreateIfNotExistsAsync();
                
                return container;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting or creating container '{containerName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Uploads a file to Azure Blob Storage
        /// </summary>
        public async Task<string> UploadBlobAsync(string containerName, string blobName, Stream content, bool overwrite = true)
        {
            try
            {
                var container = await GetOrCreateContainerAsync(containerName);
                var blobClient = container.GetBlobClient(blobName);
                
                // Upload the file
                await blobClient.UploadAsync(content, overwrite: overwrite);
                
                // Return the blob URI
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error uploading blob '{blobName}' to container '{containerName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Downloads a file from Azure Blob Storage
        /// </summary>
        public async Task<BinaryData> DownloadBlobAsync(string containerName, string blobName)
        {
            try
            {
                var container = await GetOrCreateContainerAsync(containerName);
                var blobClient = container.GetBlobClient(blobName);
                
                // Check if blob exists
                if (!await blobClient.ExistsAsync())
                {
                    return null;
                }
                
                // Download the file content
                BlobDownloadInfo download = await blobClient.DownloadAsync();
                return await BinaryData.FromStreamAsync(download.Content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading blob '{blobName}' from container '{containerName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a blob from Azure Blob Storage
        /// </summary>
        public async Task<bool> DeleteBlobAsync(string containerName, string blobName)
        {
            try
            {
                var container = await GetOrCreateContainerAsync(containerName);
                var blobClient = container.GetBlobClient(blobName);
                
                // Delete the blob if it exists
                var result = await blobClient.DeleteIfExistsAsync();
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting blob '{blobName}' from container '{containerName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Checks if a blob exists
        /// </summary>
        public async Task<bool> BlobExistsAsync(string containerName, string blobName)
        {
            try
            {
                var container = await GetOrCreateContainerAsync(containerName);
                var blobClient = container.GetBlobClient(blobName);
                
                return await blobClient.ExistsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking if blob '{blobName}' exists in container '{containerName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the storage account name
        /// </summary>
        public string AccountName => _blobServiceClient.AccountName;
    }
}
