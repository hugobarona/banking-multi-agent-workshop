using ModelContextProtocol.Client;

namespace MultiAgentCopilot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Load configuration from appsettings.json and environment variables
            builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
                                 .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                 .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
                                 .AddEnvironmentVariables();

            // Add CORS policy
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
            });


            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            if (!builder.Environment.IsDevelopment())                
                builder.Services.AddApplicationInsightsTelemetry();

            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Services.Configure<LoggerFilterOptions>(options =>
            {
                options.MinLevel = LogLevel.Trace;
            });

            //builder.AddApplicationInsightsTelemetry();

            builder.AddCosmosDBService();
            builder.AddSemanticKernelService();

            builder.AddChatService();
            builder.Services.AddScoped<ChatEndpoints>();

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // // Create an MCPClient for the GitHub server
            // await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
            // {
            //     Name = "Azure MCP Server",
            //     Command = "npx",
            //     Arguments = ["-y", "@azure/mcp@latest", "server", "start"],
            // }));
            builder.Services.AddSingleton<IMcpClient>(serviceProvider =>
            {
                // Synchronously wait for the async factory
                return Task.Run(async () =>
                {
                    return await McpClientFactory.CreateAsync(new StdioClientTransport(new()
                    {
                        Name = "Azure MCP Server",
                        Command = "npx",
                        Arguments = new[] { "-y", "@azure/mcp@latest", "server", "start" }
                    }));
                }).GetAwaiter().GetResult();
            });


            var app = builder.Build();
            app.UseCors("AllowAllOrigins");

            app.UseExceptionHandler(exceptionHandlerApp
                    => exceptionHandlerApp.Run(async context
                        => await Results.Problem().ExecuteAsync(context)));

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseAuthorization();

            // Map the chat REST endpoints:
            using (var scope = app.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<MultiAgentCopilot.ChatEndpoints>();
                service?.Map(app);
            }

            app.Run();
        }
    }
}
