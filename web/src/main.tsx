import "@fontsource/inter/latin-400.css";
import "@fontsource/inter/latin-500.css";
import "@fontsource/inter/latin-600.css";
import "./tokens/tokens.css";
import "./styles/base.css";
import "./styles/components.css";
import "./styles/layout.css";

import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AssistantView } from "./views/AssistantView";

const container = document.getElementById("root");
if (!container) throw new Error("Falta el contenedor #root");

createRoot(container).render(
  <StrictMode>
    <AssistantView />
  </StrictMode>,
);
