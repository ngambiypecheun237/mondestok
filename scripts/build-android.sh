#!/usr/bin/env bash
#
# Compile l'application Android Destock.
#
# Usage :
#   scripts/build-android.sh [Debug|Release] [options]
#
# Options :
#   --rust             Recompile d'abord le moteur Rust pour Android
#   --install          Installe l'APK sur l'appareil connecté après compilation
#   --device SERIAL    Numéro de série ADB à utiliser (avec --install)
#   -h, --help         Affiche cette aide
#
# Variables :
#   JAVA_HOME          JDK 17+ (détection automatique dans ~/.jdks si absent)
#   ANDROID_HOME       SDK Android (défaut : ~/Android/Sdk)
#   ANDROID_NDK_HOME   NDK utilisé par --rust
#
# Signature Release :
#   Le script charge ~/.destock/signing.env s'il existe (créé par
#   scripts/create-release-keystore.sh). Sinon, la compilation Release produit
#   un APK non signé, non installable en l'état.
#
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/src/Destock.App/Destock.App.csproj"
PACKAGE_ID="com.destock.app"
CONFIG="Debug"
DO_RUST=false
DO_INSTALL=false
DEVICE="${ANDROID_SERIAL:-}"

usage() {
    sed -n '3,30p' "$0" | sed 's/^# \{0,1\}//'
}

log() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33mAvertissement :\033[0m %s\n' "$*"; }
err() { printf '\033[1;31mErreur :\033[0m %s\n' "$*" >&2; }

if [[ $# -gt 0 && "$1" != -* ]]; then
    CONFIG="$1"
    shift
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rust) DO_RUST=true ;;
        --install) DO_INSTALL=true ;;
        --device)
            shift
            DEVICE="${1:-}"
            [[ -n "$DEVICE" ]] || { err "--device nécessite un numéro de série."; exit 1; }
            ;;
        -h|--help) usage; exit 0 ;;
        *) err "Option inconnue : $1"; usage; exit 1 ;;
    esac
    shift
done

if [[ "$CONFIG" != "Debug" && "$CONFIG" != "Release" ]]; then
    err "Configuration invalide : $CONFIG (attendu : Debug ou Release)."
    exit 1
fi

ANDROID_HOME="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-$HOME/Android/Sdk}}"
export ANDROID_HOME

if [[ -z "${JAVA_HOME:-}" ]]; then
    if [[ -d "$HOME/.jdks" ]]; then
        while IFS= read -r candidate; do
            if [[ -x "$candidate/bin/java" ]]; then
                JAVA_HOME="$candidate"
            fi
        done < <(find "$HOME/.jdks" -mindepth 1 -maxdepth 1 -type d | sort -V)
    fi
fi

if [[ -z "${JAVA_HOME:-}" || ! -x "$JAVA_HOME/bin/java" ]]; then
    err "JDK introuvable. Définissez JAVA_HOME (JDK 17 ou supérieur)."
    exit 1
fi

if [[ ! -d "$ANDROID_HOME" ]]; then
    err "SDK Android introuvable dans $ANDROID_HOME."
    exit 1
fi

export JAVA_HOME
export PATH="$JAVA_HOME/bin:$ANDROID_HOME/platform-tools:$PATH"

log "Configuration : $CONFIG"
log "JDK : $JAVA_HOME"
log "SDK Android : $ANDROID_HOME"

if [[ "$DO_RUST" == true ]]; then
    "$ROOT_DIR/scripts/build-rust-android.sh"
fi

for abi in arm64-v8a armeabi-v7a x86_64; do
    if [[ ! -f "$ROOT_DIR/native/android/$abi/libdestock_core.so" ]]; then
        err "Moteur Rust manquant pour $abi. Lancez scripts/build-rust-android.sh ou ajoutez --rust."
        exit 1
    fi
done

build_args=(
    "$PROJECT"
    -c "$CONFIG"
    -p:AndroidSdkDirectory="$ANDROID_HOME"
    -p:JavaSdkDirectory="$JAVA_HOME"
)

# En Debug, MAUI utilise par défaut le déploiement rapide : l'APK produit seul
# ne contient pas les assemblies .NET et plante s'il est installé avec adb.
# On force leur inclusion pour obtenir un APK autonome.
if [[ "$CONFIG" == "Debug" ]]; then
    build_args+=(-p:EmbedAssembliesIntoApk=true)
fi

if [[ "$CONFIG" == "Release" ]]; then
    if [[ -f "$HOME/.destock/signing.env" ]]; then
        # shellcheck disable=SC1091
        source "$HOME/.destock/signing.env"
    fi

    if [[ -n "${DESTOCK_KEYSTORE:-}" ]]; then
        if [[ ! -f "$DESTOCK_KEYSTORE" ]]; then
            err "Keystore introuvable : $DESTOCK_KEYSTORE"
            exit 1
        fi
        if [[ -z "${DESTOCK_KEY_ALIAS:-}" || -z "${DESTOCK_STORE_PASSWORD:-}" || -z "${DESTOCK_KEY_PASSWORD:-}" ]]; then
            err "Variables de signature incomplètes. Vérifiez ~/.destock/signing.env."
            exit 1
        fi

        log "Signature Release : $DESTOCK_KEYSTORE (alias $DESTOCK_KEY_ALIAS)"
        build_args+=(
            -p:AndroidKeyStore=true
            -p:AndroidSigningKeyStore="$DESTOCK_KEYSTORE"
            -p:AndroidSigningKeyAlias="$DESTOCK_KEY_ALIAS"
            -p:AndroidSigningStorePass="$DESTOCK_STORE_PASSWORD"
            -p:AndroidSigningKeyPass="$DESTOCK_KEY_PASSWORD"
        )
    else
        warn "Aucun keystore configuré : l'APK Release ne sera pas signé."
        warn "Lancez scripts/create-release-keystore.sh pour en créer un."
    fi
fi

log "Compilation de l'application…"
dotnet build "${build_args[@]}"

OUTPUT_DIR="$ROOT_DIR/src/Destock.App/bin/$CONFIG/net10.0-android"
APK="$OUTPUT_DIR/$PACKAGE_ID-Signed.apk"
if [[ ! -f "$APK" ]]; then
    APK="$OUTPUT_DIR/$PACKAGE_ID.apk"
fi

if [[ ! -f "$APK" ]]; then
    err "APK introuvable dans $OUTPUT_DIR."
    exit 1
fi

log "APK : $APK ($(du -h "$APK" | cut -f1))"

if [[ "$DO_INSTALL" == true ]]; then
    ADB="$ANDROID_HOME/platform-tools/adb"
    if [[ ! -x "$ADB" ]]; then
        ADB="$(command -v adb || true)"
    fi
    if [[ -z "$ADB" ]]; then
        err "adb introuvable. Installez les platform-tools du SDK Android."
        exit 1
    fi

    adb_args=()
    if [[ -n "$DEVICE" ]]; then
        adb_args+=(-s "$DEVICE")
    fi

    state="$("$ADB" "${adb_args[@]}" get-state 2>/dev/null || true)"
    if [[ "$state" != "device" ]]; then
        err "Aucun appareil Android autorisé (état : ${state:-absent})."
        echo "Branchez l'appareil, activez le débogage USB et acceptez l'invite sur le téléphone." >&2
        "$ADB" "${adb_args[@]}" devices -l >&2 || true
        exit 1
    fi

    log "Installation sur l'appareil…"
    "$ADB" "${adb_args[@]}" install -r "$APK"
    "$ADB" "${adb_args[@]}" shell am force-stop "$PACKAGE_ID"
    "$ADB" "${adb_args[@]}" shell monkey -p "$PACKAGE_ID" -c android.intent.category.LAUNCHER 1 >/dev/null
    log "Application installée et lancée."
fi
