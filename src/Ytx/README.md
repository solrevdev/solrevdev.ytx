# `ytx` — YouTube → JSON (title, description, transcript)

`ytx` prints a single JSON object for a YouTube URL:

```json
{
  "url": "https://youtube.com/…",
  "title": "Video title",
  "description": "Video description…",
  "transcriptRaw": "Full transcript as one string…",
  "transcript": "- [00:03](https://www.youtube.com/watch?v=...&t=3s) First line\n- [00:07](...) Next line"
}
```

- Prefers English captions; falls back to any available.
- Markdown transcript with timestamped links.
- Works on macOS, Windows, Linux (.NET 8/9).

## Install

```bash
dotnet tool install -g solrevdev.ytx
# upgrades:
dotnet tool update -g solrevdev.ytx
```

## Usage

```bash
ytx "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
# or:
echo '{"url":"https://www.youtube.com/watch?v=dQw4w9WgXcQ"}' | ytx
```

## JSON fields

- `url` — input URL.
- `title` — video title.
- `description` — full description.
- `transcriptRaw` — all caption text, normalized.
- `transcript` — Markdown list with `[HH:MM(:SS)](yt?t=Ns) text`.

If captions are unavailable, `transcriptRaw` is empty and `transcript` states why.

## Exit codes

- `0` success
- `1` unexpected error
- `2` usage error (missing/invalid URL)

## License

MIT