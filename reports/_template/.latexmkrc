# latexmk configuration for this report
# Usage: latexmk

$pdf_mode = 1;                # Generate PDF via pdflatex
$out_dir  = 'build';          # Keep artifacts out of the source tree
$bibtex_use = 2;              # Run bibtex/biber when needed, delete .bbl on clean

# Automatically clean up on ctrl-c
$cleanup_mode = 1;
