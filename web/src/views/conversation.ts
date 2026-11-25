import type { PreguntarOutcome } from "../services/apiClient";
import type { Usuario } from "../models/contract";

/**
 * Estado de la conversación (SPEC-WEB §8.2: estado local + un reducer). Historial solo en memoria (W-R4);
 * cada turno se envía de forma independiente y el historial nunca se reenvía (W-R5).
 */
export type ResolvedOutcome = Exclude<PreguntarOutcome, { kind: "cancelled" }>;

export type TurnStatus = { phase: "pending" } | { phase: "done"; outcome: ResolvedOutcome };

export interface Turn {
  id: number;
  pregunta: string;
  usuario: Usuario;
  status: TurnStatus;
}

export interface ConversationState {
  turns: Turn[];
  nextId: number;
}

export type ConversationAction =
  | { type: "send"; pregunta: string; usuario: Usuario }
  | { type: "resolved"; id: number; outcome: ResolvedOutcome }
  | { type: "retry"; id: number }
  | { type: "remove"; id: number };

export const initialConversation: ConversationState = { turns: [], nextId: 1 };

export function conversationReducer(state: ConversationState, action: ConversationAction): ConversationState {
  switch (action.type) {
    case "send":
      return {
        nextId: state.nextId + 1,
        turns: [
          ...state.turns,
          { id: state.nextId, pregunta: action.pregunta, usuario: action.usuario, status: { phase: "pending" } },
        ],
      };
    case "resolved":
      return {
        ...state,
        turns: state.turns.map((t) => (t.id === action.id ? { ...t, status: { phase: "done", outcome: action.outcome } } : t)),
      };
    case "retry":
      return {
        ...state,
        turns: state.turns.map((t) => (t.id === action.id ? { ...t, status: { phase: "pending" } } : t)),
      };
    case "remove":
      return { ...state, turns: state.turns.filter((t) => t.id !== action.id) };
  }
}
