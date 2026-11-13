# Code Quest — novo visual "Grimório à luz da tocha": plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aplicar ao jogo a direção visual aprovada na maquete (https://claude.ai/artifact/68bNqFJagxLxeuBdCDNqa1) e fazer as duas correções pedidas: mostrar o resultado do código do jogador caso a caso e centrar o portão no pop-up de vitória.

**Architecture:** O contrato de validação passa a devolver todos os casos (entrada, esperado, obtido), não só as falhas; o resultado (`Passed`/`ChecksFailed`) leva essa lista para a UI mostrar "Resultado do feitiço". O visual vive em CSS (tokens em `wwwroot/app.css` + CSS isolado por componente) e em SVG: ícones/portas inline nos componentes, uma vinheta por nível em `content/levels/<id>/scene.svg`.

**Tech Stack:** .NET 10 (`net10.0`, SDK 11), Blazor Interactive Server, Roslyn, xUnit, bUnit, Playwright.

**Spec:** `CLAUDE.md`. **Referência visual:** a maquete acima (mapa, página do nível, pop-up).

## Papéis

| Papel | Modelo | O que faz |
|---|---|---|
| Planner | Opus | Mantém este plano, despacha, integra, verificação final no browser. |
| Developer | Sonnet | Uma tarefa de cada vez, em TDD. |
| Advisor | Opus | Revê cada tarefa (spec, qualidade, acessibilidade, fidelidade à maquete) e no fim o branch inteiro. |

## Decisões (Opus)

- **Contrato de validação:** `Validation.Run()` devolve `IEnumerable<(string Case, string Expected, string Actual)>` com **todos** os casos. Os valores são literais C# escritos pela validação (`6`, `"M6"`, `{ 2, 3, 5 }`, `true`), por isso não têm idioma. O motor cria `CheckOutcome(string Case, string Expected, string Actual)` com `bool Passed => Expected == Actual` (ordinal). `ChecksFailed(IReadOnlyList<CheckOutcome> Checks)` quando algum falha; `Passed(int StatementCount, IReadOnlyList<CheckOutcome> Checks)` quando todos passam. Um `Run` que não devolve esse tipo é falha do nível (`InvalidOperationException`, como hoje).
- **Letras:** Google Fonts (`Cinzel` 600/700 títulos, `Source Serif 4` 400/600 texto, `JetBrains Mono` 400/600 código) carregadas por `<link>` no `App.razor`, com fallbacks (`Georgia, serif` / `monospace`). O jogo já precisa de internet para o Monaco.
- **Paleta (tokens CSS):** pedra `#0e0c09`/`#17140f`/`#1b1713`/`#241d16`; pergaminho `#e9dcc0` com tinta `#2b2219`; tocha `#e8a33d` (realce `#f4c06a`); arcano `#4fd1c5` (claro `#7fe3d9`) **só** para código/resultado; texto `#ece3d0`, secundário `#b8ab93`; erro `#ffb3a8` sobre `#2a1715`; sucesso `#9be7dc` sobre `#132623`. Todo o texto ≥ 4,5:1.
- **Vinhetas:** `content/levels/<id>/scene.svg` obrigatório; lido pelo `LevelRepository` para `Level.SceneSvg`; mostrado inline (`MarkupString`) porque é conteúdo do próprio repositório. *(Como foi implementado, Task 4:)* o SVG é `aria-hidden`; o nome acessível vem de `sceneLabel` {pt,en} no `level.json`, num `<figure role="img" aria-label>` à volta. Teste de conteúdo: XML bem formado com raiz `svg`, só elementos de uma lista permitida, nenhum atributo `on…`, `href` só internos (`#…`), `url(` só `url(#…)`.
- **Botão de executar** passa a "Lançar feitiço" / "Cast spell".
- Animações curtas (150–300 ms) e `prefers-reduced-motion` respeitado em tudo.

## Global Constraints

- Regras do `CLAUDE.md` (TDD, 0 avisos, `dotnet test` verde nos dois projetos, Clean Code/YAGNI, APIs confirmadas no Microsoft Learn, JavaScript só em `CodeEditor.razor.js`, PT-PT + EN).
- Visual original (sem arte de outros jogos); ícones SVG inline (sem emoji); sem frameworks CSS.
- Acessibilidade: botões reais, foco visível (anel âmbar), contraste AA, SVG decorativo `aria-hidden="true"`, SVG informativo `role="img"` + `aria-label`.
- Não partir comportamentos existentes (cancelamento de execuções, pop-up modal com `inert`, foco, Grimório).
- Commits em inglês com o trailer pedido.

## Review Focus

1. Um nível cujo `Validation` devolve o tipo antigo tem de falhar de forma clara (falha do nível), não mostrar um resultado vazio. Task 1.
2. Contraste do texto secundário e dos valores no painel de resultado sobre os fundos escuros. Tasks 2 e 4.
3. Mapa com 6 salas e mundos futuros: o layout é calculado pelo índice, não posições escritas à mão por nível; funciona em ecrãs estreitos (cai para uma lista). Task 3.
4. SVG de conteúdo injetado como markup: só conteúdo do repositório, validado por teste. Task 4.
5. Portão centrado no pop-up em qualquer largura (correção pedida). Task 5.

---

### Task 1: Resultados caso a caso (motor + níveis)

**Files:** `src/CodeQuest.Core/Running/RunResult.cs` (+ `CheckOutcome.cs`), `src/CodeQuest.Engine/RoslynCodeRunner.cs`, `content/levels/*/Validation.cs` (6), `README.md` (como acrescentar um nível), testes em `tests/CodeQuest.Tests/Engine` e `Levels`, e o mínimo em `RunResultView.razor`/`LevelPage.razor` para compilar e manter o comportamento (a UI nova é a Task 4).

- [ ] Testes primeiro: runner devolve `ChecksFailed` com todos os casos e o `Passed` flag certo por caso; `Passed` traz os casos todos; `Run` com o tipo antigo → `InvalidOperationException`; cada nível: solução → todos os casos passam; erro típico (já testado hoje) → o caso certo falha com esperado/obtido.
- [ ] Reescrever as 6 validações: o nível 1 usa como `Case` o nome da variável (`name`, `health`, `mana`, `isAlive`); os outros usam a entrada (`turns = 3`, `potions = { }`, `name = "merlin"`, `rune = "earth"`).
- [ ] Commit `feat: report every check with expected and actual values`.

### Task 2: Base visual

**Files:** `src/CodeQuest.Web/Components/App.razor`, `wwwroot/app.css`, `Components/Layout/MainLayout.razor(.css)`, `LanguageSwitcher.razor.css`, `Pages/NotFound.razor`/`Error.razor` (só estilo), `content/ui.json` (`level.run`).

- [ ] Tokens da paleta em `:root`, letras, fundo de pedra (gradiente radial + textura CSS subtil), botões primário (tocha) e secundário (contorno), anel de foco âmbar, cabeçalho com a marca (SVG do hexágono com runas) e o seletor PT/EN da maquete.
- [ ] Testes bUnit existentes continuam verdes; acrescentar teste de que o cabeçalho tem a marca com nome acessível.
- [ ] Commit `feat: torchlit grimoire visual foundation`.

### Task 3: Mapa ilustrado

**Files:** `Components/Pages/Home.razor(.css)`, testes `HomeTests`.

- [ ] Título do mundo ("Mundo 1 · Primeiro andar" / "As Fundações", chaves novas PT/EN), total de estrelas do mundo, corredor SVG e salas como portas em arco: concluída (estrelas), atual (brilho de tocha, maior), trancada (cadeado, "Trancada"). Posições em ziguezague calculadas pelo índice. Abaixo de ~800 px vira uma lista vertical.
- [ ] Testes: estados das salas, sala atual = primeira não concluída desbloqueada, total de estrelas, links só nas desbloqueadas.
- [ ] Commit `feat: illustrated dungeon map`.

### Task 4: Página do nível, vinhetas e "Resultado do feitiço"

**Files:** `Core/Levels/Level.cs` + `LevelRepository.cs` (SceneSvg), `content/levels/*/scene.svg` (6), `Pages/LevelPage.razor(.css)`, `RunResultView.razor(.css)`, `content/ui.json`, testes (`LevelRepositoryTests`, `ContentTests`, `LevelPageTests`, `RunResultViewTests`, E2E se mudarem seletores).

- [ ] Duas colunas (≥ 1100 px; em ecrãs estreitos empilha): pergaminho à esquerda (vinheta, título, separadores História/Conceito/Dicas com `role="tablist"`, as dicas reveladas uma a uma como hoje) e "livro de feitiços" à direita (moldura do editor, "Lançar feitiço", Grimório, Pista).
- [ ] `RunResultView` como painel "Resultado do feitiço": cabeçalho com ícone por tipo (balança = verificações, runa rachada = compilação, ampulheta = tempo, espiral = recursão/execução, selo = rejeitado), "N de M certos", grelha de casos (entrada, "deu X · esperado Y" ou "deu X ✓"); erros de compilação/execução mantêm linha + explicação + mensagem original.
- [ ] 6 vinhetas SVG originais no estilo da maquete (pedra + elemento da sala: livro/nome, esqueleto/espada, três portas, cristal, prateleiras de poções, selo).
- [ ] Commit `feat: level page as parchment and spellbook with spell results`.

### Task 5: Pop-up de vitória, Grimório

**Files:** `Pages/LevelPage.razor(.css)` (diálogo), `GrimoirePanel.razor(.css)`, testes.

- [ ] Diálogo como na maquete: cena SVG da porta aberta **centrada** (luz, mago a atravessar), "Porta aberta!" (chave nova), estrelas grandes, frase com o resultado ("Feitiço com N instruções"), botões. Mantém `role="dialog"`, `inert`, foco e Esc.
- [ ] Grimório como livro: fundo pergaminho, separador por mundo, ícone por página (via id), exemplo em bloco de "inscrição".
- [ ] Testes bUnit atualizados; E2E: a porta está centrada no diálogo (centro horizontal da cena = centro do diálogo ± 2 px).
- [ ] Commit `feat: victory scene and grimoire as a book`.

### Task 6: Verificação final

- [ ] Opus: build 0 avisos, todos os testes; no browser, mapa, níveis 1–6 em PT e EN, um erro de cada tipo, pop-up, Grimório, ecrã estreito.
- [ ] Opus advisor: revisão final do branch.
