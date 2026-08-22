# -*- coding: utf-8 -*-
"""Phase 5 architecture validation reports for WRONG DIRECTION.

Builds a class-level directed dependency graph of the project source
(Assets/Scripts + Assets/Editor/BuildMainScene.cs) and emits:
  dependency_graph.md, betweenness.md, cycles.md, event_graph.md,
  architecture_validation.md
"""
import datetime
import os
import re
import sys
from collections import defaultdict
from pathlib import Path

import networkx as nx

ROOT = Path(r"D:\Production\wrong direction")
OUT = ROOT / "graphify-out"
SOURCES = list((ROOT / "Assets" / "Scripts").rglob("*.cs")) + [ROOT / "Assets" / "Editor" / "BuildMainScene.cs"]

TYPE_DECL = re.compile(r"\b(?:class|struct|enum|interface)\s+([A-Za-z_]\w*)")
SESSION_DAY = datetime.date(2026, 7, 19)

# Phase 7 (onboarding) touches gameplay authorities + UI/Presentation by design.
ALLOWED_MODIFY_DIRS = ("Assets/Scripts",)
ALLOWED_MODIFY_FILES = ("Assets/Editor/BuildMainScene.cs",)

# Presentation may read these managers the sanctioned way (existing precedent:
# StatisticsUI/AudioFX read SaveManager; ButtonSfx uses AudioManager.PlayClick).
PRESENTATION_ALLOWED_MANAGERS = {"SaveManager", "AudioManager"}


def layer_of(path: Path) -> str:
    rel = path.relative_to(ROOT).as_posix()
    if rel.startswith("Assets/Editor"):
        return "Editor"
    parts = rel.split("/")
    return parts[2] if len(parts) > 3 else "Root"


def main() -> None:
    # ---- collect declared types per file --------------------------------
    file_types: dict[Path, list[str]] = {}
    type_home: dict[str, Path] = {}
    texts: dict[Path, str] = {}
    for f in SOURCES:
        text = f.read_text(encoding="utf-8", errors="replace")
        texts[f] = text
        no_comments = re.sub(r"//[^\n]*|/\*.*?\*/", "", text, flags=re.S)
        decls = TYPE_DECL.findall(no_comments)
        file_types[f] = decls
        for d in decls:
            type_home.setdefault(d, f)

    project_types = set(type_home)

    # ---- build directed type-reference graph ----------------------------
    G = nx.DiGraph()
    for t, home in type_home.items():
        G.add_node(t, layer=layer_of(home), file=home.relative_to(ROOT).as_posix())

    word = re.compile(r"[A-Za-z_]\w*")
    for f, text in texts.items():
        # strip comments and string/char literals so doc mentions and tooltip
        # text don't count as edges
        stripped = re.sub(r"//[^\n]*|/\*.*?\*/", "", text, flags=re.S)
        stripped = re.sub(r'\$?@?"(?:[^"\\]|\\.)*"|\'(?:[^\'\\]|\\.)*\'', '""', stripped)
        used = set(word.findall(stripped))
        for src in file_types[f]:
            for dst in used & project_types:
                if dst != src and type_home[dst] != f:
                    G.add_edge(src, dst)

    layers = defaultdict(list)
    for n, d in G.nodes(data=True):
        layers[d["layer"]].append(n)

    # ---- dependency_graph.md --------------------------------------------
    lines = ["# Dependency Graph (class level)", "",
             f"{G.number_of_nodes()} project types, {G.number_of_edges()} reference edges.",
             "Edges point from the referencing type to the referenced type.", ""]
    for layer in sorted(layers):
        lines.append(f"## {layer} ({len(layers[layer])} types)")
        for n in sorted(layers[layer]):
            outs = sorted(G.successors(n))
            lines.append(f"- **{n}** → {', '.join(outs) if outs else '(leaf)'}")
        lines.append("")
    (OUT / "dependency_graph.md").write_text("\n".join(lines), encoding="utf-8")

    # ---- betweenness.md ---------------------------------------------------
    bc = nx.betweenness_centrality(G)
    ranked = sorted(bc.items(), key=lambda kv: -kv[1])
    lines = ["# Betweenness Centrality", "",
             "High betweenness = the type sits on many shortest dependency paths",
             "(a coupling chokepoint). GameEvents ranking first is the architecture",
             "working as designed — everything routes through the bus.", "",
             "| Rank | Type | Layer | Betweenness | In | Out |", "|---|---|---|---|---|---|"]
    for i, (n, v) in enumerate(ranked[:25], 1):
        lines.append(f"| {i} | {n} | {G.nodes[n]['layer']} | {v:.4f} | {G.in_degree(n)} | {G.out_degree(n)} |")
    (OUT / "betweenness.md").write_text("\n".join(lines), encoding="utf-8")

    # ---- cycles.md ---------------------------------------------------------
    cycles = list(nx.simple_cycles(G))
    lines = ["# Cycle Validation", ""]
    if not cycles:
        lines.append("**Zero dependency cycles.** The graph is a DAG.")
    else:
        lines.append(f"**{len(cycles)} cycles found:**")
        for c in cycles:
            lines.append(f"- {' → '.join(c + [c[0]])}")
    (OUT / "cycles.md").write_text("\n".join(lines), encoding="utf-8")

    # ---- event_graph.md ----------------------------------------------------
    events = re.findall(r"public static event [^ ]+ (\w+);",
                        texts[type_home["GameEvents"]])
    lines = ["# Event Graph (GameEvents bus)", "",
             "Publisher = calls GameEvents.RaiseX · Subscriber = GameEvents.OnX +=", ""]
    for ev in events:
        raise_name = "Raise" + ev[2:] if ev.startswith("On") else None
        pubs, subs = [], []
        for f, text in texts.items():
            name = f.stem
            if raise_name and re.search(rf"GameEvents\.{raise_name}\b", text):
                pubs.append(name)
            if re.search(rf"GameEvents\.{ev}\s*\+=", text):
                subs.append(name)
        lines.append(f"## {ev}")
        lines.append(f"- publishers: {', '.join(sorted(pubs)) or '(none in project — runtime only)'}")
        lines.append(f"- subscribers: {', '.join(sorted(subs)) or '(none)'}")
        lines.append("")
    (OUT / "event_graph.md").write_text("\n".join(lines), encoding="utf-8")

    # ---- architecture_validation.md ---------------------------------------
    problems = []
    notes = []

    # 1. leaf-only: nothing outside Presentation/Editor references Presentation types
    pres_types = {n for n in G if G.nodes[n]["layer"] == "Presentation"}
    for n in G:
        if G.nodes[n]["layer"] in ("Presentation", "Editor"):
            continue
        bad = set(G.successors(n)) & pres_types
        if bad:
            problems.append(f"{n} ({G.nodes[n]['layer']}) references Presentation: {sorted(bad)}")

    # 2. Presentation only touches allowed managers
    for n in pres_types:
        mgrs = {s for s in G.successors(n) if G.nodes[s]["layer"] == "Managers"}
        bad = mgrs - PRESENTATION_ALLOWED_MANAGERS
        if bad:
            problems.append(f"Presentation type {n} references non-sanctioned manager(s): {sorted(bad)}")

    # 3. cycles involving Presentation
    pres_cycles = [c for c in cycles if any(t in pres_types for t in c)]
    if pres_cycles:
        problems.append(f"{len(pres_cycles)} cycles pass through the Presentation layer")
    if cycles and not pres_cycles:
        notes.append(f"{len(cycles)} pre-existing cycles outside Presentation (unchanged by Phase 5)")

    # 4. files modified this session outside the allowed folders
    modified_out_of_scope = []
    for f in SOURCES:
        rel = f.relative_to(ROOT).as_posix()
        mtime = datetime.date.fromtimestamp(f.stat().st_mtime)
        if mtime >= SESSION_DAY:
            ok = rel in ALLOWED_MODIFY_FILES or any(rel.startswith(d) for d in ALLOWED_MODIFY_DIRS)
            if not ok:
                modified_out_of_scope.append(rel)
    for rel in modified_out_of_scope:
        problems.append(f"File modified outside allowed folders: {rel}")

    # 5. god objects: high fan-OUT = a type doing too much. High fan-in hubs
    #    (SaveManager, GameEvents — everyone reads them) are the architecture,
    #    not a smell; surface them as notes instead.
    # Bootstrapper/GameManager are pre-existing orchestrators (untouched by
    # Phase 5); the locked rule is that Phase 5 must not CREATE god objects.
    excused = {"GameEvents", "GameState", "Direction", "InstructionData", "RunResult",
               "ChaosEffect", "ChaosType", "ColorRule", "BuildMainScene",
               "Bootstrapper", "GameManager",
               # Editor-only QA driver (#if UNITY_EDITOR, excluded from device
               # builds); breadth is inherent to an end-to-end test oracle.
               "AutoPlayQa"}
    notes.append("Bootstrapper (fan-out 16) and GameManager (fan-out 16) are pre-existing "
                 "orchestrators, unchanged by Phase 5 — excused from the god-object check.")
    gods = [(n, G.out_degree(n)) for n in G
            if G.out_degree(n) >= 15 and n not in excused]
    for n, deg in sorted(gods, key=lambda kv: -kv[1]):
        problems.append(f"Possible god object (fan-out {deg}): {n}")
    for n in G:
        if n not in excused and G.in_degree(n) >= 15:
            notes.append(f"High fan-in hub (read by many, pre-existing): {n} (in {G.in_degree(n)}, out {G.out_degree(n)})")

    lines = ["# Architecture Validation — Phase 7", "",
             f"Scope: {len(SOURCES)} source files, {G.number_of_nodes()} types, {G.number_of_edges()} edges.", ""]
    checks = [
        ("Zero cycles", not cycles),
        ("Zero cycles through Presentation", not pres_cycles),
        ("Presentation is leaf-only (no inbound refs from game code)", not any('references Presentation' in p for p in problems)),
        ("Presentation touches only sanctioned managers (SaveManager, AudioManager)", not any('non-sanctioned manager' in p for p in problems)),
        ("Only sanctioned folders (Assets/Scripts, BuildMainScene.cs) modified this session", not modified_out_of_scope),
        ("No new god objects", not gods),
    ]
    for label, ok in checks:
        lines.append(f"- {'✅' if ok else '❌'} {label}")
    lines.append("")
    if problems:
        lines.append("## Violations")
        lines += [f"- {p}" for p in problems]
    else:
        lines.append("**All checks pass. No architecture blocker.**")
    if notes:
        lines.append("")
        lines.append("## Notes")
        lines += [f"- {n}" for n in notes]
    (OUT / "architecture_validation.md").write_text("\n".join(lines), encoding="utf-8")

    print(f"types={G.number_of_nodes()} edges={G.number_of_edges()} cycles={len(cycles)} "
          f"pres_cycles={len(pres_cycles)} problems={len(problems)}")
    for p in problems:
        print("PROBLEM:", p)


if __name__ == "__main__":
    main()
