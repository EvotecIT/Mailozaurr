using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading;

namespace Mailozaurr;

public partial class Graph {
    /// <summary>
    /// Creates the metadata and content placeholders required for uploading a file attachment.
    /// </summary>
    /// <param name="attachmentPath">Path to the attachment file.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="preloadContent">
    /// When true, loads file chunks into memory and populates <see cref="GraphAttachmentPlaceHolder.Content"/>.
    /// When false, only metadata is prepared and chunk content is generated on demand.
    /// </param>
    /// <returns>The placeholder representing the attachment.</returns>
    public Task<GraphAttachmentPlaceHolder> CreateGraphAttachment(string attachmentPath, CancellationToken cancellationToken = default, bool preloadContent = true) {
        return CreateGraphAttachment(
            new GraphFileAttachmentSource(attachmentPath, descriptor: null),
            cancellationToken,
            preloadContent);
    }

    private Task<GraphAttachmentPlaceHolder> CreateGraphAttachment(
        GraphFileAttachmentSource source,
        CancellationToken cancellationToken = default,
        bool preloadContent = true) {
        cancellationToken.ThrowIfCancellationRequested();
        string attachmentPath = source.Path;
        if (!File.Exists(attachmentPath)) {
            LogMissingAttachmentWarning(attachmentPath);
            throw new FileNotFoundException($"Send-EmailMessage - Attachment file not found: {attachmentPath}", attachmentPath);
        }
        var fileName = string.IsNullOrWhiteSpace(source.Descriptor?.FileName)
            ? Path.GetFileName(attachmentPath)
            : source.Descriptor!.FileName!;
        var fileSize = new FileInfo(attachmentPath).Length;
        var isInline = source.Descriptor != null && IsInlineDescriptor(source.Descriptor);

        var attachmentItem = new GraphAttachmentItem("file", fileName, fileSize) {
            ContentType = string.IsNullOrWhiteSpace(source.Descriptor?.ContentType)
                ? null
                : source.Descriptor!.ContentType,
            IsInline = isInline ? true : null,
            ContentId = string.IsNullOrWhiteSpace(source.Descriptor?.ContentId)
                ? (isInline ? fileName : null)
                : source.Descriptor!.ContentId
        };

        var attachmentItemWrapper = new GraphAttachmentItemWrapper(attachmentItem);
        var attachmentItemJson = JsonSerializer.Serialize(attachmentItemWrapper, MailozaurrJsonContext.Default.GraphAttachmentItemWrapper);
        var directAttachmentJson = string.Empty;
        if (fileSize < MinimumUploadSessionAttachmentSize) {
            var directAttachment = source.Descriptor == null
                ? GraphAttachment.FromFile(attachmentPath)
                : GraphAttachment.FromDescriptor(source.Descriptor);
            directAttachmentJson = JsonSerializer.Serialize(directAttachment, MailozaurrJsonContext.Default.GraphAttachment);
        }

        List<StreamContent> content = preloadContent && fileSize >= MinimumUploadSessionAttachmentSize
            ? PrepareByteArrayContentForUpload(attachmentPath, ChunkSize, cancellationToken)
            : new List<StreamContent>();

        var placeholder = new GraphAttachmentPlaceHolder {
            Json = attachmentItemJson,
            Content = content,
            FilePath = attachmentPath,
            FileSize = fileSize,
            FileName = fileName,
            DirectAttachmentJson = directAttachmentJson
        };

        return Task.FromResult(placeholder);
    }

    /// <summary>
    /// Creates an upload session for a large attachment.
    /// </summary>
    /// <param name="draftMessage">The draft message the attachment belongs to.</param>
    /// <param name="attachmentItemJson">The serialized attachment item.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The upload session URL.</returns>
    public async Task<string> CreateUploadSession(GraphMessage draftMessage, string attachmentItemJson, CancellationToken cancellationToken = default) {
        var uploadSessionUrl = GraphDraftMessageUris.CreateUploadSession(SentFrom, draftMessage.Id!);
        using var request = new HttpRequestMessage(HttpMethod.Post, uploadSessionUrl) {
            Content = new StringContent(attachmentItemJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TokenType, AccessToken);
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        HttpResponseMessage uploadSessionResponse;
        try {
            uploadSessionResponse = await _client.SendAsync(request, cancellationToken);
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }

        using (uploadSessionResponse) {
            var uploadSessionContent = await uploadSessionResponse.Content.ReadAsStringAsync();
            if (!uploadSessionResponse.IsSuccessStatusCode) {
                GraphApiError? error = null;
                try {
                    error = JsonSerializer.Deserialize(uploadSessionContent, MailozaurrJsonContext.Default.GraphApiError);
                } catch (JsonException) {
                    // Non-JSON error response; fall back to raw content.
                }
                var errorMessage = (error == null || error.Error == null)
                    ? $"Unknown error: {uploadSessionContent}"
                    : $"Error code: {error.Error.Code}, message: {error.Error.Message}";
                var retryAfter = ParseRetryAfter(uploadSessionResponse);
                throw new GraphApiException(uploadSessionResponse.StatusCode, errorMessage, uploadSessionContent, retryAfter);
            }

            return ParseUploadSessionResult(uploadSessionContent);
        }
    }

    private async Task AddDirectAttachmentAsync(GraphMessage draftMessage, string attachmentJson, CancellationToken cancellationToken) {
        var attachmentUrl = GraphDraftMessageUris.Attachments(SentFrom, draftMessage.Id!);
        using var request = new HttpRequestMessage(HttpMethod.Post, attachmentUrl) {
            Content = new StringContent(attachmentJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TokenType, AccessToken);
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        HttpResponseMessage response;
        try {
            response = await _client.SendAsync(request, cancellationToken);
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }

        using (response) {
            if (response.IsSuccessStatusCode) {
                return;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            GraphApiError? error = null;
            try {
                error = JsonSerializer.Deserialize(responseContent, MailozaurrJsonContext.Default.GraphApiError);
            } catch (JsonException) {
                // Non-JSON error response; fall back to raw content.
            }
            var errorMessage = error?.Error == null
                ? $"Unknown error: {responseContent}"
                : $"Error code: {error.Error.Code}, message: {error.Error.Message}";
            throw new GraphApiException(response.StatusCode, errorMessage, responseContent, ParseRetryAfter(response));
        }
    }

    private static string ParseUploadSessionResult(string uploadSessionContent) {
        var uploadSessionResult = JsonSerializer.Deserialize(uploadSessionContent, MailozaurrJsonContext.Default.GraphUploadSessionResult)
            ?? throw new InvalidOperationException("Failed to deserialize the upload session response.");

        if (string.IsNullOrEmpty(uploadSessionResult.UploadUrl)) {
            throw new InvalidOperationException("Upload URL not found in the session response.");
        }

        return uploadSessionResult.UploadUrl;
    }

    /// <summary>
    /// Splits <paramref name="filePath"/> into chunks no larger than <see cref="MaxChunkSize"/>.
    /// </summary>
    /// <param name="filePath">Path to the file to split.</param>
    /// <param name="chunkSize">Desired size of each chunk in bytes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>List of stream contents representing file chunks.</returns>
    private List<StreamContent> PrepareByteArrayContentForUpload(string filePath, int chunkSize = MaxChunkSize, CancellationToken cancellationToken = default) {
        chunkSize = Math.Min(chunkSize, MaxChunkSize);
        var fileContents = new List<StreamContent>();
        var fileSize = new FileInfo(filePath).Length;

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        int bytesRead;
        long offset = 0;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            while ((bytesRead = fileStream.Read(buffer, 0, chunkSize)) > 0) {
                cancellationToken.ThrowIfCancellationRequested();

                var chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                var memoryStream = new MemoryStream(chunk, writable: false);
                var contentRange = $"bytes {offset}-{offset + bytesRead - 1}/{fileSize}";
                var streamContent = new StreamContent(memoryStream);
                streamContent.Headers.Add("Content-Range", contentRange);
                fileContents.Add(streamContent);
                offset += bytesRead;
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        return fileContents;
    }

    /// <summary>
    /// Uploads all attachments for the specified draft message.
    /// </summary>
    /// <param name="draftMessage">The draft message to attach the files to.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task UploadAttachmentsAsync(GraphMessage draftMessage, CancellationToken cancellationToken = default) {
        foreach (var source in EnumerateFileAttachmentSources()) {
            try {
                await UploadAttachmentWithRetryAsync(draftMessage, source, cancellationToken);
            } catch (FileNotFoundException) {
                // Already logged by CreateGraphAttachment.
            }
        }
    }

    /// <summary>
    /// Prepares attachments for upload by creating placeholders.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task PrepareAttachments(CancellationToken cancellationToken = default) {
        foreach (var source in EnumerateFileAttachmentSources()) {
            try {
                var attachmentItemJson = await CreateGraphAttachment(source, cancellationToken);
                AttachmentsPlaceHolders.Add(attachmentItemJson);
            } catch (FileNotFoundException) {
                // Already logged by CreateGraphAttachment.
            }
        }
    }

    /// <summary>
    /// Uploads all chunks of a file to the provided upload session URL.
    /// </summary>
    /// <param name="uploadUrl">The upload session URL.</param>
    /// <param name="fileChunks">The file chunks to upload.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task SendFileChunks(string uploadUrl, IEnumerable<StreamContent> fileChunks, CancellationToken cancellationToken = default) {
        foreach (var chunk in fileChunks) {
            await SendAttachmentChunk(uploadUrl, chunk, cancellationToken);
        }
    }

    /// <summary>
    /// Uploads all chunks of a file to the provided upload session URL without buffering the entire file.
    /// </summary>
    public async Task SendFileChunks(string uploadUrl, string filePath, long fileSize, CancellationToken cancellationToken = default) {
        var chunkSize = Math.Min(ChunkSize, MaxChunkSize);
        var buffer = new byte[chunkSize];
        long offset = 0;
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        int bytesRead;
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
            var chunk = new byte[bytesRead];
            Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
            await SendAttachmentChunkWithRetryAsync(uploadUrl, chunk, offset, fileSize, cancellationToken);
            offset += bytesRead;
        }
    }

    /// <summary>
    /// Uploads a single attachment chunk to the Graph API.
    /// </summary>
    /// <param name="uploadUrl">The upload session URL.</param>
    /// <param name="byteArrayContent">The chunk to send.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task SendAttachmentChunk(string uploadUrl, StreamContent byteArrayContent, CancellationToken cancellationToken = default) {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, uploadUrl) {
            Content = byteArrayContent
        };
        requestMessage.Headers.Add("AnchorMailbox", SentFrom);
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        try {
            using var uploadChunkResponse = await _client.SendAsync(requestMessage, cancellationToken);
            if (!uploadChunkResponse.IsSuccessStatusCode) {
                LogCollector.LogWarning(uploadChunkResponse.ToString());
            }
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }
    }

    private async Task SendAttachmentChunkWithRetryAsync(string uploadUrl, byte[] chunk, long offset, long fileSize, CancellationToken cancellationToken) {
        var policy = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        int attempts = 0;
        Exception? lastException = null;
        var maxRetries = policy?.MaxRetries ?? RetryCount;
        do {
            try {
                await SendAttachmentChunkOnceAsync(uploadUrl, chunk, offset, fileSize, cancellationToken);
                return;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                lastException = ex;
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    throw;
                }
                var retryAfter = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policy, attempts, retryAfter, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= maxRetries);

        if (lastException != null) {
            throw lastException;
        }
    }

    private async Task UploadAttachmentWithRetryAsync(GraphMessage draftMessage, GraphFileAttachmentSource source, CancellationToken cancellationToken) {
        var attachmentItemJson = await CreateGraphAttachment(source, cancellationToken, preloadContent: false);
        if (!string.IsNullOrEmpty(attachmentItemJson.DirectAttachmentJson)) {
            // A direct attachment POST is not idempotent. An ambiguous timeout may mean
            // Graph committed the attachment, so retrying could duplicate it.
            await AddDirectAttachmentAsync(draftMessage, attachmentItemJson.DirectAttachmentJson, cancellationToken);
            return;
        }

        var policy = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        int attempts = 0;
        Exception? lastException = null;
        var maxRetries = policy?.MaxRetries ?? RetryCount;
        do {
            try {
                var uploadUrl = await CreateUploadSession(draftMessage, attachmentItemJson.Json, cancellationToken);
                await SendFileChunks(uploadUrl, attachmentItemJson.FilePath, attachmentItemJson.FileSize, cancellationToken);
                return;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (FileNotFoundException) {
                throw;
            } catch (Exception ex) {
                lastException = ex;
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    throw;
                }
                var retryAfter = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policy, attempts, retryAfter, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= maxRetries);

        if (lastException != null) {
            throw lastException;
        }
    }

    private async Task SendAttachmentChunkOnceAsync(string uploadUrl, byte[] chunk, long offset, long fileSize, CancellationToken cancellationToken) {
        using var content = new StreamContent(new MemoryStream(chunk, writable: false));
        var contentRange = $"bytes {offset}-{offset + chunk.Length - 1}/{fileSize}";
        content.Headers.Add("Content-Range", contentRange);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, uploadUrl) {
            Content = content
        };
        requestMessage.Headers.Add("AnchorMailbox", SentFrom);
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        try {
            using var uploadChunkResponse = await _client.SendAsync(requestMessage, cancellationToken);
            if (!uploadChunkResponse.IsSuccessStatusCode) {
                var responseContent = await uploadChunkResponse.Content.ReadAsStringAsync();
                GraphApiError? error = null;
                try {
                    error = JsonSerializer.Deserialize(responseContent, MailozaurrJsonContext.Default.GraphApiError);
                } catch (JsonException) {
                    // Non-JSON error response; fall back to raw content.
                }
                var errorMessage = (error == null || error.Error == null)
                    ? $"Unknown error: {responseContent}"
                    : $"Error code: {error.Error.Code}, message: {error.Error.Message}";
                var retryAfter = ParseRetryAfter(uploadChunkResponse);
                throw new GraphApiException(uploadChunkResponse.StatusCode, errorMessage, responseContent, retryAfter);
            }
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response) {
        if (response.Headers.TryGetValues("Retry-After", out var values)) {
            var first = values.FirstOrDefault();
            if (int.TryParse(first, out var seconds)) {
                return TimeSpan.FromSeconds(Math.Max(0, seconds));
            }
            if (DateTimeOffset.TryParse(first, out var ts)) {
                var delta = ts - DateTimeOffset.UtcNow;
                return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
            }
        }
        return null;
    }

    private async Task DelayWithBackoffAsync(GraphSendPolicy? policy, int attempts, TimeSpan? retryAfter, Exception ex, CancellationToken cancellationToken) {
        TimeSpan delay = TimeSpan.Zero;
        if (policy != null) {
            delay = GraphRetryHelper.CalculateDelay(policy, attempts);
            if (GraphRetryHelper.IsThrottled(ex) && retryAfter.HasValue && retryAfter.Value > delay) {
                delay = retryAfter.Value;
            }
            if (policy.MaxDelayMs > 0 && delay > TimeSpan.FromMilliseconds(policy.MaxDelayMs)) {
                delay = TimeSpan.FromMilliseconds(policy.MaxDelayMs);
            }
        } else {
            delay = RetryDelayCalculator.Calculate(
                RetryDelayMilliseconds,
                RetryDelayBackoff,
                attempts,
                0,
                0);
        }

        if (delay > TimeSpan.Zero) {
            var reason = GraphRetryHelper.IsThrottled(ex) ? "throttling" : "transient";
            LogCollector.LogVerbose($"Send-EmailMessage - Retry attempt {attempts + 1}, delaying {delay.TotalMilliseconds:N0} ms due to {reason}.");
            await Task.Delay(delay, cancellationToken);
        }
    }

    private const string DefaultAttachmentName = "attachment.bin";
}
