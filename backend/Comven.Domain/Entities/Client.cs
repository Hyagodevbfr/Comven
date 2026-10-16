using Comven.Domain.Common;

namespace Comven.Domain.Entities;

public class Client
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Client() { }

    private Client(string name, decimal initialBalance)
    {
        Id = Guid.NewGuid();
        Name = name;
        Balance = initialBalance;
        CreatedAt = DateTime.UtcNow;
    }

    public static DomainResult<Client> Create( string name, decimal initialBalance)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DomainResult<Client>.Failure("O nome do cliente é obrigatório.");
      
        if (initialBalance < 0)
            return DomainResult<Client>.Failure("O saldo inicial não pode ser negativo.");
       
        return DomainResult<Client>.Success(new Client(name.Trim(), initialBalance));
    }

    public DomainResult Debit(decimal amout)
    {
        if (amout <= 0)
            return DomainResult.Failure("O valor do débito deve ser maior que zero.");

        if (Balance < amout)
            return DomainResult.Failure("Saldo insuficiente para realizar o débito.");

        Balance -= amout;

        return DomainResult.Success();
    }

    public DomainResult Credit(decimal amout)
    {
        if (amout <= 0)
            return DomainResult.Failure("O valor do crédito deve ser maior que zero.");

        Balance += amout;

        return DomainResult.Success();
    }
}
