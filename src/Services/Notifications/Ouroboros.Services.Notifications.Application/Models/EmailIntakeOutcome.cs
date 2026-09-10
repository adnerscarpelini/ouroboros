namespace Ouroboros.Services.Notifications.Application;

public enum EmailIntakeOutcome
{
	// Solicitação nova, aceita e persistida. A entrega acontece depois, pelo dispatcher.
	Accepted = 0,
	// Já conhecida, com o mesmo conteúdo: é uma reentrega do transporte. Confirmar o consumo sem
	// criar um segundo envio é exatamente o comportamento esperado.
	AlreadyAccepted = 1,
	// Mesma chave (produtor + solicitação) com conteúdo diferente. Nunca sobrescrever o que já existe:
	// vai para quarentena e alerta.
	Conflict = 2,
	// Estruturalmente inválida: versão desconhecida, template fora do catálogo, produtor sem permissão
	// sobre o template, destinatário inválido ou placeholder obrigatório ausente.
	Rejected = 3
}

public sealed record EmailIntakeResult(
	EmailIntakeOutcome Outcome,
	string? Reason
)
{
	public static EmailIntakeResult Accepted()
	{
		return new EmailIntakeResult(EmailIntakeOutcome.Accepted, null);
	}

	public static EmailIntakeResult AlreadyAccepted()
	{
		return new EmailIntakeResult(EmailIntakeOutcome.AlreadyAccepted, null);
	}

	public static EmailIntakeResult Conflict(string reason)
	{
		return new EmailIntakeResult(EmailIntakeOutcome.Conflict, reason);
	}

	public static EmailIntakeResult Rejected(string reason)
	{
		return new EmailIntakeResult(EmailIntakeOutcome.Rejected, reason);
	}
}
