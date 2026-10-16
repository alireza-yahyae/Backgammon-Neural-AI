#!/usr/bin/env python3
"""
export_weights.py - turn a trained network (.npz from backgammon_ai_v5) into the .bgnn file
that the C# game reads.

    python tools/export_weights.py path/to/models/latest.npz BackgammonByHoratiu/Ai/net.bgnn

Only numpy is needed. The file layout is described in NeuralNetwork.cs
(little endian: "BGNN", format version, feature version, layer count,
then per layer: inputs, outputs, float32 weights [inputs x outputs, row-major], float32 biases).
"""
import argparse
import struct
import sys

import numpy as np

FORMAT_VERSION = 1
FEATURE_VERSION = 2      # features.py FEATURE_VERSION
N_FEATURES = 214
N_OUT = 5


def load_npz(path):
    with np.load(path) as d:
        feature_version = int(d["feature_version"])
        n = int(d["n_layers"])
        W = [np.asarray(d[f"W{i}"], dtype="<f4") for i in range(n)]
        b = [np.asarray(d[f"b{i}"], dtype="<f4") for i in range(n)]
    if feature_version != FEATURE_VERSION or W[0].shape[0] != N_FEATURES:
        raise SystemExit(f"{path}: made for a different feature layout "
                         f"(need version {FEATURE_VERSION} with {N_FEATURES} inputs)")
    if W[-1].shape[1] != N_OUT:
        raise SystemExit(f"{path}: the network must have {N_OUT} outputs")
    return W, b


def write_bgnn(path, W, b):
    with open(path, "wb") as f:
        f.write(b"BGNN")
        f.write(struct.pack("<iii", FORMAT_VERSION, FEATURE_VERSION, len(W)))
        for w, bias in zip(W, b):
            f.write(struct.pack("<ii", w.shape[0], w.shape[1]))
            f.write(np.ascontiguousarray(w, dtype="<f4").tobytes())
            f.write(np.ascontiguousarray(bias, dtype="<f4").tobytes())


def read_bgnn(path):
    """Reads the file back (used for the self-check, and handy for debugging)."""
    with open(path, "rb") as f:
        if f.read(4) != b"BGNN":
            raise ValueError("bad header")
        fmt, feat, n = struct.unpack("<iii", f.read(12))
        W, b = [], []
        for _ in range(n):
            n_in, n_out = struct.unpack("<ii", f.read(8))
            W.append(np.frombuffer(f.read(4 * n_in * n_out), dtype="<f4").reshape(n_in, n_out))
            b.append(np.frombuffer(f.read(4 * n_out), dtype="<f4"))
        if f.read(1):
            raise ValueError("trailing bytes")
    return fmt, feat, W, b


def forward(W, b, x):
    h = np.asarray(x, dtype=np.float32)
    for w, bias in zip(W[:-1], b[:-1]):
        h = np.maximum(h @ w + bias, 0)
    z = h @ W[-1] + b[-1]
    return 0.5 * (1.0 + np.tanh(0.5 * z))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("npz", help="trained network, e.g. models/latest.npz")
    ap.add_argument("out", nargs="?", default="BackgammonByHoratiu/Ai/net.bgnn")
    args = ap.parse_args()

    W, b = load_npz(args.npz)
    write_bgnn(args.out, W, b)

    # self-check: read the file back and compare the outputs on random inputs
    _, _, W2, b2 = read_bgnn(args.out)
    x = np.random.default_rng(0).random((8, N_FEATURES)).astype(np.float32)
    err = float(np.abs(forward(W, b, x) - forward(W2, b2, x)).max())
    if err > 1e-6:
        raise SystemExit(f"self-check failed (max difference {err})")
    sizes = " -> ".join(str(s) for s in [W[0].shape[0]] + [w.shape[1] for w in W])
    print(f"wrote {args.out}  ({sizes}, self-check ok)")


if __name__ == "__main__":
    sys.exit(main())
