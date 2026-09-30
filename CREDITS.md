# Credits

## Hair models (MakeHuman community asset packs)

- **Cooper's hair**: `hair_05` by **culturalibre** (original by swift502), from the MakeHuman community "hair01" asset pack. License: **CC0**.
  https://static.makehumancommunity.org/assets/assetpacks/hair01.html
- **Nathan's curls**: `punkduck_alpha7_curly` by **punkduck**, from the MakeHuman community "hair03" asset pack. License: **CC-BY** (attribution required; this file is that attribution).
  https://static.makehumancommunity.org/assets/assetpacks/hair03.html

The files live in `ScaryStreetUnity/Assets/Resources/Hair/` (the .obj meshes are stored as `.obj.bytes` so Unity loads them as data). They are fitted to our character's head in code (`HairAssets.cs`); Nathan's curls are shortened to sit under his cap.

## Character body and clothes

The rigged human body, fitted clothes and knit texture come from this project's own web build (`index (5).html`).

## Animations

- **Breathing Idle, Walking, Running, Punching (jab) and Punching (cross)** from **Adobe Mixamo** (mixamo.com), downloaded by the project owner. Mixamo animations are royalty-free for use in games (Adobe's Mixamo terms). The source FBX files are in `tools/anim/mixamo/`; `tools/anim/bake_mixamo.py` bakes them into `ScaryStreetUnity/Assets/Resources/Anim/`.

## Fonts

- **Creepster** by **Font Diner, Inc** (the title's "SCARY"), from Google Fonts. License: **SIL Open Font License 1.1**; the license text ships with it at `ScaryStreetUnity/Assets/Resources/Fonts/Creepster-OFL.txt`.
  https://fonts.google.com/specimen/Creepster

## The friends' bodies and clothes (Cooper, Nathan, Kenny, Isaiah, Thorton, Piper, John, Will)

- **Base body, eyes, teeth and skin textures**: a Character Creator 3+ base character (Reallusion), exported by the project owner. Used under the owner's Character Creator licence; only the processed game data (`ScaryStreetUnity/Assets/Resources/RealBody/`) is in the repo, not the source export.
- **Sweater, pants (and the unused beanie)**: "Urban Outfit" by **@00p5_** (instagram.com/00p5_), purchased by the project owner. Nathan wears it as is; Cooper's tee is the sweater cut down.
- **Cooper's hair**: a free stylized hair model ("DummyHair") supplied by the project owner.
- **Piper's build**: shaped from the **Female Body Character Basemesh** by **CG Cookie, Inc.** (modeled by Jonathan Williamson), from Blend Swap. License: **CC-BY 3.0** (attribution required; this is that attribution). Its torso widths are matched slice by slice in `tools/real_bodies/people.py`; the file isn't in the repo.
  http://www.blendswap.com/blends/view/26312
- **Sneakers**: a scanned pair of Nike shoes ("Red Nike Shoes", `rednikeinstant.002` with a baked texture) supplied by the project owner; recoloured per character by `tools/real_bodies/shoe_textures.py`. Nike and the swoosh are a real brand: swap for a lookalike before any public release (see DESIGN.md's licensing note).
- Everything was fitted, re-skinned and re-posed in Blender by `tools/real_bodies/`; hair (curls, strands, fades), faces, brows, beards, hoods, socks, glasses and the nose ring are generated there too.

## Mordecai and Rigby (DLC)

Built from scratch in Blender by `tools/real_bodies/toon.py`, modelled after Regular Show's characters. **Mordecai and Rigby belong to Cartoon Network**: fine for a private build, not for a public release (rename them or make parody versions first).
