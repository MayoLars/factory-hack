using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;
using RepairPlanner.Services;

namespace RepairPlanner;

public sealed class RepairPlannerAgent(
    AIProjectClient projectClient,
    CosmosDbService cosmosDb,
    IFaultMappingService faultMapping,
    string modelDeploymentName,
    ILogger<RepairPlannerAgent> logger)
{
    private const string AgentName = "RepairPlannerAgent";

    private const string AgentInstructions = """
        You are a Repair Planner Agent for tire manufacturing equipment.
        Generate a repair plan with tasks, timeline, and resource allocation.
        Return the response as valid JSON matching the WorkOrder schema.

        Output JSON with these fields:
        - workOrderNumber, machineId, title, description
        - type: "corrective" | "preventive" | "emergency"
        - priority: "critical" | "high" | "medium" | "low"
        - status: always "new"
        - assignedTo: technician id or null
        - notes: summary of the repair plan
        - estimatedDuration: integer (minutes, e.g. 60 not "60 minutes")
        - partsUsed: [{ partId, partNumber, quantity }]
        - tasks: [{ sequence, title, description, estimatedDurationMinutes (integer), requiredSkills, safetyNotes }]

        IMPORTANT: All duration fields must be integers representing minutes (e.g. 90), not strings.
        IMPORTANT: Return ONLY valid JSON, no markdown fences, no prose.

        Rules:
        - Assign the most qualified available technician
        - Include only relevant parts; empty array if none needed
        - Tasks must be ordered and actionable
        """;

    // Handle LLM returning numbers as strings
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task EnsureAgentVersionAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Creating agent '{AgentName}' with model '{Model}'", AgentName, modelDeploymentName);
        var definition = new PromptAgentDefinition(model: modelDeploymentName)
        {
            Instructions = AgentInstructions
        };
        var version = await projectClient.Agents.CreateAgentVersionAsync(
            AgentName, new AgentVersionCreationOptions(definition), ct);
        logger.LogInformation("Agent version: {Version}", version.Value.Id);
    }

    public async Task<WorkOrder> PlanAndCreateWorkOrderAsync(DiagnosedFault fault, CancellationToken ct = default)
    {
        logger.LogInformation("Planning repair for {MachineId}, fault={FaultType}", fault.MachineId, fault.FaultType);

        // 1. Get required skills and parts from mapping
        var requiredSkills = faultMapping.GetRequiredSkills(fault.FaultType).ToList();
        var requiredPartNumbers = faultMapping.GetRequiredParts(fault.FaultType).ToList();

        // 2. Query Cosmos DB for technicians and parts
        var technicians = await cosmosDb.GetAvailableTechniciansWithSkillsAsync(requiredSkills);
        var parts = await cosmosDb.GetPartsInventoryAsync(requiredPartNumbers);

        // 3. Build prompt
        var prompt = BuildPrompt(fault, technicians, parts, requiredSkills, requiredPartNumbers);

        // 4. Invoke agent
        logger.LogInformation("Invoking agent '{AgentName}'", AgentName);
        var agent = projectClient.GetAIAgent(name: AgentName);
        var response = await agent.RunAsync(prompt, thread: null, options: null, ct);
        var responseText = response.Text ?? "";

        // Strip markdown fences if present
        responseText = responseText.Trim();
        if (responseText.StartsWith("```"))
        {
            var firstNewline = responseText.IndexOf('\n');
            if (firstNewline >= 0) responseText = responseText[(firstNewline + 1)..];
            if (responseText.EndsWith("```")) responseText = responseText[..^3];
            responseText = responseText.Trim();
        }

        // 5. Parse response and apply defaults
        var workOrder = JsonSerializer.Deserialize<WorkOrder>(responseText, JsonOptions)
            ?? throw new InvalidOperationException("Agent returned null work order");

        // Apply defaults
        workOrder.Id = Guid.NewGuid().ToString();
        workOrder.Status ??= "new";
        workOrder.CreatedDate = DateTime.UtcNow.ToString("o");
        workOrder.MachineId = fault.MachineId;
        if (string.IsNullOrEmpty(workOrder.Priority)) workOrder.Priority = "medium";

        // 6. Save to Cosmos DB
        await cosmosDb.CreateWorkOrderAsync(workOrder);
        return workOrder;
    }

    private static string BuildPrompt(
        DiagnosedFault fault,
        List<Technician> technicians,
        List<Part> parts,
        List<string> requiredSkills,
        List<string> requiredPartNumbers)
    {
        var techJson = JsonSerializer.Serialize(technicians.Select(t => new
        {
            t.Id, t.Name, t.Role, t.Skills, t.Certifications, t.ShiftSchedule
        }), JsonOptions);

        var partsJson = JsonSerializer.Serialize(parts.Select(p => new
        {
            p.Id, p.PartNumber, p.Name, p.QuantityInStock, p.UnitCost, p.Location
        }), JsonOptions);

        return $"""
            Create a repair work order for the following diagnosed fault:

            Machine: {fault.MachineId}
            Fault Type: {fault.FaultType}
            Root Cause: {fault.RootCause}
            Severity: {fault.Severity}
            Detected At: {fault.DetectedAt}

            Required Skills: {string.Join(", ", requiredSkills)}
            Required Part Numbers: {string.Join(", ", requiredPartNumbers)}

            Available Technicians:
            {techJson}

            Available Parts:
            {partsJson}

            Generate a work order number in format WO-2026-XXX.
            """;
    }
}
