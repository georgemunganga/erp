#!/bin/bash
# Test-only certificate material. Never imports certificates into a host store.
set -euo pipefail
umask 077
fixture="${1:?An absolute new private fixture directory is required}"
case "$fixture" in /*) ;; *) exit 2 ;; esac
test ! -e "$fixture"
mkdir -m 700 "$fixture"
mkdir -m 700 "$fixture/trust"
printf %s kuubu-invitation-smtp-wire-v1 > "$fixture/scope.txt"
cat > "$fixture/server.ext" <<'EXT'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=IP:127.0.0.1
EXT
cat > "$fixture/wrong-host.ext" <<'EXT'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=IP:127.0.0.2
EXT
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$fixture/root.key" -out "$fixture/root.pem" -days 1 -set_serial 1 -subj '/CN=Kuubu private SMTP fixture root' -addext basicConstraints=critical,CA:TRUE -addext keyUsage=critical,keyCertSign,cRLSign
for name in server wrong-host; do
  openssl req -new -newkey rsa:2048 -nodes -keyout "$fixture/$name.key" -out "$fixture/$name.csr" -subj '/CN=127.0.0.1'
  serial=2
  if [ "$name" = wrong-host ]; then serial=3; fi
  openssl x509 -req -in "$fixture/$name.csr" -CA "$fixture/root.pem" -CAkey "$fixture/root.key" -set_serial "$serial" -out "$fixture/$name.pem" -days 1 -extfile "$fixture/$name.ext"
  openssl pkcs12 -export -out "$fixture/$name.pfx" -inkey "$fixture/$name.key" -in "$fixture/$name.pem" -certfile "$fixture/root.pem" -passout pass:
done
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$fixture/untrusted.key" -out "$fixture/untrusted.pem" -days 1 -subj '/CN=127.0.0.1' -addext basicConstraints=critical,CA:FALSE -addext keyUsage=critical,digitalSignature,keyEncipherment -addext extendedKeyUsage=serverAuth -addext subjectAltName=IP:127.0.0.1
openssl pkcs12 -export -out "$fixture/untrusted.pfx" -inkey "$fixture/untrusted.key" -in "$fixture/untrusted.pem" -passout pass:
