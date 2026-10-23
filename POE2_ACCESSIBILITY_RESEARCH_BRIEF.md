# Path of Exile 2 Accessibility Mod — Research & Agent Brief

> Estado: investigación inicial  
> Objetivo: construir una capa de accesibilidad **read-only y manual** para Path of Exile 2: lector de pantalla, radar sonoro, audio beacons, información contextual y navegación accesible de UI.
>
> Filosofía: **no auto-walk, no pathfinding que juegue por el usuario, no auto-target, no auto-loot, no auto-flask, no byte patching, no automatización de combate**. El jugador conserva el control; el mod traduce información visual a audio/TTS.

---

## 0. Repositorios que hay que estudiar primero

### Base principal
- GameHelper2  
  https://github.com/Gordin/GameHelper2
- Leer primero:
  - `README.md`
  - `CLAUDE.md`
  - `GameHelper/`
  - `GameOffsets/`
  - `Plugins/SamplePluginTemplate/`
  - `Plugins/Radar/`
  - `Plugins/HealthBars/`
  - `Plugins/PreloadAlert/`
  - `RemoteObjects/`
  - `RemoteObjects/Components/`

GameHelper2 es actualmente una aplicación externa Windows x64/.NET 10. Lee memoria del proceso de PoE2 y carga plugins dinámicos. No requiere inyección en el proceso para su modelo normal.

### Arquitectura alternativa / posible objetivo final
- CoreExile2  
  https://github.com/coussiraty/CoreExile2

Investigar especialmente:
- `PLUGIN_GUIDE.md`
- `SDK_DESIGN.md`
- `ExileBridge`
- Sample plugin
- Radar

Idea interesante: si `ExileBridge` expone toda la información necesaria, el plugin de accesibilidad podría depender de una API estable en vez de tocar offsets o internals de GameHelper2.

### Investigación independiente de memoria/terreno
- POE2Radar  
  https://github.com/NattKh/POE2Radar
- Original relacionado:
  https://github.com/Sikaka/POE2Radar

Investigar:
- `src/POE2Radar.Core`
- `Game/Poe2Offsets.cs`
- `Game/Poe2Live.cs`
- terrain/walkability
- entity enumeration
- landmarks
- `POE2Radar.Research`

**No copiar ni integrar las funciones de cheats/byte patches.** Solo nos interesa la investigación de estructuras, entidades, coordenadas, UI y terreno.

### Plugin útil para investigar UI
- StashUtility  
  https://github.com/AlexanderHel/StashUtility

Especialmente:
- UI Path Explorer
- Hovered Item Inspector
- entity/memory dumpers

Puede ahorrar muchísimo trabajo al investigar menús e inventario.

---

# 1. Arquitectura de referencia

## GameHelper2

Modelo aproximado:

```text
PathOfExileSteam.exe
       |
       | ReadProcessMemory
       v
GameHelper2
  |
  +-- GameProcess
  +-- StaticAddresses
  +-- RemoteObjects
  |    +-- AreaInstance
  |    +-- InGameState
  |    +-- Entity
  |         +-- Actor
  |         +-- Life
  |         +-- Positioned
  |         +-- Render
  |         +-- ...
  |
  +-- Plugin Manager
       |
       +-- AccessibilityPlugin.dll
```

GameHelper2 mantiene las estructuras nativas en:

```text
GameOffsets/
```

y sus wrappers/objetos de runtime en:

```text
GameHelper/RemoteObjects/
```

Los componentes de entidades reflejan el ECS/layout del juego.

---

# 2. Contrato de plugin que debemos comprobar

En GameHelper2, un plugin deriva de:

```csharp
PCore<TSettings>
```

y normalmente implementa:

```csharp
OnEnable(bool isGameOpened)
OnDisable()
DrawSettings()
DrawUI()
SaveSettings()
```

Reglas conocidas que los agentes deben verificar en el código actual:

1. El assembly debe contener exactamente una clase de plugin aceptable por `PManager`.
2. El plugin debe ser `sealed`.
3. El DLL debe coincidir con el nombre/directorio esperado.
4. Los plugins se cargan en su propio `AssemblyLoadContext`.
5. `DrawUI()` se ejecuta por frame.
6. Los datos del juego deben obtenerse mediante `Core.*` / objetos remotos existentes antes de añadir lecturas de memoria nuevas.

**Primera tarea del agente:** crear un plugin mínimo `Poe2Accessibility` que compile, cargue y escriba en log al detectar:
- inicio del juego;
- entrada a personaje;
- cambio de área;
- salida del juego.

Nada de TTS todavía.

---

# 3. Principio fundamental: read-only

Para la primera versión:

Permitido técnicamente en nuestro diseño:

```text
ReadProcessMemory
estado interno de GameHelper2
lectura de entidades
lectura de UI
lectura de jugador
lectura de terreno
lectura de inventario/items
audio
TTS
hotkeys propias del plugin
```

Excluir:

```text
WriteProcessMemory
byte patching
AOB patches que modifiquen código
inyección de DLL
auto-flask
auto-loot
auto-move
auto-target
auto-attack
macros multiacción
evasión/anti-detection
```

Si una fuente contiene ambas cosas, aislar únicamente la parte de lectura.

---

# 4. Orden real del proyecto

La prioridad inicial es **UI primero, gameplay después**.

El usuario debe poder:

```text
abrir el juego
    ->
leer la pantalla inicial
    ->
navegar menús
    ->
seleccionar personaje
    ->
crear personaje si hace falta
    ->
entrar al juego
```

antes de invertir tiempo serio en radar/gameplay.

## Fase A — infraestructura + UI

Crear:

```text
Poe2Accessibility/
├── Poe2Accessibility.cs
├── AccessibilitySettings.cs
├── Services/
│   ├── AccessibilityOutputService.cs
│   ├── GameStateService.cs
│   ├── UiService.cs
│   ├── UiNavigationService.cs
│   ├── EntityService.cs
│   ├── TerrainService.cs
│   └── AudioService.cs
├── Models/
│   ├── AccessibleUiNode.cs
│   ├── AccessibleEntity.cs
│   └── ...
├── Audio/
└── Localization/
```

No meter toda la lógica en `DrawUI`.

Diseño deseado:

```text
Plugin tick
   |
   +-- detectar cambio de estado/UI
   |
   +-- snapshot semántico
   |
   +-- diff contra estado anterior
   |
   +-- eventos de accesibilidad
          |
          +-- Prim
          +-- audio cues
```

Evitar leer memoria agresivamente desde múltiples servicios.

---

# 5. Salida de accesibilidad con Prim

Usar **Prim** como capa de salida.

No integrar Tolk.

Claude/Codex deben asumir que Prim es la interfaz de accesibilidad elegida para:
- texto al lector de pantalla;
- mensajes inmediatos;
- interrupciones/prioridades si Prim las soporta;
- futuras abstracciones de output.

Crear una capa propia para no acoplar toda la lógica directamente a Prim:

```csharp
public interface IAccessibilityOutputService
{
    void Speak(string text, bool interrupt = false);
    void Stop();
}
```

Requisitos:
- nunca anunciar cada frame;
- deduplicación;
- debounce;
- prioridad;
- posibilidad de interrumpir;
- cooldown por evento;
- hablar solo ante cambios o acciones del usuario.

Modelo recomendado:

```text
AccessibilityEvent
- Id
- Text
- Priority
- Interrupt
- Timestamp
- DeduplicationKey
```

Prim debe quedar aislado detrás del servicio para que UI/gameplay no dependan de su implementación concreta.

# 6. UI Y MENÚS — PRIMER OBJETIVO DEL PROYECTO

Esta es la fase inicial real del mod.

Antes de investigar radar o combate, debemos conseguir que una persona ciega pueda navegar desde el arranque hasta gameplay.

## Objetivos prioritarios

En este orden:

```text
1. pantalla inicial / login
2. main menu
3. character select
4. character creation
5. league selection si existe
6. confirmaciones/modales
7. settings
8. entrar al juego
9. NPC dialog
10. inventory
11. stash
12. vendors
13. skill UI
14. passive tree
```

No hace falta resolver todas las interfaces antes del gameplay. El primer milestone es:

```text
launch -> menus -> character select -> enter game
```

## Investigar el árbol UI

Buscar en GameHelper2:

```text
InGameState
UI root
UI element wrappers
children
parent
text
visible
enabled
focused
hovered
selected
bounds
screen position
element type
```

Usar `StashUtility` como referencia principal para:
- UI Path Explorer;
- hovered item inspector;
- dumpers;
- identificación de paths de UI.

## Primeras herramientas de investigación

Crear primero un hotkey/debug action:

```text
DumpVisibleUiTree()
```

Salida:

```text
debug/ui-tree.txt
```

Cada nodo debería incluir cuando sea posible:

```text
path
address/id
type
text
visible
enabled
focused
hovered
selected
bounds
child count
```

Hacer dumps comparables de:

```text
title/login
main menu
character select
character creation
settings
confirmation dialog
inventory closed
inventory open
NPC dialog
vendor
stash
```

## No depender de posiciones de pantalla

Evitar lógica tipo:

```text
botón 1 está en X/Y
```

Preferir:

```text
tipo de nodo
texto
rol
parent/path
estado selected/focused
relación entre siblings
```

Las coordenadas solo deben usarse como fallback o para depuración.

## Modelo semántico propio

Crear:

```csharp
AccessibleUiNode
{
    Id
    Path
    Role
    Text
    IsVisible
    IsEnabled
    IsFocused
    IsSelected
    IsHovered
    ParentId
    Children[]
}
```

La lógica de accesibilidad debe consumir `AccessibleUiNode`, no pointers crudos.

## Navegación

Investigar si el juego ya tiene:
- keyboard focus;
- gamepad focus;
- selected element;
- confirm/back actions.

Nuestra primera preferencia debe ser aprovechar la navegación nativa y **solo anunciar el elemento seleccionado/focused**.

Si no existe foco accesible/estable:
- mantener un cursor virtual propio;
- recorrer siblings/children;
- activar mediante la acción nativa adecuada;
- evitar click por coordenadas salvo último recurso.

## Eventos semánticos

Detectar:

```text
UiScreenChanged
FocusedElementChanged
SelectedElementChanged
DialogOpened
DialogClosed
ListChanged
TooltipChanged
```

Prim debe hablar por evento, no por frame.

Ejemplo:

```text
"Character Select"
"Alisson, level 12, Sorceress"
"Create new character"
"Play"
```

## EXP-UI-001 — root

Encontrar el root UI real y documentar:
- clase;
- propiedad;
- lifecycle;
- cuándo existe;
- cuándo se invalida.

## EXP-UI-002 — main menu dump

Con el menú principal visible:
- dump completo;
- identificar nodos de Play/Options/Exit;
- encontrar selected/focused state.

## EXP-UI-003 — character select

Identificar:
- lista de personajes;
- nombre;
- clase;
- nivel;
- league;
- botón Play;
- Delete;
- Create Character.

## EXP-UI-004 — focus tracking

Loggear **solo cambios** de:
- focus;
- selection;
- hovered node.

Objetivo:
```text
tecla/gamepad -> cambia selección -> Prim anuncia nuevo elemento
```

## EXP-UI-005 — activate/back

Confirmar cómo activar un elemento sin automatizaciones complejas:
- reutilizar input nativo;
- command/action del juego;
- evitar WriteProcessMemory.

## EXP-UI-006 — full boot path

Milestone:

```text
launch game
-> main menu spoken
-> navigate character list
-> choose character
-> Play
-> detect entering gameplay
```

Hasta que esto funcione, no priorizar radar.

---

# 7. Cambio de área / estado global

Localizar y documentar cómo GameHelper2 expone:

- jugador actual;
- `AreaInstance`;
- nombre/id de zona;
- loading state;
- cambio de área;
- entidad Player;
- posición de jugador;
- orientación/facing si existe;
- cámara si fuese necesaria.

Crear eventos propios:

```csharp
AreaEntered
AreaLeft
PlayerReady
PlayerPositionChanged
GameStateChanged
```

La accesibilidad no debería depender de polling de strings si ya existen eventos/coroutines en el host.

---

# 8. Entidades

Investigar cómo se enumeran entidades y qué componentes existen.

Mínimo necesario:

```text
Entity ID/address
metadata path
entity type/category
grid/world position
distance from player
alive/dead
hostile/friendly
NPC
monster
chest/container
transition
waypoint
interactable
item/loot
rarity
Life
Actor state
```

Buscar primero en:

```text
RemoteObjects/Entity
RemoteObjects/Components/
Plugins/Radar
Plugins/HealthBars
```

No inventar clasificaciones si GameHelper2 ya las calcula.

Crear un DTO independiente:

```csharp
AccessibleEntity
{
    Id
    Category
    Name
    MetadataPath
    Position
    Distance
    Bearing
    IsAlive
    IsHostile
    Rarity
}
```

Así el módulo de audio no depende directamente del layout de memoria.

---

# 9. Radar sonoro

Objetivo: radar de exploración manual parecido al usado en nuestros otros mods.

No queremos un pathfinder automático.

## Preguntas que debe resolver el agente

1. ¿Cómo representa GameHelper2 el terrain grid?
2. ¿Cuál es la resolución de cada celda/tile?
3. ¿Cómo se transforma:
   - grid -> world
   - world -> grid?
4. ¿Qué valor representa walkable/non-walkable?
5. ¿Hay altura/Z?
6. ¿Cómo se detectan cambios de nivel, puentes o capas?
7. ¿El terreno completo del área está disponible al cargarla?
8. ¿Existe `GridLandscapeData` o equivalente en la versión actual?
9. ¿Cómo lo consume `Plugins/Radar`?

## API nuestra

```csharp
bool IsWalkable(GridPos p);

RayScanResult ScanDirection(
    GridPos origin,
    float angle,
    int maxDistance);
```

Posible resultado:

```csharp
RayScanResult
{
    FreeDistance
    FirstBlockedCell
    Samples[]
}
```

Después implementar cuatro/seis/ocho direcciones relativas al jugador.

### Importante

No emitir sonido por cada tile.

El radar debe producir **una representación semántica agregada**:

```text
forward: 17 tiles free
left: 4 tiles free
right: blocked at 2
back: 12 tiles free
```

Luego traducir esto a pitch/ritmo/panning.

Mantener una capa de antispam/cambio significativo.

---

# 10. Orientación del jugador

Investigar, en este orden:

1. `Positioned`
2. `Render`
3. `Actor`
4. input/animation state
5. cámara

Necesitamos conocer el heading si queremos que "delante/izquierda/derecha" sea relativo al personaje.

Si el juego no expone un heading fiable:
- estudiar dirección de movimiento;
- mouse aim;
- target direction;
- cámara.

No asumir que world north = forward.

---

# 11. Audio beacon

API conceptual:

```csharp
BeaconTarget
{
    Position
    Type
    Id
}
```

Calcular:

```text
relative angle
distance
optional vertical difference
```

Audio:
- panning = ángulo;
- frecuencia de pulso = distancia;
- volumen opcional = distancia;
- pitch opcional = altura.

No mover al personaje.

Targets posibles:
- NPC;
- transition;
- waypoint;
- quest object;
- loot marcado;
- enemigo seleccionado manualmente.

---

# 12. Radar de entidades

Separarlo del radar de geometría.

Ejemplo de comandos/hotkeys:

```text
scan nearest hostile
scan NPCs
scan transitions
scan interactables
scan loot
repeat current target
```

El sistema debe:
1. snapshot de entidades;
2. filtrar;
3. ordenar por distancia o ángulo;
4. anunciar solo bajo petición o ante eventos importantes.

Evitar un "sonar" que verbalice decenas de entidades continuamente.

---

# 13. Combate

Primero solo información.

Investigar:

```text
Actor
Life
target/selected entity
monster rarity
states/buffs/debuffs
animation/action state
projectiles si están expuestos
```

Primer objetivo:

```text
hotkey -> enemigo relevante más cercano:
"Rare, 8 meters, front-left, 72 percent"
```

Después se puede estudiar audio posicional de amenazas.

No implementar ataque automático.

---

# 14. UI avanzada dentro del gameplay

Esta probablemente será la parte más importante junto al radar.

Estudiar:

```text
InGameState
UI tree
UI element wrappers
hovered element
visible/enabled flags
text labels
bounds
parent/children
focused/selected state
```

Usar `StashUtility` como referencia de investigación por su `UI Path Explorer`.

## Objetivos iniciales

Detectar y leer:

```text
main menu
character select
inventory
stash
skills
passive tree
NPC dialog
vendor
settings
item hover
tooltips
```

No intentar hacer todo a la vez.

### Primera prueba UI

Crear una herramienta debug que, bajo hotkey, vuelque:

```text
visible UI tree
index/path
text
bounds
visibility
child count
```

a:

```text
debug/ui-tree.txt
```

Después comparar estados:
- inventario cerrado;
- inventario abierto;
- item hovered;
- NPC dialog.

---

# 15. Items / inventario

Investigar:

```text
hovered item
base name
rarity
item level
requirements
sockets
mods
implicit mods
explicit mods
quality
stack size
position in inventory/stash
```

No reimplementar parser si ya existe en GameHelper2/StashUtility.

Objetivo:

```text
hotkey -> leer tooltip/item hovered
```

Después:
- navegación por cuadrícula;
- cambios de selección;
- descripción compacta/detallada.

---

# 16. Quest/NPC/transitions

Buscar metadata paths y categorías para:

```text
NPC
Waypoint
AreaTransition
QuestObject
Chest
Door
Interactable
```

Generar un catálogo de muestras observado en runtime:

```json
{
  "metadataPath": "...",
  "components": ["Positioned", "Render", "..."],
  "category": "transition",
  "area": "..."
}
```

No hardcodear miles de paths en la lógica principal.

Usar reglas + tabla de overrides.

---

# 17. Logging de investigación

Crear logging separado:

```text
logs/accessibility.log
logs/entities.log
logs/ui.log
logs/terrain.log
```

Cada experimento debe registrar:

```text
game build/version
GameHelper2 commit/version
area
player position
entity address/id
metadata path
component list
timestamp
```

Esto es esencial porque los offsets cambian con patches.

---

# 18. Resistencia a updates

GameHelper2 ya separa:

```text
GameOffsets/
```

de:

```text
GameHelper/
```

No añadir offsets propios al plugin salvo que sea estrictamente necesario.

Orden de preferencia:

```text
1. API pública del host
2. RemoteObjects/componentes existentes
3. ampliar upstream/core
4. lectura propia temporal de memoria
```

Si encontramos un offset nuevo útil para todos, lo correcto es implementarlo como componente/core, no esconderlo dentro del plugin.

Mantener:

```text
Accessibility logic != memory layout
```

---

# 19. CoreExile2 / ExileBridge — criterio de decisión

Antes de adoptar CoreExile2, comprobar que ExileBridge exponga:

- player position;
- area;
- entity enumeration;
- metadata path;
- Life;
- Actor;
- Positioned/Render;
- terrain/walkability;
- UI tree;
- item information;
- event/tick API.

Si cubre >= 80-90 % de lo necesario, considerar escribir el mod contra ExileBridge.

Ventaja:

```text
AccessibilityPlugin -> ExileBridge API -> host -> offsets
```

en vez de:

```text
AccessibilityPlugin -> internals de GameHelper2 -> offsets
```

No migrar solo por elegancia arquitectónica si faltan APIs esenciales.

---

# 20. Experimentos para Claude/Codex

Ejecutar EN ESTE ORDEN.

## EXP-001 — build limpio

Objetivo:
- clonar GameHelper2;
- compilar Release;
- ejecutar launcher;
- confirmar attach a PoE2.

Documentar:
- .NET SDK;
- errores;
- output;
- versión del juego.

No modificar código.

---

## EXP-002 — SamplePlugin

Objetivo:
- copiar `SamplePluginTemplate`;
- renombrarlo `Poe2Accessibility`;
- compilar;
- confirmar `OnEnable/OnDisable/DrawUI`.

Resultado esperado:

```text
[Poe2Accessibility] loaded
[Poe2Accessibility] game opened
```

---

## EXP-003 — UI root

Encontrar el root UI y documentar:
- clase exacta;
- propiedad exacta;
- lifecycle;
- invalidación;
- hijos.

---

## EXP-004 — main menu tree dump

Con el menú principal visible:

```text
hotkey -> DumpVisibleUiTree()
```

Identificar:
- Play;
- Options;
- Exit;
- focus/selection.

---

## EXP-005 — focus/selection tracking

Loggear solo cuando cambie:

```text
focused node
selected node
hovered node
screen/context
```

---

## EXP-006 — Prim

Integrar Prim detrás de:

```text
IAccessibilityOutputService
```

Prueba mínima:

```text
focus changes -> announce element text
```

No llamar Prim directamente desde todos los módulos.

---

## EXP-007 — character select

Identificar y leer:
- personajes;
- clase;
- nivel;
- league;
- selected character;
- Play;
- Create Character;
- Delete.

---

## EXP-008 — boot-to-gameplay milestone

Con navegación nativa:

```text
launch
-> read main menu
-> character select
-> choose character
-> Play
-> detect gameplay entered
```

Este es el primer milestone real.

---

## EXP-009 — player + area

Solo después del milestone de menús:

Loggear una vez por cambio:

```text
area
player entity
player grid position
player world position
```

---

## EXP-010 — entity census

En una zona segura:

```text
hotkey -> snapshot de todas las entidades
```

Guardar:

```text
id/address
metadata path
position
components
distance
```

---

## EXP-011 — terrain

Con el Radar existente:
- localizar fuente exacta del grid;
- documentar structs;
- crear función read-only:

```csharp
DumpTerrainAroundPlayer(radius: 20)
```

Salida textual:

```text
# = blocked
. = walkable
P = player
```

---

## EXP-012 — directional scan

Sobre la función anterior:

```text
forward
front-left
left
back-left
back
back-right
right
front-right
```

Devuelve distancia libre.

Todavía sin audio.

---

## EXP-013 — first accessible radar

Hotkey:

```text
announce four/eight directions
```

Después reemplazar progresivamente por audio cues.

---

## EXP-014 — gameplay UI

Investigar:
- inventory;
- NPC dialog;
- vendor;
- stash;
- tooltip;
- hovered item.

---

## EXP-015 — hovered item

Con Prim:

```text
read hovered item
```

Mínimo:
- nombre;
- tipo;
- rareza;
- propiedades principales.

# 21. Reglas para agentes de código

Copiar estas reglas a `AGENTS.md` del proyecto:

```text
1. Read existing GameHelper2 code before implementing duplicate memory readers.
2. Do not use WriteProcessMemory.
3. Do not add byte patches or cheat functionality.
4. Do not implement auto-movement, auto-combat, auto-loot or auto-target.
5. Keep accessibility logic separated from memory-layout code.
6. Prefer host events/caches over polling memory every frame.
7. Every new memory field must document source struct, offset provenance and validation method.
8. Never guess an offset.
9. Validate pointers/addresses before dereferencing.
10. Expect area changes to invalidate entity/component addresses.
11. Avoid TTS spam; speech must be event/change driven.
12. Cache entity snapshots and diff them instead of announcing every frame.
13. Terrain radar must aggregate cells before producing audio.
14. Keep all player actions manual.
15. Commit each successful research experiment separately.
16. Write findings to docs/research/*.md.
17. Preserve raw evidence/logs for uncertain structures.
18. If the game patches and a read breaks, stop and validate offsets rather than adding hacks.
```

---

# 22. Preguntas concretas que los agentes deben responder

Crear `docs/research/questions.md` e ir marcando:

```text
[ ] How is AreaInstance obtained?
[ ] How are entities enumerated?
[ ] Which object is the local player?
[ ] Stable entity ID?
[ ] Grid position API?
[ ] World position API?
[ ] Player heading/facing?
[ ] Terrain grid source?
[ ] Terrain cell size?
[ ] Walkability encoding?
[ ] Height/elevation representation?
[ ] Area transition detection?
[ ] NPC classification?
[ ] Waypoint classification?
[ ] Door/interactable classification?
[ ] Loot ground entity representation?
[ ] Hovered item source?
[ ] UI root pointer/object?
[ ] UI element text representation?
[ ] Focus/hover/selected state?
[ ] Inventory slot geometry?
[ ] Current target representation?
[ ] Monster hostility?
[ ] Monster rarity?
[ ] Life current/max?
```

---

# 23. Git branches sugeridas

```text
main
research/gamehelper
feature/plugin-bootstrap
feature/speech
feature/entity-model
feature/terrain
feature/radar
feature/ui-tree
feature/item-reader
```

Los experiments de reverse engineering deberían entrar primero como documentación/logging antes que como funcionalidades grandes.

---

# 24. MVP

Considerar MVP cuando podamos hacer:

1. arrancar junto a GameHelper2;
2. anunciar área;
3. TTS mediante Prim;
4. anunciar información básica del jugador;
5. escanear walkability en 4/8 direcciones;
6. audio/TTS para NPC, transition y waypoint cercanos;
7. beacon manual a una entidad elegida;
8. leer item hovered;
9. leer al menos inventario/NPC dialog de forma básica.

No intentar passive tree completo ni combate avanzado antes de esto.

---

# 25. Riesgo de cuenta / ToS

Grinding Gear Games restringe en sus Terms of Use la modificación/adaptación del cliente, reverse engineering y automatización, y públicamente no garantiza que herramientas de terceros sean aceptables.

Por eso este proyecto debe mantenerse deliberadamente conservador:

```text
external
read-only
no automation
no hidden-information gameplay advantage as a design goal
accessibility translation only
```

Esto **no garantiza** que GGG permita la herramienta.

Antes de distribuir públicamente una build, conviene solicitar por escrito a GGG una posición respecto a una herramienta de accesibilidad read-only para jugadores ciegos.

Fuentes:
- https://www.pathofexile.com/legal/terms-of-use-and-privacy-policy
- https://www.pathofexile.com/forum/view-thread/3518296/filter-account-type/staff

---

# 26. Fuentes técnicas verificadas — septiembre de 2026

GameHelper2:
https://github.com/Gordin/GameHelper2

CoreExile2:
https://github.com/coussiraty/CoreExile2

POE2Radar:
https://github.com/NattKh/POE2Radar

POE2Radar original:
https://github.com/Sikaka/POE2Radar

StashUtility:
https://github.com/AlexanderHel/StashUtility

Notas relevantes de GameHelper2:
- Windows x64, .NET 10.
- Plugin architecture.
- `GameOffsets` contiene layouts/offsets.
- `RemoteObjects/Components` refleja componentes como Actor, Life, Positioned y Render.
- plugins basados en `PCore<TSettings>`.
- los offsets requieren mantenimiento tras patches.
- actualmente existen Radar, HealthBars, PreloadAlert y otros plugins útiles como referencia.

Notas relevantes de POE2Radar:
- lectura externa de memoria;
- terrain/walkability;
- entidades;
- landmarks;
- proyecto de Research separado para descubrimiento/validación de offsets.

---

# 27. Prompt inicial recomendado para un agente

```text
You are investigating how to build a blind-accessibility plugin for Path of Exile 2 on top of GameHelper2.

Read this entire repository and this research brief before changing code.

FIRST PRIORITY IS UI, NOT GAMEPLAY.

The first milestone is:
launch game -> read main menu -> navigate Character Select -> select a character -> enter gameplay.

First objectives:
1. understand GameHelper2's current plugin API;
2. inspect how the UI tree is represented;
3. identify the exact classes/properties for UI root, children, text, visibility, enabled state, selected/focused/hovered state and bounds;
4. inspect StashUtility's UI Path Explorer;
5. inspect SamplePluginTemplate;
6. write docs/research/ui-api.md with exact file paths, class names, methods and data flow;
7. build a UI tree dumper;
8. track focus/selection changes;
9. integrate Prim behind an accessibility output abstraction;
10. make the main menu and Character Select readable before starting terrain/radar work.

Constraints:
- Prim is the accessibility output system.
- Do not use Tolk.
- read-only memory model;
- never use WriteProcessMemory;
- no byte patching;
- no game automation;
- no pathfinding/autowalk;
- no auto-target/auto-loot/auto-combat;
- do not guess memory offsets;
- reuse existing GameHelper2 abstractions before adding new memory reads;
- accessibility output must be event/change driven, never frame driven.

After UI boot flow works, continue with:
player/area -> entities -> terrain/walkability -> directional radar -> gameplay UI -> items -> combat information.

Do not attempt the complete mod in one pass.
Produce small validated experiments and document every finding.
```

---

## Prioridad de trabajo

Orden actual:

```text
plugin loads
    ->
UI root
    ->
main menu
    ->
focus/selection tracking
    ->
Prim output
    ->
Character Select
    ->
enter gameplay
    ->
player/area
    ->
entity enumeration
    ->
terrain/walkability
    ->
directional radar
    ->
gameplay UI
    ->
items
    ->
combat information
```

El proyecto no debe considerar el radar como prioridad hasta que podamos llegar al gameplay de forma accesible desde el arranque.
