# Stone_Finite
Stone_Finite

Unity 2022.3.47f1

[PatchNotes] - 자막 ON
https://www.youtube.com/watch?v=sjb8gKHDYFo&list=PLqqBsGlhWG3twfFLFDOJMIWJVX9QUouei

[Itch.io]
https://madamnut.itch.io/stone-the-beginning

[기획 기록]
https://www.notion.so/Tech-Tree-2b7d5bc4da9680c3a877e4f634054f25?source=copy_link

/
/
/
/
/
/
/
/
/
/
/
/
/

classDiagram
direction LR

%% =========================
%% Core / World
%% =========================
class WorldManager
class WorldGenSettings
class WorldData
class WorldDataGenerator
class WorldChunkSystem
class WorldLoadContext
class WorldSaveSystem
class Chunk
class FallingBlock
class CellLibrary
class ProceduralUtil
class StructureTemplate

%% =========================
%% Entities / Mobs / Corpse
%% =========================
class Entity
class EntityManager
class Mob
class Cow
class MobLibrary
class Corpse
class CorpseLibrary
class DroppedItem
class ItemDropper

%% =========================
%% Items / Inventory / Craft
%% =========================
class ItemData
class ItemLibrary
class RecipeLibrary
class InventoryData
class ItemSlot
class PlayerInventory
class CraftModule
class MudFurnaceModule
class Hotbar

%% =========================
%% Player / Input / UI
%% =========================
class Player
class InteractionController
class Cursor
class Heart
class ScrollViewContents

%% =========================
%% Multiblock
%% =========================
class Multiblock
class ClayKiln
class BrickFurnace
class MultiblockManager
class MultiblockLibrary

%% =========================
%% Audio / Vfx / Misc
%% =========================
class AudioManager
class VfxManager
class GodRayBG
class BackGround
class Debugger
class ImageGenerator
class LobyManager

%% ---------- Inheritance ----------
Cow --|> Mob
Mob --|> Entity
Player --|> Entity
ClayKiln --|> Multiblock
BrickFurnace --|> Multiblock

%% ---------- “has / references” edges (fields, props) ----------
InteractionController --> Player
InteractionController --> Hotbar
InteractionController --> ItemSlot
InteractionController --> WorldManager
InteractionController --> MultiblockManager
InteractionController --> AudioManager

WorldManager --> WorldGenSettings
WorldManager --> WorldData
WorldManager --> Chunk
WorldManager --> FallingBlock
WorldManager --> ItemDropper
WorldManager --> VfxManager
WorldManager --> EntityManager
WorldManager --> MobLibrary
WorldManager --> CorpseLibrary
WorldManager --> ItemLibrary
WorldManager --> WorldSaveSystem

WorldDataGenerator --> WorldGenSettings
WorldDataGenerator --> WorldData
WorldDataGenerator --> StructureTemplate
WorldDataGenerator --> ProceduralUtil
WorldDataGenerator --> CellLibrary

WorldChunkSystem --> WorldManager
Chunk --> WorldManager

ItemDropper --> EntityManager
ItemDropper --> ItemLibrary
DroppedItem --> ItemData

CorpseLibrary --> Corpse
CorpseLibrary --> ItemLibrary
CorpseLibrary --> EntityManager

MobLibrary --> Mob

RecipeLibrary --> ItemLibrary
CraftModule --> RecipeLibrary
CraftModule --> Player
MudFurnaceModule --> RecipeLibrary
MudFurnaceModule --> Player

Player --> InventoryData
PlayerInventory --> Player
PlayerInventory --> ItemSlot
ItemSlot --> InventoryData
InventoryData --> ItemData

MultiblockManager --> WorldManager
MultiblockManager --> MultiblockLibrary
MultiblockManager --> ClayKiln
Multiblock --> WorldManager
Multiblock --> MultiblockManager

Debugger --> WorldManager
Cursor --> InteractionController
Heart --> Player
GodRayBG --> WorldManager
ImageGenerator --> WorldDataGenerator
LobyManager --> WorldLoadContext
