#!/usr/bin/env bash
# gen-one.sh NAME "PROMPT BODY"
# codex imagegen2로 단일 PNG 생성. 코드 파일(.py/.svg/.html/.js)을 쓰면 실패로 보고 최대 3회 재시도.
set -u
NAME="$1"
BODY="$2"
ROOT="D:/workspace/Everything2Everthing/assets-gen"
DEST="$ROOT/$NAME.png"

for attempt in 1 2 3 4; do
  WORK="$(mktemp -d)"
  # 깨끗한 작업 폴더에서만 쓰게 하고, 결과는 out.png 한 장으로 강제
  PROMPT=$(cat <<PROMPT
You have an image generation tool available (imagegen / image_gen). Use it now.

TASK: Generate a single PNG image and save it in the current working directory as exactly "out.png".

IMAGE DESCRIPTION:
$BODY

ABSOLUTE RULES — violating any of these is a hard failure:
- You MUST call the image generation tool to produce the picture.
- DO NOT write, create, or edit ANY code file of ANY kind: no .py, .svg, .html, .js, .ts, .sh, .json, no Python, no PIL/Pillow, no matplotlib, no cairo, no SVG markup, no canvas, no ASCII art.
- DO NOT try to "draw" or "render" the image with code. If you are even slightly tempted to script it, STOP and use the image generation tool instead.
- The ONLY file that may exist when you finish is out.png produced by the image generation tool.
- After saving, reply with just: DONE
PROMPT
)
  echo "$PROMPT" | codex exec --skip-git-repo-check --sandbox workspace-write -C "$WORK" --ephemeral - >/dev/null 2>&1

  # 코드 파일을 썼는지 검사
  CODE=$(find "$WORK" -type f \( -name '*.py' -o -name '*.svg' -o -name '*.html' -o -name '*.js' -o -name '*.ts' -o -name '*.sh' \) 2>/dev/null | head -1)
  PNG=$(find "$WORK" -type f -name '*.png' 2>/dev/null | head -1)

  if [ -n "$CODE" ]; then
    echo "[$NAME] attempt $attempt: codex wrote CODE ($CODE) — reject, retry"
    rm -rf "$WORK"; continue
  fi
  if [ -z "$PNG" ]; then
    echo "[$NAME] attempt $attempt: no PNG produced — retry"
    rm -rf "$WORK"; continue
  fi
  # 너무 작은 PNG(코드로 만든 단색/플레이스홀더)도 거부
  SZ=$(stat -c%s "$PNG" 2>/dev/null || echo 0)
  if [ "$SZ" -lt 120000 ]; then
    echo "[$NAME] attempt $attempt: PNG too small ($SZ bytes) — likely synthetic, retry"
    rm -rf "$WORK"; continue
  fi
  cp "$PNG" "$DEST"
  echo "[$NAME] OK ($SZ bytes) -> $DEST"
  rm -rf "$WORK"
  exit 0
done
echo "[$NAME] FAILED after retries"
exit 1
