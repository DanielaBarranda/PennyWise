namespace PennyWise.Api.Errors;

// Services throw these; AppExceptionHandler turns them into 404 / 409 responses,
// so controllers stay short and every error has the same JSON shape.
public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);
