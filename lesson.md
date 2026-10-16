# Lessons and decisions

I keep this short record of decisions, not a claim that every path is validated.

- **Start with the real problem.** I wanted my small display without leaving TRCC and a large monitoring stack running. Media and useful activity screens come first.
- **Separate the product from the initiative.** I am Umberto Giacobbi, the author. Vibe Panel is the codename; VibeWare is my initiative.
- **Keep providers generic.** Windows media sessions supply available metadata. I do not need a Spotify account, SDK or API integration.
- **Keep the canvas independent.** .NET owns native providers and USB; Python/Skia owns themes. OpenSilver/Photino is the configuration UI. JSON Lines connects the parts without a server.
- **Packs are not providers.** A media-only pack must work without Codex. Each theme declares its category; installing its files does not enable hooks.
- **Cache expensive artwork work.** Decode and sample a cover when it changes, reuse blur/background layers, and request modest updates. A one-second interval alone does not prove low CPU use.
- **Do not invent information.** Missing temperature, quality, volume, chat title or hook activity remains unavailable.
- **Separate evidence.** A successful build is not a rendered frame; a rendered frame is not a successful physical transfer.
- **Keep visual review bounded.** The first authorized review generated forty demo frames in one run. It checks the default wide layouts, not live data, every font/geometry or physical refresh rate.
- **Owner approval is explicit.** I approve each test or device trial and any new branch/workspace before it runs. Checkpoint commits stay on the current branch.
