using Mailozaurr.Hosting;
using Mailozaurr.Cli;

return await CliRunner.RunAsync(
    args,
    Console.Out,
    Console.Error,
    builderFactory: options => new MailApplicationBuilder(options),
    input: Console.In);