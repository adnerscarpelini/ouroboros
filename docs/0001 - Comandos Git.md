# 0001 - Comandos Git

Comandos básicos de Git usados no dia a dia deste projeto.

## Ver o que mudou

Mostra quais arquivos foram alterados/criados antes de decidir o que commitar. Use antes de todo commit.

```bash
git status
```

## Commit

Salva as alterações selecionadas como um novo ponto no histórico. Use depois de terminar uma alteração e revisar o que será incluído.

```bash
git add caminho/do/arquivo
git commit -m "mensagem curta descrevendo a mudança"
```

## Push

Envia os commits locais para o GitHub. Use depois de commitar, quando quiser publicar o progresso.

```bash
git push
```

## Pull / atualizar a branch local

Traz para a branch local os commits que já estão no GitHub (ex.: feitos em outra máquina ou pelo próprio GitHub). Use antes de começar a trabalhar, para evitar divergência com o remoto.

```bash
git pull
```

## Trocar de branch

Muda para outra branch já existente localmente. Use para alternar entre `development` e `main`.

```bash
git checkout development
```

## Merge da development para a main

Leva o que já foi validado na `development` para a `main`, marcando uma versão estável. Use quando um conjunto de mudanças na `development` está pronto para virar a versão estável do projeto.

```bash
git checkout main
git merge development
git push
```
