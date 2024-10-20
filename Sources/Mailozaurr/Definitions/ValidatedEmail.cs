﻿using EmailValidation;

namespace Mailozaurr;

/// <summary>
/// Validated email class
/// </summary>
public class ValidatedEmail {
    /// <summary>
    /// Email address
    /// </summary>
    /// <value>
    /// The email address.
    /// </value>
    public string EmailAddress { get; set; } = string.Empty;

    /// <summary>
    /// Returns true if email address is valid.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
    /// </value>
    public bool IsValid { get; set; }

    /// <summary>
    /// Indicates if the email address is disposable
    /// </summary>
    /// <value>
    ///   <c>true</c> if email address is disposable; otherwise, <c>false</c>.
    /// </value>
    public bool IsDisposable { get; set; }

    /// <summary>
    /// Indicates why email address is invalid (if it is)
    /// </summary>
    public EmailValidationErrorCode Reason { get; set; }

    /// <summary>
    /// Indicates the index of the token that caused the email address to be invalid
    /// </summary>
    public int? ReasonTokenIndex { get; set; }

    /// <summary>
    /// Indicates the index of the error that caused the email address to be invalid
    /// </summary>
    public int? ReasonErrorIndex { get; set; }

    /// <summary>
    /// If error during validation happens, this will contain the error message
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    public string Error { get; set; } = string.Empty;
}