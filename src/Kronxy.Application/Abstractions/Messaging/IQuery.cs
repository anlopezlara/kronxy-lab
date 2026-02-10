using Kronxy.Domain.Abstractions;
using MediatR;

namespace Kronxy.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}