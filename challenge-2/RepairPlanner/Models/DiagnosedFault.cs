using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

public sealed class DiagnosedFault
{
    [JsonPropertyName("machineId")]
    [JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("faultType")]
    [JsonProperty("faultType")]
    public string FaultType { get; set; } = string.Empty;

    [JsonPropertyName("rootCause")]
    [JsonProperty("rootCause")]
    public string RootCause { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    [JsonProperty("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("detectedAt")]
    [JsonProperty("detectedAt")]
    public string DetectedAt { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
