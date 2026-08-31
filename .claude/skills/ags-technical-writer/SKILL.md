---
name: ags-technical-writer
description: Convenções de documentação do projeto Ouroboros — onde os documentos ficam, numeração sequencial, estilo de escrita e como desenhar fluxos em Excalidraw. Use sempre que for criar, revisar ou atualizar qualquer documento em docs/, ou desenhar um fluxo/diagrama.
---

# ags-technical-writer

Skill base pra tudo relacionado a documentação no projeto Ouroboros. Complementa a [ags-developer](../ags-developer/SKILL.md) (convenções gerais de código) — para decisões específicas de código, testes ou banco, siga também [ags-qa](../ags-qa/SKILL.md) e [ags-dba](../ags-dba/SKILL.md), mas qualquer documento que resulte disso segue as regras daqui.

## Onde ficam os documentos

- Todo documento fica em `docs/`, em **Markdown**, numerado sequencialmente: `0000 - Arquitetura.md`, `0001 - Comandos Git.md`, `0002 - Setup do Banco de Dados Local.md`, etc.
- Sempre que algo for implementado ou alterado no projeto, revisar os documentos existentes em `docs/` e editar o(s) que forem afetados pela mudança — documentação desatualizada é pior do que nenhuma.
- Só criar um documento novo, no próximo número da sequência, se nenhum documento existente cobrir o assunto. Não criar documentos por antecipação, sem uma necessidade real.

## Estilo de escrita

- Linguagem simples e direta, fácil de entender. Sem enrolação, sem introdução genérica, sem "conclusão" ao final — vai direto ao ponto do que o leitor precisa saber.
- Preferir frases curtas e listas a parágrafos longos.
- Idioma: português do Brasil (ver [ags-developer](../ags-developer/SKILL.md)).

## Fluxos e diagramas (Excalidraw)

- Quando for pedido pra desenhar um fluxo, criar um arquivo **Excalidraw** (`.excalidraw`) dentro de `docs/excalidraw/`.
- Esses arquivos seguem a mesma numeração sequencial dos documentos em Markdown. Preferencialmente, reaproveitar o número do documento `.md` ao qual o fluxo se refere (ex.: um fluxo que ilustra `0000 - Arquitetura.md` vira `docs/excalidraw/0000 - Arquitetura.excalidraw`).
- Se o fluxo não tiver um documento `.md` correspondente, usar o próximo número livre na mesma sequência única (compartilhada entre `docs/` e `docs/excalidraw/`) — não criar uma numeração independente só para os diagramas.

## Evolução

Esta skill é o lugar para acumular, com o tempo, convenções mais específicas de documentação (estrutura padrão de um documento, glossário de termos do domínio, template de fluxo no Excalidraw, etc.) à medida que forem sendo definidas.
