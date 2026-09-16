-- A migration inicial insere id=1 e id=2 explicitamente em auth.token_types (coluna identity).
-- Postgres não reajusta a sequence sozinho quando o id vem explícito no INSERT: sem este ajuste,
-- o próximo INSERT sem id explícito nessa tabela tentaria gerar id=1 de novo e colidiria com a PK.
SELECT
    setval(
        pg_get_serial_sequence('auth.token_types', 'id'),
        (SELECT max(id) FROM auth.token_types)
    );
