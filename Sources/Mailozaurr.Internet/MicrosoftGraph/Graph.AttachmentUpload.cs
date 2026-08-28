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
        string? attachmentPath = source.Path;
        if (attachmentPath != null && !File.Exists(attachmentPath)) {
            LogMissingAttachmentWarning(attachmentPath);
            throw new FileNotFoundException($"Send-EmailMessage - Attachment file not found: {attachmentPath}", attachmentPath);
        }
        var fileName = string.IsNullOrWhiteSpace(source.Descriptor?.FileName)
            ? Path.GetFileName(attachmentPath) ?? "attachment"
            : source.Descriptor!.FileName!;
        var fileSize = source.Length;
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
        var attachmentItemJson = JsonSerializer.Serialize(attachmentItemWrapper, GraphJsonContext.Default.GraphAttachmentItemWrapper);
        var directAttachmentJson = string.Empty;
        if (fileSize < MinimumUploadSessionAttachmentSize) {
            var directAttachment = source.Descriptor == null
                ? GraphAttachment.FromFile(attachmentPath!)
                : GraphAttachment.FromDescriptor(source.Descriptor);
            directAttachmentJson = JsonSerializer.Serialize(directAttachment, GraphJsonContext.Default.GraphAttachment);
        }

        List<StreamContent> content = preloadContent && fileSize >= MinimumUploadSessionAttachmentSize
            ? PrepareStreamSourceContentForUpload(source, ChunkSize, cancellationToken)
            : new List<StreamContent>();

        var placeholder = new GraphAttachmentPlaceHolder {
            Json = attachmentItemJson,
            Content = content,
            FilePath = attachmentPath ?? string.Empty,
            FileSize = fileSize,
            FileName = fileName,
            DirectAttachmentJson = directAttachmentJson
        };

        return Task.FromResult(placeholder);
    }

    private GraphAttachmentPlaceHolder CreateGraphAttachment(
        GraphAttachment attachment,
        bool preloadContent,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = DecodeGraphAttachmentContent(attachment);
        var fileName = string.IsNullOrWhiteSpace(attachment.Name) ? DefaultAttachmentName : attachment.Name;
        var attachmentItem = new GraphAttachmentItem("file", fileName, bytes.LongLength) {
            ContentType = attachment.ContentType,
            IsInline = attachment.IsInline ? true : null,
            ContentId = string.IsNullOrWhiteSpace(attachment.ContentId)
                ? (attachment.IsInline ? fileName : null)
                : attachment.ContentId
        };
        var wrapper = new GraphAttachmentItemWrapper(attachmentItem);

        return new GraphAttachmentPlaceHolder {
            Json = JsonSerializer.Serialize(wrapper, GraphJsonContext.Default.GraphAttachmentItemWrapper),
            Content = preloadContent && bytes.Length >= MinimumUploadSessionAttachmentSize
                ? PrepareByteArrayContentForUpload(bytes, ChunkSize, cancellationToken)
                : new List<StreamContent>(),
            FileSize = bytes.LongLength,
            FileName = fileName,
            DirectAttachmentJson = bytes.Length < MinimumUploadSessionAttachmentSize
                ? JsonSerializer.Serialize(attachment, GraphJsonContext.Default.GraphAttachment)
                : string.Empty
        };
    }

    private static byte[] DecodeGraphAttachmentContent(GraphAttachment attachment) {
        try {
            return Convert.FromBase64String(attachment.ContentBytes ?? string.Empty);
        } catch (FormatException ex) {
            throw new InvalidDataException($"Graph attachment '{attachment.Name}' contains invalid Base64 content.", ex);
        }
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
                    error = JsonSerializer.Deserialize(uploadSessionContent, GraphJsonContext.Default.GraphApiError);
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
                error = JsonSerializer.Deserialize(responseContent, GraphJsonContext.Default.GraphApiError);
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
        var uploadSessionResult = JsonSerializer.Deserialize(uploadSessionContent, GraphJsonContext.Default.GraphUploadSessionResult)
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
    /// <param name="expectedLength">Length captured when the attachment metadata was created.</param>
    /// <param name="chunkSize">Desired size of each chunk in bytes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>List of stream contents representing file chunks.</returns>
    private List<StreamContent> PrepareByteArrayContentForUpload(
        string filePath,
        long expectedLength,
        int chunkSize = MaxChunkSize,
        CancellationToken cancellationToken = default) {
        chunkSize = Math.Min(chunkSize, MaxChunkSize);
        var fileContents = new List<StreamContent>();

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        int bytesRead;
        long offset = 0;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            while ((bytesRead = fileStream.Read(buffer, 0, chunkSize)) > 0) {
                cancellationToken.ThrowIfCancellationRequested();
                if (offset + bytesRead > expectedLength) throw ContentLengthMismatch(expectedLength, offset + bytesRead);

                var chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                var memoryStream = new MemoryStream(chunk, writable: false);
                var contentRange = $"bytes {offset}-{offset + bytesRead - 1}/{expectedLength}";
                var streamContent = new StreamContent(memoryStream);
                streamContent.Headers.Add("Content-Range", contentRange);
                fileContents.Add(streamContent);
                offset += bytesRead;
            }
            if (offset != expectedLength) throw ContentLengthMismatch(expectedLength, offset);
        } catch {
            foreach (StreamContent content in fileContents) content.Dispose();
            throw;
        } finally {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        return fileContents;
    }

    private List<StreamContent> PrepareStreamSourceContentForUpload(
        GraphFileAttachmentSource source,
        int chunkSize = MaxChunkSize,
        CancellationToken cancellationToken = default) {
        if (source.Path != null) return PrepareByteArrayContentForUpload(
            source.Path,
            source.Length,
            chunkSize,
            cancellationToken);

        chunkSize = Math.Min(chunkSize, MaxChunkSize);
        var chunks = new List<StreamContent>();
        using Stream stream = source.Descriptor!.OpenContentStream();
        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        long offset = 0;
        try {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, 0, chunkSize);
                if (read == 0) break;
                if (offset + read > source.Length) throw ContentLengthMismatch(source.Length, offset + read);
                var chunk = new byte[read];
                Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                var content = new StreamContent(new MemoryStream(chunk, writable: false));
                content.Headers.Add("Content-Range", $"bytes {offset}-{offset + read - 1}/{source.Length}");
                chunks.Add(content);
                offset += read;
            }
            if (offset != source.Length) throw ContentLengthMismatch(source.Length, offset);
            return chunks;
        } catch {
            foreach (StreamContent chunk in chunks) chunk.Dispose();
            throw;
        } finally {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static List<StreamContent> PrepareByteArrayContentForUpload(byte[] bytes, int chunkSize = MaxChunkSize, CancellationToken cancellationToken = default) {
        chunkSize = Math.Min(chunkSize, MaxChunkSize);
        var chunks = new List<StreamContent>();
        long offset = 0;
        while (offset < bytes.LongLength) {
            cancellationToken.ThrowIfCancellationRequested();
            var length = (int)Math.Min(chunkSize, bytes.LongLength - offset);
            var chunk = new byte[length];
            Buffer.BlockCopy(bytes, (int)offset, chunk, 0, length);
            var streamContent = new StreamContent(new MemoryStream(chunk, writable: false));
            streamContent.Headers.Add("Content-Range", $"bytes {offset}-{offset + length - 1}/{bytes.LongLength}");
            chunks.Add(streamContent);
            offset += length;
        }
        return chunks;
    }

    /// <summary>
    /// Uploads all attachments for the specified draft message.
    /// </summary>
    /// <param name="draftMessage">The draft message to attach the files to.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task UploadAttachmentsAsync(GraphMessage draftMessage, CancellationToken cancellationToken = default) {
        foreach (var attachment in _deferredGraphAttachments) {
            await UploadGraphAttachmentAsync(draftMessage, attachment, cancellationToken);
        }
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
        AttachmentsPlaceHolders.Clear();
        foreach (var attachment in _deferredGraphAttachments) {
            AttachmentsPlaceHolders.Add(CreateGraphAttachment(attachment, preloadContent: true, cancellationToken));
        }
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
            if (offset + bytesRead > fileSize) throw ContentLengthMismatch(fileSize, offset + bytesRead);
            var chunk = new byte[bytesRead];
            Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
            await SendAttachmentChunkWithRetryAsync(uploadUrl, chunk, offset, fileSize, cancellationToken);
            offset += bytesRead;
        }
        if (offset != fileSize) throw ContentLengthMismatch(fileSize, offset);
    }

    private async Task SendFileChunks(
        string uploadUrl,
        GraphFileAttachmentSource source,
        CancellationToken cancellationToken) {
        var chunkSize = Math.Min(ChunkSize, MaxChunkSize);
        var buffer = new byte[chunkSize];
        long offset = 0;
        using Stream stream = await source.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        while (true) {
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0) break;
            if (offset + bytesRead > source.Length) throw ContentLengthMismatch(source.Length, offset + bytesRead);
            var chunk = new byte[bytesRead];
            Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
            await SendAttachmentChunkWithRetryAsync(uploadUrl, chunk, offset, source.Length, cancellationToken)
                .ConfigureAwait(false);
            offset += bytesRead;
        }
        if (offset != source.Length) throw ContentLengthMismatch(source.Length, offset);
    }

    private async Task SendFileChunks(string uploadUrl, byte[] bytes, CancellationToken cancellationToken) {
        var chunkSize = Math.Min(ChunkSize, MaxChunkSize);
        long offset = 0;
        while (offset < bytes.LongLength) {
            cancellationToken.ThrowIfCancellationRequested();
            var length = (int)Math.Min(chunkSize, bytes.LongLength - offset);
            var chunk = new byte[length];
            Buffer.BlockCopy(bytes, (int)offset, chunk, 0, length);
            await SendAttachmentChunkWithRetryAsync(uploadUrl, chunk, offset, bytes.LongLength, cancellationToken);
            offset += length;
        }
    }

    private async Task UploadGraphAttachmentAsync(GraphMessage draftMessage, GraphAttachment attachment, CancellationToken cancellationToken) {
        var placeholder = CreateGraphAttachment(attachment, preloadContent: false, cancellationToken);
        if (!string.IsNullOrEmpty(placeholder.DirectAttachmentJson)) {
            // The direct POST is not idempotent, so ambiguous failures must not be retried.
            await AddDirectAttachmentAsync(draftMessage, placeholder.DirectAttachmentJson, cancellationToken);
            return;
        }

        var bytes = DecodeGraphAttachmentContent(attachment);
        var policy = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        var maxRetries = policy?.MaxRetries ?? RetryCount;
        var attempts = 0;
        do {
            try {
                var uploadUrl = await CreateUploadSession(draftMessage, placeholder.Json, cancellationToken);
                await SendFileChunks(uploadUrl, bytes, cancellationToken);
                return;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    throw;
                }
                await DelayWithBackoffAsync(policy, attempts, (ex as GraphApiException)?.RetryAfter, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= maxRetries);
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
        var maxRetries = policy?.MaxRetries ?? RetryCount;
        while (true) {
            try {
                await SendAttachmentChunkOnceAsync(uploadUrl, chunk, offset, fileSize, cancellationToken);
                return;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    throw;
                }
                var retryAfter = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policy, attempts, retryAfter, ex, cancellationToken);
            }
            attempts++;
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
        var maxRetries = policy?.MaxRetries ?? RetryCount;
        while (true) {
            try {
                var uploadUrl = await CreateUploadSession(draftMessage, attachmentItemJson.Json, cancellationToken);
                await SendFileChunks(uploadUrl, source, cancellationToken);
                return;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (FileNotFoundException) {
                throw;
            } catch (Exception ex) {
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    throw;
                }
                var retryAfter = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policy, attempts, retryAfter, ex, cancellationToken);
            }
            attempts++;
        }
    }

    private static InvalidDataException ContentLengthMismatch(long declared, long observed) =>
        new InvalidDataException(
            $"Attachment content length changed while preparing a Graph upload (declared {declared}, observed {observed}).");

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
                    error = JsonSerializer.Deserialize(responseContent, GraphJsonContext.Default.GraphApiError);
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
