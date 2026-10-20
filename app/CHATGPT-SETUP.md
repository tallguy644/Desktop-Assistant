# Connect Assistant to ChatGPT without an OpenAI API key

## First connection

1. Open Assistant. The permanent **ASSISTANT / CONVERSATION** area is beside the reactor. Expand **ACCOUNT / MODEL** if its connection controls are collapsed. **CHATGPT / ASK ASSISTANT** on the left focuses the conversation input.
2. Click **Continue with ChatGPT**. Assistant opens the official OpenAI sign-in page in your browser. Sign in there; do not enter a password, token, or API key into Assistant or this support chat.
3. Choose the account/workspace you want to use, then approve Assistant and permission to use your ChatGPT plan. Availability depends on your account, workspace permissions, and plan-sharing allowance. OpenAI's current example uses a Plus or Pro account; not every account or workspace is guaranteed access.
4. Return to Assistant. It verifies the account and loads the model choices returned for that account. Choose a model, type **Hello**, then click **SEND** or press **Enter**. **Shift+Enter** adds a line.
5. After a complete answer arrives, Assistant saves it in this app's chat history and reads it using your saved voice and speed settings. Uncheck **Read aloud** for text only. Account/model controls collapse after a successful connection to leave more room for answers; expand them whenever needed. Saved accounts reconnect at startup.

The initial browser sign-in expires after five minutes. If you need more time, press **Continue with ChatGPT** again. Successful sign-in uses account authorization tokens, not an OpenAI API key. Assistant does not read or reuse Codex's existing credential files or browser cookies.

## Ask by voice

Choose **VOICE / F9** or press **F9 while Assistant is focused**. Speak one question. The first clearly recognized utterance is sent to ChatGPT, and this question microphone closes. There is a 15-second listening limit. **CANCEL** or **Esc** stops question listening; the conversation area remains visible. If recognition fails, type your question instead. Question dictation needs an installed English Windows desktop speech recognizer and microphone permission.

App launches still use **LISTEN** with “Assistant, open Notepad,” or **F8 / PUSH TO TALK** with “open Notepad.” F9 sends a question to the conversation service; it does not execute a launcher command. Ordinary launch listening pauses for F9 and resumes afterward if it was previously enabled. Recognition is ignored while the assistant speaks and briefly afterward to reduce feedback.

## Controls and history

- **REFRESH** inside **ACCOUNT / MODEL** checks the current account's model list again. A listed model still needs a successful response to confirm usable access.
- **CANCEL** stops sign-in or an active request. Interrupted replies remain visibly marked as incomplete and are not committed to conversation history. The typed question is restored for retry.
- **STOP VOICE** stops current speech without erasing the answer on screen.
- **NEW CHAT** clears this app's conversation. Assistant retains up to 40 recent messages within a 32,000-character local context budget, dropping older exchanges as necessary.
- **DISCONNECT** removes this PC's saved ChatGPT credentials and clears its local conversation history. You can also revoke access through your ChatGPT account's app/agent access settings. Closing Assistant retains the encrypted sign-in for next time.

Answers normally aim to stay brief. Very long replies are shown in full on screen, up to the local response limit; speech reads an excerpt and says the rest is on screen. Your saved Azure voice service remains separate from ChatGPT plan usage.

## Privacy and permissions

Assistant sends your submitted question and the recent conversation context to OpenAI. It does not send hardware readings, installed app paths, or microphone recordings through this connection. Dictation runs through Windows' desktop recognition engine. Windows speech and microphone privacy settings still apply.

The ChatGPT connection provides text answers only in this build. It has no program-launch, shell, file, or hardware-control tools. Existing saved launcher mappings remain the only launch mechanism. This connection does not import your existing ChatGPT conversations or memories; Assistant keeps its own history.

Account credentials and conversation history are encrypted on disk using Windows current-user protection (DPAPI), in `%LOCALAPPDATA%\JarvisDesk\chatgpt-account.protected` and `chatgpt-history.protected`. The installation's opaque host identifier is stored in `chatgpt-host.txt`. Programs running as your Windows user can access that user's protected data. Credentials are not included in copied sensor diagnostics or the download ZIP.

## If connection or replies fail

- **Plan sharing unavailable / permission not granted:** reconnect and approve the requested plan permission, or check account/workspace access with OpenAI or your workspace administrator.
- **Sign-in expired / rejected:** click **Continue with ChatGPT**. Assistant normally refreshes eligible saved tokens automatically before expiry.
- **No models:** check plan-sharing permissions, then expand **ACCOUNT / MODEL** and click **REFRESH**. The app does not invent available models or silently switch to API-key billing.
- **Usage limit reached:** check ChatGPT plan usage and try later. An HTTP connection starting successfully does not mean an answer completed.
- **Browser says it cannot reach the callback:** return to Assistant, cancel the old attempt, and start a fresh one. Keep Assistant running during sign-in. The callback listens only on `127.0.0.1`; no firewall or administrator setup is performed.

This uses OpenAI's current preview for locally hosted/open-source clients. See the [official registration and sign-in guide](https://developers.openai.com/siwc/token-sharing-open-source/sign-in), [model and inference guide](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference), and [preview limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations).

## Build and check

Run **Build.cmd** to rebuild from the included C# source and bundled calendar libraries. No separate runtime download, Node.js, or OpenAI API key is required. Keep the entire folder together, including DLLs and **Skynet.exe.config**; see **THIRD-PARTY-NOTICES.md**. Run the existing **SmokeTest.cmd** for launcher/telemetry checks. To run synthetic sign-in/security/streaming tests, use Windows PowerShell 5.1: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-Chat.ps1`. These tests use fake tokens and an isolated loopback fixture, not your real ChatGPT account.
