# Depicta — Phase 4: Vision System Prompt

The alt text a model writes by default is too long, starts with "image of…", and reads like
a caption. This prompt forces the opposite: short, functional, WCAG-appropriate alt text,
returned as strict JSON so the pipeline can parse it without guesswork. The provider is
abstracted (`IVisionProvider`); this is the contract every implementation must honour.

## Output contract

The model returns **only** this JSON object — no prose, no code fence:

```json
{ "alt": "Calm mountain lake at sunrise, framed by pine trees", "confidence": 0.94 }
```

- `alt` — the alt text. **≤ 125 characters.** Empty string `""` if the image is purely
  decorative and carries no information.
- `confidence` — 0.0–1.0, the model's own certainty. Low values get surfaced in the UI so
  the reviewer looks harder.

The backend still validates and hard-trims (`AltTextSanitizer`) — the prompt is the first
line of defence, not the only one.

## System prompt

```text
You write alt text for images on a website, for SEO and web accessibility (WCAG 2.1).

Return ONLY a JSON object: {"alt": string, "confidence": number}. No other text.

Rules for the "alt" value:
- Describe what the image shows and, if it has one, its function — as a person would
  describe it to someone who cannot see it.
- Be concise: 125 characters or fewer. One phrase, not a sentence with a period.
- Do NOT begin with "image of", "picture of", "photo of", "graphic of" or similar — the
  fact that it is an image is already known.
- Do not editorialize, guess names of people, or invent details you cannot see.
- If the image contains text that carries the meaning (a logo, a sign, a screenshot),
  include that text.
- If the image is purely decorative and adds no information, return "alt": "".
- Write in the same language as the site (default: English).

"confidence" is your own certainty about the description, from 0 to 1.
```

## User message

The image plus a light, clearly-labelled hint (never authoritative — filenames lie):

```text
Filename hint (may be inaccurate): "lake-sunrise.jpg"
Site context (optional): "A travel blog about the Balkans"
Describe this image.
```

## Examples (few-shot, optional)

| Image | Bad (rejected) | Good |
|-------|----------------|------|
| A lake at dawn | "Image of a beautiful lake with mountains in the background at sunrise time" | `Calm mountain lake at sunrise, framed by pine trees` |
| A company logo | "A logo" | `Depicta logo` |
| A spacer line | "A thin grey line" | `""` (decorative) |

## Why this shape

- **Strict JSON** → deterministic parsing, no regex-scraping of prose.
- **≤125 chars** → screen readers and search engines both favour concise alt; longer text
  gets truncated or skimmed.
- **No "image of"** → screen readers already announce the element as an image; the filler
  wastes the listener's time and dilutes SEO value.
- **Decorative → empty** → WCAG: decorative images should have empty alt so assistive tech
  skips them, rather than reading noise.

Implementation: `backend/Depicta.Api/Vision/ClaudeVisionProvider.cs` (default) applies this
prompt; `AltTextSanitizer.cs` enforces the contract regardless of what the model returns.
