namespace IronMagnate.Core.Production;

using IronMagnate.Core.Resources;
using IronMagnate.Core.Storage;
using IronMagnate.Core.Workforce;

public sealed class ProductionCoordinator
{
    private readonly Dictionary<Guid, Machine> machines = [];

    public bool RegisterMachine(Machine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);

        return machines.TryAdd(machine.Id, machine);
    }

    public Machine? GetMachine(Guid machineId)
    {
        return machines.GetValueOrDefault(machineId);
    }

    public int EnableMachine(Guid machineId, Workforce workforce)
    {
        ArgumentNullException.ThrowIfNull(workforce);

        var machine = GetMachine(machineId);
        if (machine is null)
        {
            return 0;
        }

        machine.Enable();
        return FillCrew(machineId, workforce);
    }

    public bool DisableMachine(Guid machineId, Workforce workforce)
    {
        ArgumentNullException.ThrowIfNull(workforce);

        var machine = GetMachine(machineId);
        if (machine is null)
        {
            return false;
        }

        machine.Disable();
        workforce.ReleaseWorkstation(machine.Id.ToString());
        FillEnabledMachines(workforce);
        return true;
    }

    public bool IsFullyStaffed(Guid machineId, Workforce workforce)
    {
        ArgumentNullException.ThrowIfNull(workforce);

        var machine = GetMachine(machineId);

        if (machine is null)
        {
            return false;
        }

        var assignedCount = workforce.Assignments.Count(
            assigment => assigment.WorkstationId == machineId.ToString()
        );

        return assignedCount == machine.RequiredCrew;
    }

    public int FillCrew(Guid machineId, Workforce workforce)
    {
        ArgumentNullException.ThrowIfNull(workforce);

        var machine = GetMachine(machineId);

        if (machine is null || !machine.IsEnabled)
        {
            return 0;
        }

        var workstationId = machine.Id.ToString();
        var assignedBefore = workforce.Assignments.Count(
            assignment => assignment.WorkstationId == workstationId);

        if (!workforce.TryAllocateCrew(
                workstationId,
                machine.RequiredCrew,
                out var assignedCrew))
        {
            return 0;
        }

        return assignedCrew.Count - assignedBefore;
    }

    public bool TryAssignWorker(
        Guid machineId,
        Guid workerId,
        Workforce workforce
    )
    {
        ArgumentNullException.ThrowIfNull(workforce);
        Machine? machine = GetMachine(machineId);

        if (machine is null)
        {
            return false;
        }

        string workstationId = machine.Id.ToString();

        int assignedCount = workforce.Assignments.Count(
            assignment => assignment.WorkstationId == workstationId
        );

        if (assignedCount >= machine.RequiredCrew)
        {
            return false;
        }

        return workforce.TryAssign(workerId, workstationId);
    }

    public int FillEnabledMachines(Workforce workforce)
    {
        ArgumentNullException.ThrowIfNull(workforce);

        int totalAssigned = 0;

        foreach (var machine in machines.Values.OrderBy(m => m.Id))
        {
            if (!machine.IsEnabled)
                continue;

            totalAssigned += FillCrew(machine.Id, workforce);
        }

        return totalAssigned;
    }

    public bool CanOperate(
    Guid machineId,
    Workforce workforce,
    bool hasPower)
    {
        ArgumentNullException.ThrowIfNull(workforce);

        Machine? machine = GetMachine(machineId);

        if (machine is null)
            return false;

        if (!machine.IsEnabled)
            return false;

        if (!hasPower)
            return false;

        return IsFullyStaffed(machineId, workforce);
    }

    public bool TryStartCycle(
        Guid machineId,
        Workforce workforce,
        bool hasPower,
        Storage inputStorage)
    {
        ArgumentNullException.ThrowIfNull(workforce);
        ArgumentNullException.ThrowIfNull(inputStorage);

        var machine = GetMachine(machineId);
        if (machine is null || machine.ActiveCycle is not null)
        {
            return false;
        }

        if (!CanOperate(machineId, workforce, hasPower))
        {
            return false;
        }

        var schematic = machine.SelectedSchematic;
        var requirements = schematic.Inputs
            .Select(input => new ResourceQuantity(input.Key, input.Value))
            .ToList();

        if (!HasAvailableInputs(inputStorage, requirements) ||
            !machine.OutputBuffer.CanReserve(schematic.OutputQuantity))
        {
            return false;
        }

        if (!machine.OutputBuffer.TryReserve(schematic.OutputQuantity))
        {
            return false;
        }

        try
        {
            if (!inputStorage.TryWithdraw(requirements))
            {
                machine.OutputBuffer.Release(schematic.OutputQuantity);
                return false;
            }
        }
        catch
        {
            machine.OutputBuffer.Release(schematic.OutputQuantity);
            throw;
        }

        machine.StartCycle();
        return true;
    }

    private static bool HasAvailableInputs(
        Storage inputStorage,
        IReadOnlyCollection<ResourceQuantity> requirements)
    {
        return requirements.All(requirement =>
        {
            var resource = ResourceCatalog.Get(requirement.ResourceType);
            return inputStorage.AcceptedCategories.Contains(resource.Category) &&
                inputStorage.GetQuantity(requirement.ResourceType) >= requirement.Quantity;
        });
    }

}
