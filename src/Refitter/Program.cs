﻿using System.ComponentModel;
using System.Text.Json;
using Exceptionless;
using Exceptionless.Plugins;
using Refitter.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using ValidationResult = Spectre.Console.ValidationResult;

ExceptionlessClient.Default.Startup("pRql7vmgecZ0Iph6MU5TJE5XsZeesdTe0yx7TN4f");

var app = new CommandApp<GenerateCommand>();
app.Configure(
    config =>
    {
        var configuration = config
            .SetApplicationName("refitter")
            .SetApplicationVersion(typeof(GenerateCommand).Assembly.GetName().Version!.ToString());
        
        configuration
            .AddExample(
                new[]
                {
                    "./openapi.json",
                });
        
        configuration
            .AddExample(
                new[]
                {
                    "https://petstore3.swagger.io/api/v3/openapi.yaml"
                });
        
        configuration
            .AddExample(
                new[]
                {
                    "./openapi.json",
                    "--namespace",
                    "\"Your.Namespace.Of.Choice.GeneratedCode\"",
                    "--output",
                    "./GeneratedCode.cs"
                });
        
        configuration
            .AddExample(
                new[]
                {
                    "./openapi.json",
                    "--namespace",
                    "\"Your.Namespace.Of.Choice.GeneratedCode\"",
                    "--internal"
                });
        
        configuration
            .AddExample(
                new[]
                {
                    "./openapi.json",
                    "--output",
                    "./IGeneratedCode.cs",
                    "--interface-only"
                });
        
        configuration
            .AddExample(
                new[]
                {
                    "./openapi.json",
                    "--use-api-response"
                });
        
        configuration
            .AddExample(
                new[]
                {
                    "./openapi.json",
                    "--cancellation-tokens"
                });
        
        configuration
            .AddExample(
                new[]
                {
                    "./openapi.json",
                    "--no-operation-headers"
                });
    });
return app.Run(args);

internal sealed class GenerateCommand : AsyncCommand<GenerateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("URL or file path to OpenAPI Specification file")]
        [CommandArgument(0, "[URL or input file]")]
        public string? OpenApiPath { get; set; }

        [Description("Default namespace to use for generated types")]
        [CommandOption("-n|--namespace")]
        [DefaultValue("GeneratedCode")]
        public string? Namespace { get; set; }

        [Description("Path to Output file")]
        [CommandOption("-o|--output")]
        [DefaultValue("Output.cs")]
        public string? OutputPath { get; set; }

        [Description("Don't add <auto-generated> header to output file")]
        [CommandOption("--no-auto-generated-header")]
        [DefaultValue(false)]
        public bool NoAutoGeneratedHeader { get; set; }

        [Description("Don't generate contract types")]
        [CommandOption("--interface-only")]
        [DefaultValue(false)]
        public bool InterfaceOnly { get; set; }

        [Description("Return Task<IApiResponse<T>> instead of Task<T>")]
        [CommandOption("--use-api-response")]
        [DefaultValue(false)]
        public bool ReturnIApiResponse { get; set; }

        [Description("Set the accessibility of the generated types to 'internal'")]
        [CommandOption("--internal")]
        [DefaultValue(false)]
        public bool InternalTypeAccessibility { get; set; }

        [Description("Use cancellation tokens")]
        [CommandOption("--cancellation-tokens")]
        [DefaultValue(false)]
        public bool UseCancellationTokens { get; set; }

        [Description("Don't generate operation headers")]
        [CommandOption("--no-operation-headers")]
        [DefaultValue(false)]
        public bool NoOperationHeaders { get; set; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.OpenApiPath))
            return ValidationResult.Error("Input file is required");

        if (IsUrl(settings.OpenApiPath))
            return base.Validate(context, settings);
        
        return File.Exists(settings.OpenApiPath)
            ? base.Validate(context, settings)
            : ValidationResult.Error($"File not found - {Path.GetFullPath(settings.OpenApiPath)}");
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var refitGeneratorSettings = new RefitGeneratorSettings
        {
            OpenApiPath = settings.OpenApiPath!,
            Namespace = settings.Namespace ?? "GeneratedCode",
            AddAutoGeneratedHeader = !settings.NoAutoGeneratedHeader,
            GenerateContracts = !settings.InterfaceOnly,
            ReturnIApiResponse = settings.ReturnIApiResponse,
            UseCancellationTokens = settings.UseCancellationTokens,
            GenerateOperationHeaders = !settings.NoOperationHeaders,
            TypeAccessibility = settings.InternalTypeAccessibility
                ? TypeAccessibility.Internal
                : TypeAccessibility.Public
        };

        try
        {
            var generator = await RefitGenerator.CreateAsync(refitGeneratorSettings);
            var code = generator.Generate();
            await File.WriteAllTextAsync(settings.OutputPath ?? "Output.cs", code);
            AnsiConsole.MarkupLine($"[green]Output: {code.Length} bytes[/]");
            return 0;
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLine($"[red]Error:{Environment.NewLine}{exception.Message}[/]");
            AnsiConsole.MarkupLine($"[yellow]Stack Trace:{Environment.NewLine}{exception.StackTrace}[/]");
            await LogError(exception, refitGeneratorSettings);
            return exception.HResult;
        }
    }

    private static async Task LogError(Exception exception, RefitGeneratorSettings refitGeneratorSettings)
    {
        exception
            .ToExceptionless(
                new ContextData(
                    JsonSerializer.Deserialize<Dictionary<string, object>>(
                        JsonSerializer.Serialize(refitGeneratorSettings))!))
            .Submit();

        await ExceptionlessClient.Default.ProcessQueueAsync();
    }

    private static bool IsUrl(string openApiPath)
    {
        return Uri.TryCreate(openApiPath, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

}