# 2026092511 - Configuracao segura de producao

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar a configuração de produção do auth-service, incluindo transporte, segredos, logs e funcionamento atrás de proxy.

## Análise

O Compose atual é de desenvolvimento: expõe HTTP e Seq, define `ASPNETCORE_ENVIRONMENT=Development` e registra tokens de confirmação/reset em texto claro. Criar perfil de produção separado, com HTTPS na entrada, segredos externos à imagem/repositório, Seq protegido e headers encaminhados apenas por proxies conhecidos, conforme orientação do ASP.NET Core. A remoção dos tokens dos logs depende de um canal real de entrega de e-mails; sem ele, cadastro e reset ficariam inoperantes. Esta spec não implementa o envio de e-mails e não declara o serviço pronto para produção antes dessa dependência. Rate limits por IP da spec 2026092501 devem usar a origem confiável.

## Tarefas

- [ ] **Dev** — Separar configuração de desenvolvimento e produção; garantir HTTPS na borda, impedir Swagger de desenvolvimento em produção e aceitar `X-Forwarded-*` só de proxies confiáveis.
- [ ] **Dev** — Retirar tokens sensíveis dos logs quando o envio de e-mails estiver funcional e usar armazenamento/rotação de segredos apropriado ao ambiente, sem gravá-los na imagem ou no repositório.
- [ ] **Tester** — Verificar configuração de produção, origem do IP nos rate limits, ausência de token em log e comportamento de inicialização quando segredos obrigatórios faltarem.
- [ ] **Tech Writer** — Documentar variáveis, topologia TLS/proxy, dependência do envio de e-mails e acesso restrito ao Seq em `docs/project/`.
