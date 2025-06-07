

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

using OpenAI.Chat;
using System.Text.Json;
using MultiAgentCopilot.StructuredFormats;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Logs;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Services;
using static MultiAgentCopilot.StructuredFormats.ChatResponseFormatBuilder;
using MultiAgentCopilot.Plugins;
using ModelContextProtocol.Client;


namespace MultiAgentCopilot.Factories
{
    internal class AgentFactory
    {
        private string GetAgentName(AgentType agentType)
        {

            string name = string.Empty;
            switch (agentType)
            {
                case AgentType.Sales:
                    name = "Sales";
                    break;
                case AgentType.Transactions:
                    name = "Transactions";
                    break;
                case AgentType.CustomerSupport:
                    name = "CustomerSupport";
                    break;
                case AgentType.Coordinator:
                    name = "Coordinator";
                    break;
                case AgentType.MortgageAdvisorMCP:
                    name = "MortgageAdvisorMCP";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null);
            }

            return name;
        }


        private string GetAgentPrompts(AgentType agentType)
        {

            string promptFile = string.Empty;
            switch (agentType)
            {
                case AgentType.Sales:
                    promptFile = "Sales.prompty";
                    break;
                case AgentType.Transactions:
                    promptFile = "Transactions.prompty";
                    break;
                case AgentType.CustomerSupport:
                    promptFile = "CustomerSupport.prompty";
                    break;
                case AgentType.Coordinator:
                    promptFile = "Coordinator.prompty";
                    break;
                case AgentType.MortgageAdvisorMCP:
                    promptFile = "MortgageAdvisorMCP.prompty";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null);
            }

            string prompt = $"{File.ReadAllText("Prompts/" + promptFile)}{File.ReadAllText("Prompts/CommonAgentRules.prompty")}";

            return prompt;
        }


        public ChatCompletionAgent BuildAgent(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, BankingDataService bankService, string tenantId, string userId, IMcpClient mcpClient)
        {
            ChatCompletionAgent agent = new ChatCompletionAgent
            {
                Name = GetAgentName(agentType),
                Instructions = $"""{GetAgentPrompts(agentType)}""",
                Kernel = GetAgentKernel(kernel, agentType, loggerFactory, bankService, tenantId, userId, mcpClient),
                Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
            };

            return agent;
        }

        private Kernel GetAgentKernel(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, BankingDataService bankService, string tenantId, string userId, IMcpClient mcpClient)
        {
            Kernel agentKernel = kernel.Clone();
            switch (agentType)
            {
                case AgentType.Sales:
                    var salesPlugin = new SalesPlugin(loggerFactory.CreateLogger<SalesPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(salesPlugin);
                    break;
                case AgentType.Transactions:
                    var transactionsPlugin = new TransactionPlugin(loggerFactory.CreateLogger<TransactionPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(transactionsPlugin);
                    break;
                case AgentType.CustomerSupport:
                    var customerSupportPlugin = new CustomerSupportPlugin(loggerFactory.CreateLogger<CustomerSupportPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(customerSupportPlugin);
                    break;
                case AgentType.Coordinator:
                    var CoordinatorPlugin = new CoordinatorPlugin(loggerFactory.CreateLogger<CoordinatorPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(CoordinatorPlugin);
                    break;
                case AgentType.MortgageAdvisorMCP:
                    var MortgageAdvisorMCPPlugin = new MortgageAdvisorMCPPlugin(loggerFactory.CreateLogger<MortgageAdvisorMCPPlugin>(), bankService, tenantId, userId, mcpClient);
                    var tools = MortgageAdvisorMCPPlugin.GetAzureMcpTools(mcpClient).GetAwaiter().GetResult();
                    agentKernel.Plugins.AddFromFunctions("MAMcp", tools.Select(aiFunction => aiFunction.WithName(aiFunction.Name.Replace('-', '_')).AsKernelFunction()));
                    break;
                default:
                    throw new ArgumentException("Invalid plugin name");
            }

            return agentKernel;
        }
        
               
       public delegate void LogCallback(string key, string value);
  
       public static string GetStrategyPrompts(ChatResponseStrategy strategyType)
       {
          string prompt = string.Empty;
          switch (strategyType)
          {
                case ChatResponseStrategy.Continuation:
                   prompt = File.ReadAllText("Prompts/SelectionStrategy.prompty");
                   break;
                case ChatResponseStrategy.Termination:
                   prompt = File.ReadAllText("Prompts/TerminationStrategy.prompty");
                   break;
          }
          return prompt;
       }

       public AgentGroupChat BuildAgentGroupChat(Kernel kernel, ILoggerFactory loggerFactory, LogCallback logCallback, BankingDataService bankService, string tenantId, string userId, IMcpClient mcpClient)
        {
            AgentGroupChat agentGroupChat = new AgentGroupChat();
            var chatModel = kernel.GetRequiredService<IChatCompletionService>();

            kernel.AutoFunctionInvocationFilters.Add(new AutoFunctionInvocationLoggingFilter(loggerFactory.CreateLogger<AutoFunctionInvocationLoggingFilter>()));

            foreach (AgentType agentType in Enum.GetValues(typeof(AgentType)))
            {
                agentGroupChat.AddAgent(BuildAgent(kernel, agentType, loggerFactory, bankService, tenantId, userId, mcpClient));
            }

            agentGroupChat.ExecutionSettings = GetAgentGroupChatSettings(kernel, logCallback);


            return agentGroupChat;
        }

        private OpenAIPromptExecutionSettings GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStrategy strategyType)
        {
            ChatResponseFormat infoFormat;
            infoFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: $"agent_result_{strategyType.ToString()}",
            jsonSchema: BinaryData.FromString($"""
                {ChatResponseFormatBuilder.BuildFormat(strategyType)}
                """));
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = infoFormat
            };

            return executionSettings;
        }

        private KernelFunction GetStrategyFunction(ChatResponseFormatBuilder.ChatResponseStrategy strategyType)
        {

            KernelFunction function =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""
                    {{{GetStrategyPrompts(strategyType)}}}
                    
                    RESPONSE:
                    {{$lastmessage}}
                    """,
                    safeParameterNames: "lastmessage");

            return function;
        }

        private AgentGroupChatSettings GetAgentGroupChatSettings(Kernel kernel, LogCallback logCallback)
        {
            ChatHistoryTruncationReducer historyReducer = new(5);

            AgentGroupChatSettings ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy =
                    new KernelFunctionSelectionStrategy(GetStrategyFunction(ChatResponseFormatBuilder.ChatResponseStrategy.Continuation), kernel)
                    {
                        Arguments = new KernelArguments(GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStrategy.Continuation)),
                        // Save tokens by only including the final few responses
                        HistoryReducer = historyReducer,
                        // The prompt variable name for the history argument.
                        HistoryVariableName = "lastmessage",
                        // Returns the entire result value as a string.
                        ResultParser = (result) =>
                        {
                            var resultString = result.GetValue<string>();
                            if (!string.IsNullOrEmpty(resultString))
                            {
                                var ContinuationInfo = JsonSerializer.Deserialize<ContinuationInfo>(resultString);
                                logCallback("SELECTION - Agent", ContinuationInfo!.AgentName); 
                                logCallback("SELECTION - Reason", ContinuationInfo!.Reason);                       
                                return ContinuationInfo!.AgentName;
                            }
                            else
                            {
                                return string.Empty;
                            }
                        }
                    },
                TerminationStrategy =
                    new KernelFunctionTerminationStrategy(GetStrategyFunction(ChatResponseFormatBuilder.ChatResponseStrategy.Termination), kernel)
                    {
                        Arguments = new KernelArguments(GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStrategy.Termination)),
                        // Save tokens by only including the final response
                        HistoryReducer = historyReducer,
                        // The prompt variable name for the history argument.
                        HistoryVariableName = "lastmessage",
                        // Limit total number of turns
                        MaximumIterations = 8,
                        // user result parser to determine if the response is "yes"
                        ResultParser = (result) =>
                        {
                            var resultString = result.GetValue<string>();
                            if (!string.IsNullOrEmpty(resultString))
                            {
                                var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(resultString);
                                logCallback("TERMINATION - Continue", terminationInfo!.ShouldContinue.ToString()); 
                                logCallback("TERMINATION - Reason", terminationInfo!.Reason); 
                                return !terminationInfo!.ShouldContinue;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    },
            };

            return ExecutionSettings;
        }

    }
}