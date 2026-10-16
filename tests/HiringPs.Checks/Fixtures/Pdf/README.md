# Synthetic PDF fixtures

These documents contain test announcements, not real opportunities. No personal data or downloaded source advertisements are included.

- `english-native.pdf`: native Latin text with a reference and numeric deadline.
- `arabic-native.pdf`: native Arabic with embedded shaping; its imperfect Unicode mapping exercises visual transcription instead of reversing strings.
- `arabic-scanned.pdf`: a raster page with Arabic, English and a numeric deadline; OCR can misread some words, so visual review remains required.
- `mixed-pages.pdf`: native English page followed by the Arabic raster page.
- `columns.pdf`: two native text columns with distinct opportunity references.
- `vector-unreadable.pdf`: vector graphics without a readable text layer; never silently treated as a blank page.
- `image-unreadable.pdf`: an image page without recognizable text.
- `blank.pdf`, `encrypted.pdf`, `too-many-pages.pdf`: rejection cases. The encrypted fixture uses the public test password `fixture-reader`.

Created with PyMuPDF and Pillow/RAQM using Noto Naskh Arabic and DejaVu Sans from the test machine. These Python packages and fonts are only fixture-authoring tools; running the application or checks uses the committed PDFs and does not require Python.

Run `dotnet run --project tests/HiringPs.Checks -c Release -- --pdf-check` with Poppler, Tesseract and `ara+eng` installed. This exercises real extraction/OCR locally without SQL, a browser or an AI API call. Archives created by the checks are isolated in a temporary directory and removed afterward.
