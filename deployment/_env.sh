# Sourced by the other deployment scripts. Credentials live outside the repository.
# Override with SAKSAPP_AUTH_DIR; the default matches the local layout in SPEC.md.
SAKSAPP_AUTH_DIR="${SAKSAPP_AUTH_DIR:-$HOME/.config/saksapp}"
if [ ! -r "$SAKSAPP_AUTH_DIR/secret.env" ]; then
    echo "Missing $SAKSAPP_AUTH_DIR/secret.env (set SAKSAPP_AUTH_DIR to override)" >&2
    exit 1
fi
source "$SAKSAPP_AUTH_DIR/secret.env"
