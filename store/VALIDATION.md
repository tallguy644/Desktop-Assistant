# Release preparation checks — October 20, 2026

Passed on the development Windows x64 PC:

- Rebuilt both WPF desktop app and taskbar recovery helper from the staged source with the .NET Framework compiler.
- Rebuilt the standalone installer with a clean payload containing no historical screenshots, audio previews or user profile files.
- Microsoft MakeAppx 10.0.28000.2705 validated the manifest and successfully packed the unsigned development MSIX.
- App self-tests: whitelist launch validation, wake parsing, settings round-trip, synthetic temperature/GPU/fan fixtures, unavailable sensor handling and Fahrenheit conversion.
- Azure voice catalog tests: multilingual and HD parsing, filtering, safe voice IDs, SSML, cancellation, cache handling and zero-RPM fan transitions. Live catalog loading and standard/HD speech responses also passed on the development PC without publishing account details.
- Unified Setup and HUD checks: section navigation, saved preferences, canceled color previews, private-field cleanup, voice browser controls, fan hide/show transitions and independent reactor animation.
- Chat tests: authentication state/PKCE/nonce/JWT validation, encrypted synthetic credential storage, streaming completion/cancellation and invalid-response handling.
- Calendar tests: recurrence, dates/time zones, exclusions/cancellations, private-feed URL validation, cache isolation and encrypted synthetic data round-trip.

The chat/calendar protected-file tests initially encountered sandbox file-operation restrictions. They passed when run with the normal Windows user context. These tests do not contact real ChatGPT accounts or private calendars. Real telemetry read smoke testing is local and is not published in the test report.

Not yet verified for this release: signed MSIX installation/activation/update/uninstall, Windows App Certification Kit, packaged startup task, packaged privacy dialog, multiple target PCs, clean-PC dependency setup, new microphone testing, Store identity, provider distribution eligibility and Store certification. Existing desktop/taskbar feature testing does not substitute for packaged testing. Screenshots and final Store metadata remain publisher tasks described in SUBMISSION.md.
