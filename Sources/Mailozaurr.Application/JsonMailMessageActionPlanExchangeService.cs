using System.Text.Json;

namespace Mailozaurr.Application;

/// <summary>
/// Imports and exports normalized message action plans as JSON documents.
/// </summary>
public sealed class JsonMailMessageActionPlanExchangeService : IMailMessageActionPlanExchangeService {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <inheritdoc />
    public async Task<MessageActionExecutionPlan> LoadAsync(string path, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Action plan path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var plan = await JsonSerializer.DeserializeAsync<MessageActionExecutionPlan>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (plan == null) {
            throw new InvalidDataException($"Action plan file '{fullPath}' did not contain a valid action plan document.");
        }

        return plan;
    }

    /// <inheritdoc />
    public async Task SaveAsync(string path, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Action plan path is required.", nameof(path));
        }
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, plan, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageActionExecutionPlan>> LoadBatchAsync(string path, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Action plan batch path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var plans = await JsonSerializer.DeserializeAsync<List<MessageActionExecutionPlan>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (plans == null) {
            throw new InvalidDataException($"Action plan batch file '{fullPath}' did not contain a valid action plan array.");
        }

        return plans;
    }

    /// <inheritdoc />
    public async Task SaveBatchAsync(string path, IReadOnlyList<MessageActionExecutionPlan> plans, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Action plan batch path is required.", nameof(path));
        }
        if (plans == null) {
            throw new ArgumentNullException(nameof(plans));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, plans, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
