#!/bin/bash
source auth/secret.env

ssh root@${UPCLOUD__HOST} docker logs --since 24h -t root-saksappweb-1
