# AzSLD models

- `azsld_sign_classifier.bytes`: the pack's own sign model (small Transformer over landmark sequences, per the plan), exported to TFLite or ONNX and renamed to `.bytes`. Assign it to the pack's **Recognition Model** field and return an `ISignClassifier` from `AzSLDLanguagePack.CreateClassifier()`. Until then, recognition uses template matching only.
- Optional overrides of the MediaPipe `hand_landmarker.task` / `face_landmarker.task` files (renamed `.bytes`). Leave empty to use the app defaults on `MediaPipeLandmarkProvider`.
