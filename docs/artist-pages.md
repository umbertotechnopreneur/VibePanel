# Artist pages

Music themes alternate playback and artist information every 12 seconds, with a 600 ms fade. The artist page is skipped when no matching biography is available. The studio links to the biography, photo license and any sales reference.

The Windows engine sends the current artist and album names to the public [MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API). It requires one exact artist-name match, then follows that artist's Wikidata link to the English Wikipedia article. It does not guess between artists with the same name. No account or API key is needed. Audio stays on the PC.

[Wikipedia](https://www.mediawiki.org/wiki/Extension:TextExtracts) supplies a short attributed introduction. [Wikimedia Commons](https://www.mediawiki.org/wiki/API:Imageinfo) supplies a photo only when the author and a supported public-domain or Creative Commons license are present. The resized photo keeps its credit; the studio links to its source and license. These sources can be incomplete or disagree, so the page does not combine their formation dates.

Album sales are optional. [Wikidata P2664](https://www.wikidata.org/wiki/Property:P2664) must have a worldwide scope, date, unitless count and HTTPS source reference. Regional, format-specific, undated and unsourced statements are omitted. These are reported units sold, not streams, certified units or a live total. There is no direct Spotify integration or fabricated popularity figure.

One background lookup runs at a time. MusicBrainz calls are spaced at least 1.1 seconds apart. Requests have a ten-second deadline and a 2 MB response cap. The in-memory cache holds at most 16 artist/album pairs: successful entries expire after 24 hours and unsuccessful entries after one hour. Photos are limited to 512 px and 400 KB. Playback does not wait for a lookup. A missing photo or sales record does not remove an available biography.

The online response is data, not executable code. No additional library was imported for this feature. Future external binaries still require targeted Microsoft Defender scans of both their archives and extracted dependencies before use.

<!-- VBWR B — Vibe Panel. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E -->
