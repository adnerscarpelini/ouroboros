namespace Ouroboros.Services.Notifications.Domain;

public enum EmailDeliveryStatus
{
	Pending = 0,
	// Falhou de forma transitória e vai ser tentada de novo em NextAttemptAt.
	RetryScheduled = 1,
	// O servidor SMTP aceitou a mensagem. Não significa que chegou à caixa de entrada, que foi lida,
	// nem que não vai voltar como bounce — ver RFC 5321, seções 4.5.3.2 e 6.1.
	AcceptedByProvider = 2,
	// Falha permanente ou tentativas esgotadas. Continua na tabela, com o último erro, para inspeção.
	Failed = 3,
	// O link que a mensagem carregava venceu antes de ela ser entregue. Enviar depois disso só
	// produziria um link morto na caixa de entrada.
	Expired = 4
}
