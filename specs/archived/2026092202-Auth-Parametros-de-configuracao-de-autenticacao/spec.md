# 2026092202 - Parametros de configuracao de autenticacao

**Data:** 22/09/2026
**Servico(s):** auth-service

## Solicitação

Antes de implementar login com JWT + Refresh Token, o usuário levantou a necessidade de ter algum lugar centralizado pra configurar tempos de expiração de cada tipo de token e outros parâmetros de autenticação — cogitou tanto um arquivo de configuração quanto uma tabela de parâmetros (chave/valor) no banco.

## Análise

Decisão tomada com o usuário: usar `appsettings.json` + `IOptions<T>` (Options pattern), o padrão do próprio ASP.NET Core, em vez de uma tabela de parâmetros no banco. Justificativa: os valores (chave de assinatura do JWT, tempo de expiração do access token, tempo de expiração do refresh token) não precisam mudar sem um deploy, e uma tabela adicionaria uma dependência de banco em todo fluxo de emissão/validação de token, além de cache e invalidação. Se no futuro surgir uma necessidade real de ajustar esses valores em runtime (ex.: um painel administrativo), essa decisão pode ser revisitada numa spec própria. Esta spec é pré-requisito das specs de login, refresh e logout, que consomem esses parâmetros.
