namespace Ouroboros.BuildingBlocks.Application;

// Identificador lógico deste serviço como produtor (ex.: "auth"), gravado em toda mensagem da outbox
// e usado pelo consumidor como metade da chave de deduplicação.
//
// Não vem do appsettings de propósito: é identidade de código, validada do outro lado contra um
// catálogo fechado. Um erro de digitação em arquivo de configuração viraria mensagem rejeitada.
public sealed record OutboxProducer(string Name);
