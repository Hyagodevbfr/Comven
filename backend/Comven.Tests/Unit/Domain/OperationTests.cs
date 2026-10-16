using Comven.Domain.Entities;
using Comven.Domain.Enums;

namespace Comven.Tests.Unit.Domain;

public class OperationTests
{
    private static Operation NewOperation() =>
        Operation.Create(Guid.NewGuid(), Guid.NewGuid(), OperationType.VehicleConsulting, 12.50m).Value!;

    [Fact]
    public void Create_WithValidData_ReturnsPendingOperation()
    {
        var clientId = Guid.NewGuid();
        var key = Guid.NewGuid();

        var result = Operation.Create(clientId, key, OperationType.DocumentIssuance, 45m);

        Assert.True(result.IsSuccess);
        var op = result.Value!;
        Assert.NotEqual(Guid.Empty, op.Id);
        Assert.Equal(clientId, op.ClientId);
        Assert.Equal(key, op.IdempotencyKey);
        Assert.Equal(OperationType.DocumentIssuance, op.Type);
        Assert.Equal(45m, op.Amount);
        Assert.Equal(OperationStatus.Pending, op.Status);
        Assert.Null(op.FinishedAt);
    }

    [Fact]
    public void Create_WithEmptyClientId_Fails()
    {
        var result = Operation.Create(Guid.Empty, Guid.NewGuid(), OperationType.VehicleConsulting, 10m);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Create_WithEmptyIdempotencyKey_Fails()
    {
        var result = Operation.Create(Guid.NewGuid(), Guid.Empty, OperationType.VehicleConsulting, 10m);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_Fails(int amount)
    {
        var result = Operation.Create(Guid.NewGuid(), Guid.NewGuid(), OperationType.VehicleConsulting, amount);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Complete_WhenPending_MarksCompletedAndSetsFinishedAt()
    {
        var op = NewOperation();

        var result = op.Complete();

        Assert.True(result.IsSuccess);
        Assert.Equal(OperationStatus.Completed, op.Status);
        Assert.NotNull(op.FinishedAt);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_FailsAndKeepsStatus()
    {
        var op = NewOperation();
        op.Complete();
        var finishedAt = op.FinishedAt;

        var result = op.Complete();

        Assert.True(result.IsFailure);
        Assert.Equal(OperationStatus.Completed, op.Status);
        Assert.Equal(finishedAt, op.FinishedAt);
    }
}
