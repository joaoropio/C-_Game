# Code Quest: a masmorra do C#

Jogo web onde o jogador é um mago e a magia é código C#. Cada sala da masmorra é um desafio: o código escrito no editor é compilado com Roslyn, validado por testes escondidos e, se passar, a porta abre. Pensado para aprender C# do zero, com interface em PT-PT e EN, Grimório (documentação dentro do jogo) e progresso guardado no `localStorage`.

Estado atual: Mundo 1, níveis 1 a 3 (variáveis e tipos, operadores, if/else e switch).

## Requisitos

- Visual Studio 2026 ou um SDK de .NET que compile projetos `net10.0`. Foi desenvolvido com o SDK 11 que vem com o Visual Studio (não há `global.json`).
- Internet: o editor Monaco é carregado da CDN jsDelivr (versão fixa), por isso o jogo não funciona offline. Os testes E2E também precisam de internet na primeira execução, para descarregar o Chromium.

## Como correr

```powershell
dotnet run --project src/CodeQuest.Web
```

O endereço aparece na consola. Em alternativa, abre `CodeQuest.sln` no Visual Studio e arranca o projeto `CodeQuest.Web`.

## Como correr os testes

```powershell
dotnet test
```

- `CodeQuest.Tests` (xUnit e bUnit): domínio, motor, conteúdo, solução de cada nível e componentes Blazor.
- `CodeQuest.E2ETests` (Playwright): percursos do jogador num browser real, com o servidor a correr em Kestrel. No arranque, o fixture executa `playwright install chromium` (via `Microsoft.Playwright`), por isso não é preciso instalar o browser à mão. A primeira execução descarrega o Chromium e demora mais.

Para correr só um projeto: `dotnet test tests/CodeQuest.Tests` ou `dotnet test tests/CodeQuest.E2ETests`.

## Arquitetura

| Projeto | Responsabilidade | Depende de |
|---|---|---|
| `src/CodeQuest.Core` | Domínio: níveis, Grimório, textos da interface, progresso e estrelas; interface `ICodeRunner`. Só usa a BCL (`System.Text.Json`). | nada |
| `src/CodeQuest.Engine` | Compila o código do jogador com Roslyn e executa-o num `AssemblyLoadContext` colecionável. Verifica APIs proibidas, injeta o limite de ciclos e explica erros a iniciantes. Implementa `ICodeRunner`. | Core |
| `src/CodeQuest.Web` | Blazor Web App (Interactive Server): páginas, componentes, interop com o Monaco (`CodeEditor.razor.js`) e leitura do progresso no `localStorage`. | Core, Engine |
| `tests/CodeQuest.Tests` | Testes xUnit e bUnit. | Core, Engine, Web |
| `tests/CodeQuest.E2ETests` | Testes Playwright. | Web |

As dependências apontam para dentro: o Core não conhece Blazor nem Roslyn.

O conteúdo fica em `content/`, na raiz, partilhado pela Web e pelos testes (os três projetos copiam-no para a saída):

```
content/
  levels/<NN-nome>/
    level.json      metadados e textos PT/EN
    Template.cs     código à volta do código do jogador
    Starter.cs      código inicial mostrado no editor
    Solution.cs     solução de referência
    Validation.cs   testes escondidos
  grimoire/<id>.json   uma página do Grimório por conceito
  ui.json              textos da interface em PT/EN
```

## Como acrescentar um nível

Não é preciso mexer no motor, na interface nem nos `.csproj`: os níveis são descobertos a partir das pastas de `content/levels/`.

1. Cria a pasta `content/levels/<NN-nome>/` (por exemplo `04-loops`).
2. `level.json`, com estes campos:
   - `id` (igual ao nome da pasta), `world`, `number`;
   - `title`, `story` e `concept`, cada um com `pt` e `en`;
   - `hints`: lista de textos `{ pt, en }`. Cada nível tem exatamente 2 dicas;
   - `maxStatementsForThreeStars`: nº máximo de instruções (contadas pelo Roslyn) da solução para ter 3 estrelas;
   - `grimoirePageId`: `id` da página do Grimório deste nível.
3. `Template.cs`: o código C# à volta do código do jogador. Tem de ter exatamente uma linha `// {{PLAYER_CODE}}`, sozinha na linha (os espaços à volta ignoram-se). É aí que o código do jogador é inserido. O jogador só escreve o corpo de um método já dado.
4. `Starter.cs`: o código inicial que aparece no editor. Tem de **não** passar na validação.
5. `Solution.cs`: uma solução que passa na validação e cabe em `maxStatementsForThreeStars`.
6. `Validation.cs`: os testes escondidos, compilados juntamente com o `Template.cs` e o código do jogador. Define `public static class Validation` com `public static IEnumerable<(string Pt, string En)> Run()`. Cada `yield return (pt, en)` é uma falha com a mensagem para o jogador; se não devolver nada, o nível passa. Exemplo:

   ```csharp
   public static class Validation
   {
       public static System.Collections.Generic.IEnumerable<(string Pt, string En)> Run()
       {
           if (Spell.Damage(10, 3, 2) != 17)
           {
               yield return (
                   "Com ataque 10, defesa 3 e multiplicador 2, o dano devia ser 17.",
                   "With attack 10, defense 3 and multiplier 2, the damage should be 17.");
           }
       }
   }
   ```

7. `content/grimoire/<id>.json`: a página do Grimório (`id`, `world`, `title`, `whatItIs`, `syntax`, `example`, `commonMistakes` com 2 ou 3 entradas, `learnUrl` para o Microsoft Learn). O `example` tem de compilar e não pode reutilizar os identificadores do nível. Se o nível usar uma página que já existe, não é preciso criar outra.
8. Corre `dotnet test`. Os testes descobrem o novo nível sozinhos e verificam que a solução passa, que o inicial não passa, que a solução cabe no limite de estrelas, que há PT e EN em todos os textos, 2 dicas, um único marcador e uma página do Grimório válida.

Regras do código do jogador: só são permitidos os namespaces `System`, `System.Collections.Generic`, `System.Linq` e `System.Text` (mais os tipos do próprio nível). Dentro de `System` ficam proibidos, entre outros, `Environment`, `AppDomain`, `GC`, `Activator`, `Type`, `Delegate`, a leitura da consola e `dynamic`. Um nível que precise de mais namespaces tem de alargar essa lista em `ForbiddenApiChecker`.

## Segurança

O código do jogador corre no mesmo processo da aplicação, num `AssemblyLoadContext` colecionável, com uma lista de APIs permitidas, um contador que interrompe ciclos infinitos (`LoopGuardRewriter`) e um limite de 3 segundos por execução. Isto chega para uso local, mas o .NET moderno não isola código não confiável dentro do processo. Num alojamento público, o executor tem de correr num contentor ou noutra sandbox; a execução está atrás de `ICodeRunner` precisamente para isso.
