/**
 * Render seguro de la respuesta (SPEC-WEB §4.2): solo se produce estructura (párrafos y listas) a partir
 * de texto plano; nunca HTML. El contenido de cada línea se entrega a React como texto.
 */
export type AnswerBlock =
  | { kind: "paragraph"; lines: string[] }
  | { kind: "bullets"; items: string[] }
  | { kind: "numbered"; items: string[] };

const BULLET = /^\s{0,3}(?:[-*•])\s+(.*)$/u;
const NUMBERED = /^\s{0,3}\d{1,3}[.)]\s+(.*)$/u;

export function parseAnswer(text: string): AnswerBlock[] {
  const blocks: AnswerBlock[] = [];
  let open = false;

  for (const line of text.replace(/\r\n?/g, "\n").split("\n")) {
    if (line.trim().length === 0) {
      open = false;
      continue;
    }
    const last = open ? blocks[blocks.length - 1] : undefined;
    const bullet = BULLET.exec(line);
    const numbered = bullet ? null : NUMBERED.exec(line);

    if (bullet) {
      const item = bullet[1] ?? "";
      if (last?.kind === "bullets") last.items.push(item);
      else blocks.push({ kind: "bullets", items: [item] });
    } else if (numbered) {
      const item = numbered[1] ?? "";
      if (last?.kind === "numbered") last.items.push(item);
      else blocks.push({ kind: "numbered", items: [item] });
    } else if (last?.kind === "paragraph") {
      last.lines.push(line);
    } else {
      blocks.push({ kind: "paragraph", lines: [line] });
    }
    open = true;
  }
  return blocks;
}
