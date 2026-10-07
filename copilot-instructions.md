# Copilot Instructions for UltrakULL

## Working Rules

Treat the user's explicit request as the source of truth for the desired outcome, while preserving the project constraints below unless the user explicitly asks to change them. If a conflict or ambiguity could affect runtime behavior, a public API, dependencies, or compatibility, ask before proceeding. Otherwise, make the smallest safe assumption and state it.

For each task, inspect the relevant code and conventions, make focused changes, run the smallest relevant validation, and review the final diff. Report checks that could not be run; do not describe unverified behavior as verified.

## 1. Project Overview

UltrakULL is a BepInEx/Harmony localization mod for the compiled PC version of ULTRAKILL.

This is **NOT a Unity Editor project**.

The mod is compiled as a .NET Framework DLL and injected into the game through BepInEx. It interacts with the existing Unity runtime, game assemblies, UI, TextMeshPro, scenes, textures, audio, fonts, and other systems through runtime code and Harmony patches.

Do not assume that Unity Editor project files, prefabs, scenes, ScriptableObjects, or editable Unity assets are available.

When working with Unity APIs, consider the actual runtime behavior of the compiled game rather than typical Unity Editor workflows.

---

## 2. Target Environment

The project currently targets:

* .NET Framework 4.7.2
* C# 7.3
* Unity 2022.3
* BepInEx 5
* Harmony
* TextMeshPro
* EmguCV

The project is built using the existing `.csproj` / `.sln` files.

Do not change the target framework, language version, or project architecture unless explicitly requested.

### C# Compatibility

The project must remain compatible with **C# 7.3**.

Do NOT introduce C# 8.0+ language features, including:

* target-typed `new()`
* switch expressions
* recursive patterns
* `or` / `and` patterns
* nullable reference type syntax
* range/index operators
* other syntax unavailable in C# 7.3

Prefer explicit, compatible syntax even when newer syntax would be shorter.

---

## 3. Runtime Compatibility

This is a mod for an existing compiled game.

Do not assume that a seemingly unusual implementation is incorrect.

Before changing code that interacts with Unity, BepInEx, Harmony, or game-specific classes, understand the surrounding implementation and why it works the way it does. Follow the specific runtime, coroutine, and patching guidance below.

Remember that Unity object lifetime and `UnityEngine.Object` null semantics differ from normal C# objects.

Do not replace Unity-specific behavior with generic C# patterns without verifying that the runtime behavior remains equivalent.

---

## 4. Refactoring Rules

The primary goal of refactoring is to improve maintainability **without changing existing behavior**.

Before making a significant refactor:

1. Analyze the existing implementation.
2. Identify dependencies and side effects.
3. Identify behavior that must remain unchanged.
4. Explain the proposed changes.
5. Prefer incremental changes over large rewrites.

Do not rewrite an entire subsystem simply because the existing code is not aesthetically clean.

Do not introduce abstractions unless they solve a real maintenance problem.

Do not split classes or methods purely to reduce line count.

Do not optimize code based on assumptions. Identify the actual bottleneck first.

Avoid speculative optimizations.

Preserve existing public APIs unless changing them is necessary and explicitly approved.

When possible, keep refactoring changes separate from behavioral changes.

---

## 5. Performance

Performance optimizations must be based on actual runtime behavior.

Pay particular attention to:

* unnecessary allocations
* repeated texture processing
* repeated `GetComponent` / component lookups
* repeated scene searches
* unnecessary LINQ in frequently executed code
* repeated file I/O
* unnecessary material or texture creation
* excessive coroutine activity
* repeated reflection
* unnecessary string allocations
* expensive work performed on the Unity main thread

Do not sacrifice correctness or readability for micro-optimizations.

Avoid introducing `Task`, `async/await`, or `UniTask` unless explicitly requested.

Do not move Unity API calls to background threads unless the specific API is known to be thread-safe.

---

## 6. Unity and Coroutine Rules

Unity APIs generally require execution on the Unity main thread.

Be careful when modifying:

* coroutines
* scene loading
* scene transitions
* `GameObject` / `Component` access
* textures and materials
* UI elements
* TextMeshPro objects

Do not replace coroutine-based code with `Task` or `async/await` merely because it appears cleaner.

When modifying cancellation or coroutine lifecycle logic, verify:

* when the coroutine starts
* when it stops
* whether a scene transition can interrupt it
* whether objects can be destroyed during execution
* whether cancellation tokens are disposed correctly
* whether cached Unity objects are still valid

---

## 7. BepInEx and Harmony

UltrakULL relies on BepInEx and Harmony patches.

Do not remove or simplify a Harmony patch merely because it appears unusual.

Before changing a patch, inspect:

* the target method
* the patch type
* Prefix/Postfix/Transpiler behavior
* method parameters
* execution order
* interactions with other patches
* assumptions about the original game's implementation

Preserve Harmony patch behavior unless the task explicitly requires changing it.

Do not replace a Harmony patch with direct modification of game assemblies.

---

## 8. Decompiled Game Assemblies

ULTRAKILL's game assemblies are external compiled dependencies.

When navigating or inspecting game code through decompilation:

* treat decompiled code as runtime reference material
* do not assume decompiled names or structure are identical to original source
* verify behavior before relying on implementation details
* do not modify decompiled game assemblies

If a solution depends on undocumented game behavior, clearly identify that dependency.

---

## 9. Existing Code and Project Conventions

Preserve the existing project structure unless there is a strong reason to change it.

Preserve useful existing:

* comments
* mappings
* constants
* configuration formats
* resource paths
* localization keys
* naming conventions
* compatibility workarounds

Do not remove code simply because it looks redundant without verifying whether it has a runtime purpose.

When cleaning up duplicated code, prefer small, explicit helper methods over overly generic frameworks.

Prefer readable and explicit code over clever abstractions.

---

## 10. Language and Comments

All source-code comments must be written in **English**.

All runtime log messages must be written in **English**.

Do not translate existing English code comments or log messages into Russian.

When communicating with the developer, explanations may be written in Russian.

---

## 11. Error Handling and Logging

Do not silently swallow exceptions.

When an exception is intentionally ignored, there must be a clear reason.

Use the project's existing logging conventions.

Do not add excessive logging to frequently executed code paths.

For new error handling, provide enough context to identify:

* what operation failed
* which resource or object was involved
* whether the failure is recoverable

Avoid logging sensitive or unnecessary information.

---

## 12. Configuration and User Data

Do not hard-code machine-specific paths.

Do not modify `.csproj.user` with repository-specific assumptions unless explicitly requested.

The local `ULTRAKILLPath` is provided through the user's local project configuration.

Do not commit machine-specific paths, credentials, API keys, or other local environment data.

---

## 13. Build and Validation

The primary build command is:

`dotnet build UltrakULL.sln --configuration Release`

The project must remain buildable using the existing solution and project files.

Choose validation that matches the changed files:

* After changing C# code, build the solution and run relevant tests when available.
* For documentation-only changes, review the text and links; do not run an unrelated build.
* For configuration or resource changes, validate the format and check affected keys, paths, or references with available project tools or tests.
* A successful build does not by itself verify runtime behavior. Only claim runtime verification if the relevant behavior was exercised.

After modifying C# code:

1. Build the project.
2. Check compiler errors and warnings.
3. Fix errors caused by the change.
4. Review the resulting diff.
5. Confirm that unrelated files were not modified.

Do not claim that a change works if the project was not successfully built or the relevant behavior was not verified.

Do not automatically launch ULTRAKILL unless explicitly requested.

---

## 14. Git and Changes

Keep changes focused on the requested task.

Do not reset, revert, discard, or overwrite the user's existing changes unless explicitly instructed.

Before finishing or creating a commit, inspect the actual diff and working-tree status. Distinguish task changes from pre-existing user changes.

Do not create a commit automatically unless explicitly requested.

---

# Commit Messages (CRITICAL)

Always generate commit messages in **English only**.

Do NOT generate commits in Russian or any other language.

## Format

Use Conventional Commits:

`<type>(<scope>): <subject>`

## Types

* `feat` - new feature
* `fix` - bug fix
* `refactor` - code refactoring
* `docs` - documentation changes
* `chore` - build, dependencies, tooling
* `test` - tests
* `perf` - performance optimization
* `style` - code style changes
* `ci` - CI/CD configuration

## Scope

Use the most specific affected module.

Known scopes include:

* `LanguageManager` - localization system
* `AudioSwapper` - audio dubbing
* `TexturePatcher` - texture replacement
* `Main` - core initialization
* `HarmonyPatches` - Harmony patches
* `SubtitledSources` - subtitle and audio sources

If the change affects a module that is not listed above, use the actual affected module name rather than forcing an unrelated scope.

Avoid using a broad scope when a more specific one is available.

## Subject

* Maximum 50 characters
* Start with a lowercase letter
* Do not end with a period
* Be specific and concise
* Describe the actual change
* Use imperative mood where practical

## Good Examples

* `feat(LanguageManager): add missing translation sync`
* `fix(AudioSwapper): prevent null audio source`
* `refactor(TexturePatcher): cache texture lookups`
* `docs(Main): update localization setup guide`
* `chore(deps): update Newtonsoft.Json`

## Bad Examples

* `Добавлена функция локализации` — Russian is forbidden
* `updated stuff` — too vague
* `Fix bug` — incorrect Conventional Commit style
* `feat: modified LanguageManager and AudioSwapper` — missing scope and too broad

## Commit Message Analysis

When generating a commit message:

1. Inspect the actual Git diff.
2. Identify the files and code that changed.
3. Determine the primary purpose of the change.
4. Select the most specific applicable type.
5. Select the most specific affected scope.
6. Write a concise technical subject.
7. Do not guess the purpose of a change from filenames alone.

If a change contains multiple unrelated modifications, do not hide them behind a vague commit message. Ask whether the changes should be split into separate commits when appropriate.
