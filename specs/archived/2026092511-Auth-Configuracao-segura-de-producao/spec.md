# 2026092511 - Configuracao segura de producao

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a configuração apareceu pensada só para desenvolvimento: Compose com `Development`, tokens logados em texto claro, container rodando como root, migrations executadas no startup da API e um único papel de banco com todos os privilégios. O usuário aprovou uma configuração de produção. Os forwarded headers foram para a 2026092501, e a remoção definitiva dos tokens do log fica para a futura spec de envio de e-mail.

## Análise

**Escopo reduzido na revisão de 06/10/2026.** Ficou só o que dá para implementar e testar localmente. O compose de produção, os Docker secrets, o TLS no proxy, a autenticação do Seq e o `AllowedHosts` ficam adiados (decisões 1, 2, 3, 9 e 10): não existe ambiente de destino, nada disso se testa aqui, e a decisão 5 já diz que o serviço não funciona fora de dev sem o envio de e-mail. Voltam em spec própria quando houver para onde implantar.

Extensão do `auth-service` e da infraestrutura do monorepo. Referências: guias do ASP.NET Core para hospedagem atrás de proxy e para configuração key-per-file, imagens .NET sem root (`APP_UID`) e o princípio do menor privilégio no SQL Server.

Decisões:
1. **Adiada: dois perfis (compose de produção).** `docker-compose.yml` continua sendo o de desenvolvimento. O `docker-compose.prod.yml` e o `appsettings.Production.json` voltam quando existir um ambiente de destino.
2. **Adiada: TLS no proxy de borda.** Sem proxy nem ambiente de produção, não há o que configurar nem testar.
3. **Adiada: segredos por Docker secrets (`AddKeyPerFile`).** Dependem do compose de produção. Em dev, a configuração segue por variáveis de ambiente do Compose. A 2026092518 lê o PEM por caminho de arquivo configurável, sem depender disto.
4. **Swagger só em Development**, como já é hoje. Um teste garante isso.
5. **Tokens no log, solução provisória.** Enquanto não houver envio de e-mail, os tokens de confirmação e de reset só são logados quando o ambiente é `Development`. Em qualquer outro ambiente, nada é logado.
   - Consequência assumida: fora de dev, cadastro e reset não funcionam até existir envio de e-mail.
   - Por isso o serviço **não é declarado pronto para produção** antes da spec de e-mail, que também remove o log de vez.
6. **Container sem root:** `USER $APP_UID` no estágio final do Dockerfile, usando o usuário `app` das imagens .NET 8+. A porta 8082 já está acima de 1024.
7. **Migrations fora do startup da API.** A API não roda DDL.
   - A mesma imagem, chamada com `migrate` (`dotnet Auth.Api.dll migrate`), roda o DbUp e sai.
   - No Compose, um serviço `auth-migrate` roda antes, e a API depende dele com `depends_on: condition: service_completed_successfully`.
   - Isso resolve a corrida entre réplicas.
   - Localmente, basta rodar `dotnet run -- migrate` antes.
8. **Papéis de banco separados (menor privilégio):**
   - `auth_migrator` é dono do schema e faz DDL (`db_owner` no banco do serviço). Só o `migrate` usa esse papel.
   - `auth_service` só tem DML (`SELECT`, `INSERT`, `UPDATE`, `DELETE`), via `GRANT` no schema `auth`, que vale também para as tabelas futuras. As colunas `IDENTITY` não exigem permissão extra.

   Isso é a base da tabela de auditoria só de inserção (2026092519). Os bancos de dev já existentes são descartáveis: a doc manda recriar o volume (`docker compose down -v`). Não haverá script de transferência de ownership.
9. **Adiada: Seq em produção** (sem porta publicada, API key e UI autenticada). Depende do compose de produção.
10. **Adiada: `AllowedHosts` restrito ao domínio público.** Depende de um domínio e de um ambiente de produção.
