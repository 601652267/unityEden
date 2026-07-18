# Eden Gallery for Unity 2018

This module plays the first ten Eden character portraits with the Spine 3.7
runtime. `EdenGalleryBootstrap` starts automatically from the existing sample
scene, so no scene object needs to be wired by hand.

Controls:

- Left / right arrow buttons or keyboard arrows: switch characters.
- Bottom character buttons: select one of the ten characters.
- Top 1 / 2 / 3 buttons or number keys: switch portrait variants.
- Horizontal swipe in the stage area: switch characters on touch devices.

Runtime API:

```csharp
EdenGalleryController.Instance.LoadCharacter("11100003", 2);
EdenGalleryController.Instance.LoadCharacter(4, 0);
EdenGalleryController.Instance.LoadStage(1);
EdenGalleryController.Instance.NextCharacter();
```

To regenerate the ten-character resource subset from `edenvue`:

```bash
node Tools/sync_eden_gallery.js \
  /Users/zhuhaiming/Desktop/edenvue/public \
  /Users/zhuhaiming/Desktop/unity/unityEden \
  10
```

The copied Spine runtime is licensed under its bundled `LICENSE.txt`. Use and
redistribution remain subject to the Spine Runtimes license terms.
