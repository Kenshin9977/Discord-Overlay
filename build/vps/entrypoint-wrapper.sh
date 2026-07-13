#!/bin/sh
# Runs INSIDE the certum-signer container, ahead of the base image's
# entrypoint.
#
# monit is PID 1 here, so it writes its pidfile containing "1". That file
# lives in the container's writable layer and survives a restart. On the
# next start monit reads it, asks "is PID 1 alive?" — which is always true,
# because that is monit itself — concludes a daemon is already running,
# signals it ("Monit daemon with PID 1 awakened") and exits 0.
#
# The container therefore starts exactly once. Any restart, reboot or
# docker-daemon bounce puts it into a permanent crash loop that no amount
# of restarting can clear, because the stale pidfile is never rewritten.
# (That is what happened here: it ran from 2026-05-20, stopped, then
# restart-looped ~7000 times.)
#
# Clearing the stale state makes the container restart-safe.
rm -f /run/monit.pid /var/run/monit.pid /var/monit_state

exec /usr/local/bin/entrypoint.sh
