#!/bin/bash
source auth/secret.env
ssh root@${UPCLOUD__HOST} docker logs -f root-saksappweb-1
