// Declaración mínima de lo que usan las pruebas y la configuración (evita depender de @types/node, SPEC-WEB §8.3).
declare const process: { env: Record<string, string | undefined>; cwd(): string };

declare module "node:fs" {
  export function readFileSync(path: string, encoding: "utf8"): string;
  export function readdirSync(path: string, options: { recursive: true }): string[];
  export function mkdirSync(path: string, options: { recursive: true }): void;
  export function rmSync(path: string, options: { recursive: true; force: true }): void;
}
