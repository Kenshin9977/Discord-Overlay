# Derived image for the signing VPS.
#
# Base = hpvb/certum-container built locally as `certum-base` (inspect
# and build it yourself from https://github.com/hpvb/certum-container —
# its README explicitly says never to use prebuilt images). The base
# runs Certum's SimplySign Desktop under Xvnc and exposes a p11-kit
# socket. This layer only adds what the *unattended re-login* needs:
# a TOTP generator and a way to drive the SimplySign window headlessly.
FROM certum-base

RUN dnf -y install --setopt=install_weak_deps=False xdotool oathtool \
 && dnf clean all

COPY relogin-internal.sh /usr/local/bin/relogin-internal.sh
RUN chmod 0755 /usr/local/bin/relogin-internal.sh

# ENTRYPOINT is inherited from certum-base (starts Xvnc + SimplySign).
# relogin-internal.sh is invoked on demand via `docker exec` by the
# host's /usr/local/bin/certum-relogin.
