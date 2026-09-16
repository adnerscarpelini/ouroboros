using Microsoft.AspNetCore.Http;
using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// Lê o X-Correlation-Id que o Api Gateway garante em todo request. Serviço nenhum gera o seu — todos
// leem o que veio, para que o mesmo id apareça no log de cada serviço que atendeu aquela requisição.
public sealed class HttpCorrelationIdAccessorService : ICorrelationIdAccessor
{
	public const string HeaderName = "X-Correlation-Id";

	private readonly IHttpContextAccessor _httpContextAccessor;

	public HttpCorrelationIdAccessorService(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public string? GetCorrelationId()
	{
		// Null fora de um request (processador de fundo): o chamador precisa aceitar isso em vez de
		// inventar um id que não corresponde a requisição nenhuma.
		var correlationId = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();

		return string.IsNullOrWhiteSpace(correlationId)
			? null
			: correlationId;
	}
}
