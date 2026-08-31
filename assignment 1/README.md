# Measures Converter (Flutter/Dart)

A single-screen Flutter app that converts a value between metric and
imperial units of length (millimeters, centimeters, meters, kilometers,
inches, feet, yards, miles).

## Project layout

```
assignment 1/
├── lib/main.dart              # App source (UI + conversion logic)
├── pubspec.yaml                # Dependencies / project metadata
├── android/app/src/main/AndroidManifest.xml
├── web/                         # Web target (index.html, manifest.json)
├── test/widget_test.dart       # Widget tests
└── target-layout-reference.png # Reference layout from the assignment brief
```

## Running it from GitHub Codespaces (browser only, no local install needed)

1. On the repository page, click **Code → Codespaces → Create codespace on
   `claude/flutter-conversion-app-gysxwm`**. This repo includes a
   `.devcontainer/devcontainer.json` that runs
   `.devcontainer/setup-flutter.sh` automatically when the codespace
   builds, cloning the Flutter stable SDK and running `flutter pub get`
   for this project. The first build takes a few minutes.
2. Once setup finishes, open a terminal (it should already have Flutter on
   `PATH`; if not, run `export PATH="$PATH:$HOME/flutter/bin"`) and run:
   ```bash
   cd "assignment 1"
   flutter run -d web-server --web-port 8080
   ```
3. Codespaces will prompt you to open the forwarded port `8080` — open it
   in a new browser tab to use the running app.
4. Take a screenshot of that browser tab for your assignment submission
   (Word doc + GitHub link), replacing/supplementing
   `target-layout-reference.png`.

## How the conversion works

Every unit is defined by its conversion factor to meters
(`lib/main.dart`, `kLengthUnits`). Converting between any two units is
therefore always: `value_in_meters = value * fromUnit.metersPerUnit`,
then `result = value_in_meters / toUnit.metersPerUnit`. This avoids
needing a separate formula for every possible unit pair.

## Tests

```bash
cd "assignment 1"
flutter test
```
