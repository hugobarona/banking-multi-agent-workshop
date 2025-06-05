using Microsoft.SemanticKernel;
using System.ComponentModel;
using MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;
using ModelContextProtocol.Client;

namespace MultiAgentCopilot.Plugins
{
    public class MortgageAdvisorMCPPlugin : BasePlugin
    {
        private readonly IMcpClient _mcpClient;
        public MortgageAdvisorMCPPlugin(ILogger<BasePlugin> logger, BankingDataService bankService, string tenantId, string userId, IMcpClient mcpClient)
         : base(logger, bankService, tenantId, userId)
        {
            _mcpClient = mcpClient;
        }

        public async Task<IList<McpClientTool>> GetAzureMcpTools(IMcpClient mcpClient)
        {
            var tools = await mcpClient.ListToolsAsync();
            foreach (var tool in tools)
            {
                Console.WriteLine($"{tool.Name}: {tool.Description}");
            }
            Console.WriteLine();

            return tools;
        }

    }
}
