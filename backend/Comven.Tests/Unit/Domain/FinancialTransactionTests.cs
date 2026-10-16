using Comven.Domain.Entities;
using Comven.Domain.Enums;

namespace Comven.Tests.Unit.Domain;

public class FinancialTransactionTests
{
    [Fact]
    public void CreateDebit_WithValidData_ReturnsDebitTransaction()
    {
        var clientId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        var result = FinancialTransaction.CreateDebit(clientId, operationId, 12.50m);

        Assert.True(result.IsSuccess);
        var tx = result.Value!;
        Assert.NotEqual(Guid.Empty, tx.Id);
        Assert.Equal(clientId, tx.ClientId);
        Assert.Equal(operationId, tx.OperationId);
        Assert.Equal(FinancialTransactionType.Debit, tx.Type);
        Assert.Equal(12.50m, tx.Amount);
    }

    [Fact]
    public void CreateRefund_WithValidData_ReturnsRefundTransaction()
    {
        var result = FinancialTransaction.CreateRefund(Guid.NewGuid(), Guid.NewGuid(), 45m);

        Assert.True(result.IsSuccess);
        Assert.Equal(FinancialTransactionType.Refund, result.Value!.Type);
        Assert.Equal(45m, result.Value.Amount);
    }

    [Fact]
    public void CreateDebit_WithEmptyClientId_Fails()
    {
        Assert.True(FinancialTransaction.CreateDebit(Guid.Empty, Guid.NewGuid(), 10m).IsFailure);
    }

    [Fact]
    public void CreateRefund_WithEmptyClientId_Fails()
    {
        Assert.True(FinancialTransaction.CreateRefund(Guid.Empty, Guid.NewGuid(), 10m).IsFailure);
    }

    [Fact]
    public void CreateDebit_WithEmptyOperationId_Fails()
    {
        Assert.True(FinancialTransaction.CreateDebit(Guid.NewGuid(), Guid.Empty, 10m).IsFailure);
    }

    [Fact]
    public void CreateRefund_WithEmptyOperationId_Fails()
    {
        Assert.True(FinancialTransaction.CreateRefund(Guid.NewGuid(), Guid.Empty, 10m).IsFailure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CreateDebit_WithNonPositiveAmount_Fails(int amount)
    {
        Assert.True(FinancialTransaction.CreateDebit(Guid.NewGuid(), Guid.NewGuid(), amount).IsFailure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CreateRefund_WithNonPositiveAmount_Fails(int amount)
    {
        Assert.True(FinancialTransaction.CreateRefund(Guid.NewGuid(), Guid.NewGuid(), amount).IsFailure);
    }
}
