using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;

namespace RepairPlanner.Services;

public sealed class CosmosDbService
{
    private readonly Container _techniciansContainer;
    private readonly Container _partsContainer;
    private readonly Container _workOrdersContainer;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(CosmosDbOptions options, ILogger<CosmosDbService> logger)
    {
        _logger = logger;
        var client = new CosmosClient(options.Endpoint, options.Key);
        var database = client.GetDatabase(options.DatabaseName);
        _techniciansContainer = database.GetContainer("Technicians");
        _partsContainer = database.GetContainer("PartsInventory");
        _workOrdersContainer = database.GetContainer("WorkOrders");
    }

    public async Task<List<Technician>> GetAvailableTechniciansWithSkillsAsync(List<string> requiredSkills)
    {
        // Query all available technicians, then filter by skills in-memory
        // (Cosmos DB doesn't support ARRAY_CONTAINS with a parameter list directly)
        var query = new QueryDefinition("SELECT * FROM c WHERE c.available = true");
        var iterator = _techniciansContainer.GetItemQueryIterator<Technician>(query);
        var technicians = new List<Technician>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            technicians.AddRange(response);
        }

        // Filter: technician must have at least one of the required skills
        var matched = technicians
            .Where(t => t.Skills.Any(s => requiredSkills.Contains(s, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        _logger.LogInformation("Found {Count} available technicians matching skills", matched.Count);
        return matched;
    }

    public async Task<List<Part>> GetPartsInventoryAsync(List<string> partNumbers)
    {
        if (partNumbers.Count == 0) return [];

        // Query parts by part numbers using cross-partition query
        var parts = new List<Part>();
        foreach (var partNumber in partNumbers)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.partNumber = @partNumber")
                .WithParameter("@partNumber", partNumber);
            var iterator = _partsContainer.GetItemQueryIterator<Part>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                parts.AddRange(response);
            }
        }

        _logger.LogInformation("Fetched {Count} parts", parts.Count);
        return parts;
    }

    public async Task<string> CreateWorkOrderAsync(WorkOrder workOrder)
    {
        var response = await _workOrdersContainer.CreateItemAsync(workOrder, new PartitionKey(workOrder.Status));
        _logger.LogInformation("Created work order {Id} with status {Status}", response.Resource.Id, response.Resource.Status);
        return response.Resource.Id;
    }
}
