# Prompt Enhancer for Windows

Turn an English draft into one editable prompt for ChatGPT. Press **Ctrl+Alt+E** in the desktop draft field, review the detected task, type the model name you use in ChatGPT, and choose **Enhance**. The pop-up lists what it added and lets you fill missing details. You decide when to send the result.

## Install and try it

1. Download **PromptEnhancer-win-x64.zip** and its SHA-256 file from [GitHub Releases](https://github.com/Vansh97-expert/prompt-enhancer/releases). This is a self-contained Windows x64 build. Compare the checksum with `Get-FileHash .\PromptEnhancer-win-x64.zip`, then extract the entire zip to one folder.
2. Open **PromptEnhancer.exe**. Paste a draft to try it immediately. **Hide** clears the session and keeps the global shortcut available. Closing the window exits the app. Change a conflicting shortcut in **Settings**.
3. Try **“add a delete button for projects”**. Choose the coding task, enter your target model, and answer **“only the owner”**. The preview should ask for an owner check during deletion and a test that a non-owner cannot delete. Both the added instruction and the verification step are visible before insertion.
4. In a supported ChatGPT draft field, use **Insert**. If the draft changed, the chat lost its identity, or the accessibility interface cannot write the field, use **Copy**. Review the chat before pasting if a write could not be confirmed. The enhancer never presses Send.

The preview build is unsigned. Computers that require enterprise-signed software may block it. On a managed machine, use your organization's approved software distribution or signing process.

## How rewrites work

Coding and defensive review, comparison, writing, extraction, classification, planning, math, summarization, research questions, translation, ideation, tutoring, media interpretation, evaluation, and operational requests each have a task-specific rule and an alternative technique. Unknown tasks have a general fallback. Informal English and common spelling variants are supported. The original requested language, identifiers, exclusions, and secondary deliverables stay in the draft.

Edit the suggested task or technique if the app misunderstood. The **target model** is the model that will receive the finished prompt. An unfamiliar name is accepted exactly as typed and gets a portable text profile. Profiles describe prompt wording only. Tools, native schema enforcement, and access to a referenced attachment depend on the target chat. The text profiles were checked against [OpenAI’s ChatGPT model guide](https://help.openai.com/en/articles/20001354-gpt-6-and-other-models-in-chatgpt).

The rules use the task matrix and transformation rules in revision 2 of *Prompt Engineering Knowledge Base for a Prompt Enhancer*. They implement direct instructions, audience briefs, source-grounded prompts, output structure, and task-specific checks. Multi-call workflows, offline optimizers, live research, and tool execution are reference material for future work.

Hard conflicts recognized by the rules produce a question. A missing ordinary fact becomes a labeled blank. For pasted material, start a new line with **Source:** or use a fenced block. Review formatting, source boundaries, and sensitive information before insertion. Pattern matching and secret detection have limits, especially with unusual prose or credentials.

## Local AI setup (optional)

The built-in rules need no account, model download, API key, or network connection. To add on-device model assistance:

1. Install [Ollama for Windows](https://docs.ollama.com/windows) in its default per-user location. Use a current release with cloud-disable support (minimum accepted version 0.15). Download a local instruction model that fits your computer, for example `ollama pull qwen2.5:0.5b`.
2. Run `ollama list`. In **Settings**, select local AI and enter the exact installed **name:tag**. This is independent of the target model you type for ChatGPT.
3. During enhancement the app starts a short-lived local-only runner, verifies the loopback listener belongs to that process, and disables proxies, redirects, and Ollama cloud features. The model receives the redacted draft and selects compatible editing guidance from a constrained list. The same local compiler preserves the original instructions and renders the review. **Cancel generation** stops the operation. On failure, choose **Use rules instead**.

Installations without the runner use rules immediately. Production requests never download models. See [Ollama’s local-only configuration](https://docs.ollama.com/faq#how-do-i-disable-ollamas-cloud-features).

## Privacy and storage

- Only pressing the shortcut initiates a draft capture. The app checks the Windows package and the focused composer, and reads draft text only. Attachments, previous messages, and browser tabs stay outside its input.
- Detected private keys, tokens, secret assignments, environment values, and URL credentials are redacted before either engine and checked again before copying or inserting an edited preview. Redaction reports use category names. Review personal details manually.
- Settings are saved atomically at `%LOCALAPPDATA%\PromptEnhancer\settings.json`. See [settings.example.json](settings.example.json). The app has no accounts, analytics, prompt logs, or history. **Copy** deliberately puts the reviewed text on the Windows clipboard.
- The pop-up treats labeled pasted source material as data. The local model cannot issue commands, run tools, or supply arbitrary instructions for insertion.

## Build, test, and package

Use Windows x64, PowerShell, and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) version pinned in [global.json](global.json). From the repository root:

~~~powershell
dotnet restore PromptEnhancer.slnx --configfile NuGet.Config
dotnet build PromptEnhancer.slnx -c Release --no-restore
dotnet run --project tests/PromptEnhancer.Checks -c Release --no-build
dotnet run --project src/PromptEnhancer.Windows -c Release --no-build
./scripts/package.ps1
~~~

The package and checksum are written under `artifacts/`. The checks exercise two phrasings per task and every supported technique, privacy fixtures, constraints, clarification, fallback, draft guards, and keyboard/automation access. The benchmark runs 160 warm English rewrites and prints p95 latency, operating system, and logical processor count. All inputs are synthetic.

To repeat the real-model test, run the **Windows checks** workflow manually with **local_model** enabled, or run `./scripts/check-local-model.ps1 -Runner 'C:\path\to\ollama.exe'` after building. This explicitly downloads a small test model to the ignored workspace cache. The default suite needs no model or provider credentials.

## Release verification

For each supported ChatGPT desktop version, record the package version, Windows build, and results of capture, insertion, edited-draft refusal, chat switching, and manual paste/copy. Run a keyboard-only review and a screen-reader pass through model entry, clarification, redaction, and preview. Confirm the extracted zip starts on a clean compatible machine. The automated UI workflow uses synthetic input. A successful automated run does not certify a specific ChatGPT build or screen reader.

Cloud enhancement, other assistants, and additional launch languages are deferred. Source is distributed under the [MIT license](LICENSE).

