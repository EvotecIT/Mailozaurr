using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading;

namespace Mailozaurr;

public partial class Graph {
    /// <summary>
    /// Sends the prepared message via the Graph API.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public async Task<GraphSmtpResult> SendMessageAsync(CancellationToken cancellationToken = default) {
        var operationStopwatch = StartOperationTimer();
        // create message
        CreateMessage();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph send.");
            return new GraphSmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        if (IsLargerAttachment) {
            LogCollector.LogVerbose("Send-EmailMessage - Serialized attachment payload exceeds the Graph simple-send limit; using a draft upload session.");
            return await SendMessageDraftAsync(messagePrepared: true, cancellationToken);
        }
        LogCollector.LogVerbose("Send-EmailMessage - Sending email via Graph API");
        // Create the request URI outside the loop.
        var requestUri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{MessageContainer.Message.From!.Email.Address}/sendMail");

        var policy = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        if (policy != null && policy.MaxConcurrency > 0) {
            MicrosoftGraphUtils.MaxConcurrentRequests = policy.MaxConcurrency;
        }

        var policyDraft = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        if (policyDraft != null && policyDraft.MaxConcurrency > 0) {
            MicrosoftGraphUtils.MaxConcurrentRequests = policyDraft.MaxConcurrency;
        }

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri) {
                    Content = new StringContent(MessageJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

                await WaitForConcurrencyAsync(operationStopwatch, cancellationToken);
                try {
                    using var response = await _client.SendAsync(request, cancellationToken);
                    var content = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode) {
                        var okResult = new GraphSmtpResult(true, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, response.StatusCode.ToString(), "");
                        await PostGraphWebhookAsync(okResult, cancellationToken);
                        return okResult;
                    }
                    var error = JsonSerializer.Deserialize(content, GraphJsonContext.Default.GraphApiError);
                    var errorMessage = (error == null || error.Error == null || error.Error.InnerError == null)
                        ? $"Unknown error: {content}"
                        : $"Error code: {error.Error.Code}, message: {error.Error.Message}, request ID: {error.Error.InnerError.RequestId}, date: {error.Error.InnerError.Date}";
                    var retryAfter = ParseRetryAfter(response);
                    throw new GraphApiException(response.StatusCode, errorMessage, content, retryAfter);
                } finally {
                    MicrosoftGraphUtils.ConcurrencySemaphore.Release();
                }
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Sending via Graph API cancelled: {ex.Message}");
                var maxRetries = policy?.MaxRetries ?? RetryCount;
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    var failResult = CreateGraphFailureResult(operationStopwatch, ex.Message);
                    await PostGraphWebhookAsync(failResult, cancellationToken);
                    return await TrySmtpFallbackAsync(policy, failResult, ex, cancellationToken);
                }
                await DelayWithBackoffAsync(policy, attempts, null, ex, cancellationToken);
            } catch (Exception ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
                var maxRetries = policy?.MaxRetries ?? RetryCount;
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var failResult = CreateGraphFailureResult(operationStopwatch, ex.Message);
                    await PostGraphWebhookAsync(failResult, cancellationToken);
                    return await TrySmtpFallbackAsync(policy, failResult, ex, cancellationToken);
                }
                var retryAfter = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policy, attempts, retryAfter, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= (policy?.MaxRetries ?? RetryCount));

        var finalResult = CreateGraphFailureResult(operationStopwatch, lastException?.Message);
        await PostGraphWebhookAsync(finalResult, cancellationToken);
        return await TrySmtpFallbackAsync(policy, finalResult, lastException, cancellationToken);
    }

    /// <summary>
    /// Sends a message by first creating a draft and then uploading attachments.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public async Task<GraphSmtpResult> SendMessageDraftAsync(CancellationToken cancellationToken = default) {
        return await SendMessageDraftAsync(messagePrepared: false, cancellationToken);
    }

    private async Task<GraphSmtpResult> SendMessageDraftAsync(bool messagePrepared, CancellationToken cancellationToken) {
        var operationStopwatch = StartOperationTimer();
        if (DryRun) {
            if (!messagePrepared) {
                CreateMessage();
            }
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph draft send.");
            return new GraphSmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        var policyDraft = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        if (policyDraft != null && policyDraft.MaxConcurrency > 0) {
            MicrosoftGraphUtils.MaxConcurrentRequests = policyDraft.MaxConcurrency;
        }

        GraphMessage draftMessage;
        try {
            draftMessage = await CreateDraftMessageAsync(messagePrepared, cancellationToken);
            await UploadAttachmentsAsync(draftMessage, cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Error while creating the Graph draft or adding attachments: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            var failResult = CreateGraphFailureResult(operationStopwatch, ex.Message);
            await PostGraphWebhookAsync(failResult, cancellationToken);
            return await TrySmtpFallbackAsync(policyDraft, failResult, ex, cancellationToken);
        }

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                return await SendDraftMessage(draftMessage, cancellationToken);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Sending draft via Graph API cancelled: {ex.Message}");
                var maxRetries = policyDraft?.MaxRetries ?? RetryCount;
                var shouldRetry = (policyDraft?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    var failResult = CreateGraphFailureResult(operationStopwatch, ex.Message);
                    await PostGraphWebhookAsync(failResult, cancellationToken);
                    return await TrySmtpFallbackAsync(policyDraft, failResult, ex, cancellationToken);
                }
                await DelayWithBackoffAsync(policyDraft, attempts, null, ex, cancellationToken);
            } catch (Exception ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
                var maxRetries = policyDraft?.MaxRetries ?? RetryCount;
                var shouldRetry = (policyDraft?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var failResult = CreateGraphFailureResult(operationStopwatch, ex.Message);
                    await PostGraphWebhookAsync(failResult, cancellationToken);
                    return await TrySmtpFallbackAsync(policyDraft, failResult, ex, cancellationToken);
                }
                var ra = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policyDraft, attempts, ra, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= (policyDraft?.MaxRetries ?? RetryCount));

        var finalResult = CreateGraphFailureResult(operationStopwatch, lastException?.Message);
        await PostGraphWebhookAsync(finalResult, cancellationToken);
        return await TrySmtpFallbackAsync(policyDraft, finalResult, lastException, cancellationToken);
    }

    /// <summary>
    /// Sends a previously created draft message.
    /// </summary>
    /// <param name="draftMessage">The draft message to send.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The result of the send operation.</returns>
    public async Task<GraphSmtpResult> SendDraftMessage(GraphMessage draftMessage, CancellationToken cancellationToken = default) {
        var operationStopwatch = StartOperationTimer();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph draft send.");
            return new GraphSmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        // Send the draft message
        var sendRequestUri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{MessageContainer.Message.From!.Email.Address}/messages/{draftMessage.Id!}/send");
        using var sendRequest = new HttpRequestMessage(HttpMethod.Post, sendRequestUri);

        // Add the authorization header
        sendRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        // Send the HTTP request for sending the draft message
        await WaitForConcurrencyAsync(operationStopwatch, cancellationToken);
        try {
            using var sendResponse = await _client.SendAsync(sendRequest, cancellationToken);

            // If the status code indicates success, return a successful result
            if (sendResponse.IsSuccessStatusCode) {
                var okResult = new GraphSmtpResult(true, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, sendResponse.StatusCode.ToString(), "");
                await PostGraphWebhookAsync(okResult, cancellationToken);
                return okResult;
            }

            // If the status code indicates an error, throw an exception with the content
            var sendContent = await sendResponse.Content.ReadAsStringAsync();
            var sendError = JsonSerializer.Deserialize(sendContent, GraphJsonContext.Default.GraphApiError);
            var sendErrorMessage = (sendError == null || sendError.Error == null || sendError.Error.InnerError == null)
                ? $"Unknown error: {sendContent}"
                : $"Error code: {sendError.Error.Code}, message: {sendError.Error.Message}, request ID: {sendError.Error.InnerError.RequestId}, date: {sendError.Error.InnerError.Date}";
            var retryAfter = ParseRetryAfter(sendResponse);
            throw new GraphApiException(sendResponse.StatusCode, sendErrorMessage, sendContent, retryAfter);
        } catch (GraphApiException ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
            var failResult = CreateGraphFailureResult(operationStopwatch, ex.Message, ex.ResponseContent);
            await PostGraphWebhookAsync(failResult, cancellationToken);
            throw;
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Sends the current message using Microsoft Graph batch requests.
    /// </summary>
    public async Task<GraphSmtpResult> SendMessageBatchAsync(CancellationToken cancellationToken = default) {
        var operationStopwatch = StartOperationTimer();
        CreateMessage();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph batch send.");
            return new GraphSmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        var credential = new GraphCredential {
            ClientId = ApplicationID,
            ClientSecret = ApplicationKey,
            DirectoryId = TenantDomain
        };
        var request = CreateBatchSendRequest();
        if (GetBatchPayloadSize(request) > GraphPayloadLimitBytes) {
            TryRouteConvertedFileAttachmentsThroughUploadSession();
            request = CreateBatchSendRequest();
            TryRouteEligibleAttachments(() => GetBatchPayloadSize(CreateBatchSendRequest()) > GraphPayloadLimitBytes);
            request = CreateBatchSendRequest();
            if (GetBatchPayloadSize(request) > GraphPayloadLimitBytes) {
                throw new InvalidOperationException("The complete serialized Graph batch request exceeds the 4MB payload limit after draft attachments were removed. Reduce the message body, recipients, or headers.");
            }
        }
        if (IsLargerAttachment) {
            LogCollector.LogVerbose("Send-EmailMessage - Serialized request exceeds the Graph batch payload limit; using a draft upload session.");
            if (string.IsNullOrWhiteSpace(AccessToken) || string.IsNullOrWhiteSpace(TokenType)) {
                var connection = await ConnectO365GraphAsync(cancellationToken);
                if (!connection.Status) {
                    return connection;
                }
            }
            return await SendMessageDraftAsync(messagePrepared: true, cancellationToken);
        }
        var results = await MicrosoftGraphUtils.SendBatchAsync(credential, new[] { request }, cancellationToken);
        var response = results.FirstOrDefault();
        var success = response != null && response.Status >= 200 && response.Status < 300;
        if (!success) {
            return CreateGraphFailureResult(operationStopwatch, response?.Body.ToString(),
                response?.Status.ToString() ?? string.Empty);
        }
        return new GraphSmtpResult(
            true,
            EmailAction.Send,
            SentTo,
            SentFrom,
            "GraphAPI",
            0,
            operationStopwatch.Elapsed,
            response?.Status.ToString() ?? string.Empty,
            string.Empty);
    }

    private GraphSmtpResult CreateGraphFailureResult(Stopwatch operationStopwatch,
        string? error, string? outputMessage = null) => new(false, EmailAction.Send,
        SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed,
        outputMessage ?? string.Empty, error) {
        GraphError = GraphApiErrorParser.Parse(error)
    };

    private GraphBatchRequest CreateBatchSendRequest() {
        var bodyObj = JsonSerializer.Deserialize(MessageJson, MailozaurrJsonContext.Default.JsonElement);
        return new GraphBatchRequest {
            Id = "1",
            Method = GraphHttpMethod.POST,
            Url = $"/users/{MessageContainer.Message.From!.Email.Address}/sendMail",
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body = bodyObj
        };
    }

    private static int GetBatchPayloadSize(GraphBatchRequest request) {
        var payload = new GraphBatchPayload {
            Requests = new List<GraphBatchRequestPayload> {
                new() {
                    Id = request.Id,
                    Method = request.Method.ToString(),
                    Url = request.Url.TrimStart('/'),
                    Headers = request.Headers,
                    Body = request.Body
                }
            }
        };
        return JsonSerializer.SerializeToUtf8Bytes(payload, GraphJsonContext.Default.GraphBatchPayload).Length;
    }

    /// <summary>
    /// Creates a draft message on the server and returns the resulting <see cref="GraphMessage"/>.
    /// </summary>
    /// <returns>The created draft message.</returns>
    public async Task<GraphMessage> CreateDraftMessageAsync(CancellationToken cancellationToken = default) {
        return await CreateDraftMessageAsync(messagePrepared: false, cancellationToken);
    }

    private async Task<GraphMessage> CreateDraftMessageAsync(bool messagePrepared, CancellationToken cancellationToken) {
        if (!messagePrepared) {
            CreateMessage();
        }
        // This entry point always completes through the draft attachment endpoints.
        // Remove any file-backed attachments that happened to fit the simple payload
        // so UploadAttachmentsAsync can add each file exactly once.
        TryRouteConvertedFileAttachmentsThroughUploadSession();

        //var options = new JsonSerializerOptions() {
        //    WriteIndented = true
        //};

        //// Serialize only the GraphMessage to a JSON string, excluding the SaveToSentItems property
        //var messageJson = JsonSerializer.Serialize(MessageContainer.Message, options);

        var messageJson = JsonSerializer.Serialize(MessageContainer.Message, GraphJsonContext.Default.GraphMessage);

        var draftRequestUri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{MessageContainer.Message.From!.Email.Address}/mailfolders/drafts/messages");
        using var draftRequest = new HttpRequestMessage(HttpMethod.Post, draftRequestUri) {
            Content = new StringContent(messageJson, Encoding.UTF8, "application/json")
        };

        // Add the authorization header
        draftRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        // Send the HTTP request for creating the draft message
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        HttpResponseMessage draftResponse;
        try {
            draftResponse = await _client.SendAsync(draftRequest, cancellationToken);
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }

        using (draftResponse) {
            // Read the response content
            var draftContent = await draftResponse.Content.ReadAsStringAsync();

            if (!draftResponse.IsSuccessStatusCode) {
                var error = JsonSerializer.Deserialize(draftContent, GraphJsonContext.Default.GraphApiError);
                var errorMessage = (error == null || error.Error == null)
                    ? $"Unknown error: {draftContent}"
                    : $"Error code: {error.Error.Code}, message: {error.Error.Message}";
                var retryAfter = ParseRetryAfter(draftResponse);
                throw new GraphApiException(draftResponse.StatusCode, errorMessage, draftContent, retryAfter);
            }

            // Deserialize the draft message
            var draftMessage = JsonSerializer.Deserialize(draftContent, GraphJsonContext.Default.GraphMessage);

            if (draftMessage == null) {
                throw new InvalidOperationException("Failed to create draft message.");
            }

            return draftMessage;
        }
    }

    /// <summary>
    /// Creates a draft message locally and returns its JSON representation.
    /// </summary>
    /// <returns>The JSON payload for the draft message.</returns>
    public string CreateDraftForMg() {
        return CreateDraft();
    }

    /// <summary>
    /// Serializes the current, already prepared Graph message for a draft request.
    /// </summary>
    /// <remarks>
    /// Call <see cref="CreateMessage"/> first. This avoids rebuilding state after
    /// automatic inline-image conversion has rewritten local image references to CIDs.
    /// </remarks>
    /// <returns>The JSON payload for the prepared draft message.</returns>
    public string CreatePreparedDraft() {
        return JsonSerializer.Serialize(MessageContainer.Message, GraphJsonContext.Default.GraphMessage);
    }

    /// <summary>
    /// Serializes the current message to JSON without saving it to the Sent Items folder.
    /// </summary>
    /// <returns>The JSON representation of the message.</returns>
    public string CreateDraft() {
        CreateMessage();
        return CreatePreparedDraft();
    }

    private Task PostGraphWebhookAsync(GraphSmtpResult result, CancellationToken cancellationToken) =>
        Helpers.PostWebhookAsync(WebhookUrl, result, GraphJsonContext.Default.GraphSmtpResult, cancellationToken);
}
