# Manifiesto de transcripciones

Generado por `scripts/sanitizar-logs` (determinista). El SHA-256 es el del archivo ORIGINAL sin sanitizar.
Tipos de reemplazo: correo → `[EMAIL]`; google-AIza, google-AQ, sk, github, aws, clave-privada, asignacion-llave, cabecera-autorizacion, x-goog-api-key → `[SECRETO]`; cuenta → `[CUENTA]`.

| Archivo | Id de sesión | Fase | Rol / agente | Descripción | Bytes (original) | SHA-256 (original) | Reemplazos por tipo |
|---|---|---|---|---|---|---|---|
| `2026-11-25_F0-F2_orquestador_db521dcb.jsonl` | db521dcb-9498-4475-9f59-53f8c286052b | F0-F2 | orquestador | sesión del orquestador (F0–F2; creció durante F2) | 9819375 | `318c2e94f530656051e997e15878f80ef88aff5e02e02db67ae962af4806b723` | cuenta=322, asignacion-llave=9, google-AQ=3, correo=6 |
| `2026-11-25_F2_arnes_a5fd480b.jsonl` | a5fd480ba9a52ed56 | F2 | arnes | T-00 Arnés: SDK .NET en Docker | 178430 | `c81901451037093ee9d38de88ace7bf2b1fb38310e4fef92cc1ced3dbc38786d` | cuenta=1 |
| `2026-11-25_F2_arnes_a6607938.jsonl` | a66079388d6f8b09e | F2 | arnes | T-10.1 Arnés: citas permitidas por SPEC | 612184 | `30326d9ec53eb8beceb3837c49a5e28474a99c3bfcdd04ba122dbf2ed5f959f3` | cuenta=1 |
| `2026-11-25_F2_arnes_a7a9befb.jsonl` | a7a9befb44d05e91d | F2 | arnes | T-07b Arnés: contrato y smoke contenedor | 524685 | `9645fca31f3c2642c616f984ab6bf68c778ed88e5ab8ae8cdef098d74b537517` | cuenta=1 |
| `2026-11-25_F2_arnes_a8701e68.jsonl` | a8701e68d707dd2ff | F2 | arnes | T-04 Arnés: CI base, Docker, dataset | 1129107 | `6bf9c987df85ca29e58dfb0b4aff9464d6b7f512430cd8f9d4e00c598d2be891` | cuenta=1, asignacion-llave=6 |
| `2026-11-25_F2_arnes_acb909a8.jsonl` | acb909a87cf1ae1a2 | F2 | arnes | T-10 Arnés: evals reales con Gemini | 1463293 | `bf70f37e70f9a1d5d166c0a7e7990df418841eac190db2e12bbc28790a092ca0` | cuenta=2, asignacion-llave=3 |
| `2026-11-25_F2_arnes_ad8764d5.jsonl` | ad8764d5c2b199e7f | F2 | arnes | T-07a Arnés: runner [ret] y mutantes | 1342675 | `4f1f380f24af9290694e1bd8cda32d8d1f028ddb9bb03b75c8d830b030be6666` | cuenta=1, cabecera-autorizacion=1 |
| `2026-11-25_F2_candidato-paralela_bb78739b.jsonl` | bb78739b-f7d1-4791-8dcf-90d336a30ccb | F2 | candidato-paralela | sesión paralela del candidato (no es un agente de la fábrica) | 529001 | `6882898b6d3868475613648c67b17a6713ea4d4dab6643ad171f77016ec5a096` | cuenta=17, correo=3 |
| `2026-11-25_F2_core_a0dfeec5.jsonl` | a0dfeec518ce0e187 | F2 | core | T-05d Core: herramientas y despachador | 1074797 | `af901543c4dd1fee8884fcf99b2b1568901f74a8081ef4654588330b3a721d99` | cuenta=1, asignacion-llave=1 |
| `2026-11-25_F2_core_a2fed204.jsonl` | a2fed204ea5f99d98 | F2 | core | T-05a Core: fuente de datos y roster | 563759 | `a93017b1afe3dd96be1cfeb0a9cafa3a724b9596b01bcb7f80bc078cbdda3050` | cuenta=1 |
| `2026-11-25_F2_core_a5617119.jsonl` | a5617119a4404432e | F2 | core | T-08.1 Core: RES-11.a literal y 403 L4 | 290852 | `7a1e2bd49425a732a4f0bb87a29a83af25ceef411241c38643370cdcd9203ffc` | cuenta=1, correo=1 |
| `2026-11-25_F2_core_a6de7b33.jsonl` | a6de7b33155f48d54 | F2 | core | T-05c Core: guardias L2a, L2b, L5 | 801306 | `2d4cf95f4a0fdc72f687b9a18bab11a9c3595e03dd8cc3c9379d56c208b59561` | cuenta=1 |
| `2026-11-25_F2_core_a7e274bd.jsonl` | a7e274bd3ef883009 | F2 | core | T-01 Core: esqueleto .NET | 613887 | `ae6f9c2680ec61cab8d809cb64a3eb0961dfc9bcf47e078c7cf8fdb21eb92779` | cuenta=1 |
| `2026-11-25_F2_core_a8e1eeb2.jsonl` | a8e1eeb2a9539f077 | F2 | core | T-08 Core: integración E2E | 1837135 | `46aeb51aa2b59aaf4d3c2e715b44ba73335959e8cce58a60ebde5222b8cb5bf8` | cuenta=1, asignacion-llave=1 |
| `2026-11-25_F2_core_a928e85f.jsonl` | a928e85fb6feac82b | F2 | core | T-02 Core: dominio y política | 828009 | `572d97ff678e2117baef27864b05422fc9df2c97132d3cd32daaa61310e184b0` | cuenta=1 |
| `2026-11-25_F2_core_ae763f6e.jsonl` | ae763f6e3ec3d6bea | F2 | core | T-05e Core: auditoría por petición | 827823 | `64db97b66e59b15d623eed88787018b8b724ef66d48d06da999c7abd87003a65` | cuenta=1, asignacion-llave=1 |
| `2026-11-25_F2_core_afd1bf0b.jsonl` | afd1bf0b19865f147 | F2 | core | T-05b Core: cuerpo, identidad L1, API | 965760 | `5942b252d01f14acc311cbe8df161bf2bf304aba734d09d2491879b007d06621` | cuenta=1, correo=1 |
| `2026-11-25_F2_ia_a51d0af8.jsonl` | a51d0af8228c959f2 | F2 | ia | T-03 IA: chunking, cuarentena, BM25 | 470643 | `1df3b255a0b1d15b64024658079352507a907398d743be3ca5a927d2660b2d56` | cuenta=1 |
| `2026-11-25_F2_ia_a6d6f154.jsonl` | a6d6f15408cff9e44 | F2 | ia | T-06.2 IA: thought_signature Gemini | 314053 | `ce3be675d9f819037b6f0ecbf808edfd2e42ffb015df9673c675e862e1208fea` | cuenta=1, google-AIza=3 |
| `2026-11-25_F2_ia_a89710bc.jsonl` | a89710bc98eb77488 | F2 | ia | T-06.3 IA: cifras en redacción y 403 bm25 | 431940 | `0fad4934b136ce64f76584656e3f2f8aeb612bb6ca028622c754ebabc6b5b656` | cuenta=1 |
| `2026-11-25_F2_ia_a89a28f5.jsonl` | a89a28f59bf3880e5 | F2 | ia | T-09 IA: vector, hybrid, mejora bm25 | 622165 | `6b226d23fa576d81773809a2f529993564fe69364fd133ebdfccd1a1f43c0028` | cuenta=1 |
| `2026-11-25_F2_ia_aec50cab.jsonl` | aec50caba654311c5 | F2 | ia | T-06 IA: LLM, bucle, citas | 1050618 | `95ef2b019af70c123b84b77641e691dd5d5589e557d3ce7d7eae925d7e76fa19` | cuenta=1, asignacion-llave=1 |
| `2026-11-25_F2_ia_af6a6bed.jsonl` | af6a6bed6e1ee7d11 | F2 | ia | T-06.1 IA: catálogo vacío y tokens L4 | 303263 | `270e1ab1e32f18a9af26bb695fda0f40e763ac328ff9e14db1d716a37acb6da0` | cuenta=1 |
| `2026-11-25_F2_web-fetch_a9bed96f.jsonl` | a9bed96fb52b8e105 | F2 | web-fetch | Fetch Gemini pricing and limits | 157394 | `1bdd7563b89fd85e52cf23e9a369233089a5baf91218ce41fb8cb25c5c811e02` | cuenta=1 |

Totales por tipo: cuenta=362, asignacion-llave=22, cabecera-autorizacion=1, x-goog-api-key=0, google-AIza=3, google-AQ=3, sk=0, github=0, aws=0, clave-privada=0, correo=11.
