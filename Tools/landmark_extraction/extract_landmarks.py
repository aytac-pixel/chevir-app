#!/usr/bin/env python3
"""Offline landmark extraction for Çevir sign references.

Runs MediaPipe HandLandmarker + FaceLandmarker (the same models the app uses on-device) over dataset
videos such as AzSLD and writes one raw-landmark JSON per video. Import the folder in Unity with
"Çevir > Import Sign References From Folder...": the importer converts landmarks to features with the
app's own SignFeatureExtractor, so reference and live features always match.

Only landmarks are kept; videos are not copied or redistributed.

Usage:
    pip install mediapipe opencv-python
    python extract_landmarks.py --index index.csv --out refs/ \
        --hand-model hand_landmarker.task --face-model face_landmarker.task

index.csv columns: video_path,sign_id,signer_id[,dominant_hand]
    e.g. AzSLD/dactyl/A/s07_003.mp4,azsld.letter.a,s07,Right
Mapping AzSLD's own JSON annotations to sign ids is dataset-specific; build index.csv from them first.
"""
import argparse
import csv
import json
import os
import sys

import cv2
import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision


def create_landmarkers(hand_model, face_model):
    hands = vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_path=hand_model),
        running_mode=vision.RunningMode.VIDEO,
        num_hands=2,
        min_hand_detection_confidence=0.5,
        min_hand_presence_confidence=0.5,
        min_tracking_confidence=0.5))
    face = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_path=face_model),
        running_mode=vision.RunningMode.VIDEO,
        num_faces=1))
    return hands, face


def flatten(landmarks):
    return [round(v, 5) for p in landmarks for v in (p.x, p.y, p.z)]


def process_video(path, hands, face, timestamp_offset_ms):
    capture = cv2.VideoCapture(path)
    fps = capture.get(cv2.CAP_PROP_FPS) or 30.0
    width = capture.get(cv2.CAP_PROP_FRAME_WIDTH)
    height = capture.get(cv2.CAP_PROP_FRAME_HEIGHT)
    frames = []
    index = 0
    while True:
        ok, bgr = capture.read()
        if not ok:
            break
        rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
        image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        t = index / fps
        # VIDEO mode needs monotonically increasing timestamps across the landmarker's whole lifetime.
        ts = timestamp_offset_ms + int(t * 1000)

        hand_result = hands.detect_for_video(image, ts)
        face_result = face.detect_for_video(image, ts)

        frame = {"t": round(t, 4), "left": [], "right": [], "face": []}
        best = {}
        for landmarks, handedness in zip(hand_result.hand_landmarks, hand_result.handedness):
            label = handedness[0].category_name
            score = handedness[0].score
            # Dataset videos are not mirrored, but MediaPipe labels assume a mirrored image: swap.
            side = "right" if label == "Left" else "left"
            if side not in best or best[side][0] < score:
                best[side] = (score, landmarks)
        for side, (_, landmarks) in best.items():
            frame[side] = flatten(landmarks)
        if face_result.face_landmarks:
            frame["face"] = flatten(face_result.face_landmarks[0])

        frames.append(frame)
        index += 1
    capture.release()
    aspect = (width / height) if height else 1.0
    return frames, aspect, timestamp_offset_ms + int(index / fps * 1000) + 1000


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--index", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--hand-model", required=True)
    parser.add_argument("--face-model", required=True)
    parser.add_argument("--root", default="", help="Prefix for relative video paths in the index.")
    args = parser.parse_args()

    os.makedirs(args.out, exist_ok=True)
    hands, face = create_landmarkers(args.hand_model, args.face_model)
    offset = 0
    with open(args.index, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    for i, row in enumerate(rows):
        video = os.path.join(args.root, row["video_path"])
        if not os.path.exists(video):
            print("missing:", video, file=sys.stderr)
            continue
        frames, aspect, offset = process_video(video, hands, face, offset)
        payload = {
            "signId": row["sign_id"],
            "source": row["video_path"],
            "signerId": row.get("signer_id", ""),
            "imageAspect": round(aspect, 5),
            "dominantHand": row.get("dominant_hand") or "Right",
            "frames": frames,
        }
        name = os.path.splitext(os.path.basename(video))[0]
        out_path = os.path.join(args.out, f"{row['sign_id']}__{payload['signerId']}__{name}.json")
        with open(out_path, "w", encoding="utf-8") as out:
            json.dump(payload, out, ensure_ascii=False, separators=(",", ":"))
        print(f"[{i + 1}/{len(rows)}] {out_path} ({len(frames)} frames)")


if __name__ == "__main__":
    main()
