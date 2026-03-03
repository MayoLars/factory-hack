# Data Model Reference

Complete specification of the Cosmos DB data model, all container schemas, partition key strategies, and seed data structures.

## Database: `FactoryOpsDB`

## Container Definitions

### Core Factory Data (Seeded)

#### 1. Machines (partition key: `/type`)

5 machines representing different equipment in the tire manufacturing line.

```json
{
  "id": "machine-001",
  "name": "Tire Curing Press A1",
  "type": "tire_curing_press",
  "location": "Curing Department, Line A",
  "manufacturer": "TireTech Systems Inc",
  "model": "TCP-4000",
  "serialNumber": "TCP-4000-2023-001",
  "installDate": "2023-01-15",
  "status": "operational",
  "operatingHours": 12450,
  "cyclesCompleted": 45680,
  "maintenanceHistory": [
    {
      "date": "2024-11-01",
      "type": "preventive",
      "description": "Bladder inspection and heating element check",
      "technician": "John Smith"
    }
  ],
  "specifications": {
    "maxPressure": 200,
    "curingTemperature": 170,
    "operatingTemperatureRange": { "min": 150, "max": 180 },
    "cycleTime": 12,
    "powerConsumption": 150
  }
}
```

**All 5 machines:**

| ID | Name | Type | Status |
|----|------|------|--------|
| machine-001 | Tire Curing Press A1 | `tire_curing_press` | operational |
| machine-002 | Tire Building Machine B1 | `tire_building_machine` | operational |
| machine-003 | Tire Extruder C1 | `tire_extruder` | operational |
| machine-004 | Tire Uniformity Machine D1 | `tire_uniformity_machine` | maintenance_required |
| machine-005 | Banbury Mixer E1 | `banbury_mixer` | operational |

#### 2. Thresholds (partition key: `/machineType`)

Warning and critical thresholds for each machine type's telemetry metrics.

```json
{
  "id": "threshold-tire-curing-press",
  "machineType": "tire_curing_press",
  "metrics": {
    "curing_temperature": { "warning": 178, "critical": 185 },
    "cycle_time": { "warning": 14, "critical": 16 },
    "pressure": { "warning": 195, "critical": 200 }
  }
}
```

#### 3. Telemetry (partition key: `/machineId`, TTL: 30 days)

Time-series sensor readings. All 5 machines have pre-seeded warning telemetry.

```json
{
  "id": "telemetry-001-1",
  "machineId": "machine-001",
  "timestamp": "2026-01-16T10:00:00Z",
  "readings": {
    "curing_temperature": 179.2,
    "cycle_time": 14.5,
    "pressure": 175
  }
}
```

#### 4. KnowledgeBase (partition key: `/machineType`)

Machine-specific troubleshooting knowledge (supplementary to Foundry IQ).

#### 5. PartsInventory (partition key: `/category`)

Spare parts with stock levels, reorder points, and compatibility.

```json
{
  "id": "part-001",
  "partNumber": "TCP-HTR-4KW",
  "name": "Heating Element 4kW",
  "category": "electrical",
  "quantityInStock": 5,
  "reorderLevel": 2,
  "unitCost": 450.00,
  "leadTimeDays": 7,
  "location": "Warehouse A, Shelf 12",
  "compatibleMachines": ["tire_curing_press"]
}
```

#### 6. Technicians (partition key: `/department`)

Available maintenance technicians with skills and schedules.

```json
{
  "id": "tech-001",
  "name": "John Smith",
  "department": "Maintenance",
  "skills": ["tire_curing_press", "temperature_control", "electrical_systems"],
  "certifications": ["Certified Maintenance Professional"],
  "available": true,
  "shiftSchedule": "day",
  "currentAssignments": []
}
```

#### 7. WorkOrders (partition key: `/status`)

Created by the Repair Planner Agent. **Important:** partition key is `/status`, so updating the status requires delete + reinsert.

```json
{
  "id": "wo-2024-468",
  "workOrderNumber": "WO-2026-001",
  "machineId": "machine-001",
  "title": "Repair curing temperature issue",
  "description": "Heating element malfunction causing excessive temperatures",
  "type": "corrective",
  "priority": "high",
  "status": "new",
  "faultType": "curing_temperature_excessive",
  "assignedTo": "tech-001",
  "createdDate": "2026-01-16T12:00:00Z",
  "estimatedDuration": 90,
  "partsUsed": [
    { "partId": "part-001", "partNumber": "TCP-HTR-4KW", "quantity": 1 }
  ],
  "tasks": [
    {
      "sequence": 1,
      "title": "Inspect heating elements",
      "description": "Visual and electrical inspection of all heating elements",
      "estimatedDurationMinutes": 30,
      "requiredSkills": ["electrical_systems", "temperature_control"],
      "safetyNotes": "Ensure machine is powered off and cooled down"
    }
  ]
}
```

#### 8. MaintenanceHistory (partition key: `/machineId`)

Historical maintenance records for MTBF analysis.

```json
{
  "id": "mh-001",
  "machineId": "machine-001",
  "faultType": "curing_temperature_excessive",
  "occurrenceDate": "2024-06-15T08:30:00Z",
  "resolutionDate": "2024-06-15T14:00:00Z",
  "downtime": 330,
  "cost": 2500.00
}
```

#### 9. MaintenanceWindows (partition key: `/isAvailable`)

Available time slots for scheduling maintenance.

```json
{
  "id": "mw-2026-01-17-night",
  "startTime": "2026-01-17T22:00:00Z",
  "endTime": "2026-01-18T06:00:00Z",
  "productionImpact": "Low",
  "isAvailable": true
}
```

### Agent-Created Containers (Created at Runtime)

#### 10. ChatHistories (partition key: `/entityId`)

Persistent memory for Challenge 3 agents. Stores conversation history per machine or work order.

```json
{
  "id": "machine-001",
  "entityId": "machine-001",
  "entityType": "machine",
  "historyJson": "[{\"role\":\"user\",\"content\":\"...\"},{\"role\":\"assistant\",\"content\":\"...\"}]",
  "purpose": "predictive_maintenance",
  "updatedAt": "2026-01-16T15:30:00Z"
}
```

**Key design decisions:**
- `id` = `entityId` = machine ID or work order ID (point reads)
- `historyJson` stores a JSON array of `{role, content}` messages
- Agents keep only the last 10 messages to prevent context overflow
- Last 5 messages are prepended to agent prompts for continuity

#### 11. MaintenanceSchedules (partition key: `/id`)

Output from the Maintenance Scheduler Agent.

```json
{
  "id": "sched-1705412345.678",
  "workOrderId": "wo-2024-468",
  "machineId": "machine-001",
  "scheduledDate": "2026-01-17T22:00:00Z",
  "maintenanceWindow": {
    "id": "mw-2026-01-17-night",
    "startTime": "2026-01-17T22:00:00Z",
    "endTime": "2026-01-18T06:00:00Z",
    "productionImpact": "Low",
    "isAvailable": true
  },
  "riskScore": 75,
  "predictedFailureProbability": 0.45,
  "recommendedAction": "URGENT",
  "reasoning": "Based on MTBF analysis...",
  "createdAt": "2026-01-16T15:30:00Z"
}
```

#### 12. PartsOrders (partition key: `/id`)

Output from the Parts Ordering Agent.

```json
{
  "id": "order-1705412345.678",
  "workOrderId": "wo-2024-468",
  "orderItems": [
    {
      "partNumber": "TCP-HTR-4KW",
      "partName": "Heating Element 4kW",
      "quantity": 2,
      "unitCost": 450.00,
      "totalCost": 900.00
    }
  ],
  "supplierId": "supplier-001",
  "supplierName": "Industrial Parts Supply Co.",
  "totalCost": 900.00,
  "expectedDeliveryDate": "2026-01-20T00:00:00Z",
  "orderStatus": "Pending",
  "createdAt": "2026-01-16T15:35:00Z"
}
```

## Fault Type Taxonomy

These fault types are the canonical vocabulary used across all agents:

| Fault Type | Machine Type | Associated Parts |
|-----------|-------------|-----------------|
| `curing_temperature_excessive` | tire_curing_press | TCP-HTR-4KW, GEN-TS-K400 |
| `curing_cycle_time_deviation` | tire_curing_press | TCP-BLD-800, TCP-SEAL-200 |
| `building_drum_vibration` | tire_building_machine | TBM-BRG-6220 |
| `ply_tension_excessive` | tire_building_machine | TBM-LS-500N, TBM-SRV-5KW |
| `extruder_barrel_overheating` | tire_extruder | EXT-HTR-BAND, GEN-TS-K400 |
| `low_material_throughput` | tire_extruder | EXT-SCR-250, EXT-DIE-TR |
| `high_radial_force_variation` | tire_uniformity_machine | (none) |
| `load_cell_drift` | tire_uniformity_machine | TUM-LC-2KN, TUM-ENC-5000 |
| `mixing_temperature_excessive` | banbury_mixer | BMX-TIP-500, GEN-TS-K400 |
| `excessive_mixer_vibration` | banbury_mixer | BMX-BRG-22320, BMX-SEAL-DP |

## Fault-to-Skills Mapping

Used by the Repair Planner Agent's `FaultMappingService`:

| Fault Type | Required Skills |
|-----------|----------------|
| `curing_temperature_excessive` | tire_curing_press, temperature_control, instrumentation, electrical_systems, plc_troubleshooting, mold_maintenance |
| `curing_cycle_time_deviation` | tire_curing_press, plc_troubleshooting, mold_maintenance, bladder_replacement, hydraulic_systems, instrumentation |
| `building_drum_vibration` | tire_building_machine, vibration_analysis, bearing_replacement, alignment, precision_alignment, drum_balancing, mechanical_systems |
| `ply_tension_excessive` | tire_building_machine, tension_control, servo_systems, precision_alignment, sensor_alignment, plc_programming |
| `extruder_barrel_overheating` | tire_extruder, temperature_control, rubber_processing, screw_maintenance, instrumentation, electrical_systems, motor_drives |
| `low_material_throughput` | tire_extruder, rubber_processing, screw_maintenance, motor_drives, temperature_control |
| `high_radial_force_variation` | tire_uniformity_machine, data_analysis, measurement_systems, tire_building_machine, tire_curing_press |
| `load_cell_drift` | tire_uniformity_machine, load_cell_calibration, measurement_systems, sensor_alignment, instrumentation |
| `mixing_temperature_excessive` | banbury_mixer, temperature_control, rubber_processing, instrumentation, electrical_systems, mechanical_systems |
| `excessive_mixer_vibration` | banbury_mixer, vibration_analysis, bearing_replacement, alignment, mechanical_systems, preventive_maintenance |

**Default (unknown fault):** skills = `["general_maintenance"]`, parts = `[]`

## Partition Key Strategy Notes

| Container | Key | Rationale |
|-----------|-----|-----------|
| Machines | `/type` | Queries by machine type (e.g., "all curing presses") |
| Thresholds | `/machineType` | Direct lookup by machine type |
| Telemetry | `/machineId` | Time-series queries always scoped to one machine |
| WorkOrders | `/status` | **Caution:** Status changes require delete+reinsert |
| MaintenanceHistory | `/machineId` | History queries always per-machine |
| MaintenanceWindows | `/isAvailable` | Filter available windows |
| ChatHistories | `/entityId` | Point reads by machine/work order ID |
| MaintenanceSchedules | `/id` | Simple key for upsert |
| PartsOrders | `/id` | Simple key for upsert |
