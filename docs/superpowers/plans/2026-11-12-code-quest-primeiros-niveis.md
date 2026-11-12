# Code Quest — primeiros 3 níveis: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 3 níveis jogáveis de ponta a ponta (variáveis, operadores, if/else/switch), cada um com página no Grimório, interface PT/EN, progresso em `localStorage`, tudo coberto por testes, e README.

**Architecture:** `CodeQuest.Core` (domínio, sem dependências externas) ← `CodeQuest.Engine` (Roslyn + `AssemblyLoadContext` colecionável, implementa `ICodeRunner`) ← `CodeQuest.Web` (Blazor Web App, Interactive Server global, sem prerender). O conteúdo vive em `content/` na raiz e é copiado para o output da Web e dos testes. Testes em três níveis: unitários (xUnit), componentes (bUnit) e ponta a ponta num browser real (Playwright).

**Tech Stack:** .NET 10 (`net10.0`, compilado com o SDK 11.0.100), C#, Blazor, Roslyn (`Microsoft.CodeAnalysis.CSharp`), xUnit, bUnit, Playwright for .NET, `Microsoft.AspNetCore.Mvc.Testing`, Monaco (CDN jsDelivr).

**Spec:** `CLAUDE.md` (raiz do repositório).

## Papéis

| Papel | Modelo | O que faz |
|---|---|---|
| Planner | Opus | Escreve e mantém este plano, despacha cada tarefa, integra, faz a verificação final. |
| Developer | Sonnet | Implementa uma tarefa de cada vez, em TDD (teste a falhar → código → teste a passar → commit). Só vê a sua tarefa + `CLAUDE.md`. |
| Advisor | Fable | Revê o diff de cada tarefa contra este plano e o `CLAUDE.md` (Clean Code, YAGNI, APIs confirmadas no Microsoft Learn, testes a cobrir todo o comportamento novo). Veredicto: aprovado ou lista de alterações. No fim, revê o branch inteiro. |

Ciclo por tarefa: Sonnet implementa → Fable revê → se houver alterações, Sonnet corrige → Fable confirma → Opus passa à próxima.

## Global Constraints

- `TargetFramework` = `net10.0` em todos os projetos; `Nullable` = `enable`; `TreatWarningsAsErrors` = `true`; `ImplicitUsings` = `enable` (em `Directory.Build.props`).
- Core sem pacotes NuGet. Só `System.Text.Json` (BCL).
- Antes de usar uma API de .NET, Roslyn ou Blazor, confirmá-la no MCP Microsoft Learn. Para bUnit e Playwright, confirmar na documentação oficial de cada um.
- **Todo o comportamento tem teste.** Nenhuma tarefa fica concluída com código sem teste que o exercite. Código Web testado com bUnit; JavaScript e `localStorage` testados com Playwright.
- Tarefa só fica concluída com `dotnet build` sem avisos e `dotnet test` verde. Dizer explicitamente o que ficou por verificar.
- Código e identificadores em inglês; textos para o jogador em PT-PT e EN.
- JavaScript só no ficheiro `CodeEditor.razor.js`; sem wrappers de terceiros para o Monaco.
- Monaco: `https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs` (versão fixa). Os testes E2E precisam de internet por causa disto.
- Sem som. Visual próprio (CSS/SVG simples), sem arte de outros jogos.
- Mensagens de commit em inglês, formato `feat:`, `test:`, `chore:`, `docs:`.

## Review Focus

Cada risco tem um comportamento decidido e testes na tarefa indicada.

1. **`}` solto ou código vazio** → antes de compilar, `CodeRejected` com mensagem clara ("Tens uma `}` a mais na linha N", "Ainda não escreveste nenhum feitiço"). Task 4.
2. **Ciclos infinitos e APIs perigosas** → contador nos ciclos + limite de 3 s dão `TimedOut` com mensagem que ensina; `Thread`, `Task`, `Environment`, `System.IO`, `Process` e afins dão `CodeRejected` antes de executar. Task 5.
3. **Exceções durante a execução** → `RuntimeError` com a linha do jogador (PDB em memória) e explicação PT/EN para divisão por zero, `null`, índice fora do array e formato inválido. Task 6.
4. **`localStorage` corrompido ou de outra versão** → progresso vazio e aviso ao jogador ("O progresso guardado não pôde ser lido"). Campo `version: 1`. Task 3 (regra) e Task 9 (aviso).
5. **Erros de compilação fora do código do jogador** → traduzidos (CS0103 → "Falta declarar a variável `health`"), com a mensagem original por baixo. Task 4.

---

### Task 1: Esqueleto da solução

**Files:**
- Create: `CodeQuest.sln`, `Directory.Build.props`, `.editorconfig`
- Create: `src/CodeQuest.Core/CodeQuest.Core.csproj`
- Create: `src/CodeQuest.Engine/CodeQuest.Engine.csproj` (pacote `Microsoft.CodeAnalysis.CSharp`, versão estável mais recente; referência a Core)
- Create: `src/CodeQuest.Web/` com `dotnet new blazor -int Server --all-interactive --empty -f net10.0` (referências a Core e Engine); acrescentar `public partial class Program { }` ao `Program.cs`
- Create: `tests/CodeQuest.Tests/` (`dotnet new xunit`; referências a Core, Engine e Web; pacote `bunit`)
- Create: `tests/CodeQuest.E2ETests/` (`dotnet new xunit`; referência a Web; pacotes `Microsoft.Playwright`, `Microsoft.AspNetCore.Mvc.Testing`)
- Create: `content/.gitkeep`
- Test: `tests/CodeQuest.Tests/SmokeTests.cs`, `tests/CodeQuest.E2ETests/SmokeTests.cs`

**Interfaces:**
- Produces: Web e os dois projetos de teste copiam `content/**` para `$(OutDir)/content/`. Pasta em runtime: `Path.Combine(AppContext.BaseDirectory, "content")`.
- Produces (E2E): `public sealed class GameServerFixture : IAsyncLifetime` em `tests/CodeQuest.E2ETests/GameServerFixture.cs` — cria `WebApplicationFactory<Program>`, chama `UseKestrel(0)` e `StartServer()`, expõe `string BaseUrl`, `IBrowser Browser` (Chromium headless). Browsers instalados com o script `playwright.ps1 install chromium` gerado no build (documentar no README).

- [ ] **Step 1:** Criar ficheiros e projetos; adicionar todos à `.sln`.
- [ ] **Step 2:** Testes: `SmokeTests.ContentFolder_IsCopiedToOutput` (`Directory.Exists(...content)`) e `E2E SmokeTests.HomePage_Loads` (abre `BaseUrl`, resposta 200, página tem `<title>`).
- [ ] **Step 3:** Run `dotnet build` → 0 avisos. Run `dotnet test` → PASS nos dois projetos.
- [ ] **Step 4:** Commit (inclui `CLAUDE.md`, `.gitignore`, este plano): `chore: scaffold solution`.

### Task 2: Conteúdo do domínio e leitura de JSON

**Files:**
- Create: `src/CodeQuest.Core/Language.cs`, `LocalizedText.cs`
- Create: `src/CodeQuest.Core/Levels/Level.cs`, `Levels/LevelRepository.cs`
- Create: `src/CodeQuest.Core/Grimoire/GrimoirePage.cs`, `Grimoire/GrimoireRepository.cs`
- Create: `src/CodeQuest.Core/Localization/UiTextCatalog.cs`
- Test: `tests/CodeQuest.Tests/Core/LevelRepositoryTests.cs`, `GrimoireRepositoryTests.cs`, `UiTextCatalogTests.cs`, `LocalizedTextTests.cs` (usam pastas temporárias com ficheiros de exemplo, não o conteúdo real)

**Interfaces:**
- Produces:
  - `public enum Language { Pt, En }`
  - `public sealed record LocalizedText(string Pt, string En) { public string Get(Language language); }`
  - `public sealed record Level(string Id, int World, int Number, LocalizedText Title, LocalizedText Story, LocalizedText Concept, string Template, string StarterCode, string Solution, string ValidationSource, IReadOnlyList<LocalizedText> Hints, int MaxStatementsForThreeStars, string GrimoirePageId);`
  - `public static class LevelRepository { public static IReadOnlyList<Level> Load(string levelsDirectory); }` — ordenado por `World`, depois `Number`.
  - `public sealed record GrimoirePage(string Id, int World, LocalizedText Title, LocalizedText WhatItIs, string Syntax, string Example, IReadOnlyList<LocalizedText> CommonMistakes, string LearnUrl);`
  - `public static class GrimoireRepository { public static IReadOnlyList<GrimoirePage> Load(string grimoireDirectory); }`
  - `public sealed class UiTextCatalog { public static UiTextCatalog Load(string path); public string Get(string key, Language language); }` — chave em falta lança `KeyNotFoundException` com o nome da chave.

**Formato de cada nível** (`content/levels/<NN-nome>/`):
- `level.json`: `id`, `world`, `number`, `title`, `story`, `concept` (objetos `{ "pt": ..., "en": ... }`), `hints` (lista desses objetos), `maxStatementsForThreeStars`, `grimoirePageId`.
- `Template.cs`: código escondido; contém exatamente uma linha cujo conteúdo, sem espaços, é `// {{PLAYER_CODE}}`.
- `Starter.cs`, `Solution.cs`: fragmentos (corpo do método).
- `Validation.cs`: `public static class Validation { public static IEnumerable<(string Pt, string En)> Run() }` — cada elemento devolvido é uma verificação falhada.

**Formato do Grimório** (`content/grimoire/<id>.json`): `id`, `world`, `title`, `whatItIs`, `syntax`, `example`, `commonMistakes`, `learnUrl`.

**Formato da UI** (`content/ui.json`): `{ "chave": { "pt": ..., "en": ... } }`.

- [ ] **Step 1:** Testes: `LocalizedText_Get_ReturnsRequestedLanguage`, `Load_ReturnsLevelsOrderedByWorldThenNumber`, `Load_ReadsTemplateStarterSolutionAndValidationFiles`, `Load_TemplateWithoutMarker_Throws` (`InvalidDataException`), `Load_TemplateWithTwoMarkers_Throws`, `Load_MissingFile_ThrowsWithLevelFolderName`, `GrimoireRepository_Load_ReadsAllPages`, `UiTextCatalog_Get_ReturnsTextForLanguage`, `UiTextCatalog_Get_MissingKey_Throws`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implementar com `System.Text.Json` (`PropertyNameCaseInsensitive = true`). **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: load levels, grimoire and ui text from content`.

### Task 3: Progresso e estrelas

**Files:**
- Create: `src/CodeQuest.Core/Progress/StarCalculator.cs`, `Progress/PlayerProgress.cs`, `Progress/ProgressLoadResult.cs`
- Test: `tests/CodeQuest.Tests/Core/StarCalculatorTests.cs`, `PlayerProgressTests.cs`

**Interfaces:**
- Produces:
  - `public static class StarCalculator { public static int Calculate(int world, int statementCount, int maxStatements, bool usedHint, bool sawSolution); }`
  - `public sealed class PlayerProgress` com: `const int CurrentVersion = 1`, `RecordFailure(string levelId)`, `RecordHintUsed(string levelId)`, `RecordSolutionSeen(string levelId)`, `RecordPass(string levelId, int stars)` (guarda o máximo), `MarkPageViewed(string pageId)`, `int StarsFor(string levelId)`, `bool IsCompleted(string levelId)`, `bool UsedHint(string levelId)`, `bool SawSolution(string levelId)`, `bool CanSeeSolution(string levelId)` (≥ 3 falhas), `bool IsUnlocked(IReadOnlyList<Level> orderedLevels, string levelId)`, `bool IsPageAvailable(GrimoirePage page, IReadOnlyList<Level> orderedLevels)` (página de um nível desbloqueado ou já vista), `string ToJson()` (inclui `"version": 1`), `static ProgressLoadResult FromJson(string? json)`.
  - `public sealed record ProgressLoadResult(PlayerProgress Progress, bool WasUnreadable);`

Regras (do `CLAUDE.md`):
- `sawSolution` → 0 estrelas.
- Senão: 1 estrela por passar; +1 se `world == 1` ou `!usedHint`; +1 se a segunda foi ganha **e** `statementCount <= maxStatements`.
- Primeiro nível da lista está sempre desbloqueado; os outros só se o anterior estiver completo.
- `FromJson`: `null` ou vazio → progresso novo, `WasUnreadable = false`; JSON inválido, sem `version` ou com `version` ≠ 1 → progresso novo, `WasUnreadable = true`.

- [ ] **Step 1:** Testes `StarCalculator`: `(1, 3, 4, false, false) → 3`, `(1, 9, 4, true, false) → 2`, `(2, 3, 4, true, false) → 1`, `(2, 3, 4, false, false) → 3`, `(2, 9, 4, false, false) → 2`, `(1, 1, 4, false, true) → 0`.
- [ ] **Step 2:** Testes `PlayerProgress`: `NewProgress_OnlyFirstLevelUnlocked`, `RecordPass_UnlocksNextLevel`, `RecordPass_KeepsBestStars`, `CanSeeSolution_FalseAfterTwoFailures`, `CanSeeSolution_TrueAfterThreeFailures`, `ToJson_FromJson_RoundTrips`, `ToJson_IncludesVersion1`, `FromJson_Null_ReturnsEmptyAndReadable`, `FromJson_Corrupted_ReturnsEmptyAndUnreadable` (`"{nope"`), `FromJson_UnknownVersion_ReturnsEmptyAndUnreadable` (`{"version":99}`), `IsPageAvailable_PageOfUnlockedLevel`, `IsPageAvailable_LockedButViewedPageStaysAvailable`, `IsPageAvailable_LockedAndNotViewed_False`.
- [ ] **Step 3:** Run → FAIL. **Step 4:** Implementar. **Step 5:** Run → PASS.
- [ ] **Step 6:** Commit `feat: add star rules and player progress`.

### Task 4: Compilador, verificações prévias e mensagens claras

**Files:**
- Create: `src/CodeQuest.Core/Running/ICodeRunner.cs`, `Running/RunRequest.cs`, `Running/RunResult.cs`, `Running/CompilationError.cs`
- Create: `src/CodeQuest.Engine/PlayerCodePrecheck.cs`, `Engine/PlayerCompiler.cs`, `Engine/StatementCounter.cs`, `Engine/PlayerMessages.cs`
- Test: `tests/CodeQuest.Tests/Engine/PlayerCodePrecheckTests.cs`, `PlayerCompilerTests.cs`, `StatementCounterTests.cs`

**Interfaces:**
- Produces (Core):
  - `public sealed record RunRequest(string Template, string PlayerCode, string ValidationSource);`
  - `public sealed record CompilationError(int? Line, int? Column, string Message, LocalizedText? Explanation);` — `Line`/`Column` relativos ao código do jogador (base 1); `null` quando o erro está fora dele. `Explanation` é a tradução amigável quando existe.
  - `public abstract record RunResult;` com os derivados `sealed record`: `CodeRejected(LocalizedText Reason, int? Line)`, `CompilationFailed(IReadOnlyList<CompilationError> Errors)`, `ChecksFailed(IReadOnlyList<LocalizedText> Failures)`, `Passed(int StatementCount)`, `TimedOut()`, `RuntimeError(LocalizedText Explanation, string Message, int? Line)`.
  - `public interface ICodeRunner { Task<RunResult> RunAsync(RunRequest request, Language language, CancellationToken cancellationToken); }`
- Produces (Engine):
  - `internal static class PlayerCodePrecheck { public static CodeRejected? Check(string playerCode); }` — vazio/só espaços/só comentários → "Ainda não escreveste nenhum feitiço." / "You haven't written any spell yet."; `}` sem par → "Tens uma `}` a mais na linha N." / "There's an extra `}` on line N."; `{` sem par → "Falta fechar uma `{` aberta na linha N." / "A `{` opened on line N is never closed.". Contar chavetas com o tokenizer do Roslyn (ignora strings e comentários).
  - `internal sealed class PlayerCompiler { public CompileOutcome Compile(RunRequest request, Language language); }` com `internal sealed record CompileOutcome(byte[]? Assembly, byte[]? Pdb, IReadOnlyList<CompilationError> Errors, int PlayerLineOffset);`
  - `internal static class StatementCounter { public static int Count(string playerCode); }` — conta todos os `StatementSyntax` exceto `BlockSyntax`, analisando `"{" + playerCode + "}"`.
  - `internal static class PlayerMessages` — todos os textos PT/EN do motor (verificações prévias, explicações de erros, APIs proibidas, exceções).

Notas:
- Substituir a linha do marcador pelo código do jogador; compilar com `OutputKind.DynamicallyLinkedLibrary`, referências a partir de `AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`; emitir também PDB portátil em memória (para a Task 6).
- Mensagens do compilador: `Diagnostic.GetMessage(culture)` com `pt-BR` para `Language.Pt` (o Roslyn não tem `pt-PT`) e `en` para `Language.En`. Só diagnósticos de severidade `Error`.
- Erro `CS0103` fora do código do jogador → `Explanation` = "Falta declarar a variável `{nome}`." / "You still need to declare the variable `{nome}`." (nome tirado de `Diagnostic.GetMessage` via argumentos do diagnóstico, confirmar API no Learn).

- [ ] **Step 1:** Testes `PlayerCodePrecheck`: `Check_EmptyCode_Rejected`, `Check_OnlyComments_Rejected`, `Check_ExtraClosingBrace_RejectedWithLine` (`"int a = 1;\n}"` → linha 2), `Check_UnclosedBrace_RejectedWithLine`, `Check_BraceInsideString_Accepted` (`"string s = \"}\";"`), `Check_ValidCode_ReturnsNull`.
- [ ] **Step 2:** Testes `PlayerCompiler`: `Compile_ValidCode_ReturnsAssemblyAndPdb`, `Compile_SyntaxErrorOnPlayerLine2_ReportsLine2`, `Compile_ErrorOutsidePlayerCode_ReportsNullLine`, `Compile_MissingVariable_ExplainsWhichOne` (template usa `health`, jogador não o declara → `Explanation.Pt` contém `health`), `Compile_Portuguese_UsesLocalizedMessage` (mensagem ≠ versão `en`), `Compile_OnlyErrorsAreReported` (código com aviso, ex.: variável não usada, compila sem erros). `StatementCounter_CountsNestedStatements` (`"int a = 1; if (a > 0) { a++; }"` → 3), `StatementCounter_EmptyCode_IsZero`.
- [ ] **Step 3:** Run → FAIL. **Step 4:** Implementar. **Step 5:** Run → PASS.
- [ ] **Step 6:** Commit `feat: precheck and compile player code with roslyn`.

### Task 5: Guarda de ciclos e APIs proibidas

**Files:**
- Create: `src/CodeQuest.Engine/LoopGuardRewriter.cs`, `Engine/ForbiddenApiChecker.cs`
- Modify: `src/CodeQuest.Engine/PlayerCompiler.cs` (aplica o rewriter só à árvore do código do jogador, junta a fonte do guarda e corre o verificador de APIs antes de emitir)
- Test: `tests/CodeQuest.Tests/Engine/LoopGuardRewriterTests.cs`, `ForbiddenApiCheckerTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed class LoopGuardRewriter : CSharpSyntaxRewriter` — insere `__LoopGuard.Tick();` como primeira instrução do corpo de cada `while`, `do`, `for` e `foreach` (envolvendo o corpo num bloco se não o for), sem mudar números de linha. Fonte do guarda: `internal static class __LoopGuard { public const int Limit = 1_000_000; ... Tick() lança TimeoutException ao passar o limite }`.
  - `internal static class ForbiddenApiChecker { public static CodeRejected? Check(SemanticModel model, SyntaxNode playerRoot, int playerLineOffset); }` — rejeita qualquer símbolo usado pelo jogador cujo namespace comece por `System.IO`, `System.Threading`, `System.Diagnostics`, `System.Net`, `System.Reflection`, `System.Runtime.InteropServices`, ou cujo tipo seja `System.Environment`, `System.AppDomain`, `System.GC`, `System.Activator`. Mensagem: "Esse feitiço é proibido nesta masmorra: `{símbolo}` (linha N)." / "That spell is forbidden in this dungeon: `{symbol}` (line N).". O `PlayerCompiler` passa a devolver o `CodeRejected` no `CompileOutcome` (campo novo `CodeRejected? Rejected`).

- [ ] **Step 1:** Testes `LoopGuardRewriter`: `Rewrite_WhileTrue_InsertsTick`, `Rewrite_ForWithoutBlock_WrapsBodyAndInsertsTick`, `Rewrite_DoWhile_InsertsTick`, `Rewrite_Foreach_InsertsTick`, `Rewrite_NestedLoops_InsertsTickInEach`, `Rewrite_PreservesPlayerLineNumbers` (erro na linha 3 depois de um `while` na linha 1 continua na linha 3).
- [ ] **Step 2:** Testes `ForbiddenApiChecker`: `Check_ThreadSleep_Rejected`, `Check_EnvironmentExit_Rejected`, `Check_FileReadAllText_Rejected`, `Check_TaskRun_Rejected`, `Check_FullyQualifiedProcessStart_Rejected` (`System.Diagnostics.Process.Start("x")`), `Check_MathAndStrings_Accepted` (`Math.Max`, `string.Join`, `Console.WriteLine`), `Check_ReportsPlayerLine`.
- [ ] **Step 3:** Run → FAIL. **Step 4:** Implementar. **Step 5:** Run → PASS (todos os testes).
- [ ] **Step 6:** Commit `feat: guard loops and forbid dangerous apis`.

### Task 6: Executor Roslyn

**Files:**
- Create: `src/CodeQuest.Engine/PlayerLoadContext.cs`, `Engine/RoslynCodeRunner.cs`, `Engine/RuntimeErrorExplainer.cs`
- Test: `tests/CodeQuest.Tests/Engine/RoslynCodeRunnerTests.cs`, `RuntimeErrorExplainerTests.cs`

**Interfaces:**
- Consumes: `PlayerCodePrecheck`, `PlayerCompiler`, `StatementCounter`, `PlayerMessages` (Task 4–5), tipos `Running/*` (Task 4).
- Produces: `public sealed class RoslynCodeRunner : ICodeRunner` (construtor sem parâmetros). Fluxo: precheck → `CodeRejected` | compilar → `CodeRejected` ou `CompilationFailed` | carregar assembly + PDB em `PlayerLoadContext` (colecionável, `Load` devolve `null`) → invocar `Validation.Run()` por reflexão e materializar → falhas → `ChecksFailed`, nenhuma → `Passed(StatementCounter.Count(playerCode))` → `Unload()` no fim.
- `TimeoutException` (do guarda) → `TimedOut`; limite de relógio de 3 s com `Task.Run` + `WaitAsync` → `TimedOut`; outra exceção → `RuntimeError`.
- `internal static class RuntimeErrorExplainer { public static RuntimeError Explain(Exception exception, int playerLineOffset, int playerLineCount); }` — linha do jogador tirada do `StackTrace` (frames com ficheiro do jogador). Explicações: `DivideByZeroException` → "Dividiste por zero." / "You divided by zero."; `NullReferenceException` → "Usaste algo que está vazio (null)." / "You used something that is empty (null)."; `IndexOutOfRangeException` e `ArgumentOutOfRangeException` → "Tentaste usar uma posição que não existe." / "You tried to use a position that doesn't exist."; `FormatException` → "O texto não tem o formato esperado." / "The text isn't in the expected format."; outras → "O feitiço falhou durante a execução." / "The spell failed while running.".

- [ ] **Step 1:** Testes com um template de exemplo (`public static int Twice(int x) { // {{PLAYER_CODE}} }`): `Run_CorrectCode_Passed`, `Run_WrongResult_ChecksFailedWithMessages`, `Run_CompileError_CompilationFailed`, `Run_EmptyCode_CodeRejected`, `Run_ClosingBrace_CodeRejected`, `Run_ForbiddenApi_CodeRejected`, `Run_WhileTrue_TimedOutUnder5Seconds`, `Run_ForEverAndNestedLoops_TimedOut`, `Run_DivideByZero_RuntimeErrorWithPlayerLine`, `Run_Passed_ReportsStatementCount`, `Run_TwiceInARow_BothPass` (assembly anterior descarregado não interfere).
- [ ] **Step 2:** Testes `RuntimeErrorExplainer`: um por tipo de exceção acima + `Explain_UnknownException_GenericText` + `Explain_FrameOutsidePlayerCode_NullLine`.
- [ ] **Step 3:** Run → FAIL. **Step 4:** Implementar. **Step 5:** Run → PASS.
- [ ] **Step 6:** Commit `feat: run player code in collectible load context`.

### Task 7: Os 3 níveis

**Files:**
- Create: `content/levels/01-variables/{level.json,Template.cs,Starter.cs,Solution.cs,Validation.cs}`
- Create: `content/levels/02-operators/...`, `content/levels/03-conditions/...`
- Test: `tests/CodeQuest.Tests/Levels/LevelSolutionTests.cs`

Conteúdo:
- **01 variáveis** (mundo 1, nº 1, página `variables`, máx. 4 instruções). Template: `public record Hero(string Name, int Health, double Mana, bool IsAlive);` e `public static class Spell { public static Hero Cast() { // {{PLAYER_CODE}} return new Hero(name, health, mana, isAlive); } }`. Objetivo: `name = "Merlin"`, `health = 100`, `mana = 50.5`, `isAlive = true`. Starter: só `string name = "Merlin";` e um comentário com o que falta.
- **02 operadores** (mundo 1, nº 2, página `operators`, máx. 1). `public static class Spell { public static int Damage(int attack, int defense, int multiplier) { ... } }`. Regra: `attack * multiplier - defense`. Verificações: `(10,3,2)=17`, `(5,0,3)=15`, `(7,4,1)=3`. Starter: `return 0;`.
- **03 condições** (mundo 1, nº 3, página `conditions`, máx. 5). `public static string ChooseDoor(string rune)`: `"fire"→"left"`, `"ice"→"right"`, `"wind"→"center"`, outra → `"back"`. Starter: `return "back";`. Solução com `switch`.
- Cada nível: 2 dicas, história curta no tema masmorra/mago, conceito com explicação curta + exemplo. Mensagens de falha em `Validation.cs` em PT-PT e EN, dizendo o que se esperava.

- [ ] **Step 1:** Testes (Theory sobre `LevelRepository.Load` do conteúdo real): `Solution_Passes`, `Starter_DoesNotPass`, `Solution_FitsThreeStarLimit`, `AllTexts_HavePtAndEn`, `EachLevel_HasTwoHints`, `Level1_MissingVariable_ExplainsWhichOne` (Starter → `CompilationFailed` com `Explanation` a nomear `health`).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Escrever o conteúdo. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: add first three levels`.

### Task 8: Grimório e textos da interface

**Files:**
- Create: `content/grimoire/{variables,operators,conditions}.json`, `content/ui.json`
- Test: `tests/CodeQuest.Tests/Content/ContentTests.cs`

Notas: cada página com o que é, sintaxe, exemplo copiável, 2–3 erros comuns, `learnUrl` do Microsoft Learn confirmado pelo MCP (tipos integrados, operadores aritméticos, instruções de seleção).

Chaves de UI: `app.title`, `map.title`, `map.locked`, `level.run`, `level.running`, `level.hint`, `level.solution`, `level.passed`, `level.next`, `level.compileErrors`, `level.checksFailed`, `level.timedOut` ("O teu ciclo nunca acaba: verifica a condição."), `level.runtimeError`, `level.rejected`, `level.line`, `level.originalMessage`, `grimoire.open`, `grimoire.title`, `grimoire.search`, `grimoire.copy`, `grimoire.learnMore`, `grimoire.mistakes`, `language.label`, `progress.unreadable` ("O progresso guardado não pôde ser lido. Começaste de novo.").

- [ ] **Step 1:** Testes: `EveryLevel_HasGrimoirePage`, `EveryPage_HasPtAndEnTexts`, `EveryPage_LearnUrlIsMicrosoftLearn` (começa com `https://learn.microsoft.com/`), `EveryPage_ExampleCompiles` (o exemplo, inserido num template neutro, não dá `CompilationFailed`), `UiText_HasEveryKey` (lista acima numa constante partilhada `UiKeys` em `tests/CodeQuest.Tests/Content/UiKeys.cs`), `UiText_AllKeysHavePtAndEn`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Escrever o conteúdo. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: add grimoire pages and ui text`.

### Task 9: Base da Web, idioma, progresso e mapa

**Files:**
- Modify: `src/CodeQuest.Web/Program.cs`, `Components/App.razor` (`@rendermode="new InteractiveServerRenderMode(prerender: false)"` em `Routes` e `HeadOutlet`)
- Create: `src/CodeQuest.Web/Services/GameContent.cs`, `Services/UiText.cs`, `Services/ProgressStore.cs`
- Create: `Components/Layout/MainLayout.razor(.css)`, `Components/LanguageSwitcher.razor`, `Components/Pages/Home.razor(.css)`, `wwwroot/app.css`
- Test: `tests/CodeQuest.Tests/Web/UiTextTests.cs`, `ProgressStoreTests.cs`, `LanguageSwitcherTests.cs`, `HomeTests.cs`, `tests/CodeQuest.Tests/Web/TestContextExtensions.cs` (regista serviços reais com conteúdo real + `JSInterop` do bUnit para `localStorage`)

**Interfaces:**
- Produces:
  - `public sealed class GameContent` (singleton): `IReadOnlyList<Level> Levels`, `IReadOnlyList<GrimoirePage> Pages`, `UiTextCatalog Ui`, `Level? FindLevel(string id)`, `GrimoirePage? FindPage(string id)`.
  - `public sealed class UiText` (scoped): `Language Language`, `string this[string key]`, `Task SetLanguageAsync(Language)`, `Task LoadAsync()`, `event Action? Changed`. Idioma em `localStorage["cq.language"]` (`"pt"`/`"en"`; qualquer outro valor → PT).
  - `public sealed class ProgressStore` (scoped): `Task<ProgressLoadResult> LoadAsync()`, `Task SaveAsync(PlayerProgress)`. Chave `localStorage["cq.progress"]`, via `IJSRuntime.InvokeAsync<string?>("localStorage.getItem", ...)` e `InvokeVoidAsync("localStorage.setItem", ...)`.
  - `ICodeRunner` registado como singleton (`RoslynCodeRunner`).
- MainLayout: cabeçalho com título e `LanguageSwitcher`; mostra o aviso `progress.unreadable` uma vez quando `WasUnreadable`.
- Home (`/`): mapa com os níveis por ordem; cada um mostra título, estrelas ou cadeado; desbloqueados ligam a `/level/{id}`.

- [ ] **Step 1:** Testes: `UiText_DefaultsToPortuguese`, `UiText_LoadsSavedLanguage`, `UiText_InvalidSavedLanguage_FallsBackToPortuguese`, `UiText_SetLanguage_SavesAndRaisesChanged`, `ProgressStore_Load_Empty_ReturnsNewProgress`, `ProgressStore_SaveThenLoad_RoundTrips`, `ProgressStore_Corrupted_ReturnsUnreadable`, `LanguageSwitcher_Click_ChangesTexts`, `Home_NewPlayer_FirstLevelOpenOthersLocked`, `Home_CompletedLevel_ShowsStars`, `Home_LockedLevel_HasNoLink`, `MainLayout_UnreadableProgress_ShowsWarning`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implementar. **Step 4:** build sem avisos; `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: add map, language switch and progress store`.

### Task 10: Editor Monaco

**Files:**
- Create: `src/CodeQuest.Web/Components/CodeEditor.razor`, `CodeEditor.razor.js`, `CodeEditor.razor.css`
- Test: `tests/CodeQuest.Tests/Web/CodeEditorTests.cs`

**Interfaces:**
- Produces: `<CodeEditor @ref="editor" InitialCode="..." />` com `[Parameter] string InitialCode`, `Task<string> GetCodeAsync()`, `Task SetCodeAsync(string code)`, `Task InsertAsync(string text)`, `Task ShowErrorsAsync(IReadOnlyList<CompilationError> errors)` (sublinha só erros com `Line`), `Task ClearErrorsAsync()`. Implementa `IAsyncDisposable` (apanha `JSDisconnectedException`).
- JS (`CodeEditor.razor.js`, exporta `create`, `getValue`, `setValue`, `insert`, `setErrors`, `dispose`): carrega `loader.js` da CDN, `require.config({ paths: { vs: ... } })`, cria o editor com `language: "csharp"`, `quickSuggestions: false`, `suggestOnTriggerCharacters: false`, tema escuro; marcadores com `monaco.editor.setModelMarkers`.

- [ ] **Step 1:** Testes bUnit (módulo JS simulado com `JSInterop.SetupModule`): `FirstRender_CreatesEditorWithInitialCode`, `GetCode_ReturnsValueFromJs`, `ShowErrors_SendsOnlyErrorsWithLine`, `Insert_CallsJsInsert`, `Dispose_DisposesModule`, `Dispose_WhenDisconnected_DoesNotThrow`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implementar. **Step 4:** build sem avisos; `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: add monaco code editor`.
- O JS real é testado na Task 13.

### Task 11: Página do nível

**Files:**
- Create: `src/CodeQuest.Web/Components/Pages/LevelPage.razor`, `LevelPage.razor.css`, `Components/RunResultView.razor`
- Test: `tests/CodeQuest.Tests/Web/LevelPageTests.cs`, `RunResultViewTests.cs` (`ICodeRunner` falso que devolve o resultado pedido pelo teste)

**Interfaces:**
- Consumes: `GameContent`, `UiText`, `ProgressStore`, `ICodeRunner`, `CodeEditor`, `StarCalculator`.
- Produces: `<RunResultView Result="..." />` — mostra cada tipo de `RunResult`: `CodeRejected` (razão + linha), `CompilationFailed` (por erro: "linha N:" quando há linha, explicação amigável quando há, e a mensagem original por baixo com `level.originalMessage`), `ChecksFailed`, `TimedOut`, `RuntimeError` (explicação + linha + mensagem original), `Passed` (nada; tratado pela página).
- Rota `/level/{Id}`; nível bloqueado ou inexistente → redireciona para `/`.
- Mostra história, conceito, editor com `StarterCode`, botão Executar (desativado enquanto corre).
- Qualquer resultado que não seja `Passed` chama `RecordFailure`; `CompilationFailed` também chama `editor.ShowErrorsAsync`.
- `Passed` → `StarCalculator.Calculate(...)`, `RecordPass`, animação CSS da porta a abrir e do herói a avançar, estrelas, botão para o próximo nível (ou para o mapa no último).
- Dicas uma a uma (`RecordHintUsed`); botão "ver solução" só quando `CanSeeSolution` (`RecordSolutionSeen` e põe a solução no editor).
- Grava progresso depois de cada alteração.

- [ ] **Step 1:** Testes `RunResultView`: um por tipo de resultado, `CompilationError_WithoutLine_ShowsNoLineNumber`, `CompilationError_WithExplanation_ShowsExplanationAndOriginal`.
- [ ] **Step 2:** Testes `LevelPage`: `LockedLevel_RedirectsHome`, `UnknownLevel_RedirectsHome`, `ShowsStoryConceptAndStarter`, `Run_Failure_RecordsFailureAndSaves`, `Run_CompilationFailed_ShowsErrorsInEditor`, `Run_Passed_ShowsStarsAndOpensDoor`, `Run_Passed_World1WithHint_GivesTwoOrThreeStars`, `Hint_ShowsNextHintAndRecordsUse`, `SolutionButton_HiddenBeforeThreeFailures`, `SolutionButton_AfterThreeFailures_PutsSolutionInEditorAndZeroStars`, `LastLevel_Passed_NextGoesToMap`, `RunButton_DisabledWhileRunning`.
- [ ] **Step 3:** Run → FAIL. **Step 4:** Implementar. **Step 5:** build sem avisos; `dotnet test` → PASS.
- [ ] **Step 6:** Commit `feat: add playable level page`.

### Task 12: Painel do Grimório

**Files:**
- Create: `src/CodeQuest.Web/Components/GrimoirePanel.razor`, `GrimoirePanel.razor.css`
- Modify: `LevelPage.razor` (botão `grimoire.open`; o editor mantém o código)
- Test: `tests/CodeQuest.Tests/Web/GrimoirePanelTests.cs`, mais 2 testes em `LevelPageTests.cs`

**Interfaces:**
- Produces: `<GrimoirePanel IsOpen="..." OnClose="..." OnCopyExample="..." InitialPageId="..." />` — `EventCallback<string> OnCopyExample` recebe o exemplo; a página do nível chama `editor.InsertAsync`.
- Lista páginas agrupadas por mundo, só as disponíveis (`PlayerProgress.IsPageAvailable`); pesquisa por palavra (sem distinguir maiúsculas nem acentos) no título e no texto do idioma atual; abrir uma página chama `MarkPageViewed` e grava.

- [ ] **Step 1:** Testes: `Closed_RendersNothing`, `Open_ListsOnlyAvailablePages`, `Open_GroupsByWorld`, `InitialPage_IsShownAndMarkedViewed`, `Search_FiltersByTitle`, `Search_IgnoresCaseAndAccents` ("variaveis" encontra "Variáveis"), `Search_NoMatch_ShowsEmptyState`, `Copy_RaisesOnCopyExampleWithExample`, `LearnMore_LinksToLearnUrl`; em `LevelPageTests`: `GrimoireCopy_InsertsIntoEditor`, `OpeningGrimoire_KeepsEditorCode`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implementar. **Step 4:** build sem avisos; `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: add grimoire side panel`.

### Task 13: Testes de ponta a ponta (browser real)

**Files:**
- Create: `tests/CodeQuest.E2ETests/GameJourneyTests.cs`

**Interfaces:**
- Consumes: `GameServerFixture` (Task 1). Cada teste usa um contexto de browser novo (`localStorage` limpo).

- [ ] **Step 1:** Testes: `Level1_TypeSolution_DoorOpensAndStarsShown` (escreve no Monaco real, executa, vê estrelas), `CompileError_IsUnderlinedInEditor` (existe marcador `.squiggly-error` no editor), `InfiniteLoop_ShowsTimedOutMessage`, `Reload_KeepsProgress` (passa nível 1, recarrega, nível 2 desbloqueado), `SwitchToEnglish_PersistsAfterReload`, `CorruptedProgress_ShowsWarning` (escreve lixo em `cq.progress` antes de abrir), `Grimoire_CopyExample_AppearsInEditor`.
- [ ] **Step 2:** Run `dotnet test tests/CodeQuest.E2ETests` → PASS.
- [ ] **Step 3:** Commit `test: add end-to-end journeys`.

### Task 14: README e verificação final

**Files:**
- Create: `README.md` (curto: o que é, como correr, como correr os testes incluindo instalar o Chromium do Playwright, arquitetura com a tabela de projetos, como acrescentar um nível passo a passo, nota de segurança do executor).

- [ ] **Step 1:** Escrever o README. Commit `docs: add readme`.
- [ ] **Step 2 (Opus):** `dotnet build` (0 avisos) e `dotnet test` (tudo verde, incluindo E2E).
- [ ] **Step 3 (Opus):** Correr a app e jogar os 3 níveis no browser, em PT e EN, para validar o que os testes não medem: aspeto visual, animação e textos.
- [ ] **Step 4 (Fable):** Revisão final do branch inteiro contra o `CLAUDE.md`.
