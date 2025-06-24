# Mailozaurr

Mailozaurr is a powerful .NET library for sending emails using various providers (SMTP, SendGrid, Mailgun, Microsoft Graph, etc.), supporting advanced features like attachments, HTML bodies, and authentication.

## Features
- Send emails via SMTP, SendGrid, Mailgun, Microsoft Graph, and more
- Support for attachments, HTML, and plain text
- OAuth2 and basic authentication
- Delivery notifications and advanced options
- Designed for integration in C# and PowerShell projects

## Installation

Add a reference to the `Mailozaurr` project or its compiled DLL in your C# project:

```
// If using .csproj
<ProjectReference Include="../Mailozaurr/Mailozaurr.csproj" />
```
Or, if you have the DLL:
```
// In your .csproj
<ItemGroup>
  <Reference Include="Mailozaurr">
    <HintPath>path/to/Mailozaurr.dll</HintPath>
  </Reference>
</ItemGroup>
```

## Usage in C#

### Basic Example: Sending an Email via SMTP

```csharp
using Mailozaurr;
using MailKit.Security;

var smtp = new Smtp();
smtp.From = "sender@example.com";
smtp.To = new[] { "recipient@example.com" };
smtp.Subject = "Test Email";
smtp.HtmlBody = "<b>Hello from Mailozaurr!</b>";
smtp.Server = "smtp.example.com";
smtp.Port = 587;
smtp.SecureSocketOptions = SecureSocketOptions.StartTls;

// Optional: authentication
smtp.Authenticate("username", "password");

// Send the email
var result = smtp.Send();
if (result.Status)
{
    Console.WriteLine("Email sent successfully!");
}
else
{
Console.WriteLine($"Failed: {result.ErrorMessage}");
}
```

> **Note**
> When `Connect` is called with the `useSsl` flag and
> `SecureSocketOptions` left as `Auto`, the library automatically uses
> `SecureSocketOptions.StartTls`. Provide an explicit option if a
> different behaviour is required.

### Using Microsoft Graph

```csharp
using Mailozaurr;

var graph = new Graph();
graph.From = "sender@example.com";
graph.To = new[] { "recipient@example.com" };
graph.Subject = "Graph API Email";
graph.HTML = "<p>Sent via Microsoft Graph!</p>";
// ... set up authentication ...
// graph.Authenticate(...);
// graph.SendMessageAsync();
```

## Key Classes
- `Smtp` - for SMTP email sending
- `Graph` - for Microsoft Graph API
- `SendGridClient` - for SendGrid
- `MailgunClient` - for Mailgun
- `SmtpResult` - result object for send operations

## Building from Source

1. Clone the repository
2. Open the solution in Visual Studio or run `dotnet build` in the root directory
3. Reference the built DLL in your project

## License

(c) 2011 - 2024 Przemyslaw Klys @ Evotec. All rights reserved.

---

*For more advanced usage, see the source code and examples in the repository.*