namespace KotoDibo.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, string key)
        : base($"Entity \"{entityName}\" with key ({key}) was not found.")
    {
    }
}
