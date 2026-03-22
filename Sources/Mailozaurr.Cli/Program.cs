using Mailozaurr.Application;
using Mailozaurr.Cli;

return await CliRunner.RunAsync(
    args,
    Console.Out,
    Console.Error,
    builderFactory: options => new MailApplicationBuilder(options));
