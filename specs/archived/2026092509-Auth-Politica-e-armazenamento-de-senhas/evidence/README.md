# Evidências

## Execução

- Comando: `dotnet test Ouroboros.slnx`
- Data: 06/10/2026
- Resultado: 357 testes, 357 aprovados, 0 falhas. Saída bruta em `dotnet-test.txt`.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 56 | 56 | 0 |
| Auth.Application.Tests | 154 | 154 | 0 |
| Auth.Integration.Tests | 147 | 147 | 0 |

Antes desta spec eram 313 testes (50 + 139 + 124). Os testes de integração usam um SQL Server real em container (Testcontainers) e trocam o Pwned Passwords por um fake: nenhum teste faz chamada de rede.

## Rastreabilidade

Caminhos relativos a `test/auth-service/`.

### Decisões da spec

| Decisão | Teste | Resultado |
|---|---|---|
| 1. Tamanho 15 a 128 em code points, sem composição | `Auth.Application.Tests/UseCases/RegisterUser/RegisterUserInteractorTests.cs::ShouldThrowDomainExceptionWhenPasswordIsShorterThanFifteenCharacters` (vazia e 14 caracteres), `::ShouldAcceptPasswordWithExactlyFifteenCharacters`, `::ShouldAcceptPasswordWithExactlyOneHundredAndTwentyEightCharacters`, `::ShouldThrowDomainExceptionWhenPasswordIsLongerThanOneHundredAndTwentyEightCharacters`, `::ShouldCountCodePointsNotUtf16UnitsWhenCheckingLength`, `::ShouldAcceptPassphraseWithSpacesAndAccentsAndNoComposition`; `Auth.Integration.Tests/Api/PasswordPolicyApiTests.cs::ShouldAcceptPassphraseWithoutCompositionRules`, `::ShouldRejectPasswordLongerThanOneHundredAndTwentyEightCharacters` | Passou |
| 2. NFKC antes de medir, hashear e verificar | `Auth.Domain.Tests/Policies/PasswordPolicyTests.cs::ShouldNormalizeWithNfkc`, `::ShouldMeasureLengthAfterNormalization`; `Auth.Integration.Tests/Security/PasswordHasherTests.cs::ShouldNormalizeWithNfkcBeforeHashingAndVerifying` | Passou |
| 3. Lista local, sem diferenciar maiúsculas | `RegisterUserInteractorTests::ShouldThrowDomainExceptionWhenPasswordIsInTheLocalCommonList` (2 casos); `PasswordPolicyTests::ShouldCompareCommonPasswordsAfterNormalizationIgnoringCase`; `PasswordPolicyApiTests::ShouldRejectShortOrCommonPasswordWhenRegistering` | Passou |
| 3. Palavras do contexto (login e parte local com 4+ caracteres, `ouroboros`) | `RegisterUserInteractorTests::ShouldThrowDomainExceptionWhenPasswordContainsLoginEmailOrServiceName` (3 casos), `::ShouldIgnoreLoginShorterThanFourCharactersWhenCheckingContextWords`; `PasswordPolicyTests::ShouldIgnoreEmailLocalPartShorterThanFourCharacters`, `::ShouldRejectEmailLocalPartWithFourOrMoreCharacters`; `PasswordPolicyApiTests::ShouldRejectPasswordContainingTheLogin` | Passou |
| 3. Senhas vazadas (mensagem única, só depois da política local) | `RegisterUserInteractorTests::ShouldThrowDomainExceptionWhenPasswordHasAppearedInADataBreach`, `::ShouldNotQueryBreachServiceWhenPasswordFailsTheLocalPolicy`; `ResetPasswordInteractorTests::ShouldThrowDomainExceptionAndKeepTokenPendingWhenNewPasswordHasAppearedInADataBreach`; `PasswordPolicyApiTests::ShouldRejectBreachedPasswordWithTheSingleMessage` | Passou |
| 3. k-anonymity, padding, timeout de 2 s e fail-open com `Warning` | `Auth.Integration.Tests/Security/PwnedPasswordsCheckerTests.cs::ShouldSendOnlyTheFirstFiveHexCharactersAndAskForPadding`, `::ShouldReportBreachedWhenSuffixIsInTheResponseWithPositiveCount`, `::ShouldIgnorePaddingEntriesWithCountZero`, `::ShouldNotReportBreachedWhenSuffixIsAbsent`, `::ShouldFailOpenAndLogWarningWhenServiceReturnsError`, `::ShouldFailOpenAndLogWarningWhenRequestTimesOut`, `::ShouldUseTwoSecondTimeout` | Passou |
| 4. Senhas antigas continuam funcionando | `PasswordHasherTests::ShouldVerifyAndFlagRehashWhenHashWasMadeWithOneHundredThousandIterations`; `PasswordPolicyApiTests::ShouldKeepOldPasswordWorkingAndRehashItOnTheNextSuccessfulLogin` | Passou |
| 5. PBKDF2 com 600.000 iterações, formato mantido | `PasswordHasherTests::ShouldHashWithSixHundredThousandIterations`, `::ShouldMeasureVerificationTimeWithSixHundredThousandIterations` | Passou |
| 6. Re-hash no login, condicional ao hash lido | `Auth.Application.Tests/UseCases/Login/LoginInteractorTests.cs::ShouldRehashPasswordWhenStoredHashHasLowerCost`, `::ShouldNotRehashPasswordWhenStoredHashHasCurrentCost`, `::ShouldNotRehashPasswordWhenLoginIsRejected`, `::ShouldCompleteLoginWhenRehashLosesTheRaceAgainstAPasswordChange`; `PasswordHasherTests::ShouldFlagRehashWhenHashIsMalformed` | Passou |
| 7. Hash fictício com as mesmas 600.000 iterações | `PasswordHasherTests::ShouldUseTheSameIterationsInTheDummyHash` | Passou |
| 8. Custo de CPU contido pelos limites da 2026092501 | só documentada, sem mudança de código | Não se aplica |

### Tarefa Dev

| Critério | Teste | Resultado |
|---|---|---|
| `PasswordPolicy` reescrita | `PasswordPolicyTests`, `RegisterUserInteractorTests` (testes de política acima) | Passou |
| Lista local embutida e gateway `IBreachedPasswordChecker` | `PwnedPasswordsCheckerTests`, `PasswordPolicyTests::ShouldCompareCommonPasswordsAfterNormalizationIgnoringCase` | Passou |
| Política aplicada no cadastro e no reset | `RegisterUserInteractorTests`, `ResetPasswordInteractorTests::ShouldThrowDomainExceptionWhenNewPasswordIsWeak` (4 casos), `::ShouldNotOpenUnitOfWorkWhenNewPasswordIsRejected` | Passou |
| Hasher com 600.000 iterações, NFKC, `NeedsRehash`, re-hash no login | `PasswordHasherTests`, `LoginInteractorTests` | Passou |

### Tarefa DBA — atualização condicional do hash

| Critério | Teste | Resultado |
|---|---|---|
| `UPDATE ... WHERE password_hash = @antigo` | `Auth.Integration.Tests/Persistence/RepositorySqlTests.cs::ShouldReplaceHashOnlyWhenItIsStillTheOneThatWasRead`, `::ShouldNotChangePasswordChangedAtWhenRehashing` | Passou |

### Tarefa Tester

| Critério | Teste | Resultado |
|---|---|---|
| Unitários: limites 14/15/128/129, frase com espaços e acentos, composição dispensada, senha comum, com o login e vazada, falha da API externa, hash antigo, NFKC | `RegisterUserInteractorTests`, `PasswordPolicyTests`, `PwnedPasswordsCheckerTests`, `PasswordHasherTests` | Passou |
| Integração: re-hash não sobrescreve troca de senha concorrente | `RepositorySqlTests::ShouldKeepTheNewPasswordWhenRehashRacesWithAPasswordChange`, `::ShouldReplaceHashOnlyWhenItIsStillTheOneThatWasRead` | Passou |
| Medir o tempo de uma verificação com 600 mil iterações | `PasswordHasherTests::ShouldMeasureVerificationTimeWithSixHundredThousandIterations`: **261 ms** em média (5 execuções), nesta máquina de desenvolvimento, abaixo da referência de 500 ms | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Política, custo do hash e checagem de senhas vazadas documentados | `docs/auth/0004 - Recuperacao de Senha.md` (seções "Política de senha" e "Armazenamento"), com remissões em `docs/auth/0001 - Confirmacao de Cadastro.md` e `docs/auth/0003 - Login e Tokens.md` | Entregue |

## Observações

- **Senhas dos testes.** As senhas dos testes antigos tinham 8 a 11 caracteres. Foram trocadas por frases de 15 ou mais caracteres, e os testes de composição (maiúscula, minúscula, dígito, especial) foram substituídos pelos da regra nova.
- **Tempo da suíte de integração.** Com 600.000 iterações, a suíte passou de cerca de 32 s para cerca de 66 s. O `TestApi` guarda um hash por senha para não recalcular a cada usuário de teste.
- **Parte local do e-mail.** A spec fixa o mínimo de 4 caracteres só para o login. Apliquei o mesmo mínimo à parte local do e-mail, senão `a@x.com` barraria quase toda senha.
- **Lista local.** Das 1.000.000 senhas do SecLists `Pwdb_top-1000000`, só entram as com 15 a 40 caracteres ASCII, em minúsculas e sem repetição, limitadas às 2.000 mais frequentes. As com menos de 15 caracteres já caem no tamanho mínimo.
- **Falha da API externa.** O `PwnedPasswordsChecker` cobre `HttpRequestException` e timeout. Uma linha de resposta malformada (sem `:`) é ignorada.
- **Login ainda não transacional.** O re-hash roda depois do `TryResetFailedAccessAsync`, fora de transação. A spec 2026092506 torna o login atômico e absorve essa escrita.
