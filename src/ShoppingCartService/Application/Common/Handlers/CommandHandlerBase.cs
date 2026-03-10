using ShoppingCartService.Application.Common.Exceptions;

namespace ShoppingCartService.Application.Common.Handlers;

public abstract class CommandHandlerBase<TCommand, TResult>
{
    protected ILogger Logger { get; }

    protected CommandHandlerBase(ILogger logger)
    {
        Logger = logger;
    }

    public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteAsync(command, cancellationToken);
            return Result<TResult>.Success(result);
        }
        catch (CartDomainException ex)
        {
            return Result<TResult>.Failure(ex.Message, ex.StatusCode);
        }
        catch (InvalidOperationException ex)
        {
            return Result<TResult>.Failure(ex.Message, 400);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {Handler}", GetType().Name);
            return Result<TResult>.Failure("An unexpected error occurred", 500);
        }
    }

    protected abstract Task<TResult> ExecuteAsync(TCommand command, CancellationToken cancellationToken);
}
