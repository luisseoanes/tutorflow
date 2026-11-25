# scripts/mutantes

Prueba de eficacia del arnés (FABRICA.md §5.3). `catalogo.json` lista M1..M16 con la tarea que introduce el código, el parche (buscar/reemplazar exacto) y los criterios que deben matarlo.

```bash
node scripts/mutantes/mutantes.js                 # mutantes de tareas integradas; PENDIENTE no cuenta como aprobado
node scripts/mutantes/mutantes.js --completo      # cierre de F2: PENDIENTE también falla
node scripts/mutantes/mutantes.js --solo M1,M9    # subconjunto
node scripts/mutantes/mutantes.js --listar
# SDK en Docker (fábrica):
DOTNET_CMD="docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet" node scripts/mutantes/mutantes.js
```

- Exige árbol limpio. Aplica un mutante en sitio, ejecuta `dotnet test TutorFlow.sln --filter "Criterio=A|Criterio=B"` y restaura siempre (finally y señales); comprueba `git status` limpio al final de cada uno.
- Estados: `MUERTO` (alguna prueba falla), `SOBREVIVE` (pasan), `PENDIENTE` (tarea no integrada, según los `merge: T-xx` como en trazabilidad), `FALLA` (tarea integrada y parche inaplicable = catálogo desactualizado; el mutante no compila; o el filtro no ejecutó ninguna prueba).
- Salida distinta de 0 si algún mutante `SOBREVIVE` o `FALLA` (y, con `--completo`, `PENDIENTE`).
- Solo en esta corrida se pasa `-p:TreatWarningsAsErrors=false`: algunos mutantes dejan código inalcanzable (CS0162) que `Directory.Build.props` trata como error. Así compila y deben matarlo las pruebas. El build normal sigue con `-warnaserror`.
- Los mutantes de tareas futuras tienen `parches: []`: se rellenan al integrarse la tarea (T-07b los completa antes de la exigencia total). Con la tarea integrada y sin parche, el resultado es `FALLA`.
- Los criterios de un mutante que aún no tienen prueba (p. ej. AUTH-20.a de T-05d) simplemente no seleccionan nada en el filtro; si ninguno selecciona pruebas, el resultado es `FALLA`, no "sobrevive".
