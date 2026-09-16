# Ouroboros.Contracts.Notifications

Contrato de integração entre quem **solicita** uma notificação (qualquer serviço produtor) e quem a
**entrega** (o serviço de Notificações). É a única exceção à regra de que um serviço nunca referencia
projeto de outro — ver [src/Services/README.md](../../Services/README.md).

Regras deste projeto:

- Só DTOs versionados e constantes. Sem entidades de domínio, sem SDK de broker, sem dependência de projeto.
- Mudança compatível adiciona campo opcional. Mudança semântica cria uma versão nova (`...V2`), com o
  consumidor implantado antes do produtor.
- O nome da classe CLR não é o contrato de rede: quem identifica a mensagem é `MessageType` +
  `SchemaVersion`, validados antes da desserialização.
