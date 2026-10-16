using Comven.Domain.Entities;

namespace Comven.Tests.Unit.Domain;

public class ClientTests
{
    private static Client NewClient(decimal balance = 100m) => Client.Create("Maria", balance).Value!;

    [Fact]
    public void Create_WithValidData_ReturnsClientWithTrimmedName()
    {
        var result = Client.Create("  Maria  ", 50m);

        Assert.True(result.IsSuccess);
        Assert.Equal("Maria", result.Value!.Name);
        Assert.Equal(50m, result.Value.Balance);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Fact]
    public void Create_WithZeroBalance_Succeeds()
    {
        Assert.True(Client.Create("Maria", 0m).IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyName_Fails(string? name)
    {
        var result = Client.Create(name!, 10m);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Create_WithNegativeBalance_Fails()
    {
        var result = Client.Create("Maria", -0.01m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Debit_WithEnoughBalance_ReducesBalance()
    {
        var client = NewClient(100m);

        var result = client.Debit(30m);

        Assert.True(result.IsSuccess);
        Assert.Equal(70m, client.Balance);
    }

    [Fact]
    public void Debit_OfEntireBalance_Succeeds()
    {
        var client = NewClient(100m);

        Assert.True(client.Debit(100m).IsSuccess);
        Assert.Equal(0m, client.Balance);
    }

    [Fact]
    public void Debit_WithInsufficientBalance_FailsAndKeepsBalance()
    {
        var client = NewClient(10m);

        var result = client.Debit(10.01m);

        Assert.True(result.IsFailure);
        Assert.Contains("Saldo insuficiente", result.Error);
        Assert.Equal(10m, client.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Debit_WithNonPositiveAmount_FailsAndKeepsBalance(int amount)
    {
        var client = NewClient(100m);

        var result = client.Debit(amount);

        Assert.True(result.IsFailure);
        Assert.Equal(100m, client.Balance);
    }

    [Fact]
    public void Credit_WithPositiveAmount_IncreasesBalance()
    {
        var client = NewClient(100m);

        var result = client.Credit(25m);

        Assert.True(result.IsSuccess);
        Assert.Equal(125m, client.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Credit_WithNonPositiveAmount_FailsAndKeepsBalance(int amount)
    {
        var client = NewClient(100m);

        var result = client.Credit(amount);

        Assert.True(result.IsFailure);
        Assert.Equal(100m, client.Balance);
    }

    [Fact]
    public void DebitThenCredit_RestoresOriginalBalance()
    {
        var client = NewClient(100m);

        client.Debit(12.50m);
        client.Credit(12.50m);

        Assert.Equal(100m, client.Balance);
    }
}
