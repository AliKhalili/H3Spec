using System.Net.Quic;
using H3Spec.Core;
using H3Spec.Core.Cli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

internal class Program
{
    private static int Main(string[] args)
    {
        if (!QuicConnection.IsSupported)
        {
            Console.WriteLine("QUIC is not supported, check for presence of libmsquic and support of TLS 1.3.");
            return -1;
        }

        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
           .AddEnvironmentVariables();

        IConfiguration config = builder.Build();

        var http3Servers = config.GetSection("http3Servers").Get<Http3ServerOptions[]>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(http3Servers);

        var app = new CommandApp<TestCommand>(new TypeRegistrar(serviceCollection));
        return app.Run(args);
    }
}
