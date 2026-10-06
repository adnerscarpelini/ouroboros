# 2026092511 - Configuracao segura de producao

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a configuração apareceu pensada só para desenvolvimento: Compose com `Development`, tokens logados em texto claro, container rodando como root, migrations executadas no startup da API e um único papel de banco com todos os privilégios. O usuário aprovou uma configuração de produção. Os forwarded headers foram para a 2026092501, e a remoção definitiva dos tokens do log fica para a futura spec de envio de e-mail.

## Análise

Extensão do `auth-service` e da infraestrutura do monorepo. Referências: guias do ASP.NET Core para hospedagem atrás de proxy e para configuração key-per-file, imagens .NET sem root (`APP_UID`) e o princípio do menor privilégio no SQL Server.

Decisões:
1. **Dois perfis.** O `docker-compose.yml` continua sendo o de desenvolvimento. Produção usa o override `docker-compose.prod.yml` com `appsettings.Production.json` e `ASPNETCORE_ENVIRONMENT=Production`.
2. **TLS no proxy de borda**, com HSTS lá. O Kestrel fica só em HTTP na rede interna, sem porta publicada no host em produção. O IP do proxy vai em `ForwardedHeaders:KnownProxies` (2026092501).
3. **Segredos.** Connection string e chave de assinatura (2026092518) chegam por Docker secrets, lidos com o provider key-per-file (`AddKeyPerFile("/run/secrets")`). Nunca ficam na imagem, num `.env` de produção ou no repositório. Em nuvem, basta trocar pelo cofre do provedor (Key Vault ou Secrets Manager), sem mudar código.
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

   Isso é a base da tabela de auditoria só de inserção (2026092519). Os bancos de dev já existentes precisam de transferência de ownership (`ALTER AUTHORIZATION`, script documentado) ou de recriação do volume.
9. **Seq em produção:** sem porta publicada, ingestão com API key (`Seq:ApiKey`) e UI com autenticação.
10. **`AllowedHosts`** restrito ao domínio público em produção.
