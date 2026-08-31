#!/usr/bin/env bash
# Installs the Flutter stable SDK into the codespace and fetches this
# project's dependencies, so `flutter run` works with no local setup.
set -euo pipefail

if [ ! -d "$HOME/flutter" ]; then
  git clone https://github.com/flutter/flutter.git -b stable --depth 1 "$HOME/flutter"
fi

if ! grep -q 'flutter/bin' "$HOME/.bashrc" 2>/dev/null; then
  echo 'export PATH="$PATH:$HOME/flutter/bin"' >> "$HOME/.bashrc"
fi

export PATH="$PATH:$HOME/flutter/bin"
flutter config --enable-web
flutter doctor

cd "$(dirname "$0")/../assignment 1"
flutter pub get
