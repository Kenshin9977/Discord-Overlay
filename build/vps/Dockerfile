# Derived image for the signing VPS.
#
# Base = hpvb/certum-container built locally as `certum-base` (inspect
# and build it yourself from https://github.com/hpvb/certum-container —
# its README explicitly says never to use prebuilt images). The base
# runs Certum's SimplySign Desktop under Xvnc and exposes a p11-kit
# socket. This layer only adds what the *unattended re-login* needs:
# a TOTP generator and a way to drive the SimplySign window headlessly.
FROM certum-base

# xdotool + oathtool drive the unattended re-login. ImageMagick's `import`
# is the only way to SEE the headless X display: the login sequence has to
# be calibrated against a GUI nobody can watch, and a screenshot beats
# guessing at keystrokes. Keep it — it is how this gets debugged.
RUN dnf -y install --setopt=install_weak_deps=False xdotool oathtool ImageMagick \
 && dnf clean all

COPY relogin-internal.sh /usr/local/bin/relogin-internal.sh
COPY entrypoint-wrapper.sh /usr/local/bin/entrypoint-wrapper.sh
RUN chmod 0755 /usr/local/bin/relogin-internal.sh \
                /usr/local/bin/entrypoint-wrapper.sh

# The wrapper clears monit's stale PID-1 pidfile, then hands over to the
# base image's entrypoint (Xvnc + SimplySign). Without it the container
# starts exactly once and crash-loops forever after any restart — see the
# comment in entrypoint-wrapper.sh.
ENTRYPOINT ["/usr/local/bin/entrypoint-wrapper.sh"]

# relogin-internal.sh is invoked on demand via `docker exec` by the
# host's /usr/local/bin/certum-relogin.
