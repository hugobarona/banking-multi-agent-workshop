// Import packages
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;

// Populate values from your OpenAI deployment
var modelId = "gpt-4o";
var endpoint = "";
var apiKey = "";

// Create a kernel with Azure OpenAI chat completion
var builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);

// Add enterprise components
builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Build the kernel
Kernel kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// Create an MCPClient for the GitHub server
await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
{
    Name = "Azure MCP Server",
    Command = "npx",
    Arguments = new[] { "-y", "@azure/mcp@latest", "server", "start" }
}));

// Retrieve the list of tools available on the Azure MCP server
var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
foreach (var tool in tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
kernel.Plugins.AddFromFunctions("AzMCPPG", tools.Select(aiFunction => aiFunction.WithName(aiFunction.Name.Replace('-', '_')).AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Enable automatic function calling
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
OpenAIPromptExecutionSettings executionSettings = new()
{
    Temperature = 0,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
};
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.


// Add a plugin (the LightsPlugin class is defined below)
kernel.Plugins.AddFromType<LightsPlugin>("Lights");

// Enable planning
OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new() 
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// Create a history store the conversation
var history = new ChatHistory();

// Initiate a back-and-forth chat
string? userInput;
do {
    // Collect user input
    Console.Write("User > ");
    userInput = Console.ReadLine();

    // Add user input
    history.AddUserMessage(userInput);

    // Get the response from the AI
    var result = await chatCompletionService.GetChatMessageContentAsync(
        history,
        executionSettings: openAIPromptExecutionSettings,
        kernel: kernel);

    // Print the results
    Console.WriteLine("Assistant > " + result);

    // Add the message from the agent to the chat history
    history.AddMessage(result.Role, result.Content ?? string.Empty);
} while (userInput is not null);