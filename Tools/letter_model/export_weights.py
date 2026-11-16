"""Converts a web translator model (chevirapp.com/tercume) into the file the app's C# network reads.

Source folders in chevir-app-vers-1.0/public/tercume/assets/: letter_model/ and word_model/ (model.onnx +
manifest.json). Assets/Core/Scripts/Recognition/Letters/SignNet.cs reads the header, the tensors in this order
and the labels.

    pip install onnx numpy
    python Tools/letter_model/export_weights.py <assets>/letter_model Assets/Core/Resources/azsl_letters.bytes
    python Tools/letter_model/export_weights.py <assets>/word_model Assets/Core/Resources/azsl_words.bytes <assets>/sign_merge.json

With sign_merge.json, a second file <name>_groups.txt is written next to the weights: one "LABEL<TAB>GROUP" line per
model label and spelling variant, so forms of the same sign (ACAM, ACMİŞAM) count as one word, as on the site.
"""
import json
import os
import struct
import sys

import numpy as np
import onnx
from onnx import numpy_helper

folder, dst = sys.argv[1], sys.argv[2]
manifest = json.load(open(os.path.join(folder, 'manifest.json'), encoding='utf-8'))
src = os.path.join(folder, manifest.get('model_file', 'model.onnx'))
W = {i.name: numpy_helper.to_array(i).astype(np.float32) for i in onnx.load(src).graph.initializer}
labels = manifest['labels']
n_feat, dim = W['onnx::MatMul_572'].shape
assert W['net.head.weight'].shape[0] == len(labels), 'labels do not match the head'

matmuls = [f'onnx::MatMul_{n}' for n in (572, 577, 580, 585, 588, 593, 596, 597, 599, 600, 601,
                                         606, 609, 614, 617, 622, 625, 626, 628, 629, 630)]
convs = [(f'onnx::Conv_{a}', f'onnx::Conv_{a + 1}') for a in (551, 554, 557, 560, 563, 566)]
mm, cv, out = iter(matmuls), iter(convs), []


def add(a):
    out.append(np.ascontiguousarray(a, dtype=np.float32))


add(W[next(mm)]); add(W['net.stem.bias']); add(W['net.stem_norm.weight']); add(W['net.stem_norm.bias'])
for b in range(8):
    p = f'net.blocks.{b}.'
    if b in (3, 7):  # transformer block
        add(W[p + 'n1.weight']); add(W[p + 'n1.bias']); add(W[next(mm)]); add(W[p + 'attn.in_proj_bias'])
        add(W[next(mm)]); add(W[p + 'attn.out_proj.bias']); add(W[p + 'n2.weight']); add(W[p + 'n2.bias'])
        add(W[next(mm)]); add(W[p + 'ff.0.bias']); add(W[next(mm)]); add(W[p + 'ff.3.bias'])
    else:  # depthwise-conv block (BatchNorm folded into the conv by the ONNX export)
        add(W[p + 'norm.weight']); add(W[p + 'norm.bias']); add(W[next(mm)]); add(W[p + 'pw1.bias'])
        w, bias = next(cv)
        add(W[w][:, 0, :]); add(W[bias]); add(W[p + 'eca.conv.weight'][0, 0]); add(W[next(mm)]); add(W[p + 'pw2.bias'])
add(W['net.norm.weight']); add(W['net.norm.bias']); add(W['net.head.weight'].T); add(W['net.head.bias'])

with open(dst, 'wb') as f:
    f.write(b'CHVL')
    f.write(struct.pack('<iiiii', 2, n_feat, dim, len(labels), len(out)))
    for a in out:
        f.write(struct.pack('<i', a.size))
        f.write(a.tobytes())
    for label in labels:
        b = label.encode('utf-8')
        f.write(struct.pack('<i', len(b)))
        f.write(b)
if len(sys.argv) > 3:
    merge = json.load(open(sys.argv[3], encoding='utf-8'))
    spelling, groups = merge.get('spelling', {}), merge.get('merge', {})

    def group(g):
        c = spelling.get(g, g)
        return groups.get(c, c)

    names = list(dict.fromkeys(labels + list(spelling)))
    with open(os.path.splitext(dst)[0] + '_groups.txt', 'w', encoding='utf-8', newline='\n') as f:
        for n in names:
            f.write(f'{n}\t{group(n)}\n')
print(f'{len(out)} tensors, {sum(a.size for a in out)} weights, {n_feat} features, {len(labels)} labels -> {dst}')
