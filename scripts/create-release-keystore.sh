#!/usr/bin/env bash
#
# Crée un keystore de signature Release pour Destock et enregistre les mots de
# passe hors du dépôt, dans ~/.destock/signing.env (chmod 600).
#
# Le keystore n'est jamais versionné. Conservez ces deux fichiers en lieu sûr :
# sans eux, il sera impossible de publier une mise à jour signée avec la même
# identité. Pour une publication sur le Play Store, utilisez de préférence un
# keystore géré par votre organisation.
#
set -euo pipefail

KEYSTORE="${DESTOCK_KEYSTORE:-$HOME/.destock/destock-release.keystore}"
ENV_FILE="${DESTOCK_SIGNING_ENV:-$HOME/.destock/signing.env}"
ALIAS="${DESTOCK_KEY_ALIAS:-destock}"

log() { printf '\033[1;32m==>\033[0m %s\n' "$*"; }
err() { printf '\033[1;31mErreur :\033[0m %s\n' "$*" >&2; }

if [[ -f "$KEYSTORE" && -f "$ENV_FILE" ]]; then
    log "Keystore déjà présent : $KEYSTORE"
    log "Configuration : $ENV_FILE"
    exit 0
fi

if [[ -z "${JAVA_HOME:-}" ]]; then
    if [[ -d "$HOME/.jdks" ]]; then
        while IFS= read -r candidate; do
            if [[ -x "$candidate/bin/keytool" ]]; then
                JAVA_HOME="$candidate"
            fi
        done < <(find "$HOME/.jdks" -mindepth 1 -maxdepth 1 -type d | sort -V)
    fi
fi

KEYTOOL="${JAVA_HOME:+$JAVA_HOME/bin/keytool}"
if [[ -z "$KEYTOOL" || ! -x "$KEYTOOL" ]]; then
    KEYTOOL="$(command -v keytool || true)"
fi
if [[ -z "$KEYTOOL" ]]; then
    err "keytool introuvable. Définissez JAVA_HOME vers un JDK."
    exit 1
fi

umask 077
mkdir -p "$(dirname "$KEYSTORE")" "$(dirname "$ENV_FILE")"

PASSWORD="$(openssl rand -base64 32 | tr -d '\n')"

"$KEYTOOL" -genkeypair \
    -keystore "$KEYSTORE" \
    -alias "$ALIAS" \
    -keyalg RSA -keysize 2048 -validity 10000 \
    -storepass "$PASSWORD" -keypass "$PASSWORD" \
    -dname "CN=Destock, OU=Mobile, O=Destock, L=Douala, C=CM"

cat > "$ENV_FILE" <<EOF
DESTOCK_KEYSTORE="$KEYSTORE"
DESTOCK_KEY_ALIAS="$ALIAS"
DESTOCK_STORE_PASSWORD="$PASSWORD"
DESTOCK_KEY_PASSWORD="$PASSWORD"
EOF

chmod 600 "$KEYSTORE" "$ENV_FILE"

log "Keystore créé : $KEYSTORE"
log "Identifiants enregistrés : $ENV_FILE"
log "Sauvegardez ces fichiers. Ils sont requis pour signer les futures versions."
