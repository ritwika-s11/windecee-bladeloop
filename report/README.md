# BladeLoop report — Overleaf project

Compiles clean with `pdflatex` + `bibtex`. Verified locally before upload.

## Setting it up

1. Overleaf → **New Project → Upload Project** → zip this folder and drop it in.
   (Or New Project → Blank, then upload the files individually, keeping
   `sections/` as a folder.)
2. Menu → **Compiler: pdfLaTeX**, **Main document: main.tex**.
3. Share → invite Hari with **Can edit** (he reviews continuously) and the rest
   with **Can view** so nobody edits by accident.

## Structure

    main.tex                 preamble, title page, includes
    sections/01-introduction.tex
    sections/02-user-documentation.tex
    sections/03-developer-documentation.tex
    sections/04-conclusion.tex
    refs.bib                 12 sources, all with URLs
    figures/                 put the flowcharts here

Section order follows the reference report from the previous batch
(Introduction / User Documentation / Developer Documentation / Conclusion /
References) because that is what this course expects.

## The siunitx note

`\SI{}{}` is defined locally in `main.tex` as a two-argument shim rather than
loading `siunitx`, so the document builds in environments without the package.
Overleaf **does** have siunitx: if you prefer the real thing, add
`\usepackage{siunitx}` and delete the shim block — the existing `\SI` calls are
compatible.

## Numbers already in the document — verified against the running app

High-grade design case, 600 °C / 35 min / 6,500 kg/h / 2 mm:

| stream | share | rate |
|---|---|---|
| glass fibre | 69.0 % | 4,482 kg/h |
| pyrolysis oil | 15.8 % | 1,024 kg/h |
| syngas | 7.9 % | 512 kg/h |
| char | 5.9 % | 384 kg/h |
| loss | 1.5 % | 98 kg/h |

Fibre purity **93.0 %** (not 99 %), tensile retention 90.0 % as a declared
design constant. Campaign 1,071 h ≈ 44.6 days at continuous running.

Stage durations: 23.8 / 9.6 / 30.4 / 33.6 / 46.7 s, total 144.1 s.

## Where the writing goes

Every `\textit{[prose]}` marker is a place to write. The comments above each
one say what belongs there. Section 3.3.3 (`Problems encountered`) has the six
real faults listed in comments — that section is the one that will make this
report worth reading, so don't cut it for space.
