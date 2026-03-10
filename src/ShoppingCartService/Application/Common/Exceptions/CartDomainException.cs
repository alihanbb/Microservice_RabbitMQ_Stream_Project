namespace ShoppingCartService.Application.Common.Exceptions;

public class CartDomainException : Exception
{
    public int StatusCode { get; }

    public CartDomainException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}
