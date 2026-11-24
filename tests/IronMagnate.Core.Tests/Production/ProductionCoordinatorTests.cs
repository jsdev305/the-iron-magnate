using IronMagnate.Core.Production;
using IronMagnate.Core.Resources;
using IronMagnate.Core.Storage;
using FactoryWorkforce = IronMagnate.Core.Workforce.Workforce;
using SupplyStorage = IronMagnate.Core.Storage.Storage;
using Xunit;

namespace IronMagnate.Core.Tests.Production;

public class ProductionCoordinatorTests
{
    [Fact]
    public void RegistersAValidMachine()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine();

        var registered = coordinator.RegisterMachine(machine);

        Assert.True(registered);
        Assert.Same(machine, coordinator.GetMachine(machine.Id));
    }

    [Fact]
    public void RejectsRegisteringTheSameMachineTwice()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine();

        coordinator.RegisterMachine(machine);

        Assert.False(coordinator.RegisterMachine(machine));
        Assert.Same(machine, coordinator.GetMachine(machine.Id));
    }

    [Fact]
    public void RejectsADifferentMachineWithAnAlreadyRegisteredId()
    {
        var coordinator = new ProductionCoordinator();
        var machineId = Guid.NewGuid();
        var registeredMachine = CreateMachine(machineId, "Stamping press");
        var duplicateIdMachine = CreateMachine(machineId, "Rolling mill");

        coordinator.RegisterMachine(registeredMachine);

        Assert.False(coordinator.RegisterMachine(duplicateIdMachine));
        Assert.Same(registeredMachine, coordinator.GetMachine(machineId));
    }

    [Fact]
    public void RejectsNullMachineRegistration()
    {
        var coordinator = new ProductionCoordinator();

        Assert.Throws<ArgumentNullException>(() => coordinator.RegisterMachine(null!));
    }

    [Fact]
    public void RetrievesARegisteredMachineById()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine();
        coordinator.RegisterMachine(machine);

        var retrievedMachine = coordinator.GetMachine(machine.Id);

        Assert.Same(machine, retrievedMachine);
    }

    [Fact]
    public void ReturnsNullWhenRetrievingAnUnknownMachine()
    {
        var coordinator = new ProductionCoordinator();

        Assert.Null(coordinator.GetMachine(Guid.NewGuid()));
    }

    [Fact]
    public void FailedRegistrationDoesNotModifyExistingRegistryEntries()
    {
        var coordinator = new ProductionCoordinator();
        var firstMachine = CreateMachine();
        var secondMachine = CreateMachine();
        var duplicateIdMachine = CreateMachine(firstMachine.Id, "Rolling mill");
        coordinator.RegisterMachine(firstMachine);
        coordinator.RegisterMachine(secondMachine);

        coordinator.RegisterMachine(duplicateIdMachine);

        Assert.Same(firstMachine, coordinator.GetMachine(firstMachine.Id));
        Assert.Same(secondMachine, coordinator.GetMachine(secondMachine.Id));
    }

    [Fact]
    public void IsFullyStaffedReturnsFalseWhenNoWorkersAreAssigned()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        coordinator.RegisterMachine(machine);

        Assert.False(coordinator.IsFullyStaffed(machine.Id, CreateWorkforce(5)));
    }

    [Fact]
    public void IsFullyStaffedReturnsFalseWhenOnlyPartOfTheCrewIsAssigned()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        var workforce = CreateWorkforce(5);
        AssignWorkers(workforce, machine.Id, 3);
        coordinator.RegisterMachine(machine);

        Assert.False(coordinator.IsFullyStaffed(machine.Id, workforce));
    }

    [Fact]
    public void IsFullyStaffedReturnsTrueWhenTheCompleteCrewIsAssigned()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        var workforce = CreateWorkforce(5);
        AssignWorkers(workforce, machine.Id, 5);
        coordinator.RegisterMachine(machine);

        Assert.True(coordinator.IsFullyStaffed(machine.Id, workforce));
    }

    [Fact]
    public void IsFullyStaffedDoesNotCountWorkersAssignedToAnotherMachine()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        var workforce = CreateWorkforce(6);
        AssignWorkers(workforce, machine.Id, 4);
        Assert.True(workforce.TryAssign(workforce.Employees[4].Id, Guid.NewGuid().ToString()));
        coordinator.RegisterMachine(machine);

        Assert.False(coordinator.IsFullyStaffed(machine.Id, workforce));
    }

    [Fact]
    public void IsFullyStaffedReturnsFalseForAnUnknownMachine()
    {
        var coordinator = new ProductionCoordinator();

        Assert.False(coordinator.IsFullyStaffed(Guid.NewGuid(), CreateWorkforce(5)));
    }

    [Fact]
    public void IsFullyStaffedReturnsTrueForADisabledMachineWithACompleteCrew()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        var workforce = CreateWorkforce(5);
        AssignWorkers(workforce, machine.Id, 5);
        machine.Disable();
        coordinator.RegisterMachine(machine);

        Assert.True(coordinator.IsFullyStaffed(machine.Id, workforce));
    }

    [Fact]
    public void IsFullyStaffedRejectsANullWorkforce()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        coordinator.RegisterMachine(machine);

        Assert.Throws<ArgumentNullException>(() => coordinator.IsFullyStaffed(machine.Id, null!));
    }

    [Fact]
    public void IsFullyStaffedDoesNotChangeWorkforceAssignments()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        var workforce = CreateWorkforce(5);
        AssignWorkers(workforce, machine.Id, 3);
        var assignmentsBeforeReadinessCheck = workforce.Assignments;
        coordinator.RegisterMachine(machine);

        coordinator.IsFullyStaffed(machine.Id, workforce);

        Assert.Equal(assignmentsBeforeReadinessCheck, workforce.Assignments);
    }

    [Fact]
    public void IsFullyStaffedReturnsFalseAfterAnAssignedWorkerIsDismissed()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        var workforce = CreateWorkforce(5);
        AssignWorkers(workforce, machine.Id, 5);
        coordinator.RegisterMachine(machine);

        Assert.True(workforce.Dismiss(workforce.Employees[0].Id));

        Assert.False(coordinator.IsFullyStaffed(machine.Id, workforce));
    }

    [Fact]
    public void CanOperateReturnsTrueForARegisteredEnabledFullyStaffedPoweredMachine()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(2);
        AssignWorkers(workforce, machine.Id, 2);

        Assert.True(coordinator.CanOperate(machine.Id, workforce, hasPower: true));
    }

    [Fact]
    public void CanOperateReturnsFalseForAnUnknownMachine()
    {
        var coordinator = new ProductionCoordinator();

        Assert.False(coordinator.CanOperate(Guid.NewGuid(), CreateWorkforce(2), hasPower: true));
    }

    [Fact]
    public void CanOperateReturnsFalseForADisabledMachineEvenWhenFullyStaffedAndPowered()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 2);
        var workforce = CreateWorkforce(2);
        AssignWorkers(workforce, machine.Id, 2);
        coordinator.RegisterMachine(machine);

        Assert.False(coordinator.CanOperate(machine.Id, workforce, hasPower: true));
    }

    [Fact]
    public void CanOperateReturnsFalseWhenPowerIsUnavailableEvenWhenOtherRequirementsPass()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(2);
        AssignWorkers(workforce, machine.Id, 2);

        Assert.False(coordinator.CanOperate(machine.Id, workforce, hasPower: false));
    }

    [Fact]
    public void CanOperateReturnsFalseWhenNoWorkersAreAssigned()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 2);

        Assert.False(coordinator.CanOperate(machine.Id, CreateWorkforce(2), hasPower: true));
    }

    [Fact]
    public void CanOperateReturnsFalseWhenMachineIsPartiallyStaffed()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(2);
        AssignWorkers(workforce, machine.Id, 1);

        Assert.False(coordinator.CanOperate(machine.Id, workforce, hasPower: true));
    }

    [Fact]
    public void CanOperateReturnsTrueWhenExactlyTheRequiredCrewIsAssigned()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 3);
        var workforce = CreateWorkforce(3);
        AssignWorkers(workforce, machine.Id, 3);

        Assert.True(coordinator.CanOperate(machine.Id, workforce, hasPower: true));
    }

    [Fact]
    public void CanOperateReturnsFalseWhenMachineIsOverstaffed()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(3);
        AssignWorkers(workforce, machine.Id, 3);

        Assert.False(coordinator.CanOperate(machine.Id, workforce, hasPower: true));
    }

    [Fact]
    public void CanOperateRejectsANullWorkforce()
    {
        var coordinator = new ProductionCoordinator();

        Assert.Throws<ArgumentNullException>(() => coordinator.CanOperate(Guid.NewGuid(), null!, hasPower: true));
    }

    [Fact]
    public void CanOperateDoesNotMutateMachineStateOrWorkforceAssignments()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(2);
        AssignWorkers(workforce, machine.Id, 2);
        var assignmentsBeforeChecks = workforce.Assignments;
        var selectedSchematicBeforeChecks = machine.SelectedSchematic;
        var enabledBeforeChecks = machine.IsEnabled;

        Assert.True(coordinator.CanOperate(machine.Id, workforce, hasPower: true));
        Assert.True(coordinator.CanOperate(machine.Id, workforce, hasPower: true));

        Assert.Equal(assignmentsBeforeChecks, workforce.Assignments);
        Assert.Same(selectedSchematicBeforeChecks, machine.SelectedSchematic);
        Assert.Equal(enabledBeforeChecks, machine.IsEnabled);
    }

    [Fact]
    public void FillCrewAssignsOnlyTheTwoVacanciesWhenThreeOfFiveWorkersAreAlreadyAssigned()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(7);
        AssignWorkers(workforce, machine.Id, 3);

        var assigned = coordinator.FillCrew(machine.Id, workforce);

        Assert.Equal(2, assigned);
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
        Assert.Equal(2, workforce.IdleWorkers.Count);
    }

    [Fact]
    public void FillCrewDoesNotPartiallyStaffAMachineWhenFewerWorkersAreAvailableThanRequired()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(2);

        var assigned = coordinator.FillCrew(machine.Id, workforce);

        Assert.Equal(0, assigned);
        Assert.Equal(0, AssignedCrewCount(workforce, machine.Id));
        Assert.Equal(2, workforce.IdleWorkers.Count);
    }

    [Fact]
    public void FillCrewAssignsNoWorkersWhenTheMachineAlreadyHasACompleteCrew()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(5);
        AssignWorkers(workforce, machine.Id, 5);

        Assert.Equal(0, coordinator.FillCrew(machine.Id, workforce));
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void RepeatedFillCrewCallsNeverExceedTheRequiredCrewCapacity()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(10);

        Assert.Equal(5, coordinator.FillCrew(machine.Id, workforce));
        Assert.Equal(0, coordinator.FillCrew(machine.Id, workforce));
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void FillCrewAssignsExactlyFiveOfTenAvailableWorkersToAMachineRequiringFive()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(10);

        Assert.Equal(5, coordinator.FillCrew(machine.Id, workforce));
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
        Assert.Equal(5, workforce.IdleWorkers.Count);
        Assert.All(
            workforce.Employees.Take(5),
            worker => Assert.Contains(
                workforce.Assignments,
                assignment => assignment.WorkerId == worker.Id && assignment.WorkstationId == machine.Id.ToString()));
        Assert.All(
            workforce.Employees.Skip(5),
            worker => Assert.DoesNotContain(
                workforce.Assignments,
                assignment => assignment.WorkerId == worker.Id));
    }

    [Fact]
    public void FillCrewAssignsNoWorkersForAnUnknownMachine()
    {
        var coordinator = new ProductionCoordinator();
        var workforce = CreateWorkforce(2);

        Assert.Equal(0, coordinator.FillCrew(Guid.NewGuid(), workforce));
        Assert.Empty(workforce.Assignments);
    }

    [Fact]
    public void FillCrewDoesNotReleaseExistingAssignmentsForADisabledMachine()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        var workforce = CreateWorkforce(5);
        AssignWorkers(workforce, machine.Id, 3);
        var assignmentsBeforeFillAttempt = workforce.Assignments;
        coordinator.RegisterMachine(machine);

        Assert.Equal(0, coordinator.FillCrew(machine.Id, workforce));
        Assert.Equal(assignmentsBeforeFillAttempt, workforce.Assignments);
    }

    [Fact]
    public void FillCrewAssignsNoWorkersWhenNoWorkersAreAvailable()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(2);
        AssignWorkers(workforce, Guid.NewGuid(), 2);

        Assert.Equal(0, coordinator.FillCrew(machine.Id, workforce));
        Assert.Equal(0, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void FillCrewDoesNotStealWorkersAssignedToAnotherMachine()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var otherMachineId = Guid.NewGuid();
        var workforce = CreateWorkforce(6);
        Assert.True(workforce.TryAssign(workforce.Employees[0].Id, otherMachineId.ToString()));

        Assert.Equal(5, coordinator.FillCrew(machine.Id, workforce));
        Assert.Contains(
            workforce.Assignments,
            assignment => assignment.WorkerId == workforce.Employees[0].Id
                && assignment.WorkstationId == otherMachineId.ToString());
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void FillCrewRejectsANullWorkforce()
    {
        var coordinator = new ProductionCoordinator();

        Assert.Throws<ArgumentNullException>(() => coordinator.FillCrew(Guid.NewGuid(), null!));
    }

    [Fact]
    public void FillCrewLeavesExistingAssignmentsUnchangedWhenItCannotAssignWorkers()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var otherMachineId = Guid.NewGuid();
        var workforce = CreateWorkforce(3);
        AssignWorkers(workforce, machine.Id, 2);
        Assert.True(workforce.TryAssign(workforce.Employees[2].Id, otherMachineId.ToString()));
        var assignmentsBeforeFillAttempt = workforce.Assignments;

        Assert.Equal(0, coordinator.FillCrew(machine.Id, workforce));
        Assert.Equal(assignmentsBeforeFillAttempt, workforce.Assignments);
    }

    [Fact]
    public void TryAssignWorkerCapsOneHundredSequentialAttemptsAtFiveSuccessfulAssignments()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(20);

        var successfulAssignments = Enumerable.Range(0, 100)
            .Count(attempt => coordinator.TryAssignWorker(
                machine.Id,
                workforce.Employees[attempt % workforce.Employees.Count].Id,
                workforce));

        Assert.Equal(5, successfulAssignments);
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void TryAssignWorkerOnlyAssignsTheSameWorkerOnce()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(1);
        var worker = workforce.Employees.Single();

        var successfulAssignments = Enumerable.Range(0, 10)
            .Count(_ => coordinator.TryAssignWorker(machine.Id, worker.Id, workforce));

        Assert.Equal(1, successfulAssignments);
        Assert.Equal(1, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void TryAssignWorkerRejectsAdditionalWorkersWhenTheMachineIsFullWithoutChangingAssignments()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(8);
        foreach (var worker in workforce.Employees.Take(5))
        {
            Assert.True(coordinator.TryAssignWorker(machine.Id, worker.Id, workforce));
        }

        var assignmentsBeforeRejectedRequests = workforce.Assignments;

        Assert.All(
            workforce.Employees.Skip(5),
            worker => Assert.False(coordinator.TryAssignWorker(machine.Id, worker.Id, workforce)));
        Assert.Equal(assignmentsBeforeRejectedRequests, workforce.Assignments);
    }

    [Fact]
    public void TryAssignWorkerFillsOnlyTheRemainingTwoVacanciesOfAPartiallyStaffedMachine()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(7);
        AssignWorkers(workforce, machine.Id, 3);

        var successfulAssignments = workforce.Employees.Skip(3)
            .Count(worker => coordinator.TryAssignWorker(machine.Id, worker.Id, workforce));

        Assert.Equal(2, successfulAssignments);
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void OneHundredFillCrewCallsNeverExceedTheRequiredCrewCapacity()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(20);

        var successfulAssignments = Enumerable.Range(0, 100)
            .Sum(_ => coordinator.FillCrew(machine.Id, workforce));

        Assert.Equal(5, successfulAssignments);
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void MixedManualAndAutomaticAssignmentPathsNeverExceedTheRequiredCrew()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(20);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (attempt % 2 == 0)
            {
                coordinator.TryAssignWorker(
                    machine.Id,
                    workforce.Employees[attempt % workforce.Employees.Count].Id,
                    workforce);
            }
            else
            {
                coordinator.FillCrew(machine.Id, workforce);
            }
        }

        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void AWorkerAssignedToOneMachineCannotBeAssignedToAnotherMachine()
    {
        var coordinator = new ProductionCoordinator();
        var firstMachine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var secondMachine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(1);
        var worker = workforce.Employees.Single();

        Assert.True(coordinator.TryAssignWorker(firstMachine.Id, worker.Id, workforce));
        Assert.False(coordinator.TryAssignWorker(secondMachine.Id, worker.Id, workforce));
        Assert.Equal(1, AssignedCrewCount(workforce, firstMachine.Id));
        Assert.Equal(0, AssignedCrewCount(workforce, secondMachine.Id));
    }

    [Fact]
    public void TryAssignWorkerAllowsManualAssignmentToADisabledMachineButFillCrewDoesNotAddWorkers()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 5);
        var workforce = CreateWorkforce(2);
        coordinator.RegisterMachine(machine);

        Assert.True(coordinator.TryAssignWorker(machine.Id, workforce.Employees[0].Id, workforce));
        Assert.Equal(0, coordinator.FillCrew(machine.Id, workforce));
        Assert.Equal(1, AssignedCrewCount(workforce, machine.Id));
        Assert.Single(workforce.IdleWorkers);
    }

    [Fact]
    public void TryAssignWorkerRejectsAnUnknownMachineWithoutModifyingWorkforceState()
    {
        var coordinator = new ProductionCoordinator();
        var workforce = CreateWorkforce(1);
        var assignmentsBeforeRejectedRequest = workforce.Assignments;

        Assert.False(coordinator.TryAssignWorker(Guid.NewGuid(), workforce.Employees.Single().Id, workforce));
        Assert.Equal(assignmentsBeforeRejectedRequest, workforce.Assignments);
    }

    [Fact]
    public void FillEnabledMachinesStaffsMultipleEnabledMachines()
    {
        var coordinator = new ProductionCoordinator();
        var firstMachine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var secondMachine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(4);

        Assert.Equal(4, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(2, AssignedCrewCount(workforce, firstMachine.Id));
        Assert.Equal(2, AssignedCrewCount(workforce, secondMachine.Id));
    }

    [Fact]
    public void FillEnabledMachinesDoesNotAssignWorkersToDisabledMachines()
    {
        var coordinator = new ProductionCoordinator();
        var disabledMachine = CreateMachine(requiredCrew: 2);
        var enabledMachine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(4);
        coordinator.RegisterMachine(disabledMachine);

        Assert.Equal(2, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(0, AssignedCrewCount(workforce, disabledMachine.Id));
        Assert.Equal(2, AssignedCrewCount(workforce, enabledMachine.Id));
    }

    [Fact]
    public void FillEnabledMachinesPreservesAssignmentsWhenAMachineIsDisabled()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(2);
        coordinator.FillEnabledMachines(workforce);
        var assignmentsBeforeDisabling = workforce.Assignments;
        machine.Disable();

        Assert.Equal(0, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(assignmentsBeforeDisabling, workforce.Assignments);
    }

    [Fact]
    public void FillEnabledMachinesSkipsMachinesThatCannotReceiveACompleteCrew()
    {
        var coordinator = new ProductionCoordinator();
        var firstMachine = RegisterEnabledMachine(coordinator, requiredCrew: 3);
        var secondMachine = RegisterEnabledMachine(coordinator, requiredCrew: 3);
        var workforce = CreateWorkforce(4);

        Assert.Equal(3, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(
            [0, 3],
            new[]
            {
                AssignedCrewCount(workforce, firstMachine.Id),
                AssignedCrewCount(workforce, secondMachine.Id),
            }.OrderBy(count => count));
    }

    [Fact]
    public void FillEnabledMachinesAllocatesByMachineIdRatherThanRegistrationOrder()
    {
        var coordinator = new ProductionCoordinator();
        var firstById = CreateMachine(
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            requiredCrew: 2);
        var secondById = CreateMachine(
            id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            requiredCrew: 2);
        secondById.Enable();
        firstById.Enable();
        coordinator.RegisterMachine(secondById);
        coordinator.RegisterMachine(firstById);
        var workforce = CreateWorkforce(3);

        coordinator.FillEnabledMachines(workforce);

        Assert.Equal(2, AssignedCrewCount(workforce, firstById.Id));
        Assert.Equal(0, AssignedCrewCount(workforce, secondById.Id));
    }

    [Fact]
    public void RepeatedFillEnabledMachinesPassesDoNotCreateDuplicateAssignments()
    {
        var coordinator = new ProductionCoordinator();
        var firstMachine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var secondMachine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(4);

        Assert.Equal(4, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(0, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(4, workforce.Assignments.Count);
        Assert.Equal(2, AssignedCrewCount(workforce, firstMachine.Id));
        Assert.Equal(2, AssignedCrewCount(workforce, secondMachine.Id));
    }

    [Fact]
    public void NewlyHiredWorkersFillOutstandingVacanciesOnASubsequentPass()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(2);

        Assert.Equal(0, coordinator.FillEnabledMachines(workforce));
        workforce.Hire("Worker 3");
        workforce.Hire("Worker 4");
        workforce.Hire("Worker 5");

        Assert.Equal(5, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void FillEnabledMachinesReturnsOnlyNewlySuccessfulAssignments()
    {
        var coordinator = new ProductionCoordinator();
        var machine = RegisterEnabledMachine(coordinator, requiredCrew: 5);
        var workforce = CreateWorkforce(5);
        AssignWorkers(workforce, machine.Id, 2);

        Assert.Equal(3, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(5, AssignedCrewCount(workforce, machine.Id));
    }

    [Fact]
    public void FillEnabledMachinesReturnsZeroForAnEmptyMachineRegistry()
    {
        var coordinator = new ProductionCoordinator();

        Assert.Equal(0, coordinator.FillEnabledMachines(CreateWorkforce(2)));
    }

    [Fact]
    public void FillEnabledMachinesRejectsANullWorkforce()
    {
        var coordinator = new ProductionCoordinator();

        Assert.Throws<ArgumentNullException>(() => coordinator.FillEnabledMachines(null!));
    }

    [Fact]
    public void FillEnabledMachinesNeverOverstaffsWhenMoreWorkersThanTotalCapacityAreAvailable()
    {
        var coordinator = new ProductionCoordinator();
        var firstMachine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var secondMachine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var thirdMachine = RegisterEnabledMachine(coordinator, requiredCrew: 2);
        var workforce = CreateWorkforce(20);

        Assert.Equal(6, coordinator.FillEnabledMachines(workforce));
        Assert.Equal(2, AssignedCrewCount(workforce, firstMachine.Id));
        Assert.Equal(2, AssignedCrewCount(workforce, secondMachine.Id));
        Assert.Equal(2, AssignedCrewCount(workforce, thirdMachine.Id));
        Assert.Equal(14, workforce.IdleWorkers.Count);
    }

    [Fact]
    public void EnableMachineAutomaticallyAssignsACompleteCrewWithoutStartingProduction()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 2);
        Assert.True(coordinator.RegisterMachine(machine));
        var workforce = CreateWorkforce(2);

        var assigned = coordinator.EnableMachine(machine.Id, workforce);

        Assert.Equal(2, assigned);
        Assert.True(machine.IsEnabled);
        Assert.Equal(2, AssignedCrewCount(workforce, machine.Id));
        Assert.Null(machine.ActiveCycle);
        Assert.Equal(0, machine.OutputBuffer.ReservedQuantity);
    }

    [Fact]
    public void FillEnabledMachinesUsesMachineIdOrderAndSkipsIncompleteCrewRequests()
    {
        var coordinator = new ProductionCoordinator();
        var firstById = CreateMachine(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            requiredCrew: 3);
        var secondById = CreateMachine(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            requiredCrew: 2);
        firstById.Enable();
        secondById.Enable();
        Assert.True(coordinator.RegisterMachine(secondById));
        Assert.True(coordinator.RegisterMachine(firstById));
        var workforce = CreateWorkforce(2);

        var assigned = coordinator.FillEnabledMachines(workforce);

        Assert.Equal(2, assigned);
        Assert.Equal(0, AssignedCrewCount(workforce, firstById.Id));
        Assert.Equal(2, AssignedCrewCount(workforce, secondById.Id));
    }

    [Fact]
    public void DisableMachineImmediatelyReleasesItsAssignedWorkers()
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: 2);
        Assert.True(coordinator.RegisterMachine(machine));
        var workforce = CreateWorkforce(2);
        Assert.Equal(2, coordinator.EnableMachine(machine.Id, workforce));

        var disabled = coordinator.DisableMachine(machine.Id, workforce);

        Assert.True(disabled);
        Assert.False(machine.IsEnabled);
        Assert.Empty(workforce.Assignments);
        Assert.Equal(2, workforce.IdleWorkers.Count);
    }

    [Fact]
    public void WorkersReleasedByShutdownCanStaffAnotherEnabledMachine()
    {
        var coordinator = new ProductionCoordinator();
        var firstMachine = CreateMachine(requiredCrew: 2);
        var secondMachine = CreateMachine(requiredCrew: 2);
        Assert.True(coordinator.RegisterMachine(firstMachine));
        Assert.True(coordinator.RegisterMachine(secondMachine));
        var workforce = CreateWorkforce(2);
        Assert.Equal(2, coordinator.EnableMachine(firstMachine.Id, workforce));
        Assert.Equal(0, coordinator.EnableMachine(secondMachine.Id, workforce));

        Assert.True(coordinator.DisableMachine(firstMachine.Id, workforce));
        Assert.Equal(2, coordinator.FillEnabledMachines(workforce));

        Assert.Equal(0, AssignedCrewCount(workforce, firstMachine.Id));
        Assert.Equal(2, AssignedCrewCount(workforce, secondMachine.Id));
    }

    [Fact]
    public void RestartingAMachineDoesNotStealWorkersFromAnotherEnabledMachine()
    {
        var coordinator = new ProductionCoordinator();
        var firstMachine = CreateMachine(requiredCrew: 2);
        var secondMachine = CreateMachine(requiredCrew: 2);
        Assert.True(coordinator.RegisterMachine(firstMachine));
        Assert.True(coordinator.RegisterMachine(secondMachine));
        var workforce = CreateWorkforce(2);
        Assert.Equal(2, coordinator.EnableMachine(firstMachine.Id, workforce));
        Assert.Equal(0, coordinator.EnableMachine(secondMachine.Id, workforce));
        Assert.True(coordinator.DisableMachine(firstMachine.Id, workforce));
        Assert.Equal(2, coordinator.FillEnabledMachines(workforce));

        var assignedOnRestart = coordinator.EnableMachine(firstMachine.Id, workforce);

        Assert.Equal(0, assignedOnRestart);
        Assert.Equal(0, AssignedCrewCount(workforce, firstMachine.Id));
        Assert.Equal(2, AssignedCrewCount(workforce, secondMachine.Id));
    }

    [Fact]
    public void TryStartCycleCommitsInputsAndOutputReservationForTheSelectedSchematic()
    {
        var coordinator = new ProductionCoordinator();
        var schematic = CreateSchematic(
            "Copper plate",
            new Dictionary<ResourceType, int> { [ResourceType.IronOre] = 3 },
            outputQuantity: 2);
        var machine = CreateEnabledMachine(coordinator, schematic, requiredCrew: 2, outputBufferCapacity: 2);
        var workforce = CreateWorkforce(2);
        AssignWorkers(workforce, machine.Id, 2);
        var storage = RawMaterialsStorage((ResourceType.IronOre, 3));

        var started = coordinator.TryStartCycle(machine.Id, workforce, hasPower: true, storage);

        Assert.True(started);
        Assert.NotNull(machine.ActiveCycle);
        Assert.Same(schematic, machine.ActiveCycle.Schematic);
        Assert.Equal(0, storage.GetQuantity(ResourceType.IronOre));
        Assert.Equal(2, machine.OutputBuffer.ReservedQuantity);
        Assert.Equal(0, machine.OutputBuffer.StoredQuantity);
    }

    [Fact]
    public void TryStartCycleRejectsARepeatedStartWithoutFurtherMutation()
    {
        var coordinator = new ProductionCoordinator();
        var schematic = CreateSchematic("Copper plate", new Dictionary<ResourceType, int> { [ResourceType.IronOre] = 2 });
        var machine = CreateEnabledMachine(coordinator, schematic, requiredCrew: 1);
        var workforce = CreateWorkforce(1);
        AssignWorkers(workforce, machine.Id, 1);
        var storage = RawMaterialsStorage((ResourceType.IronOre, 4));

        Assert.True(coordinator.TryStartCycle(machine.Id, workforce, hasPower: true, storage));
        var firstCycle = machine.ActiveCycle;

        var startedAgain = coordinator.TryStartCycle(machine.Id, workforce, hasPower: true, storage);

        Assert.False(startedAgain);
        Assert.Same(firstCycle, machine.ActiveCycle);
        Assert.Equal(2, storage.GetQuantity(ResourceType.IronOre));
        Assert.Equal(1, machine.OutputBuffer.ReservedQuantity);
    }

    [Fact]
    public void TryStartCycleRejectsAnUnknownMachineWithoutMutation()
    {
        var coordinator = new ProductionCoordinator();
        var workforce = CreateWorkforce(1);
        var storage = RawMaterialsStorage((ResourceType.IronOre, 2));

        Assert.False(coordinator.TryStartCycle(Guid.NewGuid(), workforce, hasPower: true, storage));
        Assert.Equal(2, storage.GetQuantity(ResourceType.IronOre));
    }

    [Theory]
    [InlineData(false, true, 1, 1)]
    [InlineData(true, false, 1, 1)]
    [InlineData(true, true, 0, 1)]
    [InlineData(true, true, 2, 1)]
    public void TryStartCycleRejectsMachinesThatAreNotReadyWithoutMutation(
        bool enabled,
        bool hasPower,
        int assignedWorkers,
        int requiredCrew)
    {
        var coordinator = new ProductionCoordinator();
        var machine = CreateMachine(requiredCrew: requiredCrew);
        if (enabled)
        {
            machine.Enable();
        }

        Assert.True(coordinator.RegisterMachine(machine));
        var workforce = CreateWorkforce(Math.Max(assignedWorkers, requiredCrew));
        AssignWorkers(workforce, machine.Id, assignedWorkers);
        var storage = RawMaterialsStorage((ResourceType.IronOre, 2));

        var started = coordinator.TryStartCycle(machine.Id, workforce, hasPower, storage);

        Assert.False(started);
        Assert.Null(machine.ActiveCycle);
        Assert.Equal(2, storage.GetQuantity(ResourceType.IronOre));
        Assert.Equal(0, machine.OutputBuffer.ReservedQuantity);
    }

    [Fact]
    public void TryStartCycleRejectsAnInputStorageThatDoesNotAcceptTheSchematicInputs()
    {
        var coordinator = new ProductionCoordinator();
        var schematic = CreateSchematic("Copper plate", new Dictionary<ResourceType, int> { [ResourceType.IronOre] = 1 });
        var machine = CreateEnabledMachine(coordinator, schematic, requiredCrew: 1);
        var workforce = CreateWorkforce(1);
        AssignWorkers(workforce, machine.Id, 1);
        var storage = new SupplyStorage(
            StorageType.PartsAndProducts,
            20,
            [ResourceCategory.ProcessedMaterial, ResourceCategory.ManufacturedGood]);

        Assert.False(coordinator.TryStartCycle(machine.Id, workforce, hasPower: true, storage));
        Assert.Null(machine.ActiveCycle);
        Assert.Equal(0, machine.OutputBuffer.ReservedQuantity);
    }

    [Fact]
    public void TryStartCycleDoesNotWithdrawInputsWhenFullOutputDoesNotFit()
    {
        var coordinator = new ProductionCoordinator();
        var schematic = CreateSchematic(
            "Copper plate",
            new Dictionary<ResourceType, int> { [ResourceType.IronOre] = 2 },
            outputQuantity: 2);
        var machine = CreateEnabledMachine(coordinator, schematic, requiredCrew: 1, outputBufferCapacity: 1);
        var workforce = CreateWorkforce(1);
        AssignWorkers(workforce, machine.Id, 1);
        var storage = RawMaterialsStorage((ResourceType.IronOre, 2));

        Assert.False(coordinator.TryStartCycle(machine.Id, workforce, hasPower: true, storage));
        Assert.Null(machine.ActiveCycle);
        Assert.Equal(2, storage.GetQuantity(ResourceType.IronOre));
        Assert.Equal(0, machine.OutputBuffer.ReservedQuantity);
    }

    [Fact]
    public void TryStartCycleLeavesEveryMultiInputQuantityUnchangedWhenAnyInputIsInsufficient()
    {
        var coordinator = new ProductionCoordinator();
        var schematic = CreateSchematic(
            "Copper plate",
            new Dictionary<ResourceType, int>
            {
                [ResourceType.IronOre] = 2,
                [ResourceType.CopperOre] = 2,
            });
        var machine = CreateEnabledMachine(coordinator, schematic, requiredCrew: 1);
        var workforce = CreateWorkforce(1);
        AssignWorkers(workforce, machine.Id, 1);
        var storage = RawMaterialsStorage(
            (ResourceType.IronOre, 2),
            (ResourceType.CopperOre, 1));

        Assert.False(coordinator.TryStartCycle(machine.Id, workforce, hasPower: true, storage));
        Assert.Null(machine.ActiveCycle);
        Assert.Equal(2, storage.GetQuantity(ResourceType.IronOre));
        Assert.Equal(1, storage.GetQuantity(ResourceType.CopperOre));
        Assert.Equal(0, machine.OutputBuffer.ReservedQuantity);
    }

    [Fact]
    public void TryStartCycleRetainsTheSchematicSelectedAtActivation()
    {
        var coordinator = new ProductionCoordinator();
        var first = CreateSchematic("Iron plate", new Dictionary<ResourceType, int> { [ResourceType.IronOre] = 1 });
        var second = CreateSchematic("Copper plate", new Dictionary<ResourceType, int> { [ResourceType.CopperOre] = 1 });
        var machine = new Machine(Guid.NewGuid(), "Stamping press", 4, 1, 10, [first, second], first);
        machine.Enable();
        Assert.True(coordinator.RegisterMachine(machine));
        var workforce = CreateWorkforce(1);
        AssignWorkers(workforce, machine.Id, 1);
        var storage = RawMaterialsStorage((ResourceType.IronOre, 1));

        Assert.True(coordinator.TryStartCycle(machine.Id, workforce, hasPower: true, storage));
        machine.SelectSchematic(second);

        Assert.Same(first, machine.ActiveCycle!.Schematic);
        Assert.Same(second, machine.SelectedSchematic);
    }

    private static Machine CreateMachine(
        Guid? id = null,
        string name = "Stamping press",
        int requiredCrew = 2)
    {
        var schematic = new Schematic(
            "Iron plate",
            new Dictionary<ResourceType, int> { [ResourceType.IronOre] = 3 },
            ResourceType.CopperOre,
            1,
            15);

        return new Machine(id ?? Guid.NewGuid(), name, 4, requiredCrew, 10, [schematic], schematic);
    }

    private static Machine CreateEnabledMachine(
        ProductionCoordinator coordinator,
        Schematic schematic,
        int requiredCrew,
        int outputBufferCapacity = 10)
    {
        var machine = new Machine(
            Guid.NewGuid(),
            "Stamping press",
            4,
            requiredCrew,
            outputBufferCapacity,
            [schematic],
            schematic);
        machine.Enable();
        Assert.True(coordinator.RegisterMachine(machine));
        return machine;
    }

    private static Schematic CreateSchematic(
        string name,
        IReadOnlyDictionary<ResourceType, int> inputs,
        int outputQuantity = 1) =>
        new(name, inputs, ResourceType.CopperOre, outputQuantity, 15);

    private static SupplyStorage RawMaterialsStorage(params (ResourceType Type, double Quantity)[] inventory)
    {
        var storage = new SupplyStorage(StorageType.RawMaterials, 20, [ResourceCategory.RawMaterial]);
        foreach (var (type, quantity) in inventory)
        {
            storage.Deliver(type, quantity);
        }

        return storage;
    }

    private static Machine RegisterEnabledMachine(ProductionCoordinator coordinator, int requiredCrew)
    {
        var machine = CreateMachine(requiredCrew: requiredCrew);
        machine.Enable();
        Assert.True(coordinator.RegisterMachine(machine));
        return machine;
    }

    private static FactoryWorkforce CreateWorkforce(int workerCount)
    {
        var workforce = new FactoryWorkforce();
        for (var workerNumber = 1; workerNumber <= workerCount; workerNumber++)
        {
            workforce.Hire($"Worker {workerNumber}");
        }

        return workforce;
    }

    private static void AssignWorkers(FactoryWorkforce workforce, Guid machineId, int workerCount)
    {
        foreach (var worker in workforce.Employees.Take(workerCount))
        {
            Assert.True(workforce.TryAssign(worker.Id, machineId.ToString()));
        }
    }

    private static int AssignedCrewCount(FactoryWorkforce workforce, Guid machineId) =>
        workforce.Assignments.Count(assignment => assignment.WorkstationId == machineId.ToString());
}
