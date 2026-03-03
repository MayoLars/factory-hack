using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepairPlanner;
using RepairPlanner.Models;
using RepairPlanner.Services;

// Read environment variables
var projectEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_AI_PROJECT_ENDPOINT not set");
var modelDeploymentName = Environment.GetEnvironmentVariable("MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME not set");
var cosmosEndpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")
    ?? throw new InvalidOperationException("COSMOS_ENDPOINT not set");
var cosmosKey = Environment.GetEnvironmentVariable("COSMOS_KEY")
    ?? throw new InvalidOperationException("COSMOS_KEY not set");
var cosmosDatabaseName = Environment.GetEnvironmentVariable("COSMOS_DATABASE_NAME")
    ?? throw new InvalidOperationException("COSMOS_DATABASE_NAME not set");

// Set up DI and logging
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
services.AddSingleton(new CosmosDbOptions
{
    Endpoint = cosmosEndpoint,
    Key = cosmosKey,
    DatabaseName = cosmosDatabaseName
});
services.AddSingleton<IFaultMappingService, FaultMappingService>();
services.AddSingleton<CosmosDbService>();
services.AddSingleton(_ => new AIProjectClient(new Uri(projectEndpoint), new DefaultAzureCredential()));
services.AddSingleton(sp => new RepairPlannerAgent(
    sp.GetRequiredService<AIProjectClient>(),
    sp.GetRequiredService<CosmosDbService>(),
    sp.GetRequiredService<IFaultMappingService>(),
    modelDeploymentName,
    sp.GetRequiredService<ILogger<RepairPlannerAgent>>()));

// Build and run
await using var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<Program>>();
var agent = provider.GetRequiredService<RepairPlannerAgent>();

// Register agent version
await agent.EnsureAgentVersionAsync();

// Create sample fault (output from Challenge 1's Fault Diagnosis Agent)
var sampleFault = new DiagnosedFault
{
    MachineId = "machine-001",
    FaultType = "curing_temperature_excessive",
    RootCause = "Heating element malfunction",
    Severity = "High",
    DetectedAt = DateTime.UtcNow.ToString("o"),
    Metadata = new Dictionary<string, object>
    {
        ["ObservedTemperature"] = 179.2,
        ["Threshold"] = 178,
        ["machineType"] = "tire_curing_press"
    }
};

// Run the repair planner
var workOrder = await agent.PlanAndCreateWorkOrderAsync(sampleFault);

// Display result
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
};
logger.LogInformation("Saved work order {WoNumber} (id={Id}, status={Status}, assignedTo={AssignedTo})",
    workOrder.WorkOrderNumber, workOrder.Id, workOrder.Status, workOrder.AssignedTo);
Console.WriteLine(JsonSerializer.Serialize(workOrder, jsonOptions));
