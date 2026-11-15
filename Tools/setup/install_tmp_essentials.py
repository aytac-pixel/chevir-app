#!/usr/bin/env python3
"""Unpacks TextMesh Pro Essential Resources into Assets/ (with their .meta files) for batch-mode builds.

In the editor, Chevir > Setup imports them with Unity's own dialog. In CI the import would only finish
after the setup step has already checked for them, so the files are put in place before Unity starts.
Usage: python3 Tools/setup/install_tmp_essentials.py [version]
"""
import io
import json
import os
import sys
import tarfile
import urllib.request

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def main():
    version = sys.argv[1] if len(sys.argv) > 1 else None
    if version is None:
        with open(os.path.join(ROOT, "Packages", "manifest.json"), encoding="utf-8") as f:
            version = json.load(f)["dependencies"]["com.unity.textmeshpro"]

    url = f"https://download.packages.unity.com/com.unity.textmeshpro/-/com.unity.textmeshpro-{version}.tgz"
    print(f"Downloading {url}")
    with urllib.request.urlopen(url) as response:
        package = tarfile.open(fileobj=io.BytesIO(response.read()), mode="r:gz")

    member = package.getmember("package/Package Resources/TMP Essential Resources.unitypackage")
    unitypackage = tarfile.open(fileobj=io.BytesIO(package.extractfile(member).read()), mode="r:gz")

    entries = {}
    for item in unitypackage.getmembers():
        if not item.isfile():
            continue
        guid, _, name = item.name.lstrip("./").partition("/")
        entries.setdefault(guid, {})[name] = unitypackage.extractfile(item).read()

    written = 0
    for guid, files in entries.items():
        if "pathname" not in files:
            continue
        rel = files["pathname"].decode("utf-8").splitlines()[0].strip()
        if not rel.startswith("Assets/") or ".." in rel.split("/"):
            continue
        target = os.path.join(ROOT, rel)
        if "asset" in files:
            os.makedirs(os.path.dirname(target), exist_ok=True)
            with open(target, "wb") as f:
                f.write(files["asset"])
            written += 1
        else:
            os.makedirs(target, exist_ok=True)
        if "asset.meta" in files:
            with open(target + ".meta", "wb") as f:
                f.write(files["asset.meta"])

    if written == 0:
        sys.exit("No files found in TMP Essential Resources.unitypackage")
    print(f"TextMesh Pro Essential Resources: {written} files in Assets/TextMesh Pro")


if __name__ == "__main__":
    main()
