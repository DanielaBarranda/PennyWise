namespace PennyWise.Api.Errors;

// Services throw these; AppExceptionHandler turns them into 404 / 409 responses,
// so controllers stay short and every error has the same JSON shape.
public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

// A business rule that depends on stored data (e.g. withdrawing more than a goal
// holds). Returned as a 400 with the message attached to the named field.
public sealed class ValidationFailedException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
