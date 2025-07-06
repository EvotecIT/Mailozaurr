using System.Collections.Generic;
using System.Linq;

namespace Mailozaurr;

/// <summary>
/// Provides a fluent API for building <see cref="GraphInboxRule"/> objects.
/// </summary>
public sealed class GraphInboxRuleBuilder {
    private readonly GraphInboxRule _rule = new();

    /// <summary>Sets the display name of the rule.</summary>
    public GraphInboxRuleBuilder DisplayName(string name) {
        _rule.DisplayName = name;
        return this;
    }

    /// <summary>Sets the sequence of the rule.</summary>
    public GraphInboxRuleBuilder Sequence(int sequence) {
        _rule.Sequence = sequence;
        return this;
    }

    /// <summary>Enables or disables the rule.</summary>
    public GraphInboxRuleBuilder Enabled(bool enabled = true) {
        _rule.IsEnabled = enabled;
        return this;
    }

    private GraphInboxRulePredicates EnsurePredicates() => _rule.Conditions ??= new GraphInboxRulePredicates();
    private GraphInboxRuleActions EnsureActions() => _rule.Actions ??= new GraphInboxRuleActions();

    /// <summary>Adds senders to match.</summary>
    public GraphInboxRuleBuilder SenderContains(params string[] senders) {
        var preds = EnsurePredicates();
        preds.SenderContains = senders.ToList();
        return this;
    }

    /// <summary>Adds recipients to match.</summary>
    public GraphInboxRuleBuilder RecipientContains(params string[] recipients) {
        var preds = EnsurePredicates();
        preds.RecipientContains = recipients.ToList();
        return this;
    }

    /// <summary>Adds subjects to match.</summary>
    public GraphInboxRuleBuilder SubjectContains(params string[] subjects) {
        var preds = EnsurePredicates();
        preds.SubjectContains = subjects.ToList();
        return this;
    }

    /// <summary>Adds body text to match.</summary>
    public GraphInboxRuleBuilder BodyContains(params string[] body) {
        var preds = EnsurePredicates();
        preds.BodyContains = body.ToList();
        return this;
    }

    /// <summary>Sets the importance to match.</summary>
    public GraphInboxRuleBuilder Importance(string importance) {
        var preds = EnsurePredicates();
        preds.Importance = importance;
        return this;
    }

    /// <summary>Moves matching messages to the specified folder.</summary>
    public GraphInboxRuleBuilder MoveToFolder(string folder) {
        var actions = EnsureActions();
        actions.MoveToFolder = folder;
        return this;
    }

    /// <summary>Copies matching messages to the specified folder.</summary>
    public GraphInboxRuleBuilder CopyToFolder(string folder) {
        var actions = EnsureActions();
        actions.CopyToFolder = folder;
        return this;
    }

    /// <summary>Marks matching messages for deletion.</summary>
    public GraphInboxRuleBuilder Delete(bool delete = true) {
        var actions = EnsureActions();
        actions.Delete = delete;
        return this;
    }

    /// <summary>Forwards matching messages to the provided recipients.</summary>
    public GraphInboxRuleBuilder ForwardTo(params string[] addresses) {
        var actions = EnsureActions();
        actions.ForwardTo = addresses
            .Select(a => new GraphEmailAddress { Email = new GraphEmail { Address = a } })
            .ToList();
        return this;
    }

    /// <summary>Stops processing additional rules when this rule matches.</summary>
    public GraphInboxRuleBuilder StopProcessingRules(bool stop = true) {
        var actions = EnsureActions();
        actions.StopProcessingRules = stop;
        return this;
    }

    /// <summary>Builds the <see cref="GraphInboxRule"/>.</summary>
    public GraphInboxRule Build() => _rule;
}
