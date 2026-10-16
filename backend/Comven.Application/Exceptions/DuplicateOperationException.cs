namespace Comven.Application.Exceptions;

public class DuplicateOperationException : Exception
{
    public DuplicateOperationException()
        : base("A operação já foi registrada.")
    {
    }
}
