using Comven.Application.DTOs.Requests;
using Comven.Application.Exceptions;
using Comven.Application.Interfaces;
using Comven.Application.Interfaces.Repositories;
using Comven.Application.Interfaces.Services;
using Comven.Domain.Common;
using Comven.Domain.Entities;

namespace Comven.Application.Services;

public class OperationService
{
    private readonly IClientRepository _clients;
    private readonly IOperationRepository _operations;
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IOperationProvider _provider;
    private readonly IUnitOfWork _unitOfWork;

    public OperationService(IUnitOfWork unitOfWork, IOperationProvider provider, IFinancialTransactionRepository transactions, IOperationRepository operations, IClientRepository clients)
    {
        _unitOfWork = unitOfWork;
        _provider = provider;
        _transactions = transactions;
        _operations = operations;
        _clients = clients;
    }

    public async Task<DomainResult<Operation>> CreateAsync(CreateOperationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ClientId == Guid.Empty || request.IdempotencyKey == Guid.Empty)
            return DomainResult<Operation>.Failure("Cliente ou chave de idempotência inválidos.");

        var amount = OperationPricing.GetPrice(request.Type);

        if (amount == null)
            return DomainResult<Operation>.Failure("Tipo de operação inválido.");

        var existing = await _operations.GetByIdempotencyKeyAsync(request.ClientId, request.IdempotencyKey, cancellationToken);

        if (existing != null)
            return ExistingResult(existing, request);

        var operationResult = Operation.Create(
            request.ClientId,
            request.IdempotencyKey,
            request.Type,
            amount.Value);

        if (operationResult.IsFailure)
            return DomainResult<Operation>.Failure(operationResult.Error!);

        var operation = operationResult.Value!;

        var debitResult = FinancialTransaction.CreateDebit(operation.ClientId, operation.Id, operation.Amount);

        if (debitResult.IsFailure)
            return DomainResult<Operation>.Failure(debitResult.Error!);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var debited = await _clients.TryDebitAsync(
                request.ClientId,
                amount.Value,
                cancellationToken);

            if (!debited)
            {
                await _unitOfWork.RollbackAsync(
                    CancellationToken.None);

                var duplicated = await _operations.GetByIdempotencyKeyAsync(
                    request.ClientId,
                    request.IdempotencyKey,
                    cancellationToken);

                if (duplicated is not null)
                    return ExistingResult(duplicated, request);

                var client = await _clients.GetByIdAsync(
                    request.ClientId,
                    cancellationToken);

                return DomainResult<Operation>.Failure(
                    client is null
                        ? "Cliente não encontrado."
                        : "Saldo insuficiente.");
            }

            await _operations.AddAsync(
                operation,
                cancellationToken);

            await _transactions.AddAsync(
                debitResult.Value!,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            await _unitOfWork.CommitAsync(
                cancellationToken);
        }
        catch (DuplicateOperationException)
        {
            await _unitOfWork.RollbackAsync(
                CancellationToken.None);

            var existingOperation = await _operations
                .GetByIdempotencyKeyAsync(
                    request.ClientId,
                    request.IdempotencyKey,
                    CancellationToken.None);

            if (existingOperation is null)
                throw;

            return ExistingResult(existingOperation, request);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(
                CancellationToken.None);

            throw;
        }

        bool succeeded;

        try
        {
            succeeded = await _provider.ExecuteAsync(operation.Type, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return DomainResult<Operation>.Success(operation);
        }
        catch 
        {
            return DomainResult<Operation>.Success(operation);
        }

        if (succeeded)
            await CompleteAsync(operation.Id, CancellationToken.None);
        else
            await RefundAsync(operation, CancellationToken.None);

        var updated = await _operations.GetByIdAsync(
            operation.Id,
            CancellationToken.None);

        return DomainResult<Operation>.Success(
            updated ?? operation);
    }

    private static DomainResult<Operation> ExistingResult(Operation existing, CreateOperationRequest request)
    {
        if (existing.Type != request.Type)
        {
            return DomainResult<Operation>.Failure(
                "A chave de idempotência já foi utilizada " +
                "para outra operação.");
        }

        return DomainResult<Operation>.Success(existing);
    }
    private async Task CompleteAsync(
    Guid operationId,
    CancellationToken cancellationToken)
    {
        var completed = await _operations.TryMarkAsCompletedAsync(
            operationId,
            cancellationToken);

        if (!completed)
            throw new InvalidOperationException(
                "Não foi possível concluir a operação.");
    }

    private async Task RefundAsync(Operation operation, CancellationToken cancellationToken)
    {
        var refundResult = FinancialTransaction.CreateRefund(operation.ClientId, operation.Id, operation.Amount);

        if (refundResult.IsFailure)
            throw new InvalidOperationException("Não foi possível preparar o estorno.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var changed = await _operations.TryMarkAsFailedAsync(operation.Id, cancellationToken);

            if (!changed)
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);

                return;
            }

            var refunded = await _clients.TryRefundAsync(operation.ClientId, operation.Amount, cancellationToken);

            if (!refunded)
                throw new InvalidOperationException("Não foi possível estornar o valor do cliente.");

            await _transactions.AddAsync(refundResult.Value!, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

}
