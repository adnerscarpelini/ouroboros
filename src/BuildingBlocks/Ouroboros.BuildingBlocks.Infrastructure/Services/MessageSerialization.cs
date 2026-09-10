using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// Uma única configuração de serialização para os dois lados do transporte: o payload gravado na
// outbox pelo produtor e o payload lido pelo consumidor precisam concordar, e concordar por acidente
// não conta. Propriedade ausente no JSON não pode virar campo silenciosamente nulo — por isso o
// consumidor valida o resultado da desserialização em vez de confiar no tipo.
public static class MessageSerialization
{
	public static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never
	};
}
