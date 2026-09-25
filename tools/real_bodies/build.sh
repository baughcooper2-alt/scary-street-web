#!/bin/zsh
# Rebuilds Cooper and Nathan's real bodies (Resources/RealBody/*.bytes) with Blender in the background.
# Put the source files in WORK first (they are not in the repo):
#   WORK/Unity/unity.Fbx (+ Unity/unity.fbm textures)   Character Creator base body export
#   WORK/UrbanOutfit/Export OBJ/Beanie_Outfit_V01.obj    sweater, pants, beanie (Marvelous Designer)
#   WORK/obj_0.obj                                       sweatshirt (only split up, not used)
#   WORK/DummyHair.obj                                   Cooper's stylized hair (+ its bust)
#   WORK/curly.obj                                       copy of Assets/Resources/Hair/curly.obj.bytes
# Textures (Resources/RealBody/Tex) were converted from Unity/unity.fbm by hand with Pillow; see CLAUDE.md.
set -e
WORK=${1:?usage: build.sh WORK_DIR}
B=/Applications/Blender.app/Contents/MacOS/Blender
HERE=${0:A:h}
OUT=$HERE/../../ScaryStreetUnity/Assets/Resources/RealBody
cp $HERE/*.py $WORK/
cd $WORK
$B -b -P stage1.py -- $WORK          # import, fix axes, keep blink/mouth shapes, A-pose rest
$B -b -P stage2.py -- $WORK          # arms down (our rest pose), slimmer build
$B -b -P garments_prep.py -- $WORK   # garments to metres, decimate, split by material
for who in cooper nathan; do
  $B -b -P stage3.py -- $WORK $who urban    # fit + skin the clothes, cut Cooper's sleeves
  $B -b -P stage4.py -- $WORK $who          # hair, cap, wristband, shoes, hide covered skin
  $B -b -P export_ssrb.py -- $WORK $who $OUT/${(C)who}.bytes
done
