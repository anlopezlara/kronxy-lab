namespace Kronxy.Application.Exceptions;

public sealed record ValidationError(string PropertyName, string ErrorMessage);