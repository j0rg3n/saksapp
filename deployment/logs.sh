#!/bin/bash
source "$(dirname "$0")/_env.sh"
ssh root@${UPCLOUD__HOST} docker logs -f root-saksappweb-1
