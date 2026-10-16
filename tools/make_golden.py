#!/usr/bin/env python3
"""
make_golden.py - makes the test fixtures that prove the C# AI agrees with the Python one.

    python tools/make_golden.py path/to/backgammon_ai_v5

Writes into BackgammonByHoratiu.UnitTests/Fixtures/neural/:
  golden_net.bgnn   a small random network (random weights are enough to compare behaviour)
  golden.json       positions with the Python features, network outputs, the set of legal results,
                    and the turn chosen at depth 1 and depth 2
"""
import json
import os
import random
import sys

import numpy as np

here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, here)
sys.path.insert(0, sys.argv[1] if len(sys.argv) > 1 else ".")

from backgammon import Backgammon                  # noqa: E402
from features import N_FEATURES, encode            # noqa: E402
from model import Predictor                        # noqa: E402
from search import two_ply_policy                  # noqa: E402
from selfplay import choose_turn                   # noqa: E402
import export_weights                              # noqa: E402

OUT = os.path.join(here, "..", "BackgammonByHoratiu.UnitTests", "Fixtures", "neural")


def text(g):
    return ",".join(map(str, g.board)) + "|%d,%d|%d,%d" % (*g.bar, *g.off)


def random_net(seed=11, hidden=(32, 16)):
    rng = np.random.default_rng(seed)
    sizes = [N_FEATURES, *hidden, 5]
    W = [rng.normal(0, 0.6 / np.sqrt(sizes[i]) * 3, (sizes[i], sizes[i + 1])).astype(np.float32)
         for i in range(len(sizes) - 1)]
    b = [rng.normal(0, 0.2, sizes[i + 1]).astype(np.float32) for i in range(len(sizes) - 1)]
    return Predictor(W, b)


def harvest(rng, games=12, max_plies=300):
    """Positions (copy, with dice rolled) from games played by random choices."""
    seen = []
    for _ in range(games):
        g = Backgammon()
        g.start(rng)
        for _ in range(max_plies):
            seen.append(g.copy())
            turns = g.legal_turns()
            g.apply_turn(rng.choice(turns))
            if g.winner() is not None:
                break
            g.roll(rng)
    return seen


def pick(positions):
    chosen, used = [], set()

    def take(pred, n):
        k = 0
        for g in positions:
            key = (text(g), g.player, g.dice)
            if key not in used and pred(g):
                used.add(key)
                chosen.append(g)
                k += 1
                if k == n:
                    break

    take(lambda g: g.bar[g.player] > 0, 3)                                     # entering from the bar
    take(lambda g: g.all_home(g.player), 3)                                    # bearing off
    take(lambda g: g.dice[0] == g.dice[1] and not g.bar[g.player], 2)          # doubles
    take(lambda g: max(g.off) >= 12, 2)                                        # close to the end
    step = max(1, len(positions) // 8)
    for g in positions[::step]:
        key = (text(g), g.player, g.dice)
        if key not in used:
            used.add(key)
            chosen.append(g)
    return chosen


def main():
    os.makedirs(OUT, exist_ok=True)
    net = random_net()
    npz = os.path.join(OUT, "golden_net.npz")
    net.save(npz)
    W, b = export_weights.load_npz(npz)
    export_weights.write_bgnn(os.path.join(OUT, "golden_net.bgnn"), W, b)
    os.remove(npz)

    rng = random.Random(5)
    one_ply = lambda g, turns: choose_turn(g, turns, net)
    two_ply = two_ply_policy(net, 5)

    cases = []
    for g in pick(harvest(rng)):
        turns = g.legal_turns()
        p = g.player
        cases.append({
            "board": g.board, "bar": g.bar, "off": g.off, "player": p, "dice": list(g.dice),
            "features": [float(v) for v in encode(g, p)],
            "value": [float(v) for v in net.value(encode(g, p)[None])[0]],
            "turnKeys": [text(t.after) for t in turns],
            "onePlyKey": text(turns[one_ply(g, turns)].after),
            "twoPlyKey": text(turns[two_ply(g, turns)].after),
        })
    with open(os.path.join(OUT, "golden.json"), "w") as f:
        json.dump(cases, f)
    n_bar = sum(1 for c in cases if c["bar"][c["player"]] > 0)
    print(f"{len(cases)} cases ({n_bar} from the bar) -> {os.path.normpath(OUT)}")


if __name__ == "__main__":
    main()
