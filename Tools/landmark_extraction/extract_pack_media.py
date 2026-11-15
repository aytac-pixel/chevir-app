#!/usr/bin/env python3
"""Builds sign references from a language pack's own lesson media.

Walks a pack's Media folder (e.g. Assets/LanguagePacks/AzSLD/Media), runs MediaPipe HandLandmarker +
FaceLandmarker on every video (.mp4) and picture (.jpg/.png), and writes one landmark JSON per file into
--out. Each JSON names its sign by media file name ("salam", "herf_a"); Unity resolves the name with the
pack's media naming rule when "Çevir > AzSLD > Create Or Update Pack Skeleton" runs.

To keep the files small, only every --stride-th video frame is kept and only the face points the feature
extractor uses (anchor, brows, eyes, lips) are stored. A picture becomes a short still clip.

Usage:
    pip install mediapipe opencv-python-headless
    python extract_pack_media.py --media Assets/LanguagePacks/AzSLD/Media \
        --out Tools/references/azsld --hand-model hand_landmarker.task --face-model face_landmarker.task
"""
import argparse
import json
import os
import sys

import cv2
import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

# Face landmarks read by SignFeatureExtractor (FaceLandmarks constants in LandmarkFrame.cs).
FACE_INDICES = [1, 10, 152, 234, 454, 33, 263, 159, 386, 105, 334, 13, 14]
VIDEO_EXT = (".mp4", ".mov")
IMAGE_EXT = (".jpg", ".jpeg", ".png")
SLOW_SUFFIX = "_yavas"


def flatten(points, indices=None):
    chosen = points if indices is None else [points[i] for i in indices]
    return [round(v, 4) for p in chosen for v in (p.x, p.y, p.z)]


def frame_payload(t, hand_result, face_result):
    frame = {"t": round(t, 4), "left": [], "right": [], "face": []}
    best = {}
    for landmarks, handedness in zip(hand_result.hand_landmarks, hand_result.handedness):
        label = handedness[0].category_name
        score = handedness[0].score
        # Recordings are not mirrored, but MediaPipe labels assume a mirrored image: swap.
        side = "right" if label == "Left" else "left"
        if side not in best or best[side][0] < score:
            best[side] = (score, landmarks)
    for side, (_, landmarks) in best.items():
        frame[side] = flatten(landmarks)
    if face_result.face_landmarks:
        frame["face"] = flatten(face_result.face_landmarks[0], FACE_INDICES)
    return frame


def to_image(bgr):
    return mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB))


def process_video(path, hands, face, offset_ms, stride):
    capture = cv2.VideoCapture(path)
    fps = capture.get(cv2.CAP_PROP_FPS) or 30.0
    width = capture.get(cv2.CAP_PROP_FRAME_WIDTH)
    height = capture.get(cv2.CAP_PROP_FRAME_HEIGHT)
    frames, index = [], 0
    while True:
        ok, bgr = capture.read()
        if not ok:
            break
        t = index / fps
        # VIDEO mode needs increasing timestamps over the landmarker's lifetime; every frame is tracked.
        ts = offset_ms + int(t * 1000)
        image = to_image(bgr)
        hand_result = hands.detect_for_video(image, ts)
        face_result = face.detect_for_video(image, ts)
        if index % stride == 0:
            frames.append(frame_payload(t, hand_result, face_result))
        index += 1
    capture.release()
    aspect = (width / height) if height else 1.0
    return frames, aspect, offset_ms + int(index / fps * 1000) + 1000


def process_image(path, hands_image, face_image, still_frames):
    bgr = cv2.imread(path)
    if bgr is None:
        return [], 1.0
    image = to_image(bgr)
    frame = frame_payload(0.0, hands_image.detect(image), face_image.detect(image))
    frames = []
    for i in range(still_frames):
        f = dict(frame)
        f["t"] = round(i * 0.1, 4)
        frames.append(f)
    h, w = bgr.shape[:2]
    return frames, (w / h) if h else 1.0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--media", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--hand-model", required=True)
    parser.add_argument("--face-model", required=True)
    parser.add_argument("--signer", default="media")
    parser.add_argument("--stride", type=int, default=2, help="Keep every n-th video frame (2 = 15 fps from 30 fps).")
    parser.add_argument("--still-frames", type=int, default=8, help="Frames written for a picture.")
    args = parser.parse_args()

    base = mp_python.BaseOptions
    hands = vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(
        base_options=base(model_asset_path=args.hand_model), running_mode=vision.RunningMode.VIDEO, num_hands=2))
    face = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(
        base_options=base(model_asset_path=args.face_model), running_mode=vision.RunningMode.VIDEO, num_faces=1))
    hands_image = vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(
        base_options=base(model_asset_path=args.hand_model), running_mode=vision.RunningMode.IMAGE, num_hands=2))
    face_image = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(
        base_options=base(model_asset_path=args.face_model), running_mode=vision.RunningMode.IMAGE, num_faces=1))

    files = []
    for root, _, names in os.walk(args.media):
        for name in sorted(names):
            stem, ext = os.path.splitext(name)
            if stem.endswith(SLOW_SUFFIX) or ext.lower() not in VIDEO_EXT + IMAGE_EXT:
                continue
            files.append(os.path.join(root, name))
    files.sort()

    os.makedirs(args.out, exist_ok=True)
    offset, report = 0, []
    for i, path in enumerate(files):
        stem, ext = os.path.splitext(os.path.basename(path))
        if ext.lower() in VIDEO_EXT:
            frames, aspect, offset = process_video(path, hands, face, offset, max(1, args.stride))
        else:
            frames, aspect = process_image(path, hands_image, face_image, args.still_frames)
        with_hand = sum(1 for f in frames if f["left"] or f["right"])
        left = sum(1 for f in frames if f["left"])
        right = sum(1 for f in frames if f["right"])
        both = sum(1 for f in frames if f["left"] and f["right"])
        # The busier hand is the dominant one. Recordings can be mirrored (selfie) or left-handed; either way the
        # importer mirrors a left dominant hand into the canonical right-handed form the app compares against.
        dominant = "Left" if left > right else "Right"
        two_handed = with_hand > 0 and both / with_hand > 0.6
        report.append((stem, with_hand, len(frames)))
        if with_hand == 0:
            print(f"[{i + 1}/{len(files)}] {stem}: no hand found, skipped", file=sys.stderr)
            continue
        payload = {
            "signId": "",
            "mediaSlug": stem,
            "source": os.path.relpath(path, args.media).replace(os.sep, "/"),
            "signerId": args.signer,
            "imageAspect": round(aspect, 5),
            "dominantHand": dominant,
            "twoHanded": two_handed,
            "faceIndices": FACE_INDICES,
            "frames": frames,
        }
        with open(os.path.join(args.out, stem + ".json"), "w", encoding="utf-8") as out:
            json.dump(payload, out, ensure_ascii=False, separators=(",", ":"))
        print(f"[{i + 1}/{len(files)}] {stem}: hand in {with_hand}/{len(frames)} frames, dominant {dominant}"
              + (", two hands" if two_handed else ""))

    weak = [r for r in report if r[2] and r[1] / r[2] < 0.5]
    if weak:
        print("Hand found in fewer than half the frames: " + ", ".join(f"{s} ({h}/{n})" for s, h, n in weak))


if __name__ == "__main__":
    main()
