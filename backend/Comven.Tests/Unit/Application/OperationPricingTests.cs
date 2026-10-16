using Comven.Application.Services;
using Comven.Domain.Enums;

namespace Comven.Tests.Unit.Application;

public class OperationPricingTests
{
    [Fact]
    public void GetPrice_VehicleConsulting_Returns12_50()
    {
        Assert.Equal(12.50m, OperationPricing.GetPrice(OperationType.VehicleConsulting));
    }

    [Fact]
    public void GetPrice_DocumentIssuance_Returns45()
    {
        Assert.Equal(45.00m, OperationPricing.GetPrice(OperationType.DocumentIssuance));
    }

    [Fact]
    public void GetPrice_LicensePlateConsultation_Returns8_90()
    {
        Assert.Equal(8.90m, OperationPricing.GetPrice(OperationType.LicensePlateConsultation));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void GetPrice_ForUndefinedType_ReturnsNull(int value)
    {
        Assert.Null(OperationPricing.GetPrice((OperationType)value));
    }
}
