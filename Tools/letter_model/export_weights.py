"""Writes Assets/Core/Resources/azsl_letters.bytes from the web translator's letter model.

Source: chevir-app-vers-1.0/public/tercume/assets/letter_model/model.onnx (the model chevirapp.com/tercume runs).
The C# network in Assets/Core/Scripts/Recognition/Letters/AzslLetterModel.cs reads the tensors in this order.

    pip install onnx numpy
    python Tools/letter_model/export_weights.py path/to/model.onnx Assets/Core/Resources/azsl_letters.bytes
"""
import struct
import sys

import numpy as np
import onnx
from onnx import numpy_helper

src, dst = sys.argv[1], sys.argv[2]
W = {i.name: numpy_helper.to_array(i).astype(np.float32) for i in onnx.load(src).graph.initializer}
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
    f.write(struct.pack('<ii', 1, len(out)))
    for a in out:
        f.write(struct.pack('<i', a.size))
        f.write(a.tobytes())
print(f'{len(out)} tensors, {sum(a.size for a in out)} weights -> {dst}')
