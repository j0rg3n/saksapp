#!/bin/bash
source "$(dirname "$0")/_env.sh"
ssh root@${UPCLOUD__HOST} systemctl start saksapp
