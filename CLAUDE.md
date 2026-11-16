# Code Quest: a masmorra do C#

Jogo web em que o jogador avança numa masmorra escrevendo código C#. Cada sala é um desafio: o código é compilado e validado por testes escondidos e, se passar, a porta abre. Serve para aprender C# do zero até ao nível avançado e como projeto de portefólio do João.

## Regras de trabalho (obrigatórias)

- Age como engenheiro sénior em todas as partes: arquitetura, código, testes, UI e documentação.
- Clean Code: nomes claros, métodos pequenos com uma responsabilidade, SOLID quando resolve um problema real.
- YAGNI: só o essencial. Sem abstrações, camadas ou funcionalidades "para o futuro" que ninguém pediu. Sem código morto nem comentários que repetem o código.
- Não inventar APIs nem comportamentos. Confirmar cada API de .NET, Roslyn e Blazor no Microsoft Learn (MCP `Microsoft Learn`) antes de a usar.
- Antes de dar uma tarefa por concluída: compilar sem avisos e correr todos os testes. Dizer explicitamente o que ficou por verificar.
- Perguntar ao João antes de decisões que não estejam neste ficheiro.

## Stack

- .NET 10 (`net10.0`), solução `.sln` para abrir no Visual Studio 2026 (Windows). Compila-se com o SDK 11 instalado pelo VS; não há SDK 10 nem `global.json`.
- C# (motor, domínio, testes), Razor (componentes Blazor), CSS, JSON (conteúdo).
- JavaScript apenas o mínimo para ligar o editor Monaco: interop próprio e pequeno, sem wrappers de terceiros.
- Testes: xUnit (unitários), bUnit (componentes Blazor) e Playwright for .NET (ponta a ponta num browser real, com `WebApplicationFactory` + Kestrel). Todo o comportamento tem teste, incluindo a Web e o JavaScript.
- Controlo de versões: git (repositório local; envio para o GitHub fica para mais tarde).
- Nullable ativo, `TreatWarningsAsErrors`, `.editorconfig`.

## Arquitetura

| Projeto | Responsabilidade | Depende de |
|---|---|---|
| `CodeQuest.Core` | Domínio: níveis, desafios, Grimório, progresso; interface `ICodeRunner`. Sem dependências externas. | — |
| `CodeQuest.Engine` | Compila o código do jogador com Roslyn (`Microsoft.CodeAnalysis.CSharp`) e executa-o num `AssemblyLoadContext` colecionável com limite de tempo. Implementa `ICodeRunner`. | Core |
| `CodeQuest.Web` | Blazor Web App com render mode Interactive Server; componentes Razor; interop com Monaco; conteúdo em JSON. | Core, Engine |
| `CodeQuest.Tests` | Testes xUnit do domínio, do motor e de cada nível; testes bUnit dos componentes. | Core, Engine, Web |
| `CodeQuest.E2ETests` | Testes Playwright dos percursos do jogador num browser real (inclui Monaco e `localStorage`). | Web |

- As dependências apontam para dentro: o Core não conhece Blazor nem Roslyn.
- Acrescentar um nível = acrescentar dados JSON + testes, sem mexer no motor nem na UI.
- Segurança: o .NET moderno não isola código não confiável dentro do processo (Code Access Security não é suportado). Para uso local está bem; se o jogo for público, o executor tem de passar para um contentor. Por isso a execução fica atrás de `ICodeRunner`.
- Cada execução tem limite de tempo (um `while(true)` do jogador não pode bloquear a app). Como o .NET não aborta threads, o limite é cooperativo: antes de compilar, um `CSharpSyntaxRewriter` (`LoopGuardRewriter`) injeta um contador em cada ciclo que lança exceção ao passar o limite. A recursão infinita é travada pelo mesmo rewriter: injeta `__LoopGuard.Enter()` no início do código do jogador (o corpo do método do template; num template de classe, em que o marcador está diretamente numa classe, não há método do template e a guarda vai no início de cada método escrito pelo jogador), no início de cada função local, lambda e método anónimo escritos pelo jogador (os corpos em expressão passam a bloco) — `Enter` verifica `RuntimeHelpers.TryEnsureSufficientExecutionStack()` (lança `InsufficientExecutionStackException`, explicada como recursão infinita) e tem o seu próprio limite (`CallLimit` = 10 000 000 chamadas, separado do `Limit` dos ciclos). Quando um limite ou a pilha se esgota, `__LoopGuard.Tripped` fica a `true` para sempre e o rewriter torna inertes os `catch` do jogador (filtro `when (!Tripped)`) e os corpos de `finally` (`if (!Tripped)`), porque correm em cima da pilha já esgotada; assim nada engole a falha. A proteção cobre o código do jogador, os corpos dos seus métodos (templates de classe), funções locais e lambdas. Em templates de classe o jogador só pode escrever métodos: tipos aninhados (`class`, `struct`, `record`, `interface`, `enum`, `delegate`) e campos, propriedades, indexadores, eventos, construtores e destrutores são rejeitados; `async` (métodos, funções locais, lambdas, métodos anónimos) e `extern` são rejeitados em qualquer template (um `async void` que lança falha na thread pool e mataria o processo) (`PlayerMemberChecker`, `CodeRejected`) antes de compilar; quando o Mundo 3 deixar o jogador declarar outros membros, a proteção tem de ser alargada aos seus corpos (acessores, construtores, operadores). A contagem de instruções (`StatementCounter`) conta as instruções dentro dos métodos (um corpo em expressão conta como uma). `stackalloc` é proibido (`ForbiddenApiChecker`), pois esgota a pilha sem recursão. Recursão sem nenhum frame do jogador (p. ex. um `IQueryable` auto-referencial ou um `SelectMany` com um grupo de métodos sobre uma lista que se contém a si própria) não é coberta pela proteção; não é plausível para principiantes.
- Antes de executar, o motor rejeita com mensagem clara: código vazio, chavetas desequilibradas e APIs fora da lista permitida. Só são permitidos os namespaces `System`, `System.Collections.Generic`, `System.Linq` e `System.Text` (mais os tipos do próprio nível), e dentro de `System` ficam proibidos `Environment`, `AppDomain`, `GC`, `Activator`, `Type`, `Delegate`, `TimeProvider`, `ParallelEnumerable`, a leitura e o redirecionamento da consola e `dynamic`. Níveis futuros que precisem de outros namespaces (async, ficheiros, JSON) alargam a lista para esse nível.
- O contador de ciclos cobre `while`, `do`, `for`, `foreach` e cada `goto` (incluindo `goto case`) e, depois de passar o limite, volta a lançar em todas as chamadas (um `catch` do jogador não o anula). O que escapa (ex.: LINQ infinito) é parado pelo limite de 3 s.
- Erros para iniciantes: exceções comuns e erros fora do código do jogador são explicados em PT/EN, com a linha do jogador quando existe e a mensagem original por baixo.
- Progresso guardado tem `version`; se estiver ilegível, o jogo recomeça e avisa o jogador.
- Conteúdo (níveis, Grimório, textos da UI) fica em `content/` na raiz, partilhado pela Web e pelos testes. Os leitores de JSON ficam no Core (só `System.Text.Json`).
- Cada nível é uma pasta em `content/levels/` com `level.json` (textos PT/EN incluindo `sceneLabel`, código inicial, dicas, solução, limite de instruções para 3 estrelas), `scene.svg` (a vinheta do pergaminho; obrigatória e validada pelo `SceneSvgValidator`, porque é injetada como marcação) e `Validation.cs` (testes escondidos, compilados junto com o código do jogador), que devolve `(Case, Expected, Actual)` para cada caso, com literais C# em cultura invariante, comparados como texto (ordinal); o nível só passa se todos os casos coincidirem. Os testes descobrem os níveis automaticamente.

## Funcionalidades

- Tema: masmorra; o jogador é um mago e a magia é código.
- Progressão para iniciante absoluto: um conceito novo por nível, com explicação curta, exemplo e dica. A dificuldade sobe aos poucos.
- Código do jogador em inglês (`Hero`, `Health`, `Attack`). Interface com seletor PT/EN, PT por defeito. O PT é português de Portugal (ficheiro, ecrã).
- No Mundo 1 o jogador escreve o corpo de um método; no Mundo 2 escreve métodos `static` dentro de uma classe que não vê; `class`, `using` e `namespace` só aparecem no Mundo 3.
- Níveis desbloqueiam por ordem: o seguinte só abre depois de passar o anterior.
- Editor Monaco com realce de sintaxe e erros de compilação sublinhados. Sem autocompletar por agora. Carregado da CDN jsDelivr com versão fixa (não funciona offline).
- Erros de compilação mostrados com linha e mensagem clara.
- Resultado do feitiço caso a caso (entrada, obtido, esperado), também quando o nível passa.
- Progresso guardado em `localStorage` (sem contas nem base de dados).
- Dicas grátis no Mundo 1 (não tiram estrelas). Opção de ver a solução após 3 tentativas falhadas (o nível fica sem estrelas).
- Estrelas por nível: 1 por passar; 2 sem dicas (só conta a partir do Mundo 2; no Mundo 1 passar já dá 2); 3 se a solução tiver no máximo N instruções, contadas pelo Roslyn, com N definido no `level.json`.
- Animações simples (porta abre, herói avança). Sem som.
- Visual próprio (sem arte de outros jogos).

### Grimório (documentação dentro do jogo)

- Uma página por conceito: o que é, sintaxe, exemplo copiável para o editor, erros comuns, link para a página oficial do Microsoft Learn.
- Pesquisa por palavra e navegação por mundos.
- As páginas desbloqueiam-se com o progresso; as já vistas ficam sempre disponíveis.
- Abre como painel lateral dentro do nível, sem perder o código.
- PT/EN. Conteúdo em ficheiros de dados, separado do código.

## Roteiro de níveis

- Mundo 1 – O básico: variáveis e tipos; operadores; if/else e switch; ciclos; arrays e strings.
- Mundo 2 – Organizar o código: métodos; sobrecarga e parâmetros opcionais; exceções; List e Dictionary.
- Mundo 3 – POO: classes e objetos; construtores e encapsulamento; herança; polimorfismo; classes abstratas e interfaces; static, enum e struct.
- Mundo 4 – Intermédio: genéricos; delegates e eventos; lambdas e LINQ; records e pattern matching; nullable.
- Mundo 5 – Avançado: async/await; ficheiros e JSON; IDisposable e using; padrões de design (Factory, Strategy, Observer); testes unitários; chefe final (motor de combate por turnos).

## Objetivo da primeira sessão

3 níveis jogáveis de ponta a ponta — (1) variáveis e tipos, (2) operadores (cálculo de dano), (3) if/else e switch (escolher a porta certa) — cada um com a sua página no Grimório, interface PT/EN, progresso guardado, testes a passar e um README curto (arquitetura + como acrescentar um nível).

## Antes de começar

- Confirmar no Microsoft Learn que versão do Visual Studio é necessária para .NET 10 e verificar o SDK instalado (`dotnet --list-sdks`).
- Mostrar ao João o plano de ficheiros e pedir aprovação antes de escrever código.
