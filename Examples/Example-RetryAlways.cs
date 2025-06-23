using Mailozaurr;
using System.Net;

// Example demonstrating RetryAlways
var smtp = new Smtp();
smtp.From = "sender@example.com";
smtp.To = new object[] { "recipient@example.com" };
smtp.Subject = "Retry demo";
smtp.TextBody = "Hello";
smtp.RetryAlways = true;
smtp.RetryCount = 5;
smtp.RetryDelayMilliseconds = 500;
smtp.RetryDelayBackoff = 1.5;
// Replace with real credentials and server
smtp.Connect("smtp.example.com", 587);
var result = smtp.Send();
System.Console.WriteLine($"Status: {result.Status}, Error: {result.Error}");
