#!/bin/bash
source "$(dirname "$0")/../auth/secret.env"
ssh root@${UPCLOUD__HOST} docker logs -f root-saksappweb-1
