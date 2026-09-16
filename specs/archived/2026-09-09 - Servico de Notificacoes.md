---
title: Serviço de Notificações
state: done
---

# Serviço de Notificações

## Resultado (10/09/2026)

Implementado, **menos o transporte**. RabbitMQ foi separado em spec própria a pedido do usuário:
`2026-09-10 - Mensageria com RabbitMQ.md`.

Entregue:

- `src/Contracts/Ouroboros.Contracts.Notifications` com `EmailNotificationRequestedV1`, tipos de mensagem, produtores e catálogo de chaves de template.
- Outbox transacional em `BuildingBlocks`: `OutboxMessage`, `IOutboxMessageQueue`, `IMessagePublisher`, `OutboxPublisher`, `OutboxPublisherProcessor` e `AddTransactionalOutbox`.
- Serviço `Notifications` completo (quatro camadas), banco `ouroboros_notifications`, role `notifications_service`, schema `notifications` e `common.error_logs` local.
- Ingestão idempotente (`EmailDeliveryIntakeService`) com chave única `(producer, request_id)`, comparação de impressão digital de conteúdo e catálogo fechado de templates com validação de produtor.
- Entrega (`EmailDeliveryDispatcher`/`EmailDeliveryProcessor`) com tentativas, espera exponencial com jitter, expiração reconferida a cada envio e histórico por tentativa.
- Templates movidos do Auth, renderização com escape de HTML e assunto versionado.
- Auth cortado: sem SMTP, sem template, sem `EmailMessage`. `Token.EmailMessageId` virou `Token.NotificationRequestId` (`Guid`).
- Containerização, banco/role via script de init com `REVOKE` de `PUBLIC`, rotas de saúde no gateway, portas 5083/7273.
- 117 testes passando, dos quais 30 novos cobrindo outbox, ingestão, entrega e renderização.

Decidido durante a implementação:

- **Corte total imediato**, sem manter o envio antigo em paralelo. Em desenvolvimento, e-mail deixa de ser entregue até a mensageria existir; as solicitações ficam visíveis e pendentes em `common.outbox_messages`.
- `OutboxPublisher` é registrado por fábrica no contêiner. Com o registro por tipo, a validação de serviços na subida recusava o construtor que exige `IMessagePublisher` e derrubava a Api inteira antes de o transporte existir.
- O nome do produtor saiu do `appsettings` e virou constante de código (`OutboxProducer`), para que um erro de digitação não vire mensagem rejeitada do outro lado.
- `SmtpOptions.Username`/`Password` ganharam valor padrão: o binder de configuração exige entrada correspondente para todo parâmetro sem default.
- Não foi criado `Ouroboros.NotificationsService.Api.Tests`: o host não tem nada além dos dois health checks.
- O host não registra autenticação. Sem endpoint de negócio, a `FallbackPolicy` autenticada não teria o que proteger — o primeiro endpoint de consulta ou reprocessamento precisa definir sua autorização antes de existir.

## Status e escopo (original, 09/09/2026)

Spec para revisão, elaborada em 09/09/2026 sobre o checkout `c874df8`. Esta entrega é somente documental: não cria serviços, migrations, configurações ou dependências. As etapas abaixo descrevem trabalho futuro.

**Direção já escolhida na discussão:** Notificações terá processo e banco próprios e será responsável pelo envio, provedor, retentativas e histórico. Cada produtor gravará uma solicitação em outbox local, na transação de negócio, e publicará por mensageria. Notificações não acessará nem varrerá bancos de produtores. BuildingBlocks continuará sendo biblioteca, sem banco central.

Pedidos/vendas, pagamentos e entregas fazem parte da evolução prevista do e-commerce. Precisarão de notificações; por isso, centralizar a entrega já tem justificativa de produto. Esses serviços ainda não existem no repositório e não serão criados nesta migração. O primeiro canal continua sendo e-mail: sem SMS, push, campanhas, preferências multicanal, anexos ou editor visual de templates.

**Propostas desta spec, ainda sujeitas à revisão:** nome técnico `Notifications`, RabbitMQ com cliente .NET direto, contrato versionado de solicitação de e-mail, templates no serviço central e política de recuperação descrita abaixo. Não confundir essas escolhas com aprovação de implementação.

## Mapa do repositório e evidências

A [solution](../Ouroboros.slnx) contém oito projetos de produção: três BuildingBlocks, quatro camadas de Auth e um gateway; sete projetos de teste: três BuildingBlocks e quatro Auth. Todos os projetos de produção usam .NET 10. Não há implementação de outros contextos, frontend, broker, pacote de mensageria ou pipeline de CI versionado encontrado no levantamento. Foram consultados os arquivos de código, configurações, testes, migrations, documentação e skills relevantes; não foram inspecionados dados de um banco em execução.

Não foi encontrado `AGENTS.md` no repositório nem nos dois diretórios ancestrais imediatos consultados. [CLAUDE.md](../CLAUDE.md) e [Services/README](../src/Services/README.md) remetem às convenções do projeto; seus links antigos usam `.claude/skills`. As skills disponíveis neste checkout estão em `.codex/skills`: [developer](../.codex/skills/ags-developer/SKILL.md), [dba](../.codex/skills/ags-dba/SKILL.md), [devops](../.codex/skills/ags-devops/SKILL.md), [qa](../.codex/skills/ags-qa/SKILL.md) e [technical-writer](../.codex/skills/ags-technical-writer/SKILL.md). A reorganização de skills já estava presente antes desta entrega.

| Área existente | Evidência | Impacto da evolução |
|---|---|---|
| Arquitetura e isolamento | [0000](../docs/0000%20-%20Arquitetura.md), [Services/README](../src/Services/README.md) | Preservar camadas e banco por serviço; documentar comunicação assíncrona. |
| Entidades comuns | [Entity](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Domain/Entity.cs), [EmailMessage](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Domain/EmailMessage.cs), [ErrorLog](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Domain/ErrorLog.cs) | Separar envelope técnico de dados de entrega. `Entity` e `ErrorLog` continuam reutilizáveis. |
| Contratos comuns | [IEmailQueueService](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Application/Interfaces/IEmailQueueService.cs), [IEmailSender](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Application/Interfaces/IEmailSender.cs), [EmailOutboxOptions](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Application/Models/EmailOutboxOptions.cs) | Substituir enfileiramento de HTML/ID interno por solicitação com UUID; SMTP deixa o contrato técnico comum. |
| Persistência comum | `DbSession`, `ErrorLogService`, `OutboxPublisher` | Manter colunas base e carimbos; tornar outbox optativa e retirar responsabilidades específicas ao final. |
| DI e processamento | [CommonModule](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/CommonModule.cs), [EmailQueueService](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/Services/EmailQueueService.cs), [dispatcher](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/Services/EmailOutboxDispatcher.cs), [processor](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/Services/EmailOutboxProcessor.cs), [SMTP](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/Services/SmtpEmailSender.cs) | Manter registro de erros separado; produtor passa a publicar envelopes; Notificações envia. |
| Casos de uso produtores | [UserRegistrationService](../src/Services/AuthService/Ouroboros.AuthService.Application/Services/UserRegistrationService.cs), [PasswordResetService](../src/Services/AuthService/Ouroboros.AuthService.Application/Services/PasswordResetService.cs) | Preservar atomicidade e regras de token, substituir dependências de fila/template. |
| Identidade | [Token](../src/Services/AuthService/Ouroboros.AuthService.Domain/Token.cs), [TokenRepository](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Persistence/Repositories/TokenRepository.cs), [AuthenticationService](../src/Services/AuthService/Ouroboros.AuthService.Application/Services/AuthenticationService.cs) | Trocar correlação do token. Login, refresh, logout, hash de senha e JWT permanecem em Auth. |
| Banco de Auth | `SqlUnitOfWork`, repositórios SQL, [migrations](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Migrations) | Adicionar outbox e correlação UUID; migrations independentes por banco. |
| Templates | [renderer](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Services/EmailTemplateRenderer.cs), [templates de e-mail](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Templates), [templates HTTP](../src/Services/AuthService/Ouroboros.AuthService.Api/Templates) | Migrar apenas templates de e-mail. Páginas de sucesso/falha da confirmação ficam no Auth. |
| Hosts e contratos HTTP | [Program Auth](../src/Services/AuthService/Ouroboros.AuthService.Api/Program.cs), [AuthModule](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/AuthModule.cs), [AuthController](../src/Services/AuthService/Ouroboros.AuthService.Api/Controllers/AuthController.cs), [Postman](../src/Services/AuthService/Ouroboros.AuthService.Api/Postman/Ouroboros.postman_collection.json) | Trocar composição; preservar requests/responses públicos, confirmação e recuperação. |
| Gateway | [Program](../src/ApiGateways/Ouroboros.ApiGateway/Program.cs), [rotas](../src/ApiGateways/Ouroboros.ApiGateway/appsettings.json) | Não transportar mensagens pelo gateway; avaliar somente rota operacional de saúde. |
| Infraestrutura | [Compose](../docker-compose.yml), [modelo de ambiente](../.env.example), [init Postgres](../docker/postgres/init/01-create-auth-db.sh), [Dockerfile Auth](../src/Services/AuthService/Ouroboros.AuthService.Api/Dockerfile) | Novo host, banco/role e broker; SMTP sai de Auth. |
| Diagnóstico | [GlobalExceptionHandler](../src/Services/AuthService/Ouroboros.AuthService.Api/GlobalExceptionHandler.cs) | Propagar correlação por outbox/broker; handlers HTTP não capturam falhas de workers. |
| Verificação | [tests](../tests), [Directory.Build.targets](../Directory.Build.targets) | Adaptar unitários e acrescentar integração real de transação, concorrência e recuperação. |

## Comportamento atual

### Cadastro e recuperação

`CreateUserAsync` verifica login/e-mail, cria usuário inativo e executa a gravação dentro de `UnitOfWork.ExecuteInTransactionAsync`. O fluxo gera token, monta URL pública, renderiza HTML e chama `IEmailQueueService.EnqueueAsync`. Esse método adiciona `EmailMessage`, executa `SaveChangesAsync` e devolve seu `long Id`. O token guarda esse ID, hash e validade de 24 horas; o commit da unidade de trabalho confirma o conjunto. O `SaveChanges` intermediário participa da mesma transação e do mesmo contexto; não é um commit independente.

`RequestPasswordResetAsync` retorna silenciosamente para conta inexistente. Para conta existente, invalida tokens anteriores e cria token, e-mail e validade de uma hora na mesma transação. Um pedido novo não cancela a mensagem de e-mail anterior: um link invalidado ainda pode chegar. Confirmação e redefinição verificam tipo, uso e expiração no Auth; não consultam o estado da entrega.

`Token.EmailMessageId` é obrigatório no construtor, mas não tem navegação nem FK. Isso está explícito no domínio e na [InitialCreate](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Migrations/20260903001615_InitialCreate.cs). Não se deve substituir esse número por um ID interno do banco de Notificações.

O hash protege a coluna de token; **o token bruto está persistido indiretamente no HTML da fila, dentro da URL**. A descrição de que ele nunca é persistido, em [0003](../docs/0003%20-%20Autenticação.md), precisa ser corrigida. Esse documento também ainda afirma que não há worker, embora ele esteja implementado e registrado.

### Entrega local

O Auth persiste o negócio no schema `auth` e `ErrorLog`/outbox no schema `common` do banco `ouroboros_auth`. `AddCommon` registra os serviços comuns; `AddTransactionalOutbox` registra a outbox e o background service.

O dispatcher seleciona mensagens não enviadas com tentativas abaixo do limite, ordena por ID e pega um lote. Tenta cada mensagem e salva o lote inteiro ao final. Configuração atual: intervalo de 15 segundos, lote de 20, cinco tentativas. Não há agendamento individual, backoff, lease, exclusão entre instâncias, tabela de tentativas ou expiração de entrega. A linha guarda apenas contagem, último erro truncado a 500 caracteres e datas. Uma queda antes de salvar pode repetir vários envios do lote.

`SmtpEmailSender` usa MailKit 4.17.0 e `SecureSocketOptions.Auto`; não autentica no SMTP e não configura identidade estável de mensagem. O sucesso inclui `DisconnectAsync`, portanto uma falha após aceitação do conteúdo ainda pode ser registrada como falha e gerar reenvio. Mailpit é o destino local; não há configuração completa de provedor de produção.

O Compose sobe Postgres 17 e Mailpit como infraestrutura; Auth e gateway ficam no profile `apps`. Auth depende hoje da saúde de Mailpit para subir, mesmo que possa aceitar solicitações com SMTP indisponível. O script cria banco/role de Auth, mas não revoga privilégios de `PUBLIC`: banco e role separados, isoladamente, não demonstram a proibição de conexão cruzada. Validar permissões efetivas na implementação; não afirmar que este levantamento comprovou isolamento em execução.

## Arquitetura proposta

1. Auth determina **por que**, **para quem** e **com quais dados de negócio** enviar. Gera um `NotificationRequestId` UUID e registra token e envelope em `common.outbox_messages` na mesma transação.
2. Um publisher em segundo plano no próprio Auth lê apenas sua outbox, publica no broker e registra a confirmação de publicação.
3. Notificações consome a solicitação e grava sua deduplicação e entrega pendente em uma única transação no próprio banco. Só depois confirma o consumo. O consumidor não chama SMTP.
4. Um dispatcher de entrega no processo de Notificações renderiza o template versionado e envia pelo provedor. Persiste tentativas, resultado e próximo horário de tentativa por mensagem.
5. O usuário segue usando os endpoints de Auth para confirmar cadastro ou redefinir senha. Aceitar uma solicitação ou enviar e-mail não ativa uma conta.

Um host `Ouroboros.NotificationsService.Api` comporta consumidor e dispatcher como serviços de fundo, além de saúde HTTP. Não há necessidade de separar API e worker em dois deploys agora. Quatro camadas, banco `ouroboros_notifications`, role `notifications_service`, schema `notifications`; `common.error_logs` permanece local também nesse banco.

BuildingBlocks fornece `Entity`, sessões SQL, erros, envelope/outbox e adapter de transporte reutilizável. Templates, destinatários, estados SMTP e tentativas pertencem a Notificações. Nenhum `ProjectReference` de Auth para camadas de Notifications ou vice-versa. Proposta: um pequeno projeto neutro `src/Contracts/Ouroboros.Contracts.Notifications`, contendo somente DTOs versionados, referenciado pelas Applications. A regra de dependências deve explicitar essa exceção para contratos de integração; não colocar entidades de persistência ou SDK do broker nesse projeto. Ele é código compartilhado, sem banco, e requer revisão como parte desta spec.

Não criar consumidor de status em Auth nesta etapa: seus casos de uso não dependem disso. Caso pedidos/pagamentos precisem de confirmação de entrega no futuro, definir evento próprio então; não criar uma segunda rede de eventos apenas para espelhar histórico.

## Contrato de integração

Nome proposto: `EmailNotificationRequestedV1`, tipo lógico `notifications.email.requested`, versão `1`. A semântica é uma solicitação explícita de envio, sem exigir que Notificações interprete eventos internos de Auth.

| Campo | Regra |
|---|---|
| `MessageId` | UUID estável do envelope; igual ao `NotificationRequestId` nesta relação de uma solicitação para um e-mail. Reutilizado em toda republicação. |
| `Producer` | Identificador lógico, inicialmente `auth`; validar contra identidade/permissão do produtor, não confiar somente no JSON. |
| `OccurredAt` | UTC da criação da solicitação. |
| `SchemaVersion` / `MessageType` | Validar antes da desserialização específica; não usar nome de classe CLR como contrato de rede. |
| `Recipient` | Um destinatário, validado; sem CC/BCC nesta versão. |
| `TemplateKey`, `TemplateVersion`, `Locale` | Inicialmente `auth.email-confirmation` e `auth.password-reset`, versão `1`, `pt-BR`. |
| `Data` | Objeto validado por template: `FullName` e `ConfirmationUrl` ou `ResetUrl`. Sem senha, JWT ou entidade `User` serializada. |
| `ExpiresAt` | Mesmo instante de expiração do token. Calcular uma vez no produtor para token e solicitação. |
| `CorrelationId` | O `X-Correlation-Id` da requisição de origem, persistido e repassado para ligar os logs dos dois serviços; não substituir o UUID de idempotência por ele. |

Rejeitar campos obrigatórios ausentes, versões desconhecidas, template não permitido ao produtor e payload maior que limite configurado (proposta inicial: 64 KiB). Mudanças compatíveis adicionam campos opcionais; alterações semânticas exigem nova versão, com consumidor implantado antes do produtor. Retirar uma versão somente depois de esvaziar outbox, fila e quarentena correspondentes.

`Token.NotificationRequestId` será `Guid`, correlação sem FK remota e sem navegação para entrega. Não vincular o token por FK à outbox local: o token pode sobreviver à limpeza de envelopes publicados. O produtor gera o UUID antes de persistir, eliminando a necessidade de `SaveChanges` só para obter ID da mensagem. O contrato de outbox adiciona entidade ao mesmo contexto da unidade de trabalho e não confirma transação por conta própria.

A idempotência descrita é de uma solicitação. Repetir uma requisição HTTP de recuperação pode criar outro token e outra solicitação; não deduplicar por e-mail ou por hash de payload. Preservar a regra atual de invalidar tokens anteriores. Mensagens podem chegar fora de ordem; Auth continua sendo a autoridade sobre a validade do link. Cancelamento distribuído de mensagens antigas fica fora desta etapa.

## Modelo de persistência e garantias

Todas as novas entidades persistidas herdam `Entity`: `id`, `external_id` único, `created_at`, `updated_at`, seguindo ordem, UTC e snake_case existentes. IDs internos não atravessam serviços.

| Local/tabela proposta | Conteúdo e índices |
|---|---|
| Produtor: `common.outbox_messages` | `external_id` como `MessageId`, tipo, versão, payload JSON, contexto de trace, estado, `published_at`, `attempt_count`, `next_attempt_at`, último erro sanitizado e lease. Índice para estado/próxima tentativa e criação. Não contém estado SMTP. |
| Notifications: `notifications.email_deliveries` | UUID próprio de entidade, `producer`, `request_id`, destinatário, template/versão/locale, dados mínimos, HTML/assunto renderizados, expiração, estado, contagem, próxima tentativa, lease, datas e ID de provedor quando existir. Restrição única `(producer, request_id)`. Esta tabela também cumpre a função de inbox para solicitações válidas: não criar inbox separada redundante. |
| Notifications: `notifications.email_delivery_attempts` | FK local para entrega, número da tentativa, início/fim, resultado, código de falha e erro sanitizado. Unicidade por entrega/número. Registrar início antes da chamada externa; interrupções podem terminar como resultado desconhecido. |
| Cada banco: `common.error_logs` | Erros técnicos do próprio serviço, sem conteúdo de e-mail/token. Não é histórico de tentativas de negócio. |

Aplicar leases com aquisição atômica em transação curta, por exemplo seleção `FOR UPDATE SKIP LOCKED` e atualização do proprietário/expiração. Não manter transação/lock aberto durante chamada de rede. Timeout de rede menor que duração do lease, renovação para operações longas e atualização condicionada ao proprietário; lease vencido pode ser recuperado. Começar com concorrência pequena, mas testar duas instâncias desde a implantação para evitar duplicação trivial em reinícios sobrepostos. Lease reduz sobreposição; não cria transação com SMTP.

| Fronteira de falha | Comportamento obrigatório |
|---|---|
| Falha antes do commit de Auth | Nem alteração de negócio nem outbox ficam confirmadas. |
| Broker indisponível após commit | Cadastro/recuperação permanecem aceitos; envelope fica pendente e publisher tenta depois. |
| Publicação confirmada, queda antes de `published_at` | Republicar com mesmo ID; deduplicação do consumidor evita outra entrega. |
| Consumer grava, mas perde ACK | Redelivery consulta unicidade e confirma sem criar novo envio. Inserção concorrente é resolvida pela restrição do banco. |
| Banco de Notifications falha | Não dar ACK. Reconectar com espera, sem ciclo acelerado de requeue. |
| SMTP aceitou, queda antes de salvar resultado | Resultado é incerto; nova tentativa pode duplicar o e-mail. |

Deduplicação deve comparar também um hash canônico do conteúdo imutável: mesmo `(producer, request_id)` com conteúdo diferente é conflito, vai para quarentena e alerta; nunca sobrescrever envio existente. Manter a chave mesmo após apagar conteúdo sensível, por período que cubra retenção e replay. Proposta inicial: metadados de deduplicação por 90 dias e replay limitado a essa janela; restaurar backup antigo ou reter mensagens além dela exige reconciliação e extensão da retenção antes de publicar.

Estados de entrega propostos: `Pending`, `Sending`, `RetryScheduled`, `AcceptedByProvider`, `Failed`, `Expired` e `Unknown`. `AcceptedByProvider` significa aceitação SMTP, não chegada à caixa, leitura ou ausência de bounce. Um `Message-Id` SMTP determinístico ajuda diagnóstico, mas não obriga servidores a deduplicar. A janela ambígua é inerente ao envio sem transação/idempotência no provedor; não prometer exactly-once. Ver [RFC 5321](https://www.rfc-editor.org/rfc/rfc5321), seções 4.5.3.2 e 6.1. A aplicação também pode esgotar tentativas ou expirar solicitações: não há garantia de entrega eventual de todo e-mail.

## Mensageria: avaliação e proposta

Não há escolha técnica prévia a preservar: [Compose](../docker-compose.yml) e os `.csproj` não incluem broker/SDK. PostgreSQL atende persistência, mas sua presença não justifica Notificações consultar tabelas alheias. Uma fila em memória não atende recuperação após queda.

| Alternativa | Avaliação para este projeto |
|---|---|
| RabbitMQ + cliente .NET oficial | Proposta preferida: introdução concentrada em publisher/consumer para comandos de entrega, com broker local em Compose. Exige implementar e testar reconexão, confirmações, ACK, quarentena e contexto de trace. |
| RabbitMQ + framework de barramento | Pode reduzir código de plumbing, mas acrescenta convenções/dependência e possível mecanismo próprio de outbox/inbox. Avaliar compatibilidade, versão e licença na implementação; não combinar duas outboxes sem desenho explícito. |
| Broker gerenciado | Alternativa se houver decisão de hospedagem em nuvem; hoje não há provedor/cloud definido no repositório. Não assumir custo ou disponibilidade contratados. |
| Kafka | Não há requisito atual de log de eventos com replay/stream processing que justifique escolhê-lo nesta entrega. Reavaliar se esse requisito surgir. |
| HTTP a partir da outbox | Possível transporte em outro desenho, mas diverge da direção escolhida de mensageria e exigiria revisão dela. |

Proposta concreta para RabbitMQ: exchange durável `notifications`, routing key `email.requested.v1`, fila `notifications.email.requests.v1`, mensagens persistentes e publisher confirms. Tratar retorno por mensagem sem rota (`mandatory`) como falha; confirmação do broker não significa consumo. ACK manual somente após commit local. Esses mecanismos são independentes, conforme a [documentação de confirmações](https://www.rabbitmq.com/docs/confirms).

Propor fila quorum durável com volume persistente. Um único nó no desenvolvimento não oferece alta disponibilidade; quorum replicado exige topologia própria fora do escopo local. A escolha da imagem/versão deve ser fixada e validada na implementação. Ver [quorum queues](https://www.rabbitmq.com/docs/quorum-queues).

Provisionar exchange, fila, bindings e fila durável de quarentena antes de ativar produtores. Mensagem estruturalmente inválida ou versão desconhecida é publicada em quarentena com confirmação antes de ACK da original; queda pode duplicar a entrada de quarentena, que deve conservar ID/fingerprint. Não depender de um `reject` que possa descartar silenciosamente. Falha na quarentena mantém original sem confirmação e gera alerta. Replay usa o mesmo ID e exige corrigir a causa; não reenviar tudo indiscriminadamente.

Contas e permissões distintas: Auth publica somente na rota permitida; Notifications consome sua fila e publica quarentena; identidade administrativa separada provisiona topologia. Em AMQP, validar origem com rota/credencial autorizada e allowlist, não apenas `Producer` declarado no corpo. Usar vhost por ambiente, TLS e credenciais fora de arquivos versionados; administração do broker acessível apenas no desenvolvimento local/rede administrativa.

## Retentativas, falhas e operação

Separar três políticas: publicação da outbox, ingestão pelo consumidor e entrega SMTP. Tentativas de publicar não consomem tentativas de SMTP. Depois do commit de ingestão, falha SMTP nunca devolve a solicitação à fila do broker: o dispatcher retoma pelo banco de Notifications.

Proposta inicial configurável: publisher com backoff exponencial e jitter de 1 segundo até 5 minutos, sem descarte por indisponibilidade; SMTP com cinco tentativas totais e intervalos de 15 segundos, 1 minuto, 5 minutos e 15 minutos. Respeitar `ExpiresAt` antes de cada envio, inclusive após espera/reinício; marcar `Expired` quando a validade acabou. Auth verifica expiração novamente quando o link é usado. Filas não garantem que o usuário receba antes do vencimento.

Falhas transitórias de conexão e respostas temporárias entram em retry; destinatário inválido/falha permanente vai para `Failed`; autenticação/TLS/configuração incorreta gera alerta operacional e pausa controlada do envio para não esgotar todos os registros. Classificação deve usar tipo/código disponível, não comparação de texto da exceção. Timeout após submissão e queda deixam tentativa `Unknown`; proposta é repetir dentro do orçamento, aceitando duplicação possível e registrando esse motivo. Encerramento voluntário não deve inventar falha de destinatário.

Operação inicial por ferramenta administrativa autenticada com a role do próprio serviço, sem API pública de reenvio: consultar UUID, causa e tentativas; corrigir configuração; reativar entrega não expirada preservando request ID e histórico. Registrar operador, motivo e data. Reenviar entrega já aceita exige ação explícita separada, pois causa outro e-mail. Para token expirado, só Auth pode emitir novo token/solicitação; não estender validade no serviço central. Não criar endpoint de reenvio de cadastro nesta migração: é pendência de produto, já relevante no fluxo atual.

## Templates e dados sensíveis

Proposta: mover os dois templates HTML e a renderização de e-mails de Auth para Notifications. Auth continua montando URLs de negócio usando `App:PublicBaseUrl`; não transfere geração, hash ou validação de token. Assuntos passam a fazer parte do template versionado. Notifications nunca consulta Auth para completar dados faltantes.

O renderer atual faz `string.Replace` sem escape. O novo deve validar esquema dos dados, codificar valores em contexto HTML/atributo, limitar nomes de template a um catálogo e rejeitar placeholders ausentes. URLs devem ser validadas contra origens/esquemas configurados, preservando o escape do token. Não aceitar caminho de arquivo fornecido pela mensagem. Copiar recursos para output **e publish**; testar no container.

Renderizar e persistir assunto/corpo uma única vez antes da primeira tentativa; retries usam o mesmo conteúdo. Preservar versões antigas de template até todas as solicitações daquela versão serem processadas. Template inexistente é falha operacional tratável, não perda silenciosa. Um modo restrito `legacy-rendered.v1` pode existir somente para a importação de pendências antigas, sem transformar o contrato normal em envio arbitrário de HTML.

URLs com token atravessarão outbox, broker e banco de Notifications. Proposta: apagar payload/HTML após estado terminal e vencimento do link mais 24 horas; manter apenas metadados mínimos para deduplicação/diagnóstico por 90 dias. Os prazos precisam ser revisados pelo usuário conforme necessidade de suporte; incluir filas de quarentena, backups e logs na política. Restringir acesso aos conteúdos e mascarar destinatário; nunca colocar corpo, URL com token, senha SMTP ou connection string em logs/traces. Não copiar a chave privada JWT para Notifications.

## Infraestrutura, configuração e isolamento

Manter uma instância PostgreSQL, com novo banco lógico. `NotificationsModule` concentra persistência SQL e casos de uso, usando o schema `notifications`. Separar `AddCommon` da entrega específica.

Criar script de provisionamento de banco/role em `docker/postgres/init/`, LF, e caminho administrativo repetível para instalações com volume existente: entrypoint de init não roda novamente só porque entrou um arquivo novo. Não exigir `down -v` nem recriação do banco. Aplicar migrations SQL de Auth e Notifications separadamente pelo Database Migrator, mantendo a convenção atual de não migrar ao subir Compose.

Revisar privilégios de `PUBLIC`: revogar `CONNECT`/`TEMPORARY` nos bancos de serviço e conceder conexão à role correta; revisar schemas, tabelas, privilégios padrão e ausência de memberships cruzados. O PostgreSQL concede alguns privilégios a `PUBLIC` por padrão, incluindo conexão a bancos; ver [privilégios PostgreSQL](https://www.postgresql.org/docs/current/ddl-priv.html). Teste negativo deve demonstrar que `auth_service` não conecta/acessa Notifications e vice-versa; credenciais administrativas não entram nas aplicações. Esse ajuste corrige a lacuna entre a convenção escrita e o script observado.

| Componente | Configuração proposta |
|---|---|
| Auth | `ConnectionStrings:Postgres`, `App:PublicBaseUrl` e JWT continuam; adicionar `Messaging` e `Outbox`; retirar `EmailOutbox`/SMTP após corte. |
| Notifications | Connection string própria, `Messaging`, `EmailDelivery` (lote, retries, timeouts, leases), `Smtp` (host, porta, remetente, usuário/senha opcionais, TLS explícito). |
| Secrets | `NOTIFICATIONS_DB_PASSWORD`, credenciais de broker distintas e SMTP no `.env.example` apenas como nomes/exemplos; valores via User Secrets/ambiente/secret montado. Validar opções no startup. |
| Compose | `notifications-api` no profile `apps`, sem `ports`, porta interna 8080, contexto raiz, Dockerfile multiestágio .NET 10, usuário não root, healthcheck e `restart: unless-stopped`. |
| IDE | Propor portas 5083/7273 em `launchSettings.json`, verificando disponibilidade no momento da implementação. |
| Broker | Infraestrutura fora de `apps`, volume, healthcheck e restart; portas de desenvolvimento vinculadas a localhost, sem exposição pública de administração. |
| SMTP | Mailpit permanece para desenvolvimento, acessado por Notifications. Retirar `depends_on: mailpit` de Auth. Produção exige política TLS explícita e credencial do provedor quando necessária; não herdar fallback implícito de `Auto`. |

`/health` verifica processo; `/health/ready` de Notifications verifica banco e capacidade do consumidor de receber mensagens. SMTP indisponível sinaliza degradação/alarme de entrega, sem impedir persistência de solicitações. Notifications pode continuar enviando entregas já persistidas enquanto broker está indisponível. Auth mantém prontidão de negócio dependente do próprio banco, sem depender de SMTP, Notifications ou broker para aceitar transações locais. No Compose, dependências reais usam `service_healthy`.

A skill devops exige rota no gateway para Api nova. Proposta mínima: rotas exatas de saúde `/api/notifications/health` e `/api/notifications/health/ready`, transformadas para os caminhos internos e com política de rate limiting operacional; sem catch-all nem endpoint de envio público. Saúde é a exceção anônima já prevista nas convenções. Não adicionar `depends_on` do gateway em Notifications, para sua indisponibilidade não bloquear o acesso ao Auth. Endpoints futuros de consulta/reprocessamento exigem definição própria de autorização antes de implementação; fallback autenticado continua sendo a convenção para hosts HTTP. Se se preferir saúde somente interna, registrar a revisão da regra da skill na implementação.

## Diagnóstico e critérios operacionais

O projeto não tem rastreamento distribuído: a correlação hoje é o `X-Correlation-Id` posto pelo Api Gateway, gravado em `error_logs.trace_id`. Uma mensagem armazenada e publicada depois precisa carregar essa correlação: persistir o identificador de correlação na outbox, propagá-lo no envelope publicado e relê-lo no consumer, para que publicação, ingestão e tentativa apareçam no log com o mesmo id da requisição de origem. Manter também o UUID da solicitação em logs estruturados. Não colocar tokens nem payload em log.

Falhas de workers devem ter recuperação no próprio loop e log correlacionado; `GlobalExceptionHandler` cobre apenas HTTP. Erro de persistência de diagnóstico não pode impedir recuperação nem resultar em ACK prematuro. Histórico durável fica no banco: o log do processo é volátil e não substitui esse histórico.

Expor contagem de pendentes, idade da solicitação mais antiga, tempo até aceitação, taxas de retry/falha/expiração, conflitos de idempotência, quarentena e leases vencidos. Proposta de alertas iniciais: qualquer falha terminal/conflito, quarentena não vazia ou pendência acima de cinco minutos. Ajustar depois com dados; não prometer SLO de entrega SMTP. O projeto não tem backend de métricas: começar com instrumentos .NET e consultas/logs documentados para operação, deixando implantação de coletor de métricas como decisão explícita.

## Transição dos dados e implantação

Preservar migrations históricas: [InitialCreate](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Migrations/20260903001615_InitialCreate.cs), [navegações](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Migrations/20260903115155_AddTokenAndRefreshTokenNavigations.cs) e [tracking](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Migrations/20260903171415_AddEmailMessageDeliveryTracking.cs). Novas migrations fazem expansão e remoção em momentos diferentes; não reescrever passado.

| Etapa | Trabalho concreto | Critério de aceite |
|---|---|---|
| 1. Fechar decisões | Revisar contrato, broker/cliente, retenção e modo de corte. Confirmar se há dados úteis fora do ambiente local. | Decisões pendentes registradas antes de implementar dependências e políticas. |
| 2. Expandir | Criar contratos/outbox, coluna nullable `notification_request_id` em `auth.tokens`, banco/role e modelo de Notifications; provisionar broker. Manter legado disponível. | Migration funciona em banco vazio e cópia do legado; nenhuma linha antiga é apagada; isolamento cruzado testado. |
| 3. Preparar consumidor | Implantar consumidor idempotente e dispatcher inicialmente desabilitado por opção, templates versionados e testes de ingestão. | Mensagem duplicada cria uma entrega; crash antes/depois de ACK recupera; nenhum envio de teste real fora de Mailpit. |
| 4. Cortar produtor | Em janela curta de manutenção, parar todas as instâncias de Auth/worker antigo e esperar chamadas SMTP em curso terminarem. Inventariar pendências, executar backfill/exportador local e subir Auth com novo produtor/publisher. | Nenhuma instância antiga envia; cada operação nova grava token/outbox atomicamente; broker fora não impede operação de negócio. |
| 5. Ativar entrega | Habilitar dispatcher central, reconciliar exportação/ingestão e executar os dois fluxos via gateway/Mailpit. | Pendências têm destino ou classificação terminal; usuário confirma/redefine pelo Auth; rastreio permite localizar envio. |
| 6. Contrair | Após reconciliação e janela de rollback, retirar tabela/coluna/classes antigas, modo de importação e flags temporárias. | Zero pendência não classificada, correlações preservadas, histórico retido conforme política e nenhum produtor usa SMTP. |

O corte curto evita construir coordenação permanente entre dois caminhos. Se indisponibilidade breve não for aceitável, será necessário aprovar e desenhar rollout gradual antes da implementação; não ativar dois senders sobre os mesmos registros.

Procedimento de pendências durante o corte:

1. Fazer backup e inventário **pelo Auth**, com contagens de enviados, não enviados elegíveis, tentativas esgotadas, tokens expirados/validados e linhas sem token correspondente. Como não há FK, não assumir que todas as associações existem ou são únicas.
2. Para cada mensagem antiga, usar seu `external_id` como UUID estável de correlação e preencher tokens associados por join local de `email_message_id = email_messages.id`. Se houver inconsistência, registrar exceção para análise; manter coluna nova nullable até resolver, sem inventar vínculo.
3. Para pendentes com token ainda válido, exportador executado no contexto do Auth cria envelope `legacy-rendered.v1` com HTML/assunto existentes, mesmo UUID, validade e resumo das tentativas anteriores. Inserção na outbox e marcação local de exportação ocorrem na mesma transação, com unicidade para permitir retomada. Não regenerar token: só há hash na entidade, mas o link necessário já está no HTML.
4. Manter histórico legado como resumo (`attempt_count`, último erro/data), sem fabricar tentativas individuais que nunca foram guardadas. Tentativas SMTP anteriores contam para o orçamento; mensagem esgotada requer revisão explícita, não reset automático do contador. Mensagens expiradas/validadas não são enviadas; classificá-las. Mensagens sem vínculo exigem análise pelo produtor antes de exportação.
5. Enviados não são reenviados; conservar histórico local em modo somente leitura até expurgar ou importar como registro histórico com envio desabilitado. Se importar histórico, usar fluxo distinto de solicitação. Manter um manifesto de IDs exportados/classificados para conciliação.
6. Reconciliar quantidade e UUIDs de outbox confirmada, entregas ingeridas e exceções. Uma confirmação de publicação sozinha não prova ingestão; obter relatório do próprio Notifications e comparar relatórios, sem dar à aplicação credenciais cruzadas. Resolver diferenças antes de remover legado.

Mesmo com shutdown limpo, registros afetados por queda anterior podem ter resultado SMTP desconhecido. Listar esse risco no relatório do corte; não deduzir que `Sent=false` prova ausência de entrega.

Rollback antes da ativação central permite voltar ao legado mantendo expansão de schema. Depois que o central enviou, **não religar o sender antigo automaticamente**: pausar novas entregas, reconciliar IDs já aceitos/incertos e decidir avanço corretivo. Restaurar snapshot do Auth e reenviar toda sua fila pode duplicar mensagens. Remoções destrutivas somente depois do período acordado de observação/backup; o `Down` de migration não desfaz e-mails enviados.

## Inventário de implementação futura

Os caminhos existentes estão ligados ao repositório; os nomes abaixo marcados como novos são propostas, não arquivos já criados. Diretórios nas tabelas abrangem somente os arquivos nomeados na mesma linha.

| Ação | Arquivos/caminhos | Alteração prevista |
|---|---|---|
| Criar | `src/Contracts/Ouroboros.Contracts.Notifications/Ouroboros.Contracts.Notifications.csproj`, `EmailNotificationRequestedV1.cs` e esquemas/fixtures de contrato | DTOs neutros e validação de compatibilidade. |
| Criar | BuildingBlocks.Domain: `OutboxMessage.cs`; Application: `Interfaces/IOutboxWriter.cs`, `Interfaces/IMessagePublisher.cs`, `Models/OutboxOptions.cs`; Infrastructure: `Services/OutboxWriter.cs`, `Services/OutboxPublisher.cs`, `Services/OutboxProcessor.cs`, `Messaging/RabbitMqMessagePublisher.cs`, `Options/MessagingOptions.cs`, `Persistence/OutboxEntityConfiguration.cs` | Persistência técnica transacional, publicação confirmada, leases e contexto. Nomes dentro dos projetos homônimos existentes em `src/BuildingBlocks/`. |
| Alterar | [CommonModule.cs](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/CommonModule.cs), [csproj Infrastructure](../src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/Ouroboros.BuildingBlocks.Infrastructure.csproj) | Separar registros, adicionar cliente de broker escolhido e manter SQL explícito. |
| Criar | `src/Services/NotificationsService/Ouroboros.NotificationsService.Domain/`: csproj, `EmailDelivery.cs`, `EmailDeliveryAttempt.cs`, `EmailDeliveryStatus.cs` | Estados e regras de entrega. |
| Criar | `src/Services/NotificationsService/Ouroboros.NotificationsService.Application/`: csproj, `Interfaces/IEmailDeliveryRepository.cs`, `IUnitOfWork.cs`, `IEmailSender.cs`, `IEmailTemplateRenderer.cs`, `Services/CreateEmailDeliveryService.cs`, `SendEmailDeliveryService.cs` | Casos de uso sem EF/AMQP/SMTP concreto. |
| Criar | `src/Services/NotificationsService/Ouroboros.NotificationsService.Infrastructure/`: csproj, `NotificationsModule.cs`, `Persistence/SqlUnitOfWork.cs`, repositório SQL, `Migrations/`, `Messaging/EmailNotificationConsumer.cs`, `Services/EmailDeliveryProcessor.cs`, `SmtpEmailSender.cs`, `EmailTemplateRenderer.cs`, `Options/`, `Templates/` | Consumer, envio, agendamento, banco e templates. MailKit fica aqui. |
| Criar | `src/Services/NotificationsService/Ouroboros.NotificationsService.Api/`: csproj, `Program.cs`, `appsettings.json`, `Properties/launchSettings.json`, `Dockerfile` | Host, DI, saúde, telemetria e configuração sem segredos. |
| Alterar | [Token.cs](../src/Services/AuthService/Ouroboros.AuthService.Domain/Token.cs), [UserRegistrationService.cs](../src/Services/AuthService/Ouroboros.AuthService.Application/Services/UserRegistrationService.cs), [PasswordResetService.cs](../src/Services/AuthService/Ouroboros.AuthService.Application/Services/PasswordResetService.cs), csproj Application Auth | UUID e contrato de solicitação; manter URLs/regras/atomicidade. |
| Alterar | [AuthModule.cs](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/AuthModule.cs), [migrations SQL](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Migrations) | Mapear outbox e UUID. Revisar `SqlUnitOfWork` para retry da transação não gerar novas solicitações involuntárias. |
| Alterar | [Program Auth](../src/Services/AuthService/Ouroboros.AuthService.Api/Program.cs), [appsettings Auth](../src/Services/AuthService/Ouroboros.AuthService.Api/appsettings.json), [csproj Infrastructure Auth](../src/Services/AuthService/Ouroboros.AuthService.Infrastructure/Ouroboros.AuthService.Infrastructure.csproj), [Dockerfile Auth](../src/Services/AuthService/Ouroboros.AuthService.Api/Dockerfile) | Trocar registros/configuração, retirar cópia de templates de e-mail, incluir novo projeto Contracts no restore Docker. |
| Migrar e remover ao final | BuildingBlocks: `EmailMessage.cs`, `IEmailQueueService.cs`, `IEmailSender.cs`, `EmailOutboxOptions.cs`, `EmailQueueService.cs`, `EmailOutboxDispatcher.cs`, `EmailOutboxProcessor.cs`, `SmtpEmailSender.cs` nos caminhos do mapa | Reaproveitar regras úteis em Notifications, sem deixar dois pipelines ativos. Novas outbox/entrega não são simples rename da classe antiga. |
| Migrar e remover ao final | Auth: Application `Interfaces/IEmailTemplateRenderer.cs`, `Models/EmailTemplateNames.cs`; Infrastructure `Services/EmailTemplateRenderer.cs`, `Templates/UserCreationValidationEmail.html`, `Templates/PasswordResetEmail.html` | Templates e renderer ficam em Notifications. Preservar `Api/Templates/ConfirmationSuccess.html` e `ConfirmationFailure.html`. |
| Criar temporariamente | Auth Infrastructure `Migration/LegacyEmailOutboxExporter.cs` e ferramenta administrativa de execução; manifesto exportado fora do Git | Backfill/exportação local retomável, sem credenciais do banco central. Remover ferramenta após transição e política de histórico cumpridas. |
| Criar/alterar | `docker/postgres/init/02-create-notifications-db.sh`, script administrativo repetível para volumes existentes, [01-create-auth-db.sh](../docker/postgres/init/01-create-auth-db.sh), configuração versionada de topologia do broker sem senhas | Banco, roles, privilégios e provisionamento de mensageria. |
| Alterar | [docker-compose.yml](../docker-compose.yml), [.env.example](../.env.example), [gateway appsettings](../src/ApiGateways/Ouroboros.ApiGateway/appsettings.json), [gateway Program](../src/ApiGateways/Ouroboros.ApiGateway/Program.cs) se nova política necessária | Containers/configuração e rotas exatas de saúde, sem dependência de disponibilidade no gateway. |
| Revisar | [.dockerignore](../.dockerignore), [.gitattributes](../.gitattributes), [solution](../Ouroboros.slnx), [Directory.Build.targets](../Directory.Build.targets) | Garantir LF, proteção de segredos, inclusão de novos projetos/testes. Não copiar dados do manifesto na imagem. |

## Plano de testes e aceite funcional

Os testes existentes cobrem regras de `EmailMessage`, fila, dispatcher, cadastro, reset e tokens. [FakeUnitOfWork](../tests/Services/AuthService/Ouroboros.AuthService.Application.Tests/Fakes/FakeUnitOfWork.cs) apenas conta chamadas e executa delegates; os testes EF usam InMemory. Eles não demonstram rollback real, constraints PostgreSQL, locking ou garantias do broker.

| Camada/projeto | Testes a criar ou ajustar |
|---|---|
| BuildingBlocks.Domain.Tests | Substituir [EmailMessageTests](../tests/BuildingBlocks/Ouroboros.BuildingBlocks.Domain.Tests/EmailMessageTests.cs) por regras de outbox; regras específicas migradas para Notifications.Domain.Tests. |
| BuildingBlocks.Infrastructure.Tests | Substituir os testes legados de persistência por testes SQL contra PostgreSQL; publisher com confirms/sem rota, lease, retomada e republicação do mesmo UUID. Mover fake SMTP para Notifications. |
| Auth.Domain.Tests | Ajustar [TokenTests](../tests/Services/AuthService/Ouroboros.AuthService.Domain.Tests/TokenTests.cs) para UUID, mantendo uso/expiração/relações de negócio. |
| Auth.Application.Tests | Ajustar `UserRegistrationServiceTests.cs`, `PasswordResetServiceTests.cs`, `AuthTestContext.cs` e fakes de fila/template; novo fake de outbox. Validar conteúdo/UUID/expiração iguais ao token, invalidação anterior e silêncio para conta inexistente. |
| Auth.Infrastructure.Tests | Ajustar construção de tokens em `TokenRepositoryTests.cs`; integração PostgreSQL para falha depois de adicionar usuário/token/outbox, commit e rollback, backfill com associação ausente e exportação retomável. |
| Notifications.Domain.Tests | Estados, tentativas, expiração, orçamento, falha permanente e reprocessamento auditado. |
| Notifications.Application.Tests | Ingestão idempotente, conflito de conteúdo, renderização única, classificação de falhas, aceitação distinta de entrega ao destinatário. |
| Notifications.Infrastructure.Tests | PostgreSQL real para unicidade/concorrência, RabbitMQ real para ACK/redelivery/restart, SMTP controlado para falha antes/depois de aceitação, duas instâncias e lease vencido, publicação de quarentena confirmada. |
| Notifications.Api.Tests | Saúde/liveness/readiness e configuração. Auth.Api.Tests conserva contratos públicos e validações. Teste de stack via HTTP/AMQP, sem referência entre implementações de serviços. |

Criar os quatro projetos de testes Notifications sob `tests/Services/NotificationsService/` e incluir na solution; contratos podem ser validados por fixtures JSON nos testes dos consumidores/produtores, sem depender da implementação do outro serviço. Integração com recursos reais deve ter execução documentada e determinística, separada dos unitários por categoria quando necessário.

Aceite obrigatório da implementação:

- Cadastro e recuperação continuam retornando os contratos HTTP atuais; confirmação e reset funcionam pelo gateway a partir de e-mail recebido no Mailpit. Login, refresh e logout não regridem.
- Com broker/Notifications/SMTP desligados, Auth ainda confirma transação local; após recuperação, pendências válidas prosseguem, sem perda silenciosa.
- Redelivery/republicação e ingestão concorrente geram uma entrega por solicitação; teste de falha após aceitação SMTP documenta duplicação possível, sem afirmar unicidade ponta a ponta.
- Dois dispatchers não reclamam normalmente a mesma entrega; queda e lease vencido recuperam processamento. Nenhuma transação de banco fica aberta durante SMTP.
- Template ausente, payload inválido, erro permanente, timeout e expiração têm destino/estado observável, sem bloquear lote inteiro.
- Roles não acessam banco alheio; Auth não tem credenciais SMTP nem chave administrativa do broker; Notifications não recebe chave privada JWT.
- Stack funciona tanto pela IDE quanto por `docker compose --profile apps up -d --build`; migrations aplicadas separadamente; volume existente preservado.
- Backfill/exportação conciliados por UUID e contagem; legado removido somente após aceite. Todos os testes apropriados passam.

`Directory.Build.targets` hoje dispara testes da solution apenas ao buildar Auth.Api. Incluir novos testes na solution garante esse caminho, mas buildar Notifications isoladamente não dispara o target. Documentar comando explícito de testes da nova camada ou revisar o target sem recursão/execução duplicada. Docker continua usando `OUROBOROS_SKIP_AUTOTEST=true`. Nesta entrega documental não foram executados build ou testes de aplicação.

## Documentação a atualizar na implementação

| Documento | Atualização necessária |
|---|---|
| [0000 - Arquitetura](../docs/0000%20-%20Arquitetura.md) | Novo serviço, contratos neutros, broker, código comum versus dados locais, outbox técnica versus entrega central. |
| [0002 - Setup do Banco](../docs/0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md) | Banco/role Notifications, privilégios e provisionamento de volume já existente. |
| [0003 - Autenticação](../docs/0003%20-%20Autenticação.md) | Corrigir worker já existente e persistência indireta de token; descrever UUID, solicitação e limites de expiração. |
| [0005 - Repositórios e Unidade de Trabalho](../docs/0005%20-%20Repositórios%20e%20Unidade%20de%20Trabalho.md) | Remover referência ao `EmailMessageId` e atualizar atomicidade sem SaveChanges para obter ID de mensagem. |
| [0006 - Containers](../docs/0006%20-%20Rodando%20a%20Stack%20em%20Containers.md) | Broker, Notifications, saúde, portas, secrets, implantação e diagnóstico. |
| [0007 - Fila de E-mails](../docs/0007%20-%20Fila%20de%20E-mails%20%28Outbox%29.md) | Evoluir documento operacional para outbox transacional e entrega central; preservar explicação histórica quando útil. |
| [README](../README.md), [Services/README](../src/Services/README.md), [CLAUDE.md](../CLAUDE.md), skills dba/developer/devops/qa | Corrigir referências antigas de skills quando aplicável; revisar exemplos de EmailMessage comum, regra de Contracts, infraestrutura e comando de testes. |
| [Postman Auth](../src/Services/AuthService/Ouroboros.AuthService.Api/Postman/Ouroboros.postman_collection.json), [Excalidraw Auth](../docs/excalidraw/0003%20-%20Autenticação.excalidraw) | Preservar contratos existentes, atualizar instruções para aguardar entrega assíncrona; revisar fluxo desenhado ao implementar. |

`0001` (Git) e `0004` (SQL explícito e persistência) não demandam alteração funcional identificada. Este documento separado é uma proposta de evolução para revisão; `0007` ainda descreve a implementação atual e não foi substituído por uma arquitetura ainda inexistente.

## Decisões pendentes para a revisão

| Decisão | Proposta nesta spec | O que depende dela |
|---|---|---|
| Broker e cliente | RabbitMQ com cliente oficial .NET, versões a fixar na implementação | Pacotes, provisionamento e testes de transporte. |
| Contratos compartilhados | Projeto neutro em `src/Contracts`, sem camadas de outro serviço | Revisão explícita da convenção de referências. |
| Templates | Centralizados/versionados em Notifications; importação temporária de HTML legado | Contrato e sequência de deploy. |
| Corte | Janela curta com parada de todos os workers antigos | Confirmar necessidade de disponibilidade e existência/volume de dados reais. |
| Retenção/replay | Conteúdo até terminal + expiração + 24h; metadados 90 dias, replay limitado à janela | Política de suporte, backups, expurgo e deduplicação. |
| SMTP real | Adaptador SMTP inicialmente; provedor e credenciais ainda não definidos | TLS, autenticação, quotas e validação em ambiente real. Não bloqueia testes com Mailpit. |
| Operação de falhas | Ferramenta administrativa sem API pública; retry de resultado incerto com risco de duplicação | Procedimento de suporte e auditoria. |
| Recuperação de cadastro expirado | Não criar endpoint novo agora; definir futuramente reemissão pelo Auth | Experiência do usuário quando confirmação vence; não pode ser resolvida emitindo token em Notifications. |

Processo e banco próprios, outbox local por produtor e proibição de acesso cruzado já são a direção recebida. Essas decisões não precisam ser reabertas para analisar os pontos acima.
