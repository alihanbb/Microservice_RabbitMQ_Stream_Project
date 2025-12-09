namespace DiscountService.Domain.Exceptions;

public class DomainException : Exception
{
    private string argument;

    public DomainException() { }    
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception exception) : base(message, exception) { }

    public DomainException(string message, string argument) : this(message)
    {
        this.argument = argument;
    }
}
