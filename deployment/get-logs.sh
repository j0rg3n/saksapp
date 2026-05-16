#!/bin/bash
source "$(dirname "$0")/../auth/secret.env"
ssh root@${UPCLOUD__HOST} docker logs --since 24h -t root-saksappweb-1
