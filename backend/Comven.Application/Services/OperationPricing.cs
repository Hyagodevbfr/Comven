using Comven.Domain.Enums;

namespace Comven.Application.Services;

public class OperationPricing
{
    public static decimal? GetPrice(OperationType type)
    {
        return type switch
        {
            OperationType.VehicleConsulting => 12.50m,
            OperationType.DocumentIssuance => 45.00m,
            OperationType.LicensePlateConsultation => 8.90m,
            _ => null
        };
    }
}
