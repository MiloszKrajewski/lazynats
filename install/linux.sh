#!/bin/sh
# Installs the latest (or a pinned) lazynats release for Linux.
#
#   curl -fsSL https://raw.githubusercontent.com/MiloszKrajewski/lazynats/main/install/linux.sh | sh
#
# Environment:
#   LAZYNATS_VERSION  install this version instead of the latest (e.g. 1.2.3)
#   LAZYNATS_HOME     install root (default: ${XDG_DATA_HOME:-~/.local/share}/lazynats)
#   LAZYNATS_BIN      directory for the symlink (default: ~/.local/bin)
#
# Layout:
#   $LAZYNATS_HOME/<version>/...      extracted release (binary + native libs)
#   $LAZYNATS_BIN/lazynats            symlink to the binary of the installed version

set -eu

REPO="MiloszKrajewski/lazynats"
APP="lazynats"
RELEASES="https://github.com/$REPO/releases"

HOME_DIR="${LAZYNATS_HOME:-${XDG_DATA_HOME:-$HOME/.local/share}/$APP}"
BIN_DIR="${LAZYNATS_BIN:-$HOME/.local/bin}"

say() { printf '%s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }
have() { command -v "$1" >/dev/null 2>&1; }

detect_arch() {
	case "$(uname -m)" in
		x86_64 | amd64) echo x64 ;;
		aarch64 | arm64) echo arm64 ;;
		*) die "unsupported architecture '$(uname -m)' (supported: x86_64, aarch64); see $RELEASES" ;;
	esac
}

check_platform() {
	[ "$(uname -s)" = Linux ] || die "this script is for Linux only; see $RELEASES"
	if ls /lib/ld-musl-* >/dev/null 2>&1 || { have ldd && ldd --version 2>&1 | grep -qi musl; }; then
		die "musl-based distributions (e.g. Alpine) are not supported; the Linux builds need glibc"
	fi
}

latest_version() {
	# /releases/latest redirects to /releases/tag/<version>; no API call, so no rate limit.
	url=$(curl -fsSIL -o /dev/null -w '%{url_effective}' "$RELEASES/latest") ||
		die "could not determine the latest version"
	version=${url##*/}
	[ -n "$version" ] && [ "$version" != latest ] || die "could not determine the latest version"
	echo "$version"
}

verify_checksum() {
	# $1 = archive, $2 = checksum file (format: "<hex>  <filename>"), run from their directory
	if have sha256sum; then
		sha256sum -c "$2" >/dev/null || die "checksum mismatch for $1"
	elif have shasum; then
		shasum -a 256 -c "$2" >/dev/null || die "checksum mismatch for $1"
	else
		say "warning: neither sha256sum nor shasum found, skipping checksum verification"
	fi
}

have curl || die "curl is required"
have tar || die "tar is required"
check_platform

arch=$(detect_arch)
version=${LAZYNATS_VERSION:-$(latest_version)}
archive="$APP-$version-linux-$arch.tgz"
base="$RELEASES/download/$version"

say "Installing $APP $version (linux-$arch)..."

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT INT TERM

curl -fsSL -o "$tmp/$archive" "$base/$archive" || die "download failed: $base/$archive"
if curl -fsSL -o "$tmp/$archive.sha256" "$base/$archive.sha256"; then
	(cd "$tmp" && verify_checksum "$archive" "$archive.sha256")
else
	say "warning: no checksum published for $archive, skipping verification"
fi

target="$HOME_DIR/$version"
mkdir -p "$HOME_DIR"
rm -rf "$target.partial"
mkdir -p "$target.partial"
tar -xzf "$tmp/$archive" -C "$target.partial"

binary=$(find "$target.partial" -type f -name "$APP" | head -n 1)
[ -n "$binary" ] || die "'$APP' executable not found in $archive"
chmod +x "$binary"

# Swap into place only after a complete extraction, so a failed run never leaves a half-installed version.
rm -rf "$target"
mv "$target.partial" "$target"
binary="$target${binary#"$target.partial"}"

mkdir -p "$BIN_DIR"
ln -sfn "$binary" "$BIN_DIR/$APP"

say "Installed: $binary"
say "Symlink:   $BIN_DIR/$APP"

case ":$PATH:" in
	*":$BIN_DIR:"*) ;;
	*)
		say ""
		say "warning: $BIN_DIR is not on your PATH. Add this to your shell profile:"
		say "  export PATH=\"$BIN_DIR:\$PATH\""
		;;
esac
